using UnityEngine;

/// <summary>
/// Helper for reading UV coordinates from a texture atlas.
/// Supports any atlas size and tile count.
/// Origin: bottom-left (0,0)
/// </summary>
public static class TextureAtlas
{
    // Tile count (adjust to match your atlas)
    public static int tilesPerRow = 64;     // 384 / 16
    public static int tilesPerColumn = 32;  // 704 / 16

    /// <summary>
    /// Return UVs for a tile using bottom-left origin coordinates.
    /// </summary>
    public static Vector2[] GetUVsFromTile(int tileX, int tileY, int tileWidthInTiles = 1, int tileHeightInTiles = 1)
    {
        float tileW = 1f / tilesPerRow;
        float tileH = 1f / tilesPerColumn;

        // Unity's UV origin is bottom-left
        float x = tileX * tileW;
        float y = tileY * tileH;

        float w = tileWidthInTiles * tileW;
        float h = tileHeightInTiles * tileH;

        // return UVs in correct winding order for Unity quads
        return new Vector2[]
        {
            new Vector2(x, y),
            new Vector2(x + w, y),
            new Vector2(x + w, y + h),
            new Vector2(x, y + h)
        };
    }

}