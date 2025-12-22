using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;

[CustomEditor(typeof(BlockTextureData))]
public class BlockTextureDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BlockTextureData data = (BlockTextureData)target;

        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);

        if (GUILayout.Button("Auto-Assign Textures by Name"))
        {
            AutoAssignTextures(data);
        }
    }

    private void AutoAssignTextures(BlockTextureData data)
    {
        // 1. Determine base name
        // Strategy: Use the asset name, remove "_Block" suffix, convert to snake_case or just search loosely.
        string assetPath = AssetDatabase.GetAssetPath(data);
        string directory = Path.GetDirectoryName(assetPath);
        string filename = Path.GetFileNameWithoutExtension(assetPath); // e.g. "CraftingTable_Block"

        // Remove "_Block" suffix if present
        string baseName = filename.Replace("_Block", "").Replace("Block", ""); // "CraftingTable"
        
        // Try to find textures in the same directory or subdirectories? 
        // Usually textures might be in a Textures folder.
        // Let's search in the whole project or just the current folder? 
        // Searching whole project is safer but slower. Let's search in the same folder first.
        
        // Prefer searching for Sprite assets in project BlockTextures folder if it exists
        List<string> searchFolders = new List<string>();
        string blockTexturesPath = "Assets/Resources/BlockTextures";
        if (Directory.Exists(blockTexturesPath)) searchFolders.Add(blockTexturesPath);
        string blockSub = Path.Combine(blockTexturesPath, "block");
        if (Directory.Exists(blockSub)) searchFolders.Add(blockSub);

        string[] guids = new string[0];
        // Try preferred folders first
        foreach (var folder in searchFolders)
        {
            guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            if (guids.Length > 0)
            {
                Debug.Log($"AutoAssign: using sprites from {folder}");
                break;
            }
        }

        // If nothing found in preferred folders, search the same directory, then whole project
        if (guids.Length == 0)
        {
            guids = AssetDatabase.FindAssets("t:Sprite", new[] { directory });
            if (guids.Length == 0)
            {
                // Fallback to searching whole project for sprites, then textures
                guids = AssetDatabase.FindAssets("t:Sprite");
                if (guids.Length == 0) guids = AssetDatabase.FindAssets("t:Texture2D");
            }
        }

        List<Sprite> spriteCandidates = new List<Sprite>();
        List<Texture2D> textureCandidates = new List<Texture2D>();

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) spriteCandidates.Add(sp);
            else
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) textureCandidates.Add(tex);
            }
        }

        // Helper to find best match
        // Helper searches prefer sprites first
        Sprite FindSpriteMatch(string suffix)
        {
            string snake = ToSnakeCase(baseName);
            var match = spriteCandidates.FirstOrDefault(s => s.name.Equals($"{snake}_{suffix}", System.StringComparison.OrdinalIgnoreCase));
            if (match) return match;
            match = spriteCandidates.FirstOrDefault(s => s.name.Equals($"{baseName}_{suffix}", System.StringComparison.OrdinalIgnoreCase));
            if (match) return match;
            match = spriteCandidates.FirstOrDefault(s => s.name.Contains(baseName) && s.name.Contains(suffix));
            return match;
        }

        Texture2D FindTextureMatch(string suffix)
        {
            string snake = ToSnakeCase(baseName);
            var match = textureCandidates.FirstOrDefault(t => t.name.Equals($"{snake}_{suffix}", System.StringComparison.OrdinalIgnoreCase));
            if (match) return match;
            match = textureCandidates.FirstOrDefault(t => t.name.Equals($"{baseName}_{suffix}", System.StringComparison.OrdinalIgnoreCase));
            if (match) return match;
            match = textureCandidates.FirstOrDefault(t => t.name.Contains(baseName) && t.name.Contains(suffix));
            return match;
        }

        bool changed = false;

        // Top/Up
        Sprite topSp = FindSpriteMatch("top");
        if (topSp == null) topSp = FindSpriteMatch("up");
        if (topSp != null && data.upSprite != topSp) { data.upSprite = topSp; changed = true; }
        else
        {
            Texture2D topTex = FindTextureMatch("top");
            if (topTex == null) topTex = FindTextureMatch("up");
            if (topTex != null && data.upSprite == null) { data.upTexture = topTex; data.upSprite = Sprite.Create(topTex, new Rect(0,0,topTex.width, topTex.height), new Vector2(0.5f,0.5f)); changed = true; }
        }

        // Bottom/Down
        Sprite bottomSp = FindSpriteMatch("bottom");
        if (bottomSp == null) bottomSp = FindSpriteMatch("down");
        if (bottomSp != null && data.downSprite != bottomSp) { data.downSprite = bottomSp; changed = true; }
        else
        {
            Texture2D bottomTex = FindTextureMatch("bottom");
            if (bottomTex == null) bottomTex = FindTextureMatch("down");
            if (bottomTex != null && data.downSprite == null) { data.downTexture = bottomTex; data.downSprite = Sprite.Create(bottomTex, new Rect(0,0,bottomTex.width, bottomTex.height), new Vector2(0.5f,0.5f)); changed = true; }
        }

        // Front
        Sprite frontSp = FindSpriteMatch("front");
        if (frontSp != null && data.frontSprite != frontSp) { data.frontSprite = frontSp; changed = true; }
        else if (data.frontSprite == null)
        {
            Texture2D frontTex = FindTextureMatch("front");
            if (frontTex != null) { data.frontTexture = frontTex; data.frontSprite = Sprite.Create(frontTex, new Rect(0,0,frontTex.width, frontTex.height), new Vector2(0.5f,0.5f)); changed = true; }
        }

        // Back
        Sprite backSp = FindSpriteMatch("back");
        if (backSp != null && data.backSprite != backSp) { data.backSprite = backSp; changed = true; }
        else if (data.backSprite == null)
        {
            Texture2D backTex = FindTextureMatch("back");
            if (backTex != null) { data.backTexture = backTex; data.backSprite = Sprite.Create(backTex, new Rect(0,0,backTex.width, backTex.height), new Vector2(0.5f,0.5f)); changed = true; }
        }

        // Left
        Sprite leftSp = FindSpriteMatch("left");
        if (leftSp != null && data.leftSprite != leftSp) { data.leftSprite = leftSp; changed = true; }
        else if (data.leftSprite == null)
        {
            Texture2D leftTex = FindTextureMatch("left");
            if (leftTex != null) { data.leftTexture = leftTex; data.leftSprite = Sprite.Create(leftTex, new Rect(0,0,leftTex.width, leftTex.height), new Vector2(0.5f,0.5f)); changed = true; }
        }

        // Right
        Sprite rightSp = FindSpriteMatch("right");
        if (rightSp != null && data.rightSprite != rightSp) { data.rightSprite = rightSp; changed = true; }
        else if (data.rightSprite == null)
        {
            Texture2D rightTex = FindTextureMatch("right");
            if (rightTex != null) { data.rightTexture = rightTex; data.rightSprite = Sprite.Create(rightTex, new Rect(0,0,rightTex.width, rightTex.height), new Vector2(0.5f,0.5f)); changed = true; }
        }

        // Side (Generic)
        Sprite sideSp = FindSpriteMatch("side");
        if (sideSp != null)
        {
            if (data.frontSprite == null) { data.frontSprite = sideSp; changed = true; }
            if (data.backSprite == null) { data.backSprite = sideSp; changed = true; }
            if (data.leftSprite == null) { data.leftSprite = sideSp; changed = true; }
            if (data.rightSprite == null) { data.rightSprite = sideSp; changed = true; }
        }

        // Fallback: if "top" exists but "bottom" is null, use top?
        if (data.upSprite != null && data.downSprite == null) { data.downSprite = data.upSprite; changed = true; }
        
        // Fallback: if "front" exists but others are null? (Maybe it's a directional block like furnace)
        // If we have side, we used it. If we don't have side, but have front... 
        // usually we don't want to auto-assign front to back unless we are sure.

        if (changed)
        {
            EditorUtility.SetDirty(data);
            Debug.Log($"Auto-assigned textures for {baseName}");
        }
        else
        {
            Debug.Log($"Could not find matching textures for {baseName} (searched for {ToSnakeCase(baseName)}_top, etc.)");
        }
    }

    private string ToSnakeCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        // Simple conversion: CraftingTable -> crafting_table
        // Regex to insert underscore before capitals?
        // Or just assume the user's files are named consistently.
        // Let's try a simple heuristic:
        return System.Text.RegularExpressions.Regex.Replace(text, "([a-z])([A-Z])", "$1_$2").ToLower();
    }
}
