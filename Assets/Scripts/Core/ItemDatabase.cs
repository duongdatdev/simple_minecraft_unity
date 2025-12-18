using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central database for all items in the game
/// Singleton pattern for easy access
/// </summary>
public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance { get; private set; }

    [Header("Item Icons")]
    public Sprite grassBlockIcon;
    public Sprite dirtBlockIcon;
    public Sprite stoneBlockIcon;
    public Sprite sandBlockIcon;
    public Sprite woodBlockIcon;
    public Sprite leavesBlockIcon;
    public Sprite waterBlockIcon;
    public Sprite glassBlockIcon;
    public Sprite diamondBlockIcon;

    // Additional blocks
    public Sprite planksBlockIcon;
    public Sprite craftingTableIcon;

    [Header("Tool Icons")]
    public Sprite woodenPickaxeIcon;
    public Sprite stonePickaxeIcon;
    public Sprite ironPickaxeIcon;
    public Sprite diamondPickaxeIcon;
    public Sprite woodenAxeIcon;
    public Sprite woodenShovelIcon;

    private Dictionary<string, Item> itemRegistry = new Dictionary<string, Item>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        RegisterAllItems();
    }

    /// <summary>
    /// Register all items in the game
    /// </summary>
    private void RegisterAllItems()
    {
        // BLOCKS
        RegisterItem(CreateBlockItem("grass", "Grass Block", BlockType.Grass, grassBlockIcon));
        RegisterItem(CreateBlockItem("dirt", "Dirt", BlockType.Dirt, dirtBlockIcon));
        RegisterItem(CreateBlockItem("stone", "Stone", BlockType.Stone, stoneBlockIcon));
        RegisterItem(CreateBlockItem("sand", "Sand", BlockType.Sand, sandBlockIcon));
        RegisterItem(CreateBlockItem("wood", "Wood", BlockType.Wood, woodBlockIcon));
        RegisterItem(CreateBlockItem("leaves", "Leaves", BlockType.Leaves, leavesBlockIcon));
        RegisterItem(CreateBlockItem("water", "Water", BlockType.Water, waterBlockIcon));
        RegisterItem(CreateBlockItem("glass", "Glass", BlockType.Glass, glassBlockIcon));
        RegisterItem(CreateBlockItem("diamond_block", "Diamond Block", BlockType.DiamondBlock, diamondBlockIcon));

        // Wooden Planks and Crafting Table
        RegisterItem(CreateBlockItem("planks", "Wooden Planks", BlockType.Planks, planksBlockIcon));
        RegisterItem(CreateBlockItem("crafting_table", "Crafting Table", BlockType.CraftingTable, craftingTableIcon));

        // TOOLS - Pickaxes
        RegisterItem(CreateToolItem("wooden_pickaxe", "Wooden Pickaxe", ToolType.Pickaxe, ToolTier.Wood, 60, woodenPickaxeIcon));
        RegisterItem(CreateToolItem("stone_pickaxe", "Stone Pickaxe", ToolType.Pickaxe, ToolTier.Stone, 132, stonePickaxeIcon));
        RegisterItem(CreateToolItem("iron_pickaxe", "Iron Pickaxe", ToolType.Pickaxe, ToolTier.Iron, 251, ironPickaxeIcon));
        RegisterItem(CreateToolItem("diamond_pickaxe", "Diamond Pickaxe", ToolType.Pickaxe, ToolTier.Diamond, 1562, diamondPickaxeIcon));

        // TOOLS - Axes
        RegisterItem(CreateToolItem("wooden_axe", "Wooden Axe", ToolType.Axe, ToolTier.Wood, 60, woodenAxeIcon));

        // TOOLS - Shovels
        RegisterItem(CreateToolItem("wooden_shovel", "Wooden Shovel", ToolType.Shovel, ToolTier.Wood, 60, woodenShovelIcon));

        Debug.Log($"ItemDatabase: Registered {itemRegistry.Count} items");
    }

    /// <summary>
    /// Create a block item
    /// </summary>
    private Item CreateBlockItem(string name, string displayName, BlockType blockType, Sprite icon)
    {
        Item item = new Item(name, displayName, ItemType.Block)
        {
            blockType = blockType,
            icon = icon,
            maxStackSize = 64
        };
        return item;
    }

    /// <summary>
    /// Create a tool item
    /// </summary>
    private Item CreateToolItem(string name, string displayName, ToolType toolType, ToolTier tier, int durability, Sprite icon)
    {
        Item item = new Item(name, displayName, ItemType.Tool)
        {
            toolType = toolType,
            toolTier = tier,
            maxDurability = durability,
            durability = durability,
            icon = icon,
            maxStackSize = 1 // tools don't stack
        };
        return item;
    }

    /// <summary>
    /// Register item in database
    /// </summary>
    private void RegisterItem(Item item)
    {
        if (item != null && !itemRegistry.ContainsKey(item.itemName))
        {
            itemRegistry[item.itemName] = item;
        }
    }

    /// <summary>
    /// Get item by name
    /// </summary>
    public Item GetItem(string itemName)
    {
        if (itemRegistry.TryGetValue(itemName, out Item item))
        {
            return item.Clone(); // return a clone to avoid modifying the original
        }
        return null;
    }

    /// <summary>
    /// Get item by block type
    /// </summary>
    public Item GetItemForBlock(BlockType blockType)
    {
        foreach (var kvp in itemRegistry)
        {
            if (kvp.Value.itemType == ItemType.Block && kvp.Value.blockType == blockType)
            {
                return kvp.Value.Clone();
            }
        }
        return null;
    }

    /// <summary>
    /// Check if item exists
    /// </summary>
    public bool HasItem(string itemName)
    {
        return itemRegistry.ContainsKey(itemName);
    }

    /// <summary>
    /// Get all items (for debugging/testing)
    /// </summary>
    public List<Item> GetAllItems()
    {
        List<Item> items = new List<Item>();
        foreach (var kvp in itemRegistry)
        {
            items.Add(kvp.Value.Clone());
        }
        return items;
    }
}
