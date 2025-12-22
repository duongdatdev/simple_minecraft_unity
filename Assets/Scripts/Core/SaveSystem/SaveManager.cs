using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class SaveManager
{
    private static string SaveFolder => Path.Combine(Application.persistentDataPath, "Saves");

    public static void SaveGame(GameSaveData data)
    {
        if (!Directory.Exists(SaveFolder))
        {
            Directory.CreateDirectory(SaveFolder);
        }

        string json = JsonUtility.ToJson(data, true);
        string path = Path.Combine(SaveFolder, data.saveName + ".json");
        File.WriteAllText(path, json);
        Debug.Log($"Game saved to {path}");
    }

    public static GameSaveData LoadGame(string saveName)
    {
        string path = Path.Combine(SaveFolder, saveName + ".json");
        if (!File.Exists(path))
        {
            Debug.LogError($"Save file not found: {path}");
            return null;
        }

        string json = File.ReadAllText(path);
        return JsonUtility.FromJson<GameSaveData>(json);
    }

    public static List<string> GetSaveList()
    {
        if (!Directory.Exists(SaveFolder))
        {
            return new List<string>();
        }

        string[] files = Directory.GetFiles(SaveFolder, "*.json");
        List<string> saves = new List<string>();
        foreach (string file in files)
        {
            saves.Add(Path.GetFileNameWithoutExtension(file));
        }
        return saves;
    }
    
    public static void DeleteSave(string saveName)
    {
        string path = Path.Combine(SaveFolder, saveName + ".json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
