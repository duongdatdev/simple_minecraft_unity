using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WorldGenerator
/// - Spawn chunks grid and keep lookup dictionary.
/// - Provide global block query/set.
/// - Queue chunk rebuilds instead of immediate rebuild to avoid FPS spikes.
/// </summary>
public class WorldGenerator : MonoBehaviour
{
    public static WorldGenerator Instance { get; private set; }

    [Header("Chunk Settings")] public GameObject chunkPrefab;
    public int renderDistance = 5;
    public Transform player;
    public int seed = 1234;

    [Header("Terrain Noise")] public float heightScale = 30f;
    public float noiseScale = 20f;
    public int noiseOctaves = 4;
    public float noisePersistence = 0.5f;
    public float noiseLacunarity = 2f;

    [Header("Cave Noise")] public bool enableCaves = true;
    public float caveThreshold = 0.1f;
    public float caveScale = 12f;
    public int caveOctaves = 3;

    [Header("Rebuild Throttle")] [Tooltip("Max number of chunk rebuilds processed per frame")]
    public int maxRebuildsPerFrame = 1;

    [Tooltip("Delay before updating MeshCollider after rebuild (seconds)")]
    public float colliderRebuildDelay = 0.12f;

    private Vector2 noiseOffset2D;
    private Vector3 noiseOffset3D;

    // fast lookup of spawned chunks by chunk coordinates
    private Dictionary<Vector2Int, Chunk> chunkDict = new Dictionary<Vector2Int, Chunk>();
    // Saved chunk data loaded from save file (keyed by chunk coords)
    private Dictionary<Vector2Int, int[]> savedChunkBlocks = new Dictionary<Vector2Int, int[]>();

    // queue data structures for rebuild throttling
    private readonly HashSet<Chunk> dirtyChunks = new HashSet<Chunk>();
    private readonly List<Chunk> rebuildQueue = new List<Chunk>();

    // Chunks waiting for neighbors to be generated before they can be decorated (trees) and lit
    private HashSet<Chunk> pendingChunks = new HashSet<Chunk>();

    private void Awake()
    {
        // simple singleton
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        Instance = this;
    }

    private Vector2Int lastPlayerChunkCoord = new Vector2Int(int.MaxValue, int.MaxValue);
    private Coroutine updateChunksCoroutine;

    private void Start()
    {
        System.Random pr = new System.Random(seed);
        noiseOffset2D = new Vector2(pr.Next(-100000, 100000), pr.Next(-100000, 100000));
        noiseOffset3D = new Vector3(pr.Next(-100000, 100000), pr.Next(-100000, 100000), pr.Next(-100000, 100000));

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
        }

        // Disable player control until spawn chunk is ready
        if (player != null)
        {
            MonoBehaviour pc = player.GetComponent("PlayerController") as MonoBehaviour;
            if (pc != null) pc.enabled = false;
        }

        // Attempt to load previously selected save (if any)
        string loadSave = PlayerPrefs.GetString("LoadSaveName", "");
        if (!string.IsNullOrEmpty(loadSave))
        {
            LoadWorld(loadSave);
        }

