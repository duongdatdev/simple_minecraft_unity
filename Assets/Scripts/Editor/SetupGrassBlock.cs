using UnityEngine;
using UnityEditor;

public class SetupGrassBlock
{
    [MenuItem("Voxel/Setup Grass Block Tint")]
    public static void Setup()
    {
        string[] guids = AssetDatabase.FindAssets("Grass_Block t:BlockTextureData");
        if (guids.Length == 0)
        {
            Debug.LogWarning("Could not find Grass_Block asset.");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        BlockTextureData grassData = AssetDatabase.LoadAssetAtPath<BlockTextureData>(path);
        
        if (grassData != null)
        {
            grassData.useBiomeTint = true;
            EditorUtility.SetDirty(grassData);
            AssetDatabase.SaveAssets();
            Debug.Log($"Enabled Biome Tint for {path}");
        }
    }
}
