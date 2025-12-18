// Assets/Scripts/World/BlockData.cs

using UnityEngine;

/// <summary>
/// Block constants and face definitions for voxel mesh generation.
/// Keep indices and face directions consistent with Unity's coordinate system (+Z forward).
/// </summary>

public static class BlockData
{
    public static readonly int ChunkWidth = 16;
    public static readonly int ChunkHeight = 128; // use smaller height for faster debug
    
    // 8 cube corner vertices (local block space)
    public static readonly Vector3[] Verts = new Vector3[8]
    {
        new Vector3(0, 0, 0), // 0
        new Vector3(1, 0, 0), // 1
        new Vector3(1, 1, 0), // 2
        new Vector3(0, 1, 0), // 3
        new Vector3(0, 0, 1), // 4
        new Vector3(1, 0, 1), // 5
        new Vector3(1, 1, 1), // 6
        new Vector3(0, 1, 1) // 7
    };

    // Face vertex indices in perimeter order so two triangles share same outward normal
    // Face order: 0 = Back (-Z), 1 = Front (+Z), 2 = Top (+Y), 3 = Bottom (-Y), 4 = Left (-X), 5 = Right (+X)
    public static readonly int[][] FaceVertices = new int[6][]
    {
        new int[] { 0, 3, 2, 1 }, // Back  (-Z)
        new int[] { 4, 5, 6, 7 }, // Front (+Z)
        new int[] { 3, 7, 6, 2 }, // Top   (+Y)
        new int[] { 0, 1, 5, 4 }, // Bottom(-Y)
        new int[] { 4, 7, 3, 0 }, // Left  (-X)
        new int[] { 1, 2, 6, 5 } // Right (+X)
    };

    // Offset to check neighbor block for each face (must correspond to FaceVertices index)
    public static readonly Vector3Int[] FaceChecks = new Vector3Int[6]
    {
        new Vector3Int(0, 0, -1), // Back (-Z)
        new Vector3Int(0, 0, 1), // Front (+Z)
        new Vector3Int(0, 1, 0), // Top (+Y)
        new Vector3Int(0, -1, 0), // Bottom (-Y)
        new Vector3Int(-1, 0, 0), // Left (-X)
        new Vector3Int(1, 0, 0) // Right (+X)
    };

    public static bool IsSolid(BlockType t) => t != BlockType.Air;
    
    public static bool IsOpaque(BlockType type)
    {
        switch (type)
        {
            case BlockType.Air:
                return false;
            // Add here any transparent blocks you want light to pass through (like glass, water)
            case BlockType.Water:
            case BlockType.Glass:
                return false;
            default:
                return true;
        }
    }
}