        // Start the chunk update loop
        StartCoroutine(UpdateChunksLoop());
        StartCoroutine(WaitForSpawn());
    }

    private IEnumerator WaitForSpawn()
    {
        if (player == null) yield break;

        while (true)
        {
            int pX = Mathf.FloorToInt(player.position.x / BlockData.ChunkWidth);
            int pZ = Mathf.FloorToInt(player.position.z / BlockData.ChunkWidth);

            Chunk chunk = FindChunkAt(pX, pZ);
            // Check if chunk is loaded, has a mesh, and the collider is ready (meaning physics is safe)
            if (chunk != null && chunk.GetComponent<MeshFilter>().sharedMesh != null && chunk.GetComponent<MeshFilter>().sharedMesh.vertexCount > 0 && chunk.IsColliderReady)
            {
                // Find surface
                int localX = Mathf.FloorToInt(player.position.x) - pX * BlockData.ChunkWidth;
                int localZ = Mathf.FloorToInt(player.position.z) - pZ * BlockData.ChunkWidth;

                // Clamp local coords just in case
                localX = Mathf.Clamp(localX, 0, BlockData.ChunkWidth - 1);
                localZ = Mathf.Clamp(localZ, 0, BlockData.ChunkWidth - 1);

                int y = BlockData.ChunkHeight - 1;
                while (y > 0 && !chunk.IsBlockSolidLocal(localX, y, localZ))
                {
                    y--;
                }

                // Teleport
                player.position = new Vector3(player.position.x, y + 2, player.position.z);

                // Wait one fixed update so the physics engine registers the new MeshCollider
                yield return new WaitForFixedUpdate();

                // Enable
                MonoBehaviour pc = player.GetComponent("PlayerController") as MonoBehaviour;
                if (pc != null) pc.enabled = true;

                yield break;
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void Update()
    {
        ProcessPendingChunks();
        ProcessRebuildQueue();
    }

    private IEnumerator UpdateChunksLoop()
    {
        while (true)
        {
            if (player != null)
            {
                int pX = Mathf.FloorToInt(player.position.x / BlockData.ChunkWidth);
                int pZ = Mathf.FloorToInt(player.position.z / BlockData.ChunkWidth);
                Vector2Int currentChunkCoord = new Vector2Int(pX, pZ);

                if (currentChunkCoord != lastPlayerChunkCoord)
                {
                    lastPlayerChunkCoord = currentChunkCoord;
                    yield return StartCoroutine(UpdateChunksRoutine(currentChunkCoord));
                }
            }
            yield return new WaitForSeconds(0.5f); // Check every 0.5s
        }
    }

    private IEnumerator UpdateChunksRoutine(Vector2Int centerChunk)
    {
        if (chunkPrefab == null) yield break;

        List<Vector2Int> chunksToLoad = new List<Vector2Int>();
        for (int x = -renderDistance; x <= renderDistance; x++)
        {
            for (int z = -renderDistance; z <= renderDistance; z++)
            {
                chunksToLoad.Add(new Vector2Int(centerChunk.x + x, centerChunk.y + z));
            }
        }

        // Unload chunks
        List<Vector2Int> chunksToRemove = new List<Vector2Int>();
        foreach (var kvp in chunkDict)
        {
            if (!chunksToLoad.Contains(kvp.Key))
            {
                chunksToRemove.Add(kvp.Key);
            }
        }

        foreach (var coord in chunksToRemove)
        {
            if (chunkDict.TryGetValue(coord, out Chunk chunk))
            {
                Destroy(chunk.gameObject);
                chunkDict.Remove(coord);
                pendingChunks.Remove(chunk);
            }
            // Unload 2 chunks per frame to avoid spikes
            if (chunksToRemove.IndexOf(coord) % 2 == 0) yield return null;
        }

        // Load new chunks
        foreach (var coord in chunksToLoad)
        {
            if (!chunkDict.ContainsKey(coord))
            {
                CreateChunk(coord.x, coord.y);
                // Load 1 chunk per frame
                yield return null;
            }
        }
    }

    private void CreateChunk(int x, int z)
    {
        Vector3 pos = new Vector3(x * BlockData.ChunkWidth, 0, z * BlockData.ChunkWidth);
        GameObject go = Instantiate(chunkPrefab, pos, Quaternion.identity, transform);
        go.name = $"Chunk_{x}_{z}";

        Chunk chunk = go.GetComponent<Chunk>();
        if (chunk != null)
        {
            // Initialize blocks ONLY (buildMesh = false)
            int[] savedData = null;
            var key = new Vector2Int(x, z);
            if (savedChunkBlocks.TryGetValue(key, out var blocksData)) savedData = blocksData;

            chunk.Initialize(
                x, z,
                heightNoiseScale: noiseScale,
                heightNoiseOctaves: noiseOctaves,
                heightNoisePersistence: noisePersistence,
                heightNoiseLacunarity: noiseLacunarity,
                heightAmplitude: heightScale,
                heightOffset: noiseOffset2D,
                enableCaves: enableCaves,
                caveThreshold: caveThreshold,
                caveScale: caveScale,
                caveOctaves: caveOctaves,
                caveOffset: noiseOffset3D,
                seed: seed,
                buildMesh: false,
                savedBlockData: savedData
            );
            chunkDict.Add(key, chunk);
            pendingChunks.Add(chunk);

            // If we loaded saved data for this chunk, ensure it is marked modified so it gets updated
            if (savedData != null)
            {
                chunk.isModified = true;
            }
        }
    }

    private void ProcessPendingChunks()
    {
        if (pendingChunks.Count == 0) return;

        List<Chunk> toRemove = new List<Chunk>();
        int processed = 0;

        foreach (Chunk chunk in pendingChunks)
        {
            if (processed >= 1) break; // Process 1 chunk per frame (Decoration + Mesh is heavy)

            // Check if neighbors are loaded
            if (AreNeighborsLoaded(chunk.ChunkX, chunk.ChunkZ))
            {
                // 1. Populate Trees (now that neighbors exist)
                chunk.PopulateOakTrees(seed, attempts: 12, chance: 0.18f, minTrunk: 4, maxTrunk: 6, leafRadius: 2);
                
                // 2. Compute Light (needs neighbors for smooth light)
                // Recompute for this chunk and also request recompute for surrounding chunks so skylight across boundaries is consistent.
                chunk.RecomputeSkylightAndUpdateMesh();

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        Chunk neighbor = FindChunkAt(chunk.ChunkX + dx, chunk.ChunkZ + dz);
                        if (neighbor != null)
                        {
                            neighbor.RecomputeSkylightAndUpdateMesh();
                        }
                    }
                }

                toRemove.Add(chunk);
                processed++;
            }
        }

        foreach (var c in toRemove)
        {
            pendingChunks.Remove(c);
        }
    }

    private bool AreNeighborsLoaded(int cx, int cz)
    {
        // Check 4 direct neighbors
        if (!chunkDict.ContainsKey(new Vector2Int(cx + 1, cz))) return false;
        if (!chunkDict.ContainsKey(new Vector2Int(cx - 1, cz))) return false;
        if (!chunkDict.ContainsKey(new Vector2Int(cx, cz + 1))) return false;
        if (!chunkDict.ContainsKey(new Vector2Int(cx, cz - 1))) return false;
        
        // Also check diagonals for AO/Light correctness? 
        // For now, 4 neighbors is usually enough for basic trees and light spreading
        return true;
    }


    // Register a chunk into the dictionary for fast lookup (call after Instantiate + Initialize)
    public void RegisterChunk(Chunk chunk)
    {
        if (chunk == null) return;
        Vector2Int key = new Vector2Int(chunk.ChunkX, chunk.ChunkZ);
        chunkDict[key] = chunk;
    }

    public void UnregisterChunk(Chunk chunk)
    {
        if (chunk == null) return;
        Vector2Int key = new Vector2Int(chunk.ChunkX, chunk.ChunkZ);
        if (chunkDict.ContainsKey(key)) chunkDict.Remove(key);
        dirtyChunks.Remove(chunk);
        // rebuildQueue may still contain it; will be skipped when dequeued if null/unregistered
    }

    // Save current world (modified chunks, player, inventory) to save file
    public void SaveWorld(string saveName = "")
    {
        string name = saveName;
        if (string.IsNullOrEmpty(name)) name = PlayerPrefs.GetString("LoadSaveName", "");
        if (string.IsNullOrEmpty(name)) name = "QuickSave_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

        GameSaveData data = new GameSaveData();
        data.saveName = name;
        data.lastPlayed = System.DateTime.Now.Ticks;
        data.worldData = new WorldData();
        data.worldData.seed = seed;

        // Save modified chunks only
        foreach (var kvp in chunkDict)
        {
            Chunk c = kvp.Value;
            if (c == null) continue;
            if (!c.isModified) continue;
            ChunkData cd = new ChunkData();
            cd.x = c.ChunkX;
            cd.z = c.ChunkZ;
            cd.blocks = c.GetBlockData();
            data.worldData.modifiedChunks.Add(cd);
        }

        // Player
        data.playerData = new PlayerData();
        if (player != null)
        {
            data.playerData.position = new Vector3Data(player.position);
            data.playerData.rotation = new Vector3Data(player.eulerAngles);
            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                data.playerData.currentHealth = pc.currentHealth;
                data.playerData.currentHunger = pc.currentHunger;
            }
        }

        // Inventory
        var inv = Object.FindFirstObjectByType<Inventory>();
        if (inv != null) data.inventoryData = inv.GetInventoryData();

        SaveManager.SaveGame(data);
        PlayerPrefs.SetString("LoadSaveName", name);
        Debug.Log($"World saved to {name}");
    }

    public void LoadWorld(string saveName)
    {
        if (string.IsNullOrEmpty(saveName)) return;
        GameSaveData data = SaveManager.LoadGame(saveName);
        if (data == null) return;

        // Set seed if provided
        if (data.worldData != null) seed = data.worldData.seed;

        savedChunkBlocks.Clear();
        if (data.worldData != null && data.worldData.modifiedChunks != null)
        {
            foreach (var cd in data.worldData.modifiedChunks)
            {
                if (cd == null) continue;
                savedChunkBlocks[new Vector2Int(cd.x, cd.z)] = cd.blocks;

                // If chunk already loaded, apply immediately
                var key = new Vector2Int(cd.x, cd.z);
                if (chunkDict.TryGetValue(key, out var c) && c != null)
                {
                    c.SetBlockData(cd.blocks);
                    c.isModified = true;
                    c.RecomputeSkylightAndUpdateMesh();
                }
            }
        }

        // Player
        if (data.playerData != null && player != null)
        {
            player.position = data.playerData.position.ToVector3();
            player.eulerAngles = data.playerData.rotation.ToVector3();
            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.currentHealth = data.playerData.currentHealth;
                pc.currentHunger = data.playerData.currentHunger;
            }
        }

        // Inventory
        if (data.inventoryData != null)
        {
            var inv = Object.FindFirstObjectByType<Inventory>();
            if (inv != null) inv.LoadInventoryData(data.inventoryData);
        }

        PlayerPrefs.SetString("LoadSaveName", saveName);
        Debug.Log($"Loaded save: {saveName}");
    }

    // Find chunk by chunk coords
    public Chunk FindChunkAt(int chunkX, int chunkZ)
    {
        chunkDict.TryGetValue(new Vector2Int(chunkX, chunkZ), out var c);
        return c;
    }

    // Query whether global block is solid
    public bool IsBlockSolidAtGlobal(int gx, int y, int gz)
    {
        int chunkX = Mathf.FloorToInt((float)gx / BlockData.ChunkWidth);
        int chunkZ = Mathf.FloorToInt((float)gz / BlockData.ChunkWidth);
        Chunk chunk = FindChunkAt(chunkX, chunkZ);
        if (chunk == null) return false;
        int lx = gx - chunkX * BlockData.ChunkWidth;
        int lz = gz - chunkZ * BlockData.ChunkWidth;
        return chunk.IsBlockSolidLocal(lx, y, lz);
    }

    /// <summary>
    /// Set block at global coordinates to "type".
    /// Mark affected chunks dirty and enqueue for deferred rebuild.
    /// </summary>
    public void SetBlockAtGlobal(int gx, int y, int gz, BlockType type)
    {
        int chunkX = Mathf.FloorToInt((float)gx / BlockData.ChunkWidth);
        int chunkZ = Mathf.FloorToInt((float)gz / BlockData.ChunkWidth);

        Chunk chunk = FindChunkAt(chunkX, chunkZ);
        if (chunk == null) return; // chunk not loaded -> ignore

        int lx = gx - chunkX * BlockData.ChunkWidth;
        int lz = gz - chunkZ * BlockData.ChunkWidth;

        // get current type to detect opacity changes
        BlockType oldType = chunk.GetBlockLocal(lx, y, lz);

        // set local block
        chunk.SetBlockLocal(lx, y, lz, type);

        // If opacity changed (for example placing/removing leaves), recompute skylight for this chunk and neighbors immediately
        bool oldOpaque = BlockData.IsOpaque(oldType);
        bool newOpaque = BlockData.IsOpaque(type);
        if (oldOpaque != newOpaque)
        {
            // recompute skylight and request rebuild for this chunk and its 3x3 neighborhood so light stays consistent
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    Chunk n = FindChunkAt(chunkX + dx, chunkZ + dz);
                    if (n != null) n.RecomputeSkylightAndUpdateMesh();
                }
            }

            // We still enqueue chunk(s) for rebuild propagation and collider handling
            EnqueueChunkForRebuild(chunk);
            return;
        }

        // enqueue rebuild for this chunk and neighbors if on boundary
        EnqueueChunkForRebuild(chunk);

        if (lx == 0)
        {
            Chunk n = FindChunkAt(chunkX - 1, chunkZ);
            if (n != null) EnqueueChunkForRebuild(n);
        }
        else if (lx == BlockData.ChunkWidth - 1)
        {
            Chunk n = FindChunkAt(chunkX + 1, chunkZ);
            if (n != null) EnqueueChunkForRebuild(n);
        }

        if (lz == 0)
        {
            Chunk n = FindChunkAt(chunkX, chunkZ - 1);
            if (n != null) EnqueueChunkForRebuild(n);
        }
        else if (lz == BlockData.ChunkWidth - 1)
        {
            Chunk n = FindChunkAt(chunkX, chunkZ + 1);
            if (n != null) EnqueueChunkForRebuild(n);
        }
    }

    // Mark chunk dirty and enqueue if not already
    public void EnqueueChunkForRebuild(Chunk chunk)
    {
        if (chunk == null) return;
        if (dirtyChunks.Add(chunk))
        {
            rebuildQueue.Add(chunk);
            chunk.MarkDirtyTimestamp(Time.time);
        }
    }

    // Process limited number of rebuilds per frame
    private void ProcessRebuildQueue()
    {
        if (rebuildQueue.Count == 0) return;

        if (player != null)
        {
            Vector3 playerPos = player.position;
            rebuildQueue.Sort((a, b) =>
            {
                if (a == null) return 1;
                if (b == null) return -1;
                float distA = Vector3.SqrMagnitude(a.transform.position - playerPos);
                float distB = Vector3.SqrMagnitude(b.transform.position - playerPos);
                return distA.CompareTo(distB);
            });
        }

        int processed = 0;
        while (processed < maxRebuildsPerFrame && rebuildQueue.Count > 0)
        {
            Chunk c = rebuildQueue[0];
            rebuildQueue.RemoveAt(0);
            
            // if chunk was unregistered or null skip
            if (c == null) continue;

            // remove from dirty set (could have been re-added later)
            dirtyChunks.Remove(c);

            // ask chunk to rebuild, but chunk will apply collider after delay
            c.RebuildMeshDeferred(colliderRebuildDelay);

            processed++;
        }
    }

    /// <summary>
    /// Return skylight (0..15) at global coordinates (gx, y, gz).
    /// If chunk not loaded or out-of-range, fallback: if y >= ChunkHeight => return 15 (sky).
    /// </summary>
    public int GetSkyLightAtGlobal(int gx, int y, int gz)
    {
        if (y >= BlockData.ChunkHeight)
        {
            // above build height -> full skylight
            return 15;
        }

        int chunkX = Mathf.FloorToInt((float)gx / BlockData.ChunkWidth);
        int chunkZ = Mathf.FloorToInt((float)gz / BlockData.ChunkWidth);
        Chunk chunk = FindChunkAt(chunkX, chunkZ);
        if (chunk == null) return 0; // chunk not loaded -> treat as dark (or choose 15 if you want)
        int lx = gx - chunkX * BlockData.ChunkWidth;
        int lz = gz - chunkZ * BlockData.ChunkWidth;
        return chunk.GetSkyLightLocal(lx, y, lz);
    }

    /// <summary>
    /// Return block light (0..15) at global coordinates (gx, y, gz).
    /// Used for smooth lighting across chunk boundaries.
    /// </summary>
    public int GetBlockLightAtGlobal(int gx, int y, int gz)
    {
        if (y < 0 || y >= BlockData.ChunkHeight)
        {
            return 0; // out of bounds -> no block light
        }

        int chunkX = Mathf.FloorToInt((float)gx / BlockData.ChunkWidth);
        int chunkZ = Mathf.FloorToInt((float)gz / BlockData.ChunkWidth);
        Chunk chunk = FindChunkAt(chunkX, chunkZ);
        if (chunk == null) return 0; // chunk not loaded

        int lx = gx - chunkX * BlockData.ChunkWidth;
        int lz = gz - chunkZ * BlockData.ChunkWidth;
        return chunk.GetBlockLightLocal(lx, y, lz);
    }

    /// <summary>
    /// Check if block at global coordinates is opaque (for ambient occlusion).
    /// Returns true for out-of-bounds or unloaded chunks (conservative).
    /// </summary>
    public bool IsBlockOpaqueAtGlobal(int gx, int y, int gz)
    {
        if (y < 0 || y >= BlockData.ChunkHeight)
        {
            return false; // out of bounds vertically
        }

        int chunkX = Mathf.FloorToInt((float)gx / BlockData.ChunkWidth);
        int chunkZ = Mathf.FloorToInt((float)gz / BlockData.ChunkWidth);
        Chunk chunk = FindChunkAt(chunkX, chunkZ);
        if (chunk == null) return false; // unloaded chunk -> treat as not opaque

        int lx = gx - chunkX * BlockData.ChunkWidth;
        int lz = gz - chunkZ * BlockData.ChunkWidth;
        return chunk.IsOpaqueLocal(lx, y, lz);
    }
}