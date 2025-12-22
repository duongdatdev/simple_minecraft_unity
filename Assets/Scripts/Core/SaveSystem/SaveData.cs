using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameSaveData
{
    public string saveName;
    public long lastPlayed;
    public WorldData worldData;
    public PlayerData playerData;
    public InventoryData inventoryData;
}

[Serializable]
public class WorldData
{
    public int seed;
    public List<ChunkData> modifiedChunks = new List<ChunkData>();
}

[Serializable]
public class ChunkData
{
    public int x;
    public int z;
    // Flattened 3D array for serialization
    // Order: x + y * width + z * width * height ? No, usually x, y, z loops.
    // Let's use a simple 1D array or RLE if possible, but for now simple 1D array of ints (casted BlockType)
    public int[] blocks; 
}

[Serializable]
public class PlayerData
{
    public Vector3Data position;
    public Vector3Data rotation;
    public float currentHealth;
    public float currentHunger;
}

[Serializable]
public class InventoryData
{
    public List<InventoryItemData> items = new List<InventoryItemData>();
    public List<InventoryItemData> armorItems = new List<InventoryItemData>();
    public InventoryItemData shieldItem;
}

[Serializable]
public class InventoryItemData
{
    public int slotIndex;
    public string itemName;
    public int count;
    public int durability;
}

[Serializable]
public class Vector3Data
{
    public float x, y, z;

    public Vector3Data(Vector3 v)
    {
        x = v.x;
        y = v.y;
        z = v.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}
