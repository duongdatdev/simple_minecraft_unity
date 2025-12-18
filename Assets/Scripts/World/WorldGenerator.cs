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
    public int worldSizeInChunks = 5;
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

    // queue data structures for rebuild throttling
    private readonly HashSet<Chunk> dirtyChunks = new HashSet<Chunk>();
    private readonly Queue<Chunk> rebuildQueue = new Queue<Chunk>();

    private void Awake()
    {
        // simple singleton
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        Instance = this;
    }

    private void Start()
    {
        System.Random pr = new System.Random(seed);
        noiseOffset2D = new Vector2(pr.Next(-100000, 100000), pr.Next(-100000, 100000));
        noiseOffset3D = new Vector3(pr.Next(-100000, 100000), pr.Next(-100000, 100000), pr.Next(-100000, 100000));

        SpawnChunks();
    }

    private void Update()
    {
        ProcessRebuildQueue();
    }

    private void SpawnChunks()
    {
        if (chunkPrefab == null)
        {
            Debug.LogError("WorldGenerator: chunkPrefab not assigned!");
            return;
        }

        // 1) First pass: instantiate, initialize and register all chunks
        for (int x = 0; x < worldSizeInChunks; x++)
        {
            for (int z = 0; z < worldSizeInChunks; z++)
            {
                Vector3 pos = new Vector3(x * BlockData.ChunkWidth, 0, z * BlockData.ChunkWidth);
                GameObject go = Instantiate(chunkPrefab, pos, Quaternion.identity, transform);
                go.name = $"Chunk_{x}_{z}";

                Chunk chunk = go.GetComponent<Chunk>();
                if (chunk != null)
                {
                    // initialize chunk (generate blocks, initial mesh)
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
                        seed: seed
                    );

                    // register chunk for global lookup
                    RegisterChunk(chunk);

                    // Do NOT populate trees or force rebuild here (neighbors may not exist yet)
                    // Optionally you can avoid RebuildMeshImmediate here to reduce overhead.
                }
                else
                {
                    Debug.LogWarning($"WorldGenerator: chunk prefab at {pos} does not have a Chunk component.");
                }
            }
        }

        Debug.Log($"WorldGenerator: World spawned with {worldSizeInChunks}x{worldSizeInChunks} chunks.");

        // 2) Second pass: recompute skylight for every chunk now that all chunks exist
        foreach (var kv in chunkDict)
        {
            Chunk c = kv.Value;
            if (c != null)
            {
                c.RecomputeSkylightAndUpdateMesh();
            }
        }

        // 3) Third pass: populate trees and enqueue rebuilds (throttled)
        // Collect chunks to enqueue so we don't modify chunkDict while iterating oddly.
        List<Chunk> toEnqueue = new List<Chunk>();
        foreach (var kv in chunkDict)
        {
            Chunk c = kv.Value;
            if (c == null) continue;

            // populate trees now that neighbor chunks are present
            c.PopulateOakTrees(seed, attempts: 12, chance: 0.18f, minTrunk: 4, maxTrunk: 6, leafRadius: 2);

            // mark chunk for rebuild via the usual queue (throttled by ProcessRebuildQueue)
            toEnqueue.Add(c);
        }

        // Enqueue rebuilds (use existing throttled queue)
        foreach (var c in toEnqueue)
        {
            EnqueueChunkForRebuild(c);
        }
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

        // set local block
        chunk.SetBlockLocal(lx, y, lz, type);

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
            rebuildQueue.Enqueue(chunk);
            chunk.MarkDirtyTimestamp(Time.time);
        }
    }

    // Process limited number of rebuilds per frame
    private void ProcessRebuildQueue()
    {
        int processed = 0;
        while (processed < maxRebuildsPerFrame && rebuildQueue.Count > 0)
        {
            Chunk c = rebuildQueue.Dequeue();
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