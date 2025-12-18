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
        
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { directory });
        
        // If not found, maybe try a "Textures" folder up one level?
        if (guids.Length == 0)
        {
            guids = AssetDatabase.FindAssets("t:Texture2D"); // Search everywhere if nothing local
        }

        List<Texture2D> candidates = new List<Texture2D>();
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) candidates.Add(tex);
        }

        // Helper to find best match
        Texture2D FindMatch(string suffix)
        {
            // 1. Try exact match with snake_case: crafting_table_top
            string snake = ToSnakeCase(baseName); // CraftingTable -> crafting_table
            var match = candidates.FirstOrDefault(t => t.name.Equals($"{snake}_{suffix}", System.StringComparison.OrdinalIgnoreCase));
            if (match) return match;

            // 2. Try exact match with PascalCase: CraftingTable_Top
            match = candidates.FirstOrDefault(t => t.name.Equals($"{baseName}_{suffix}", System.StringComparison.OrdinalIgnoreCase));
            if (match) return match;

            // 3. Try contains
            match = candidates.FirstOrDefault(t => t.name.Contains(baseName) && t.name.Contains(suffix));
            return match;
        }

        bool changed = false;

        // Top/Up
        Texture2D top = FindMatch("top");
        if (top == null) top = FindMatch("up");
        if (top != null && data.upTexture != top) { data.upTexture = top; changed = true; }

        // Bottom/Down
        Texture2D bottom = FindMatch("bottom");
        if (bottom == null) bottom = FindMatch("down");
        // Fallback: if no bottom, use top? (e.g. planks) - Maybe not auto, let user decide.
        if (bottom != null && data.downTexture != bottom) { data.downTexture = bottom; changed = true; }

        // Front
        Texture2D front = FindMatch("front");
        if (front != null && data.frontTexture != front) { data.frontTexture = front; changed = true; }

        // Back
        Texture2D back = FindMatch("back");
        if (back != null && data.backTexture != back) { data.backTexture = back; changed = true; }

        // Left
        Texture2D left = FindMatch("left");
        if (left != null && data.leftTexture != left) { data.leftTexture = left; changed = true; }

        // Right
        Texture2D right = FindMatch("right");
        if (right != null && data.rightTexture != right) { data.rightTexture = right; changed = true; }

        // Side (Generic)
        Texture2D side = FindMatch("side");
        if (side != null)
        {
            if (data.frontTexture == null) { data.frontTexture = side; changed = true; }
            if (data.backTexture == null) { data.backTexture = side; changed = true; }
            if (data.leftTexture == null) { data.leftTexture = side; changed = true; }
            if (data.rightTexture == null) { data.rightTexture = side; changed = true; }
        }

        // Fallback: if "top" exists but "bottom" is null, use top?
        if (data.upTexture != null && data.downTexture == null) { data.downTexture = data.upTexture; changed = true; }
        
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
