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

    [Header("Block Texture Data (assign assets)")]
    public BlockTextureData[] blockTextures; // assign ScriptableObjects in inspector

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

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();

        mesh = new Mesh { name = "ChunkMesh" };
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
        }

        // apply vertex color updates that were marked dirty (batched)
        if (lightColorsDirty)
        {
            ApplyLightColorsToMesh();
            lightColorsDirty = false;
        }

        // handle delayed collider update
        if (colliderPending && Time.time - lastDirtyTime >= colliderDelay)
        {
            UpdateCollider();
            colliderPending = false;
        }
    }

    #region Initialization & Noise

    public void Initialize(
        int chunkX, int chunkZ,
        float heightNoiseScale, int heightNoiseOctaves, float heightNoisePersistence, float heightNoiseLacunarity,
        float heightAmplitude, Vector2 heightOffset,
        bool enableCaves, float caveThreshold, float caveScale, int caveOctaves, Vector3 caveOffset,
        int seed)
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

        GenerateBlocksFromNoise();

        // PopulateOakTrees(seed, attempts: 12, chance: 0.18f, minTrunk: 4, maxTrunk: 6, leafRadius: 2);
        ComputeInitialSkylight();

        BuildMesh(); // initial mesh build (not throttled)
        UpdateCollider(); // initial collider
    }

    private void GenerateBlocksFromNoise()
    {
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
                    if (y < columnHeight && y > columnHeight - 4) blocks[lx, y, lz] = BlockType.Dirt;
                    else if (y <= columnHeight - 4) blocks[lx, y, lz] = BlockType.Stone;
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

        for (int i = 0; i < vertCount; i++)
        {
            var info = vertexLightInfos[i];
            int bx = info.bx;
            int by = info.by;
            int bz = info.bz;

            // Sample the 4 blocks adjacent to this vertex corner
            // A vertex corner touches exactly 4 blocks in a 2x2x2 configuration
            // We need to sample based on which corner of the block this vertex represents

            int lightSum = 0;
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

                        // Take maximum of sky and block light
                        int maxLight = Math.Max(skyL, blockL);
                        lightSum += maxLight;
                        sampleCount++;

                        // Count opaque blocks for ambient occlusion
                        if (isOpaque) aoCount++;
                    }
                }
            }

            // Average light value
            float avgLight = sampleCount > 0 ? (float)lightSum / sampleCount : 0f;

            // Apply ambient occlusion based on number of opaque neighbors
            // Minecraft-style AO: each opaque block darkens by 20%
            float aoFactor = 1.0f - (aoCount / 8.0f) * 0.4f; // 8 samples max, darken up to 40%

            // Combine light and AO
            float finalLightValue = avgLight * aoFactor;

            // Apply ambient minimum
            if (finalLightValue < ambientLightLevel)
            {
                finalLightValue = ambientLightLevel;
            }

            // Map 0..15 -> 0..255
            float lightNorm = Mathf.Clamp01(finalLightValue / 15f);
            Color tint = info.tint;
            
            byte r = (byte)(tint.r * lightNorm * 255f);
            byte g = (byte)(tint.g * lightNorm * 255f);
            byte b = (byte)(tint.b * lightNorm * 255f);
            
            colors[i] = new Color32(r, g, b, 255);
        }

        mesh.colors32 = colors;
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
    
    // PUBLIC wrapper so external systems can recompute skylight and update mesh/collider
    // Call this after all neighbor chunks have been spawned/registered.
    public void RecomputeSkylightAndUpdateMesh()
    {
        // call private skylight compute
        ComputeInitialSkylight();

        // rebuild mesh and update collider
        BuildMesh();
        UpdateCollider();
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
        vertexLightInfos.Clear();

        int faceCount = 0;

        for (int x = 0; x < BlockData.ChunkWidth; x++)
        {
            for (int y = 0; y < BlockData.ChunkHeight; y++)
            {
                for (int z = 0; z < BlockData.ChunkWidth; z++)
                {
                    if (!BlockData.IsSolid(blocks[x, y, z])) continue;

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
                            AddFace(pos, face);
                            faceCount++;
                        }
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
        else if (data != null && data.useBiomeTint)
        {
            // Grass/etc: Tint top face fully, and side faces with gradient (if it's Grass)
            if (face == 2) applyTint = true; // Top
            else if (type == BlockType.Grass && face != 3) applyTint = true; // Sides (not bottom)
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
                if (type == BlockType.Leaves || face == 2)
                {
                    // Full tint for Leaves or Top face of Grass
                    finalColor = biomeColor;
                }
                else
                {
                    // Side face of Grass: Gradient tint
                    // Top vertices (y=1) get biomeColor, Bottom vertices (y=0) get white
                    if (BlockData.Verts[fv[i]].y > 0.5f)
                        finalColor = biomeColor;
                    else
                        finalColor = Color.white;
                }
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

    /// <summary>
    /// Safely set a local block inside this chunk (does not handle neighbor chunks).
    /// Call WorldGenerator.SetBlockAtGlobal to ensure neighbors are enqueued.
    /// </summary>
    public void SetBlockLocal(int lx, int y, int lz, BlockType type)
    {
        if (lx < 0 || lx >= BlockData.ChunkWidth ||
            y < 0 || y >= BlockData.ChunkHeight ||
            lz < 0 || lz >= BlockData.ChunkWidth) return;

        blocks[lx, y, lz] = type;
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
        meshCollider.sharedMesh = mesh;
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
}

