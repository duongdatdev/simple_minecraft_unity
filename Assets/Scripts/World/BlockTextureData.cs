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

    [Header("Textures (Assign these as Sprites)")]
    public Sprite upSprite;
    public Sprite downSprite;
    public Sprite frontSprite;
    public Sprite backSprite;
    public Sprite leftSprite;
    public Sprite rightSprite;

    [Header("Legacy Textures (Texture2D) - used for migration if present")]
    [HideInInspector]
    public Texture2D upTexture;
    [HideInInspector]
    public Texture2D downTexture;
    [HideInInspector]
    public Texture2D frontTexture;
    [HideInInspector]
    public Texture2D backTexture;
    [HideInInspector]
    public Texture2D leftTexture;
    [HideInInspector]
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

#if UNITY_EDITOR
    // Migrate legacy Texture2D fields to Sprites when possible so older assets still work.
    private void OnValidate()
    {
        bool dirty = false;
        if (upSprite == null && upTexture != null)
        {
            upSprite = Sprite.Create(upTexture, new Rect(0, 0, upTexture.width, upTexture.height), new Vector2(0.5f, 0.5f), 100f);
            dirty = true;
        }
        if (downSprite == null && downTexture != null)
        {
            downSprite = Sprite.Create(downTexture, new Rect(0, 0, downTexture.width, downTexture.height), new Vector2(0.5f, 0.5f), 100f);
            dirty = true;
        }
        if (frontSprite == null && frontTexture != null)
        {
            frontSprite = Sprite.Create(frontTexture, new Rect(0, 0, frontTexture.width, frontTexture.height), new Vector2(0.5f, 0.5f), 100f);
            dirty = true;
        }
        if (backSprite == null && backTexture != null)
        {
            backSprite = Sprite.Create(backTexture, new Rect(0, 0, backTexture.width, backTexture.height), new Vector2(0.5f, 0.5f), 100f);
            dirty = true;
        }
        if (leftSprite == null && leftTexture != null)
        {
            leftSprite = Sprite.Create(leftTexture, new Rect(0, 0, leftTexture.width, leftTexture.height), new Vector2(0.5f, 0.5f), 100f);
            dirty = true;
        }
        if (rightSprite == null && rightTexture != null)
        {
            rightSprite = Sprite.Create(rightTexture, new Rect(0, 0, rightTexture.width, rightTexture.height), new Vector2(0.5f, 0.5f), 100f);
            dirty = true;
        }
        if (dirty)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}