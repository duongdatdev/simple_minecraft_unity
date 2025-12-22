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

        // 2. Collect unique sprites (use sprite.texture & rect when copying).
        //    If a BlockTextureData has legacy Texture2D assigned but no Sprite, create a temporary Sprite from it so it can be packed.
        HashSet<Sprite> uniqueSprites = new HashSet<Sprite>();
        List<Sprite> tempCreatedSprites = new List<Sprite>();
        foreach (var data in blockDataList)
        {
            if (data.upSprite) uniqueSprites.Add(data.upSprite);
            else if (data.upTexture != null)
            {
                Sprite s = Sprite.Create(data.upTexture, new Rect(0, 0, data.upTexture.width, data.upTexture.height), new Vector2(0.5f, 0.5f), 100f);
                uniqueSprites.Add(s);
                tempCreatedSprites.Add(s);
            }

            if (data.downSprite) uniqueSprites.Add(data.downSprite);
            else if (data.downTexture != null)
            {
                Sprite s = Sprite.Create(data.downTexture, new Rect(0, 0, data.downTexture.width, data.downTexture.height), new Vector2(0.5f, 0.5f), 100f);
                uniqueSprites.Add(s);
                tempCreatedSprites.Add(s);
            }

            if (data.frontSprite) uniqueSprites.Add(data.frontSprite);
            else if (data.frontTexture != null)
            {
                Sprite s = Sprite.Create(data.frontTexture, new Rect(0, 0, data.frontTexture.width, data.frontTexture.height), new Vector2(0.5f, 0.5f), 100f);
                uniqueSprites.Add(s);
                tempCreatedSprites.Add(s);
            }

            if (data.backSprite) uniqueSprites.Add(data.backSprite);
            else if (data.backTexture != null)
            {
                Sprite s = Sprite.Create(data.backTexture, new Rect(0, 0, data.backTexture.width, data.backTexture.height), new Vector2(0.5f, 0.5f), 100f);
                uniqueSprites.Add(s);
                tempCreatedSprites.Add(s);
            }

            if (data.leftSprite) uniqueSprites.Add(data.leftSprite);
            else if (data.leftTexture != null)
            {
                Sprite s = Sprite.Create(data.leftTexture, new Rect(0, 0, data.leftTexture.width, data.leftTexture.height), new Vector2(0.5f, 0.5f), 100f);
                uniqueSprites.Add(s);
                tempCreatedSprites.Add(s);
            }

            if (data.rightSprite) uniqueSprites.Add(data.rightSprite);
            else if (data.rightTexture != null)
            {
                Sprite s = Sprite.Create(data.rightTexture, new Rect(0, 0, data.rightTexture.width, data.rightTexture.height), new Vector2(0.5f, 0.5f), 100f);
                uniqueSprites.Add(s);
                tempCreatedSprites.Add(s);
            }
        }

        if (uniqueSprites.Count == 0)
        {
            Debug.LogWarning("No sprites found in BlockTextureData assets.");
            return;
        }

        List<Sprite> sortedSprites = uniqueSprites.OrderBy(s => s.name).ToList();

        // 3. Calculate atlas size
        int count = sortedSprites.Count;
        int tilesPerRow = Mathf.CeilToInt(Mathf.Sqrt(count));
        int tilesPerColumn = Mathf.CeilToInt((float)count / tilesPerRow);

        int atlasWidth = tilesPerRow * (tileWidth + padding);
        int atlasHeight = tilesPerColumn * (tileHeight + padding);

        Texture2D atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
        // Clear to transparent
        Color[] clearColors = new Color[atlasWidth * atlasHeight];
        for (int i = 0; i < clearColors.Length; i++) clearColors[i] = Color.clear;
        atlas.SetPixels(clearColors);

        // 4. Pack sprites (copy sprite rects)
        Dictionary<Sprite, Vector2Int> texturePositions = new Dictionary<Sprite, Vector2Int>();

        for (int i = 0; i < count; i++)
        {
            Sprite sp = sortedSprites[i];
            int xIndex = i % tilesPerRow;
            int yIndex = i / tilesPerRow;

            int xPos = xIndex * (tileWidth + padding);
            int yPos = yIndex * (tileHeight + padding);

            Texture2D tex = sp.texture;
            Rect rect = sp.rect;

            // Ensure the source texture is readable
            string texPath = AssetDatabase.GetAssetPath(tex);
            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            int srcX = Mathf.RoundToInt(rect.x);
            int srcY = Mathf.RoundToInt(rect.y);
            int srcW = Mathf.RoundToInt(rect.width);
            int srcH = Mathf.RoundToInt(rect.height);

            Color[] pixels = tex.GetPixels(srcX, srcY, srcW, srcH);

            if (srcW != tileWidth || srcH != tileHeight)
            {
                Debug.LogWarning($"Sprite {sp.name} rect {srcW}x{srcH} != tile {tileWidth}x{tileHeight}. Resizing may be required.");
                // For simplicity, copy what fits; proper resizing omitted
            }

            atlas.SetPixels(xPos, yPos, Mathf.Min(srcW, tileWidth), Mathf.Min(srcH, tileHeight), pixels);

            // Store position (in tiles)
            texturePositions[sp] = new Vector2Int(xIndex, yIndex);
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

        // 6. Update BlockTextureData assets (set tile coordinates from sprite map)
        foreach (var data in blockDataList)
        {
            Undo.RecordObject(data, "Update Block Texture Coordinates");

            // Prefer direct sprite references
            if (data.upSprite != null && texturePositions.ContainsKey(data.upSprite)) data.up = texturePositions[data.upSprite];
            else if (data.upTexture != null)
            {
                var found = texturePositions.FirstOrDefault(kv => kv.Key != null && kv.Key.texture == data.upTexture);
                if (!found.Equals(default(KeyValuePair<Sprite, Vector2Int>))) data.up = found.Value;
            }

            if (data.downSprite != null && texturePositions.ContainsKey(data.downSprite)) data.down = texturePositions[data.downSprite];
            else if (data.downTexture != null)
            {
                var found = texturePositions.FirstOrDefault(kv => kv.Key != null && kv.Key.texture == data.downTexture);
                if (!found.Equals(default(KeyValuePair<Sprite, Vector2Int>))) data.down = found.Value;
            }

            if (data.frontSprite != null && texturePositions.ContainsKey(data.frontSprite)) data.front = texturePositions[data.frontSprite];
            else if (data.frontTexture != null)
            {
                var found = texturePositions.FirstOrDefault(kv => kv.Key != null && kv.Key.texture == data.frontTexture);
                if (!found.Equals(default(KeyValuePair<Sprite, Vector2Int>))) data.front = found.Value;
            }

            if (data.backSprite != null && texturePositions.ContainsKey(data.backSprite)) data.back = texturePositions[data.backSprite];
            else if (data.backTexture != null)
            {
                var found = texturePositions.FirstOrDefault(kv => kv.Key != null && kv.Key.texture == data.backTexture);
                if (!found.Equals(default(KeyValuePair<Sprite, Vector2Int>))) data.back = found.Value;
            }

            if (data.leftSprite != null && texturePositions.ContainsKey(data.leftSprite)) data.left = texturePositions[data.leftSprite];
            else if (data.leftTexture != null)
            {
                var found = texturePositions.FirstOrDefault(kv => kv.Key != null && kv.Key.texture == data.leftTexture);
                if (!found.Equals(default(KeyValuePair<Sprite, Vector2Int>))) data.left = found.Value;
            }

            if (data.rightSprite != null && texturePositions.ContainsKey(data.rightSprite)) data.right = texturePositions[data.rightSprite];
            else if (data.rightTexture != null)
            {
                var found = texturePositions.FirstOrDefault(kv => kv.Key != null && kv.Key.texture == data.rightTexture);
                if (!found.Equals(default(KeyValuePair<Sprite, Vector2Int>))) data.right = found.Value;
            }

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
