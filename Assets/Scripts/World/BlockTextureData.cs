using UnityEngine;

/// <summary>
/// Defines which tiles a block uses in the texture atlas.
/// Each block can have a different top, side, and bottom tile.
/// </summary>
[CreateAssetMenu(menuName = "Voxel/Block Texture Data")]
public class BlockTextureData : ScriptableObject
{
    [Header("Block Type")]
    public BlockType blockType;

    [Header("Textures (Assign these)")]
    public Texture2D upTexture;
    public Texture2D downTexture;
    public Texture2D frontTexture;
    public Texture2D backTexture;
    public Texture2D leftTexture;
    public Texture2D rightTexture;

    [Header("Tile coordinates in atlas (Auto-generated)")]
    public Vector2Int up;
    public Vector2Int down;
    public Vector2Int front;
    public Vector2Int back;
    public Vector2Int left;
    public Vector2Int right;

    [Header("Settings")]
    public bool useBiomeTint = false;

    /// <summary>
    /// Get UVs for a given face.
    /// face = 2 → top
    /// face = 3 → bottom
    /// face = 1 → front
    /// face = 0 → back
    /// face = 4 → left
    /// face = 5 → right
    /// </summary>
    public Vector2[] GetFaceUVs(int face)
    {
        Vector2Int tile;
        switch (face)
        {
            case 2: tile = up; break;      // +Y (top)
            case 3: tile = down; break;    // -Y (bottom)
            case 1: tile = front; break;   // +Z (front)
            case 0: tile = back; break;    // -Z (back)
            case 4: tile = left; break;    // -X (left)
            case 5: tile = right; break;   // +X (right)
            default: tile = up; break;
        }

        return TextureAtlas.GetUVsFromTile(tile.x, tile.y);
    }
}