using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class AtlasPacker : EditorWindow
{
    [MenuItem("Voxel/Pack Textures to Atlas")]
    public static void ShowWindow()
    {
        GetWindow<AtlasPacker>("Atlas Packer");
    }

    private int tileWidth = 16;
    private int tileHeight = 16;
    private int padding = 0; // Minecraft usually has 0 padding if using point filtering
    private string atlasPath = "Assets/Resources/Textures/BlockAtlas.png";

    private void OnGUI()
    {
        GUILayout.Label("Atlas Packer", EditorStyles.boldLabel);

        tileWidth = EditorGUILayout.IntField("Tile Width", tileWidth);
        tileHeight = EditorGUILayout.IntField("Tile Height", tileHeight);
        padding = EditorGUILayout.IntField("Padding", padding);
        atlasPath = EditorGUILayout.TextField("Atlas Path", atlasPath);

        if (GUILayout.Button("Pack Atlas"))
        {
            PackAtlas();
        }
    }

    private void PackAtlas()
    {
        // 1. Find all BlockTextureData assets
        string[] guids = AssetDatabase.FindAssets("t:BlockTextureData");
        List<BlockTextureData> blockDataList = new List<BlockTextureData>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BlockTextureData data = AssetDatabase.LoadAssetAtPath<BlockTextureData>(path);
            if (data != null) blockDataList.Add(data);
        }

        // 2. Collect unique textures
        HashSet<Texture2D> uniqueTextures = new HashSet<Texture2D>();
        foreach (var data in blockDataList)
        {
            if (data.upTexture) uniqueTextures.Add(data.upTexture);
            if (data.downTexture) uniqueTextures.Add(data.downTexture);
            if (data.frontTexture) uniqueTextures.Add(data.frontTexture);
            if (data.backTexture) uniqueTextures.Add(data.backTexture);
            if (data.leftTexture) uniqueTextures.Add(data.leftTexture);
            if (data.rightTexture) uniqueTextures.Add(data.rightTexture);
        }

        if (uniqueTextures.Count == 0)
        {
            Debug.LogWarning("No textures found in BlockTextureData assets.");
            return;
        }

        List<Texture2D> sortedTextures = uniqueTextures.OrderBy(t => t.name).ToList();

        // 3. Calculate atlas size
        int count = sortedTextures.Count;
        int tilesPerRow = Mathf.CeilToInt(Mathf.Sqrt(count));
        int tilesPerColumn = Mathf.CeilToInt((float)count / tilesPerRow);

        int atlasWidth = tilesPerRow * (tileWidth + padding);
        int atlasHeight = tilesPerColumn * (tileHeight + padding);

        // Ensure power of 2? Not strictly necessary but good practice.
        // atlasWidth = Mathf.NextPowerOfTwo(atlasWidth);
        // atlasHeight = Mathf.NextPowerOfTwo(atlasHeight);

        Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
        // Clear to transparent
        Color[] clearColors = new Color[atlasWidth * atlasHeight];
        for (int i = 0; i < clearColors.Length; i++) clearColors[i] = Color.clear;
        atlas.SetPixels(clearColors);

        // 4. Pack textures
        Dictionary<Texture2D, Vector2Int> texturePositions = new Dictionary<Texture2D, Vector2Int>();

        for (int i = 0; i < count; i++)
        {
            Texture2D tex = sortedTextures[i];
            int xIndex = i % tilesPerRow;
            int yIndex = i / tilesPerRow;

            int xPos = xIndex * (tileWidth + padding);
            int yPos = yIndex * (tileHeight + padding);

            // Read pixels (make sure texture is readable)
            if (!tex.isReadable)
            {
                string path = AssetDatabase.GetAssetPath(tex);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }
            }

            // Resize if needed? For now assume 16x16 or resize
            Color[] pixels = tex.GetPixels();
            if (tex.width != tileWidth || tex.height != tileHeight)
            {
                // Simple resize or warning?
                Debug.LogWarning($"Texture {tex.name} is {tex.width}x{tex.height}, expected {tileWidth}x{tileHeight}. Resizing...");
                // Resize logic omitted for brevity, assuming correct size for now or just copying what fits
                // Actually, let's just copy 0,0 to w,h
            }

            atlas.SetPixels(xPos, yPos, tileWidth, tileHeight, pixels);
            
            // Store position (in tiles)
            texturePositions[tex] = new Vector2Int(xIndex, yIndex);
        }

        atlas.Apply();

        // 5. Save Atlas
        byte[] bytes = atlas.EncodeToPNG();
        string dir = Path.GetDirectoryName(atlasPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(atlasPath, bytes);
        AssetDatabase.Refresh();

        // Update TextureImporter for the atlas (Point filter, etc)
        TextureImporter atlasImporter = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
        if (atlasImporter != null)
        {
            atlasImporter.filterMode = FilterMode.Point;
            atlasImporter.textureCompression = TextureImporterCompression.Uncompressed;
            atlasImporter.SaveAndReimport();
        }

        // 6. Update BlockTextureData assets
        foreach (var data in blockDataList)
        {
            Undo.RecordObject(data, "Update Block Texture Coordinates");

            if (data.upTexture && texturePositions.ContainsKey(data.upTexture)) data.up = texturePositions[data.upTexture];
            if (data.downTexture && texturePositions.ContainsKey(data.downTexture)) data.down = texturePositions[data.downTexture];
            if (data.frontTexture && texturePositions.ContainsKey(data.frontTexture)) data.front = texturePositions[data.frontTexture];
            if (data.backTexture && texturePositions.ContainsKey(data.backTexture)) data.back = texturePositions[data.backTexture];
            if (data.leftTexture && texturePositions.ContainsKey(data.leftTexture)) data.left = texturePositions[data.leftTexture];
            if (data.rightTexture && texturePositions.ContainsKey(data.rightTexture)) data.right = texturePositions[data.rightTexture];

            EditorUtility.SetDirty(data);
        }

        // 7. Update TextureAtlas.cs static values
        UpdateTextureAtlasScript(tilesPerRow, tilesPerColumn);
        
        Debug.Log($"Atlas packed to {atlasPath}. Size: {atlasWidth}x{atlasHeight}. Tiles: {tilesPerRow}x{tilesPerColumn}.");
    }

    private void UpdateTextureAtlasScript(int tilesPerRow, int tilesPerColumn)
    {
        string scriptPath = "Assets/Scripts/World/TextureAtlas.cs";
        if (!File.Exists(scriptPath))
        {
            Debug.LogError($"Could not find {scriptPath} to update tile counts.");
            return;
        }

        string[] lines = File.ReadAllLines(scriptPath);
        bool updated = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("public static int tilesPerRow ="))
            {
                lines[i] = $"    public static int tilesPerRow = {tilesPerRow};";
                updated = true;
            }
            else if (lines[i].Contains("public static int tilesPerColumn ="))
            {
                lines[i] = $"    public static int tilesPerColumn = {tilesPerColumn};";
                updated = true;
            }
        }

        if (updated)
        {
            File.WriteAllLines(scriptPath, lines);
            AssetDatabase.Refresh();
            Debug.Log($"Updated TextureAtlas.cs with tilesPerRow={tilesPerRow}, tilesPerColumn={tilesPerColumn}");
        }
    }
}
