using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chunk
/// - Holds local block array
/// - Generates initial terrain from noise on Initialize(...)
/// - BuildMesh() creates mesh (called during Init or deferred rebuild)
/// - RebuildMeshDeferred(delay) schedules rebuild on next update (throttled by WorldGenerator)
/// Notes:
/// - Avoid heavy logs in hot path.
/// - MeshCollider update is delayed to reduce physics cost per-frame.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    private BlockType[,,] blocks = new BlockType[BlockData.ChunkWidth, BlockData.ChunkHeight, BlockData.ChunkWidth];

    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;

    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();
    private List<Color> colors = new List<Color>();
    private List<Vector2> uv2s = new List<Vector2>();

    // Collision mesh data
    private Mesh collisionMesh;
    private List<Vector3> colVertices = new List<Vector3>();
    private List<int> colTriangles = new List<int>();

    // lighting arrays (per chunk)
    private byte[,,] skyLight;
    private byte[,,] blockLight;

    // mapping from vertex index -> block corner (so we can compute vertex light quickly)
    private struct VertexLightInfo
    {
        public int bx, by, bz;
        public byte cornerX, cornerY, cornerZ;
        public Color tint;
    }

    private List<VertexLightInfo> vertexLightInfos = new List<VertexLightInfo>();

    // noise parameters
    private float heightNoiseScale;
    private int heightNoiseOctaves;
    private float heightNoisePersistence;
    private float heightNoiseLacunarity;
    private float heightAmplitude;
    private Vector2 heightOffset;

    private bool cavesEnabled;
    private float caveThreshold;
    private float caveScale;
    private int caveOctaves;
    private Vector3 caveOffset;

    private int chunkX;
    private int chunkZ;

    [Header("Bedrock")]
    [Tooltip("Number of solid bedrock layers at the bottom of the world (0 = none)")]
    public int bedrockLayerCount = 4; // like Minecraft bottom layers

    // Ore generation settings
    [Header("Ore Generation")]
    [Tooltip("Number of vein generation attempts per chunk for Iron Ore")] public int ironVeinAttempts = 25;
    [Tooltip("Min blocks per vein (inclusive)")] public int ironVeinSizeMin = 4;
    [Tooltip("Max blocks per vein (inclusive)")] public int ironVeinSizeMax = 10;
    [Tooltip("Maximum world height where iron can spawn")]
    public int ironMaxHeight = 64;

    // store seed used for deterministic chunk generation
    private int generatorSeed = 0;

    [Header("Block Texture Data (assign assets)")]
    public BlockTextureData[] blockTextures; // assign ScriptableObjects in inspector

    [Header("Crops")]
    public Sprite[] carrotSprites; // 0-3
    public Material cropMaterial; // Assign a Cutout/Transparent material here
    private List<GameObject> cropObjects = new List<GameObject>();

    [Header("Debug")] public bool debugLogCounts = false;
    
    [Header("Lighting Settings")]
    [Tooltip("Minimum ambient light (0..15). Prevents completely black areas.")]
    [Range(0,15)]
    public byte ambientLightLevel = 2;

    // rebuild control
    private bool needsRebuild = false;
    private float lastDirtyTime = 0f;
    private float colliderDelay = 0.12f;
    private bool colliderPending = false;

    // light color update batching
    private bool lightColorsDirty = false;

    // Debug: sample block to inspect UVs (set in Inspector)
    [Header("Debug Sample (set tile coordinates to inspect)")]
    public bool debugLogUVs = false;

    public int debugSampleX = -1;
    public int debugSampleY = -1;
    public int debugSampleZ = -1;

    public int ChunkX => chunkX;
    public int ChunkZ => chunkZ;

    // True when the mesh collider has been applied and there is no pending collider update.
    public bool IsColliderReady => meshCollider != null && meshCollider.sharedMesh == collisionMesh && !colliderPending;

        private void Awake()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshCollider = GetComponent<MeshCollider>();

            mesh = new Mesh { name = "ChunkMesh" };
            collisionMesh = new Mesh { name = "ChunkCollisionMesh" };
            // assign mesh to filter to reuse instance and avoid allocations
            meshFilter.mesh = mesh;

        // Initialize lighting arrays so skylight/blockLight are not null.
        InitLightArrays();

        //Auto-load all BlockTextureData if not assigned manually
        if (blockTextures == null || blockTextures.Length == 0)
        {
            blockTextures = Resources.LoadAll<BlockTextureData>("BlockTextures");
            Debug.Log(
                $"[Chunk:{name}] Loaded {blockTextures.Length} BlockTextureData assets from Resources/BlockTextures");
        }

        // Pre-size lists to reduce GC churn (rough estimate)
        int estimateFaces = BlockData.ChunkWidth * BlockData.ChunkWidth * 4; // very rough
        vertices.Capacity = estimateFaces * 4;
        triangles.Capacity = estimateFaces * 6;
        uvs.Capacity = estimateFaces * 4;
        vertexLightInfos.Capacity = estimateFaces * 4;
    }


    private void Update()
    {
        // if rebuild requested, do it here (WorldGenerator limits how many chunks are enqueued per frame)
        if (needsRebuild)
        {
            needsRebuild = false;
            BuildMesh();
            // Update collider immediately after mesh rebuild for player movement
            UpdateCollider();
            colliderPending = false;
        }

        // apply vertex color updates that were marked dirty (batched)
        if (lightColorsDirty)
        {
            ApplyLightColorsToMesh();
            lightColorsDirty = false;
        }

        // handle delayed collider update (for cases where only collider needs update)
        if (colliderPending && Time.time - lastDirtyTime >= colliderDelay)
        {
            UpdateCollider();
            colliderPending = false;
        }

        // Random Tick for crops
        if (Time.frameCount % 60 == 0) // Every 60 frames (approx 1 sec)
        {
            RandomTick();
        }
    }

    #region Initialization & Noise

    public void Initialize(
        int chunkX, int chunkZ,
        float heightNoiseScale, int heightNoiseOctaves, float heightNoisePersistence, float heightNoiseLacunarity,
        float heightAmplitude, Vector2 heightOffset,
        bool enableCaves, float caveThreshold, float caveScale, int caveOctaves, Vector3 caveOffset,
        int seed, bool buildMesh = true, int[] savedBlockData = null)
    {
        this.chunkX = chunkX;
        this.chunkZ = chunkZ;

        this.heightNoiseScale = heightNoiseScale;
        this.heightNoiseOctaves = heightNoiseOctaves;
        this.heightNoisePersistence = heightNoisePersistence;
        this.heightNoiseLacunarity = heightNoiseLacunarity;
        this.heightAmplitude = heightAmplitude;
        this.heightOffset = heightOffset;

        this.cavesEnabled = enableCaves;
        this.caveThreshold = caveThreshold;
        this.caveScale = caveScale;
        this.caveOctaves = caveOctaves;
        this.caveOffset = caveOffset;
        this.generatorSeed = seed; // remember seed for deterministic ore veins

        if (savedBlockData != null)
        {
            SetBlockData(savedBlockData);
        }
        else
        {
            GenerateBlocksFromNoise();
        }

        // PopulateOakTrees(seed, attempts: 12, chance: 0.18f, minTrunk: 4, maxTrunk: 6, leafRadius: 2);
        ComputeInitialSkylight();

        if (buildMesh)
        {
            BuildMesh(); // initial mesh build (not throttled)
        }
        
        // ALWAYS update collider even if visual mesh is deferred
        // This ensures player can walk on terrain immediately
        InitializeCollisionMesh();
        UpdateCollider();
    }

    private void GenerateBlocksFromNoise()
    {
        int totalPotentialOreCount = 0; // counts ore placements BEFORE caves
        float maxOreNoise = -999f;
        const float oreThreshold = 0.45f;

        for (int lx = 0; lx < BlockData.ChunkWidth; lx++)
        {
            for (int lz = 0; lz < BlockData.ChunkWidth; lz++)
            {
                int globalX = chunkX * BlockData.ChunkWidth + lx;
                int globalZ = chunkZ * BlockData.ChunkWidth + lz;

                float h = Noise.PerlinNoise2D(globalX, globalZ, heightNoiseOctaves, heightNoisePersistence,
                    heightNoiseLacunarity, heightNoiseScale, heightOffset);
                float mapped = (h + 1f) * 0.5f;
                int columnHeight =
                    Mathf.Clamp(Mathf.FloorToInt(mapped * heightAmplitude), 0, BlockData.ChunkHeight - 1);

                for (int y = 0; y < BlockData.ChunkHeight; y++)
                {
                    // Bedrock layer at bottom (unbreakable)
                    if (y < bedrockLayerCount)
                    {
                        blocks[lx, y, lz] = BlockType.Bedrock;
                    }
                    else if (y < columnHeight && y > columnHeight - 4) blocks[lx, y, lz] = BlockType.Dirt;
                    else if (y <= columnHeight - 4)
                    {
                        // Iron Ore generation (Minecraft-like)
                        // Spawns below configurable height (use <64 by default)
                        // Uses 3D noise for veins. Tune octave/scale/threshold for visible veins.
                        if (y < 64)
                        {
                            // More permissive defaults: octaves=3, scale=12f, threshold=oreThreshold
                            float oreNoise = Noise.PerlinNoise3D(globalX, y, globalZ, 3, 0.5f, 2f, 12f, new Vector3(100, 100, 100));
                            if (oreNoise > maxOreNoise) maxOreNoise = oreNoise;
                            if (oreNoise > oreThreshold)
                            {
                                blocks[lx, y, lz] = BlockType.IronOre;
                                totalPotentialOreCount++;
                            }
                            else
                            {
                                blocks[lx, y, lz] = BlockType.Stone;
                            }
                        }
                        else
                        {
                            blocks[lx, y, lz] = BlockType.Stone;
                        }
                    }
                    else if (y == columnHeight) blocks[lx, y, lz] = BlockType.Grass;
                    else blocks[lx, y, lz] = BlockType.Air;
                }
            }
        }

        if (cavesEnabled)
        {
            for (int x = 0; x < BlockData.ChunkWidth; x++)
            {
                for (int y = 0; y < BlockData.ChunkHeight; y++)
                {
                    for (int z = 0; z < BlockData.ChunkWidth; z++)
                    {
                        int globalX = chunkX * BlockData.ChunkWidth + x;
                        int globalY = y;
                        int globalZ = chunkZ * BlockData.ChunkWidth + z;

                        float n = Noise.PerlinNoise3D(globalX, globalY, globalZ, caveOctaves, heightNoisePersistence,
                            heightNoiseLacunarity, caveScale, caveOffset);
                        if (n > caveThreshold)
                        {
                            blocks[x, y, z] = BlockType.Air;
                        }
                    }
                }
            }
        }

        // Generate veins (random-walk) after caves so veins are placed into remaining stone
        // Use deterministic PRNG from chunk seed so veins are stable
        if (ironVeinAttempts > 0)
        {
            System.Random pr = new System.Random(generatorSeed ^ (chunkX * 734287) ^ (chunkZ * 912783));
            for (int attempt = 0; attempt < ironVeinAttempts; attempt++)
            {
                int sx = pr.Next(0, BlockData.ChunkWidth);
                int sy = pr.Next(1, Mathf.Min(ironMaxHeight, BlockData.ChunkHeight - 1));
                int sz = pr.Next(0, BlockData.ChunkWidth);
                int veinSize = pr.Next(ironVeinSizeMin, ironVeinSizeMax + 1);

                for (int i = 0; i < veinSize; i++)
                {
                    // Place only inside stone blocks
                    if (blocks[sx, sy, sz] == BlockType.Stone)
                    {
                        blocks[sx, sy, sz] = BlockType.IronOre;
                    }

                    // random walk step
                    sx += pr.Next(-1, 2);
                    sy += pr.Next(-1, 2);
                    sz += pr.Next(-1, 2);

                    // clamp to chunk bounds
                    sx = Mathf.Clamp(sx, 0, BlockData.ChunkWidth - 1);
                    sy = Mathf.Clamp(sy, 0, BlockData.ChunkHeight - 1);
                    sz = Mathf.Clamp(sz, 0, BlockData.ChunkWidth - 1);
                }
            }
        }

        // Optional debug: count iron ore in this chunk if debug logging is enabled
        if (debugLogCounts)
        {
            int ironCount = 0;
            for (int x = 0; x < BlockData.ChunkWidth; x++)
            {
                for (int y = 0; y < BlockData.ChunkHeight; y++)
                {
                    for (int z = 0; z < BlockData.ChunkWidth; z++)
                    {
                        if (blocks[x, y, z] == BlockType.IronOre) ironCount++;
                    }
                }
            }
            Debug.Log($"[Chunk:{chunkX},{chunkZ}] IronOre count (after caves+veins) = {ironCount}");
            Debug.Log($"[Chunk:{chunkX},{chunkZ}] Potential Ore placements (before caves) = {totalPotentialOreCount}, maxOreNoise = {maxOreNoise:F3}");

            if (ironCount > 0 && FindTextureData(BlockType.IronOre) == null)
            {
                Debug.LogWarning($"[Chunk:{chunkX},{chunkZ}] IronOre found but no BlockTextureData assigned. Add an IronOre BlockTextureData in Resources/BlockTextures so iron looks distinct.");
            }

            if (totalPotentialOreCount == 0)
            {
                Debug.LogWarning($"[Chunk:{chunkX},{chunkZ}] No ore candidates found (maxNoise={maxOreNoise:F3}). Consider lowering oreThreshold or adjusting noise parameters.");
            }
        }
    }

    #endregion

    // Initialize lighting arrays
    private void InitLightArrays()
    {
        int W = BlockData.ChunkWidth;
        int H = BlockData.ChunkHeight;
        skyLight = new byte[W, H, W];
        blockLight = new byte[W, H, W];
    }

    // Apply vertex colors based on combined light arrays (smooth-ish by averaging adjacent cells)
    private void ApplyLightColorsToMesh()
    {
        if (vertexLightInfos.Count != vertices.Count)
        {
            Debug.LogWarning(
                $"Chunk {name}: vertexLightInfos.Count ({vertexLightInfos.Count}) != vertices.Count ({vertices.Count}). Rebuilding mapping.");
            // Attempt to recover: clear and rebuild simple 1:1 mapping where possible
            vertexLightInfos.Clear();
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v = vertices[i];
                int bx = Mathf.FloorToInt(v.x);
                int by = Mathf.FloorToInt(v.y);
                int bz = Mathf.FloorToInt(v.z);
                byte cx = (byte)Mathf.Clamp(Mathf.RoundToInt(v.x - bx), 0, 1);
                byte cy = (byte)Mathf.Clamp(Mathf.RoundToInt(v.y - by), 0, 1);
                byte cz = (byte)Mathf.Clamp(Mathf.RoundToInt(v.z - bz), 0, 1);
                vertexLightInfos.Add(new VertexLightInfo
                    { bx = bx, by = by, bz = bz, cornerX = cx, cornerY = cy, cornerZ = cz, tint = Color.white });
            }
        }

        int vertCount = vertices.Count;
        Color32[] colors = new Color32[vertCount];
        uv2s.Clear();

        for (int i = 0; i < vertCount; i++)
        {
            var info = vertexLightInfos[i];
            int bx = info.bx;
            int by = info.by;
            int bz = info.bz;

            // Sample the 4 blocks adjacent to this vertex corner
            // A vertex corner touches exactly 4 blocks in a 2x2x2 configuration
            // We need to sample based on which corner of the block this vertex represents

            int skySum = 0;
            int blockSum = 0;
            int sampleCount = 0;
            int aoCount = 0; // count of opaque blocks for ambient occlusion

            // Determine which direction to sample based on corner position
            // cornerX/Y/Z are 0 or 1, indicating which corner of the block
            int[] xOffsets = info.cornerX == 0 ? new int[] { -1, 0 } : new int[] { 0, 1 };
            int[] yOffsets = info.cornerY == 0 ? new int[] { -1, 0 } : new int[] { 0, 1 };
            int[] zOffsets = info.cornerZ == 0 ? new int[] { -1, 0 } : new int[] { 0, 1 };

            // Sample 4 blocks around the vertex (2x2 grid on the face plane)
            // For each axis, we sample 2 positions
            for (int xo = 0; xo < 2; xo++)
            {
                for (int yo = 0; yo < 2; yo++)
                {
                    for (int zo = 0; zo < 2; zo++)
                    {
                        int tx = bx + xOffsets[xo];
                        int ty = by + yOffsets[yo];
                        int tz = bz + zOffsets[zo];

                        // Convert to global coords
                        int globalX = chunkX * BlockData.ChunkWidth + tx;
                        int globalY = ty;
                        int globalZ = chunkZ * BlockData.ChunkWidth + tz;

                        // Get light values across chunk boundaries
                        int skyL = 0;
                        int blockL = 0;
                        bool isOpaque = false;

                        if (WorldGenerator.Instance != null)
                        {
                            skyL = WorldGenerator.Instance.GetSkyLightAtGlobal(globalX, globalY, globalZ);
                            blockL = WorldGenerator.Instance.GetBlockLightAtGlobal(globalX, globalY, globalZ);
                            isOpaque = WorldGenerator.Instance.IsBlockOpaqueAtGlobal(globalX, globalY, globalZ);
                        }
                        else
                        {
                            // Fallback to local only
                            if (InBounds(tx, ty, tz))
                            {
                                skyL = skyLight[tx, ty, tz];
                                blockL = blockLight[tx, ty, tz];
                                isOpaque = BlockData.IsOpaque(blocks[tx, ty, tz]);
                            }
                        }

                        skySum += skyL;
                        blockSum += blockL;
                        sampleCount++;

                        // Count opaque blocks for ambient occlusion
                        if (isOpaque) aoCount++;
                    }
                }
            }

            // Average light value
            float avgSky = sampleCount > 0 ? (float)skySum / sampleCount : 0f;
            float avgBlock = sampleCount > 0 ? (float)blockSum / sampleCount : 0f;

            // Apply ambient occlusion based on number of opaque neighbors
            // Minecraft-style AO: each opaque block darkens by 20% (tuned)
            float aoFactor = 1.0f - (aoCount / 8.0f) * 0.3f; // 8 samples max, darken up to 30%

            // Compute ambient minimum normalized (0..1)
            float ambientMinimum = Mathf.Max(ambientLightLevel, 2);
            float ambientMinimumNorm = ambientMinimum / 15f;

            // Store in uv2 (normalized values): ensure skylight component never drops below ambient floor
            float skyNorm = Mathf.Max((avgSky * aoFactor) / 15f, ambientMinimumNorm);
            float blockNorm = Mathf.Max((avgBlock * aoFactor) / 15f, 0f);
            uv2s.Add(new Vector2(skyNorm, blockNorm));

            // Combine light and AO for vertex alpha (legacy / debug) - keep consistent with uv2
            float maxLight = Mathf.Max(avgSky, avgBlock);
            float finalLightValue = Mathf.Max(maxLight * aoFactor, ambientMinimum);

            // Map 0..15 -> 0..255
            float lightNorm = Mathf.Clamp01(finalLightValue / 15f);
            Color tint = info.tint;
            
            // New Encoding: RGB = Tint Color, A = Light Level
            byte r = (byte)(tint.r * 255f);
            byte g = (byte)(tint.g * 255f);
            byte b = (byte)(tint.b * 255f);
            byte a = (byte)(lightNorm * 255f);
            
            colors[i] = new Color32(r, g, b, a);
        }

        mesh.colors32 = colors;
        mesh.SetUVs(1, uv2s);
    }

    #region Lighting: skylight + block-light API

    // Helpers
    private bool InBounds(int x, int y, int z) => x >= 0 && x < BlockData.ChunkWidth && y >= 0 &&
                                                  y < BlockData.ChunkHeight && z >= 0 && z < BlockData.ChunkWidth;

    public bool IsOpaqueLocal(int lx, int y, int lz)
    {
        if (!InBounds(lx, y, lz)) return true;
        return BlockData.IsOpaque(blocks[lx, y, lz]);
    }

    // Accessors for WorldLightManager
    public int GetBlockLightLocal(int lx, int y, int lz)
    {
        if (!InBounds(lx, y, lz)) return 0;
        return blockLight[lx, y, lz];
    }

    public void SetBlockLightLocal(int lx, int y, int lz, byte value)
    {
        if (!InBounds(lx, y, lz)) return;
        blockLight[lx, y, lz] = value;
        // mark color update required (deferred)
        lightColorsDirty = true;
    }

    // skylight compute (top-down) called once after generation
    public void ComputeInitialSkylight()
    {
        int W = BlockData.ChunkWidth;
        int H = BlockData.ChunkHeight;

        // reset arrays
        for (int x = 0; x < W; x++)
        for (int y = 0; y < H; y++)
        for (int z = 0; z < W; z++)
        {
            skyLight[x, y, z] = 0;
            blockLight[x, y, z] = 0;
        }

        for (int x = 0; x < W; x++)
        {
            for (int z = 0; z < W; z++)
            {
                int light = 15;
                for (int y = H - 1; y >= 0; y--)
                {
                    if (blocks[x, y, z] == BlockType.Air)
                    {
                        skyLight[x, y, z] = (byte)light;
                    }
                    else
                    {
                        if (BlockData.IsOpaque(blocks[x, y, z]))
                        {
                            skyLight[x, y, z] = 0;
                            light = 0;
                            break;
                        }
                        else
                        {
                            light = Math.Max(0, light - 1);
                            skyLight[x, y, z] = (byte)light;
                        }
                    }

                    if (light == 0) break;
                }
            }
        }
    }

    // Place a local (in-chunk) light source (torch). This seeds WorldLightManager to propagate across chunks.
    public void PlaceLightSourceLocal(int lx, int y, int lz)
    {
        if (!InBounds(lx, y, lz)) return;
        blockLight[lx, y, lz] = 15;
        // enqueue to world manager (global coords)
        int globalX = chunkX * BlockData.ChunkWidth + lx;
        int globalZ = chunkZ * BlockData.ChunkWidth + lz;
        WorldLightManager.Instance.PlaceLightSourceGlobal(globalX, y, globalZ);
    }

    // Remove local light source
    public void RemoveLightSourceLocal(int lx, int y, int lz)
    {
        if (!InBounds(lx, y, lz)) return;
        blockLight[lx, y, lz] = 0;
        int globalX = chunkX * BlockData.ChunkWidth + lx;
        int globalZ = chunkZ * BlockData.ChunkWidth + lz;
        WorldLightManager.Instance.RemoveLightSourceGlobal(globalX, y, globalZ);
    }
    
    // PUBLIC wrapper so external systems can recompute skylight and schedule mesh rebuild
    // Call this after all neighbor chunks have been spawned/registered.
    public void RecomputeSkylightAndUpdateMesh()
    {
        // recompute skylight arrays (fast-ish)
        ComputeInitialSkylight();

        // Instead of performing an immediate mesh build (which causes spikes), schedule the rebuild
        // via the WorldGenerator rebuild queue so it can be processed in a throttled way.
        if (WorldGenerator.Instance != null)
        {
            WorldGenerator.Instance.EnqueueChunkForRebuild(this);
        }
        else
        {
            // fallback: do immediate rebuild if no world manager exists
            BuildMesh();
            UpdateCollider();
        }
    }


    #endregion


    #region Mesh Building

    private void BuildMesh()
    {
        // clear but keep capacity to reduce allocations
        vertices.Clear();
        triangles.Clear();
        uvs.Clear();
        colors.Clear();
        uv2s.Clear();
        vertexLightInfos.Clear();
        
        colVertices.Clear();
        colTriangles.Clear();

        // Clear old crop objects
        foreach (var obj in cropObjects)
        {
            if (obj != null) Destroy(obj);
        }
        cropObjects.Clear();

        int faceCount = 0;

        for (int x = 0; x < BlockData.ChunkWidth; x++)
        {
            for (int y = 0; y < BlockData.ChunkHeight; y++)
            {
                for (int z = 0; z < BlockData.ChunkWidth; z++)
                {
                    BlockType type = blocks[x, y, z];
                    if (type == BlockType.Air) continue;

                    Vector3Int pos = new Vector3Int(x, y, z);

                    if (BlockData.IsCrop(type))
                    {
                        SpawnCropSprite(pos, type);
                        continue; // Skip mesh generation for crops
                    }

                    if (BlockData.IsSolid(type))
                    {
                        for (int face = 0; face < 6; face++)
                        {
                            Vector3Int neighbor = pos + BlockData.FaceChecks[face];

                            bool drawFace = false;
                            if (neighbor.x < 0 || neighbor.x >= BlockData.ChunkWidth ||
                                neighbor.y < 0 || neighbor.y >= BlockData.ChunkHeight ||
                                neighbor.z < 0 || neighbor.z >= BlockData.ChunkWidth)
                            {
                                drawFace = true;
                            }
                            else if (!BlockData.IsSolid(blocks[neighbor.x, neighbor.y, neighbor.z]))
                            {
                                drawFace = true;
                            }

                            if (drawFace)
                            {
                                AddFace(pos, face);
                                AddCollisionFace(pos, face);
                                faceCount++;
                            }
                        }
                    }
                    else if (BlockData.IsCrossMesh(type))
                    {
                        AddCrossMesh(pos);
                    }
                }
            }
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        ApplyLightColorsToMesh();
        meshFilter.mesh = mesh;
        
        // Update Collision Mesh
        collisionMesh.Clear();
        collisionMesh.SetVertices(colVertices);
        collisionMesh.SetTriangles(colTriangles, 0);
        collisionMesh.RecalculateNormals();
        collisionMesh.RecalculateBounds();

        // debug: print transform determinant
        // float detDebug = transform.localToWorldMatrix.determinant;
        // Debug.Log(
        //     $"Chunk {name}: transform det = {detDebug}, scale = {transform.localScale}, rotation = {transform.localEulerAngles}");
        //
        // if (debugLogCounts)
        //     Debug.Log(
        //         $"Chunk {name}: vertices={mesh.vertexCount}, triangles={mesh.triangles.Length / 3}, faces={faceCount}");
    }

    private BlockTextureData FindTextureData(BlockType type)
    {
        if (blockTextures == null) return null;
        for (int i = 0; i < blockTextures.Length; i++)
        {
            if (blockTextures[i] != null && blockTextures[i].blockType == type)
                return blockTextures[i];
        }

        return null;
    }

    private void AddFace(Vector3Int blockPos, int face)
    {
        int[] fv = BlockData.FaceVertices[face];
        int baseIndex = vertices.Count;

        // add 4 vertices for the face
        vertices.Add(blockPos + BlockData.Verts[fv[0]]);
        vertices.Add(blockPos + BlockData.Verts[fv[1]]);
        vertices.Add(blockPos + BlockData.Verts[fv[2]]);
        vertices.Add(blockPos + BlockData.Verts[fv[3]]);

        // add triangles
        triangles.Add(baseIndex + 0);
        triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 0);
        triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 3);

        // texture / UVs
        BlockType type = blocks[blockPos.x, blockPos.y, blockPos.z];
        BlockTextureData data = FindTextureData(type);

        Vector2[] faceUVs;
        if (data != null)
        {
            faceUVs = data.GetFaceUVs(face);
        }
        else
        {
            faceUVs = TextureAtlas.GetUVsFromTile(0, 0);
        }

        // orientation fixes as in your original code
        if (face == 1)
        {
            // Front (Z+) is correct — do nothing
        }
        else if (face == 0)
        {
            // Back (Z-) often needs horizontal flip so left/right match front
            faceUVs = RotateUVsCCW(faceUVs);
        }
        else if (face == 4)
        {
            // Left (X-) typically rotated CCW to stand upright
            faceUVs = RotateUVsCCW(faceUVs);
        }
        else if (face == 5)
        {
            // Right (X+) typically rotated CW to stand upright
            faceUVs = RotateUVsCCW(faceUVs);
        }
        // Top and bottom keep default orientation (face 2 and 3)

        uvs.AddRange(faceUVs);

        // Color calculation
        Color biomeColor = Color.white;
        bool applyTint = false;

        // Determine if we should apply biome tint
        if (type == BlockType.Leaves)
        {
            applyTint = true; // Leaves are tinted on all faces
        }
        else if (type == BlockType.Grass)
        {
            applyTint = true; // All faces (shader handles masking)
        }

        if (applyTint)
        {
            int globalX = chunkX * BlockData.ChunkWidth + blockPos.x;
            int globalZ = chunkZ * BlockData.ChunkWidth + blockPos.z;
            // Simple noise for temperature/humidity
            float temp = Mathf.PerlinNoise(globalX * 0.01f, globalZ * 0.01f);
            float hum = Mathf.PerlinNoise(globalX * 0.02f + 100f, globalZ * 0.02f + 100f);

            if (type == BlockType.Leaves)
                biomeColor = BiomeColor.GetFoliageColor(temp, hum);
            else
                biomeColor = BiomeColor.GetGrassColor(temp, hum);
        }
        
        // Apply colors to vertices (store per-vertex tint so lighting pass can use same colors)
        Color[] vertexTints = new Color[4];
        for (int i = 0; i < 4; i++)
        {
            Color finalColor = Color.white;
            if (applyTint)
            {
                // Apply biome tint to all vertices of the face.
                // The shader will handle masking based on texture saturation (keeping dirt brown, tinting grey grass).
                finalColor = biomeColor;
            }
            vertexTints[i] = finalColor;
            colors.Add(finalColor);
        }

        // --- IMPORTANT: record per-vertex light info so ApplyLightColorsToMesh can read it later ---
        // For each vertex, compute corner offsets (0 or 1) relative to the block position.
        // This lets us sample adjacent block cells to compute vertex lighting.
        for (int i = 0; i < 4; i++)
        {
            Vector3 localVert = blockPos + BlockData.Verts[fv[i]];
            // cornerX/Y/Z should be 0 or 1 because verts are block corners
            byte cx = (byte)Mathf.RoundToInt(localVert.x - blockPos.x);
            byte cy = (byte)Mathf.RoundToInt(localVert.y - blockPos.y);
            byte cz = (byte)Mathf.RoundToInt(localVert.z - blockPos.z);

            vertexLightInfos.Add(new VertexLightInfo
                { bx = blockPos.x, by = blockPos.y, bz = blockPos.z, cornerX = cx, cornerY = cy, cornerZ = cz, tint = vertexTints[i] });
        }
    }


    // UV helpers (rotate/flip quad UVs)
    // UV order assumed: [0]=bottom-left, [1]=bottom-right, [2]=top-right, [3]=top-left

    private Vector2[] RotateUVsCW(Vector2[] u)
    {
        // rotate 90 degrees clockwise
        // old: 0,1,2,3 -> new: 3,0,1,2
        return new Vector2[] { u[3], u[0], u[1], u[2] };
    }

    private Vector2[] RotateUVsCCW(Vector2[] u)
    {
        // rotate 90 degrees counter-clockwise
        // old: 0,1,2,3 -> new: 1,2,3,0
        return new Vector2[] { u[1], u[2], u[3], u[0] };
    }

    private Vector2[] FlipUVHorizontal(Vector2[] u)
    {
        // mirror left-right
        // old: 0,1,2,3 -> new: 1,0,3,2
        return new Vector2[] { u[1], u[0], u[3], u[2] };
    }

    private Vector2[] FlipUVVertical(Vector2[] u)
    {
        // mirror top-bottom
        // old: 0,1,2,3 -> new: 3,2,1,0
        return new Vector2[] { u[3], u[2], u[1], u[0] };
    }

    #endregion

    #region Public API for block changes

    public bool isModified = false;

    /// <summary>
    /// Safely set a local block inside this chunk (does not handle neighbor chunks).
    /// Call WorldGenerator.SetBlockAtGlobal to ensure neighbors are enqueued.
    /// </summary>
    public void SetBlockLocal(int lx, int y, int lz, BlockType type)
    {
        if (lx < 0 || lx >= BlockData.ChunkWidth ||
            y < 0 || y >= BlockData.ChunkHeight ||
            lz < 0 || lz >= BlockData.ChunkWidth) return;

        if (blocks[lx, y, lz] != type)
        {
            blocks[lx, y, lz] = type;
            isModified = true;
        }
    }

    public int[] GetBlockData()
    {
        int width = BlockData.ChunkWidth;
        int height = BlockData.ChunkHeight;
        int[] data = new int[width * height * width];
        int idx = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < width; z++)
                {
                    data[idx++] = (int)blocks[x, y, z];
                }
            }
        }
        return data;
    }

    public void SetBlockData(int[] data)
    {
        int width = BlockData.ChunkWidth;
        int height = BlockData.ChunkHeight;
        int idx = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < width; z++)
                {
                    if (idx < data.Length)
                        blocks[x, y, z] = (BlockType)data[idx++];
                }
            }
        }
        isModified = true;
    }

    /// <summary>
    /// Get block type at local coordinates
    /// </summary>
    public BlockType GetBlockLocal(int lx, int y, int lz)
    {
        if (lx < 0 || lx >= BlockData.ChunkWidth ||
            y < 0 || y >= BlockData.ChunkHeight ||
            lz < 0 || lz >= BlockData.ChunkWidth)
            return BlockType.Air;

        return blocks[lx, y, lz];
    }

    /// <summary>
    /// Return whether a local coordinate is solid (safe).
    /// </summary>
    public bool IsBlockSolidLocal(int lx, int y, int lz)
    {
        if (lx < 0 || lx >= BlockData.ChunkWidth ||
            y < 0 || y >= BlockData.ChunkHeight ||
            lz < 0 || lz >= BlockData.ChunkWidth) return false;

        return BlockData.IsSolid(blocks[lx, y, lz]);
    }

    // Called by WorldGenerator when it wants this chunk rebuilt (deferred)
    public void RebuildMeshDeferred(float delay)
    {
        needsRebuild = true;
        colliderPending = true;
        colliderDelay = delay;
        lastDirtyTime = Time.time;
    }

    // Mark dirty time (for collider scheduling)
    public void MarkDirtyTimestamp(float time)
    {
        lastDirtyTime = time;
    }

    // Immediate rebuild (for cases where you need it)
    public void RebuildMeshImmediate()
    {
        ComputeInitialSkylight();
        BuildMesh();
        UpdateCollider();
    }

    #endregion

    private void UpdateCollider()
    {
        // Assign existing mesh to collider (clear then assign helps in some cases)
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = collisionMesh;
    }

    private void InitializeCollisionMesh()
    {
        // Build collision mesh immediately for all solid blocks
        colVertices.Clear();
        colTriangles.Clear();

        for (int x = 0; x < BlockData.ChunkWidth; x++)
        {
            for (int y = 0; y < BlockData.ChunkHeight; y++)
            {
                for (int z = 0; z < BlockData.ChunkWidth; z++)
                {
                    BlockType type = blocks[x, y, z];
                    if (!BlockData.IsSolid(type)) continue;

                    Vector3Int pos = new Vector3Int(x, y, z);

                    for (int face = 0; face < 6; face++)
                    {
                        Vector3Int neighbor = pos + BlockData.FaceChecks[face];

                        bool drawFace = false;
                        if (neighbor.x < 0 || neighbor.x >= BlockData.ChunkWidth ||
                            neighbor.y < 0 || neighbor.y >= BlockData.ChunkHeight ||
                            neighbor.z < 0 || neighbor.z >= BlockData.ChunkWidth)
                        {
                            drawFace = true;
                        }
                        else if (!BlockData.IsSolid(blocks[neighbor.x, neighbor.y, neighbor.z]))
                        {
                            drawFace = true;
                        }

                        if (drawFace)
                        {
                            AddCollisionFace(pos, face);
                        }
                    }
                }
            }
        }

        collisionMesh.Clear();
        collisionMesh.SetVertices(colVertices);
        collisionMesh.SetTriangles(colTriangles, 0);
        collisionMesh.RecalculateNormals();
        collisionMesh.RecalculateBounds();
    }

    /// <summary>
    /// Populate oak trees deterministically for this chunk.
    /// Uses worldSeed and chunk coords so generation is repeatable.
    /// Only places a tree if the top local block at column is BlockType.Grass.
    /// It places blocks via WorldGenerator.Instance.SetBlockAtGlobal so neighbor chunks are handled.
    /// </summary>
    public void PopulateOakTrees(int worldSeed,
        int attempts = 10, // number of candidate positions per chunk
        float chance = 0.2f, // per-attempt probability to actually place a tree
        int minTrunk = 4,
        int maxTrunk = 6,
        int leafRadius = 2,
        BlockType trunkBlock = BlockType.Wood,
        BlockType leafBlock = BlockType.Leaves)
    {
        // Safety checks
        if (WorldGenerator.Instance == null)
        {
            Debug.LogWarning($"Chunk {name}: WorldGenerator.Instance is null — cannot place trees.");
            return;
        }

        // deterministic per-chunk RNG
        int combined = worldSeed;
        combined ^= (ChunkX * 73856093);
        combined ^= (ChunkZ * 19349663);
        System.Random pr = new System.Random(combined);

        for (int i = 0; i < attempts; i++)
        {
            if (pr.NextDouble() > chance) continue;

            // choose local coordinates
            int lx = pr.Next(0, BlockData.ChunkWidth);
            int lz = pr.Next(0, BlockData.ChunkWidth);

            // find top solid in this local column (scan from top downward)
            int topY = -1;
            for (int y = BlockData.ChunkHeight - 1; y >= 0; y--)
            {
                if (BlockData.IsSolid(blocks[lx, y, lz]))
                {
                    topY = y;
                    break;
                }
            }

            if (topY < 0) continue; // empty column

            // only plant on grass
            if (blocks[lx, topY, lz] != BlockType.Grass) continue;

            int gx = ChunkX * BlockData.ChunkWidth + lx;
            int gz = ChunkZ * BlockData.ChunkWidth + lz;
            int trunkBaseY = topY + 1;

            // ensure trunk fits
            if (trunkBaseY + minTrunk >= BlockData.ChunkHeight) continue;

            int trunkHeight = pr.Next(minTrunk, maxTrunk + 1);

            // place trunk blocks (use WorldGenerator to handle neighbor chunks)
            for (int y = trunkBaseY; y < trunkBaseY + trunkHeight; y++)
            {
                WorldGenerator.Instance.SetBlockAtGlobal(gx, y, gz, trunkBlock);
            }

            // place leaves in roughly spherical blob around top
            int leafCenterY = trunkBaseY + trunkHeight;
            for (int dx = -leafRadius; dx <= leafRadius; dx++)
            {
                for (int dy = -leafRadius; dy <= leafRadius; dy++)
                {
                    for (int dz = -leafRadius; dz <= leafRadius; dz++)
                    {
                        float dist = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                        if (dist > leafRadius + 0.5f) continue;

                        int tx = gx + dx;
                        int ty = leafCenterY + dy;
                        int tz = gz + dz;

                        if (ty < 0 || ty >= BlockData.ChunkHeight) continue;

                        // only place leaf if it's currently air (avoid overwriting existing solids)
                        if (!WorldGenerator.Instance.IsBlockSolidAtGlobal(tx, ty, tz))
                        {
                            WorldGenerator.Instance.SetBlockAtGlobal(tx, ty, tz, leafBlock);
                        }
                    }
                }
            }
            // one tree placed for this attempt; continue to next attempt
        }
    }

    // Public accessor to read skylight stored inside this chunk safely.
    // Returns 0 if out of bounds.
    public byte GetSkyLightLocal(int lx, int y, int lz)
    {
        if (lx < 0 || lx >= BlockData.ChunkWidth ||
            y < 0 || y >= BlockData.ChunkHeight ||
            lz < 0 || lz >= BlockData.ChunkWidth) return 0;
        return skyLight[lx, y, lz];
    }

    private void AddCollisionFace(Vector3Int blockPos, int face)
    {
        int[] fv = BlockData.FaceVertices[face];
        int baseIndex = colVertices.Count;

        // add 4 vertices for the face
        colVertices.Add(blockPos + BlockData.Verts[fv[0]]);
        colVertices.Add(blockPos + BlockData.Verts[fv[1]]);
        colVertices.Add(blockPos + BlockData.Verts[fv[2]]);
        colVertices.Add(blockPos + BlockData.Verts[fv[3]]);

        // add triangles
        colTriangles.Add(baseIndex + 0);
        colTriangles.Add(baseIndex + 1);
        colTriangles.Add(baseIndex + 2);
        colTriangles.Add(baseIndex + 0);
        colTriangles.Add(baseIndex + 2);
        colTriangles.Add(baseIndex + 3);
    }

    private void AddCrossMesh(Vector3Int pos)
    {
        int baseIndex = vertices.Count;
        
        // Plane 1 (Diagonal 1)
        vertices.Add(pos + new Vector3(0, 0, 0)); // 0
        vertices.Add(pos + new Vector3(1, 0, 1)); // 1
        vertices.Add(pos + new Vector3(1, 1, 1)); // 2
        vertices.Add(pos + new Vector3(0, 1, 0)); // 3
        
        // Plane 2 (Diagonal 2)
        vertices.Add(pos + new Vector3(0, 0, 1)); // 4
        vertices.Add(pos + new Vector3(1, 0, 0)); // 5
        vertices.Add(pos + new Vector3(1, 1, 0)); // 6
        vertices.Add(pos + new Vector3(0, 1, 1)); // 7
        
        // Triangles (Double sided)
        // Plane 1
        triangles.Add(baseIndex + 0); triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 1);
        triangles.Add(baseIndex + 0); triangles.Add(baseIndex + 3); triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 0); triangles.Add(baseIndex + 1); triangles.Add(baseIndex + 2);
        triangles.Add(baseIndex + 0); triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 3);
        
        // Plane 2
        triangles.Add(baseIndex + 4); triangles.Add(baseIndex + 6); triangles.Add(baseIndex + 5);
        triangles.Add(baseIndex + 4); triangles.Add(baseIndex + 7); triangles.Add(baseIndex + 6);
        triangles.Add(baseIndex + 4); triangles.Add(baseIndex + 5); triangles.Add(baseIndex + 6);
        triangles.Add(baseIndex + 4); triangles.Add(baseIndex + 6); triangles.Add(baseIndex + 7);
        
        // UVs
        BlockType type = blocks[pos.x, pos.y, pos.z];
        BlockTextureData data = FindTextureData(type);
        Vector2[] uvsArr = data != null ? data.GetFaceUVs(0) : TextureAtlas.GetUVsFromTile(0, 0);
        
        // Plane 1
        uvs.Add(uvsArr[0]); uvs.Add(uvsArr[1]); uvs.Add(uvsArr[2]); uvs.Add(uvsArr[3]);
        // Plane 2
        uvs.Add(uvsArr[0]); uvs.Add(uvsArr[1]); uvs.Add(uvsArr[2]); uvs.Add(uvsArr[3]);
        
        // Colors (White)
        for(int i=0; i<8; i++) colors.Add(Color.white);
        
        // Vertex Light Info
        // 0: 0,0,0
        vertexLightInfos.Add(new VertexLightInfo { bx = pos.x, by = pos.y, bz = pos.z, cornerX = 0, cornerY = 0, cornerZ = 0, tint = Color.white });
        // 1: 1,0,1
        vertexLightInfos.Add(new VertexLightInfo { bx = pos.x, by = pos.y, bz = pos.z, cornerX = 1, cornerY = 0, cornerZ = 1, tint = Color.white });
        // 2: 1,1,1
        vertexLightInfos.Add(new VertexLightInfo { bx = pos.x, by = pos.y, bz = pos.z, cornerX = 1, cornerY = 1, cornerZ = 1, tint = Color.white });
        // 3: 0,1,0
        vertexLightInfos.Add(new VertexLightInfo { bx = pos.x, by = pos.y, bz = pos.z, cornerX = 0, cornerY = 1, cornerZ = 0, tint = Color.white });
        
        // 4: 0,0,1
        vertexLightInfos.Add(new VertexLightInfo { bx = pos.x, by = pos.y, bz = pos.z, cornerX = 0, cornerY = 0, cornerZ = 1, tint = Color.white });
        // 5: 1,0,0
        vertexLightInfos.Add(new VertexLightInfo { bx = pos.x, by = pos.y, bz = pos.z, cornerX = 1, cornerY = 0, cornerZ = 0, tint = Color.white });
        // 6: 1,1,0
        vertexLightInfos.Add(new VertexLightInfo { bx = pos.x, by = pos.y, bz = pos.z, cornerX = 1, cornerY = 1, cornerZ = 0, tint = Color.white });
        // 7: 0,1,1
        vertexLightInfos.Add(new VertexLightInfo { bx = pos.x, by = pos.y, bz = pos.z, cornerX = 0, cornerY = 1, cornerZ = 1, tint = Color.white });
    }

    private void SpawnCropSprite(Vector3Int pos, BlockType type)
    {
        if (carrotSprites == null || carrotSprites.Length < 4) return;

        int stage = 0;
        if (type == BlockType.CarrotStage0) stage = 0;
        else if (type == BlockType.CarrotStage1) stage = 1;
        else if (type == BlockType.CarrotStage2) stage = 2;
        else if (type == BlockType.CarrotStage3) stage = 3;

        Sprite sprite = carrotSprites[stage];
        if (sprite == null) return;

        GameObject cropObj = new GameObject("Crop_" + pos);
        cropObj.transform.SetParent(this.transform);
        cropObj.transform.localPosition = pos; // Local position is the corner (0,0,0) of the block

        MeshFilter mf = cropObj.AddComponent<MeshFilter>();
        MeshRenderer mr = cropObj.AddComponent<MeshRenderer>();

        // Generate Mesh
        Mesh mesh = new Mesh();
        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // Calculate UV bounds from sprite (handles atlases if not rotated)
        Vector2[] sUVs = sprite.uv;
        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;
        foreach (var uv in sUVs)
        {
            if (uv.x < minX) minX = uv.x;
            if (uv.y < minY) minY = uv.y;
            if (uv.x > maxX) maxX = uv.x;
            if (uv.y > maxY) maxY = uv.y;
        }
        
        // BL, BR, TR, TL
        Vector2 bl = new Vector2(minX, minY);
        Vector2 br = new Vector2(maxX, minY);
        Vector2 tr = new Vector2(maxX, maxY);
        Vector2 tl = new Vector2(minX, maxY);
        
        Vector2[] quadUVs = new Vector2[] { bl, br, tr, tl };

        // Helper to add quad
        void AddQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector2[] qUVs)
        {
            int baseIdx = verts.Count;
            verts.Add(v0);
            verts.Add(v1);
            verts.Add(v2);
            verts.Add(v3);
            
            tris.Add(baseIdx + 0);
            tris.Add(baseIdx + 2);
            tris.Add(baseIdx + 1);
            tris.Add(baseIdx + 0);
            tris.Add(baseIdx + 3);
            tris.Add(baseIdx + 2);

            uvs.Add(qUVs[0]); // BL
            uvs.Add(qUVs[1]); // BR
            uvs.Add(qUVs[2]); // TR
            uvs.Add(qUVs[3]); // TL
        }
        
        // Vertices
        Vector3 p0 = new Vector3(0, 0, 0);
        Vector3 p1 = new Vector3(1, 0, 0);
        Vector3 p2 = new Vector3(1, 1, 0);
        Vector3 p3 = new Vector3(0, 1, 0);
        Vector3 p4 = new Vector3(0, 0, 1);
        Vector3 p5 = new Vector3(1, 0, 1);
        Vector3 p6 = new Vector3(1, 1, 1);
        Vector3 p7 = new Vector3(0, 1, 1);

        // Front (Z+): 4, 5, 6, 7
        AddQuad(p4, p5, p6, p7, quadUVs);
        
        // Back (Z-): 1, 0, 3, 2
        AddQuad(p1, p0, p3, p2, quadUVs);
        
        // Left (X-): 0, 4, 7, 3
        AddQuad(p0, p4, p7, p3, quadUVs);
        
        // Right (X+): 5, 1, 2, 6
        AddQuad(p5, p1, p2, p6, quadUVs);

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        mf.mesh = mesh;

        // Material and Texture Setup
        Material mat;
        if (cropMaterial != null)
        {
            mat = new Material(cropMaterial); // Clone material
        }
        else
        {
            // Create material with Cutout shader
            mat = new Material(Shader.Find("Standard"));
            mat.SetFloat("_Mode", 1); // Cutout mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            mat.SetInt("_ZWrite", 1);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 2450;
        }
        
        // Assign texture directly to material
        mat.mainTexture = sprite.texture;
        mr.material = mat;

        // Collider
        BoxCollider col = cropObj.AddComponent<BoxCollider>();
        col.center = new Vector3(0.5f, 0.5f, 0.5f);
        col.size = new Vector3(0.8f, 0.8f, 0.8f);
        col.isTrigger = true;

        CropBlock cb = cropObj.AddComponent<CropBlock>();
        cb.chunk = this;
        cb.localPosition = pos;

        cropObj.layer = this.gameObject.layer;
        cropObjects.Add(cropObj);
    }

    private void RandomTick()
    {
        bool anyGrowth = false;
        
        // Iterate through all crop objects instead of random sampling
        foreach (var cropObj in cropObjects)
        {
            if (cropObj == null) continue;
            
            CropBlock cropBlock = cropObj.GetComponent<CropBlock>();
            if (cropBlock == null) continue;
            
            Vector3Int pos = cropBlock.localPosition;
            BlockType type = blocks[pos.x, pos.y, pos.z];
            
            if (type >= BlockType.CarrotStage0 && type < BlockType.CarrotStage3)
            {
                if (UnityEngine.Random.value < 0.3f)
                {
                    blocks[pos.x, pos.y, pos.z] = type + 1;
                    anyGrowth = true;
                }
            }
        }
        
        if (anyGrowth)
        {
            BuildMesh();
        }
    }
}

