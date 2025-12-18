using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WorldLightManager
/// - Global BFS queue for block-light propagation across chunks.
/// - Handles enqueueing propagation nodes, processing limited nodes per frame (throttle).
/// - Provides API for placing/removing light sources and for chunk <-> manager communication.
/// </summary>
public class WorldLightManager : MonoBehaviour
{
    public static WorldLightManager Instance { get; private set; }

    // Node for queue: chunk coords + local coords + light value
    private struct LightNode
    {
        public int chunkX, chunkZ;
        public int x, y, z;
        public int light;
        public LightNode(int cx,int cz,int lx,int ly,int lz,int l)
        {
            chunkX = cx; chunkZ = cz; x = lx; y = ly; z = lz; light = l;
        }
    }

    private Queue<LightNode> queue = new Queue<LightNode>();

    [Header("Throttle")]
    [Tooltip("Max light nodes processed per frame")]
    public int maxNodesPerFrame = 3000;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this.gameObject);
        Instance = this;
    }

    private void Update()
    {
        ProcessQueue(maxNodesPerFrame);
    }

    // Enqueue a node (from any chunk) to propagate light; used for initial sources and cross-chunk propagation.
    public void EnqueueNode(int chunkX, int chunkZ, int localX, int y, int localZ, int light)
    {
        if (light <= 0) return;
        queue.Enqueue(new LightNode(chunkX, chunkZ, localX, y, localZ, light));
    }

    // Processing loop: BFS style propagation across chunks
    private void ProcessQueue(int maxNodes)
    {
        int processed = 0;
        while (processed < maxNodes && queue.Count > 0)
        {
            var node = queue.Dequeue();
            processed++;

            // find chunk
            Chunk chunk = WorldGenerator.Instance.FindChunkAt(node.chunkX, node.chunkZ);
            if (chunk == null) continue;

            // try to set this cell to node.light if it's greater than current
            int current = chunk.GetBlockLightLocal(node.x, node.y, node.z);
            if (node.light <= current) continue;

            chunk.SetBlockLightLocal(node.x, node.y, node.z, (byte)node.light);

            // enqueue neighbors with light-1 if allowed
            int nextLight = node.light - 1;
            if (nextLight <= 0) continue;

            // iterate 6 directions (use BlockData.FaceChecks)
            for (int f = 0; f < BlockData.FaceChecks.Length; f++)
            {
                Vector3Int d = BlockData.FaceChecks[f];
                int nx = node.x + d.x;
                int ny = node.y + d.y;
                int nz = node.z + d.z;
                int nChunkX = node.chunkX;
                int nChunkZ = node.chunkZ;

                // cross-chunk translation
                if (nx < 0) { nx += BlockData.ChunkWidth; nChunkX -= 1; }
                else if (nx >= BlockData.ChunkWidth) { nx -= BlockData.ChunkWidth; nChunkX += 1; }
                if (nz < 0) { nz += BlockData.ChunkWidth; nChunkZ -= 1; }
                else if (nz >= BlockData.ChunkWidth) { nz -= BlockData.ChunkWidth; nChunkZ += 1; }

                // bounds for y
                if (ny < 0 || ny >= BlockData.ChunkHeight) continue;

                // if neighbor chunk not loaded, skip (will be updated when chunk loads or later)
                Chunk neighborChunk = WorldGenerator.Instance.FindChunkAt(nChunkX, nChunkZ);
                if (neighborChunk == null) continue;

                // if neighbor is opaque, skip propagation
                if (neighborChunk.IsOpaqueLocal(nx, ny, nz)) continue;

                int neighborCurrent = neighborChunk.GetBlockLightLocal(nx, ny, nz);
                if (nextLight > neighborCurrent)
                {
                    // enqueue neighbor node
                    queue.Enqueue(new LightNode(nChunkX, nChunkZ, nx, ny, nz, nextLight));
                }
            }
        }
    }

    // Public helper: place a global light source (e.g. torch) at global coords
    public void PlaceLightSourceGlobal(int gx, int y, int gz)
    {
        int chunkX = Mathf.FloorToInt((float)gx / BlockData.ChunkWidth);
        int chunkZ = Mathf.FloorToInt((float)gz / BlockData.ChunkWidth);
        Chunk chunk = WorldGenerator.Instance.FindChunkAt(chunkX, chunkZ);
        if (chunk == null) return;
        int lx = gx - chunkX * BlockData.ChunkWidth;
        int lz = gz - chunkZ * BlockData.ChunkWidth;
        // set local cell to max light and enqueue
        chunk.SetBlockLightLocal(lx, y, lz, (byte)15);
        EnqueueNode(chunkX, chunkZ, lx, y, lz, 15);
    }

    // Public helper: remove a global light source (triggers removal + relight). Simplified: set to 0 and then relight neighbors by scanning neighborhood.
    // We implement a simple remove that clears the cell and re-propagates from any neighboring light.
    public void RemoveLightSourceGlobal(int gx, int y, int gz)
    {
        int chunkX = Mathf.FloorToInt((float)gx / BlockData.ChunkWidth);
        int chunkZ = Mathf.FloorToInt((float)gz / BlockData.ChunkWidth);
        Chunk chunk = WorldGenerator.Instance.FindChunkAt(chunkX, chunkZ);
        if (chunk == null) return;
        int lx = gx - chunkX * BlockData.ChunkWidth;
        int lz = gz - chunkZ * BlockData.ChunkWidth;

        // clear the cell
        chunk.SetBlockLightLocal(lx, y, lz, 0);

        // Collect neighbor positions only (no light value)
        List<(int cx,int cz,int lx2,int y2,int lz2)> relightSeeds = new List<(int,int,int,int,int)>();

        for (int f = 0; f < BlockData.FaceChecks.Length; f++)
        {
            Vector3Int d = BlockData.FaceChecks[f];
            int nx = lx + d.x;
            int ny = y + d.y;
            int nz = lz + d.z;
            int nChunkX = chunkX;
            int nChunkZ = chunkZ;

            if (nx < 0) { nx += BlockData.ChunkWidth; nChunkX -= 1; }
            else if (nx >= BlockData.ChunkWidth) { nx -= BlockData.ChunkWidth; nChunkX += 1; }
            if (nz < 0) { nz += BlockData.ChunkWidth; nChunkZ -= 1; }
            else if (nz >= BlockData.ChunkWidth) { nz -= BlockData.ChunkWidth; nChunkZ += 1; }

            if (ny < 0 || ny >= BlockData.ChunkHeight) continue;
            Chunk nChunk = WorldGenerator.Instance.FindChunkAt(nChunkX, nChunkZ);
            if (nChunk == null) continue;

            int nLight = nChunk.GetBlockLightLocal(nx, ny, nz);
            if (nLight > 0)
            {
                relightSeeds.Add((nChunkX, nChunkZ, nx, ny, nz));
            }
        }

        // When enqueueing, fetch current light from chunk and enqueue if > 0
        foreach (var s in relightSeeds)
        {
            Chunk seedChunk = WorldGenerator.Instance.FindChunkAt(s.cx, s.cz);
            if (seedChunk == null) continue;
            int currentLight = seedChunk.GetBlockLightLocal(s.lx2, s.y2, s.lz2);
            if (currentLight > 0)
                EnqueueNode(s.cx, s.cz, s.lx2, s.y2, s.lz2, currentLight);
        }
    }
}
