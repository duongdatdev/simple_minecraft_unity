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
    public Sprite ironOreIcon;

    // Additional blocks
    public Sprite planksBlockIcon;
    public Sprite craftingTableIcon;

    // Materials
    public Sprite ironIngotIcon;
    public Sprite diamondIcon; // Add this field
    public Sprite porkchopIcon;
    public Sprite cookedPorkchopIcon;
    public Sprite carrotIcon;

    [Header("Armor Icons")]
    public Sprite ironHelmetIcon;
    public Sprite ironChestplateIcon;
    public Sprite ironLeggingsIcon;
    public Sprite ironBootsIcon;

    [Header("Tool Icons")]
    public Sprite woodenPickaxeIcon;
    public Sprite stonePickaxeIcon;
    public Sprite ironPickaxeIcon;
    public Sprite diamondPickaxeIcon;
    public Sprite woodenAxeIcon;
    public Sprite woodenShovelIcon;

    // New Tools
    public Sprite woodenSwordIcon;
    public Sprite woodenHoeIcon;
    
    public Sprite stoneAxeIcon;
    public Sprite stoneShovelIcon;
    public Sprite stoneSwordIcon;
    public Sprite stoneHoeIcon;

    public Sprite ironAxeIcon;
    public Sprite ironShovelIcon;
    public Sprite ironSwordIcon;
    public Sprite ironHoeIcon;

    public Sprite diamondAxeIcon;
    public Sprite diamondShovelIcon;
    public Sprite diamondSwordIcon;
    public Sprite diamondHoeIcon;

    // Other
    public Sprite stickIcon;

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
        RegisterItem(CreateBlockItem("iron_ore", "Iron Ore", BlockType.IronOre, ironOreIcon));

        // Wooden Planks and Crafting Table
        RegisterItem(CreateBlockItem("planks", "Wooden Planks", BlockType.Planks, planksBlockIcon));
        RegisterItem(CreateBlockItem("crafting_table", "Crafting Table", BlockType.CraftingTable, craftingTableIcon));

        // Materials
        RegisterItem(CreateMaterialItem("iron_ingot", "Iron Ingot", ironIngotIcon));
        RegisterItem(CreateMaterialItem("diamond", "Diamond", diamondIcon));
        RegisterItem(CreateMaterialItem("stick", "Stick", stickIcon));

        // Food
        RegisterItem(CreateFoodItem("porkchop", "Raw Porkchop", 3, 2, porkchopIcon));
        RegisterItem(CreateFoodItem("cooked_porkchop", "Cooked Porkchop", 8, 6, cookedPorkchopIcon));
        RegisterItem(CreateFoodItem("carrot", "Carrot", 2, 2, carrotIcon));

        // Armor
        RegisterItem(CreateArmorItem("iron_helmet", "Iron Helmet", ArmorType.Helmet, 2, ironHelmetIcon));
        RegisterItem(CreateArmorItem("iron_chestplate", "Iron Chestplate", ArmorType.Chestplate, 6, ironChestplateIcon));
        RegisterItem(CreateArmorItem("iron_leggings", "Iron Leggings", ArmorType.Leggings, 5, ironLeggingsIcon));
        RegisterItem(CreateArmorItem("iron_boots", "Iron Boots", ArmorType.Boots, 2, ironBootsIcon));

        // TOOLS - Pickaxes
        RegisterItem(CreateToolItem("wooden_pickaxe", "Wooden Pickaxe", ToolType.Pickaxe, ToolTier.Wood, 60, woodenPickaxeIcon));
        RegisterItem(CreateToolItem("stone_pickaxe", "Stone Pickaxe", ToolType.Pickaxe, ToolTier.Stone, 132, stonePickaxeIcon));
        RegisterItem(CreateToolItem("iron_pickaxe", "Iron Pickaxe", ToolType.Pickaxe, ToolTier.Iron, 251, ironPickaxeIcon));
        RegisterItem(CreateToolItem("diamond_pickaxe", "Diamond Pickaxe", ToolType.Pickaxe, ToolTier.Diamond, 1562, diamondPickaxeIcon));

        // TOOLS - Axes
        RegisterItem(CreateToolItem("wooden_axe", "Wooden Axe", ToolType.Axe, ToolTier.Wood, 60, woodenAxeIcon));
        RegisterItem(CreateToolItem("stone_axe", "Stone Axe", ToolType.Axe, ToolTier.Stone, 132, stoneAxeIcon));
        RegisterItem(CreateToolItem("iron_axe", "Iron Axe", ToolType.Axe, ToolTier.Iron, 251, ironAxeIcon));
        RegisterItem(CreateToolItem("diamond_axe", "Diamond Axe", ToolType.Axe, ToolTier.Diamond, 1562, diamondAxeIcon));

        // TOOLS - Shovels
        RegisterItem(CreateToolItem("wooden_shovel", "Wooden Shovel", ToolType.Shovel, ToolTier.Wood, 60, woodenShovelIcon));
        RegisterItem(CreateToolItem("stone_shovel", "Stone Shovel", ToolType.Shovel, ToolTier.Stone, 132, stoneShovelIcon));
        RegisterItem(CreateToolItem("iron_shovel", "Iron Shovel", ToolType.Shovel, ToolTier.Iron, 251, ironShovelIcon));
        RegisterItem(CreateToolItem("diamond_shovel", "Diamond Shovel", ToolType.Shovel, ToolTier.Diamond, 1562, diamondShovelIcon));

        // TOOLS - Swords
        RegisterItem(CreateToolItem("wooden_sword", "Wooden Sword", ToolType.Sword, ToolTier.Wood, 60, woodenSwordIcon));
        RegisterItem(CreateToolItem("stone_sword", "Stone Sword", ToolType.Sword, ToolTier.Stone, 132, stoneSwordIcon));
        RegisterItem(CreateToolItem("iron_sword", "Iron Sword", ToolType.Sword, ToolTier.Iron, 251, ironSwordIcon));
        RegisterItem(CreateToolItem("diamond_sword", "Diamond Sword", ToolType.Sword, ToolTier.Diamond, 1562, diamondSwordIcon));

        // TOOLS - Hoes
        RegisterItem(CreateToolItem("wooden_hoe", "Wooden Hoe", ToolType.Hoe, ToolTier.Wood, 60, woodenHoeIcon));
        RegisterItem(CreateToolItem("stone_hoe", "Stone Hoe", ToolType.Hoe, ToolTier.Stone, 132, stoneHoeIcon));
        RegisterItem(CreateToolItem("iron_hoe", "Iron Hoe", ToolType.Hoe, ToolTier.Iron, 251, ironHoeIcon));
        RegisterItem(CreateToolItem("diamond_hoe", "Diamond Hoe", ToolType.Hoe, ToolTier.Diamond, 1562, diamondHoeIcon));

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
    /// Create a material item
    /// </summary>
    private Item CreateMaterialItem(string name, string displayName, Sprite icon)
    {
        Item item = new Item(name, displayName, ItemType.Material)
        {
            icon = icon,
            maxStackSize = 64
        };
        return item;
    }

    /// <summary>
    /// Create a food item
    /// </summary>
    private Item CreateFoodItem(string name, string displayName, int hunger, int heal, Sprite icon)
    {
        Item item = new Item(name, displayName, ItemType.Food)
        {
            icon = icon,
            maxStackSize = 64,
            isConsumable = true,
            hungerAmount = hunger,
            healAmount = heal
        };
        return item;
    }

    /// <summary>
    /// Create an armor item
    /// </summary>
    private Item CreateArmorItem(string name, string displayName, ArmorType armorType, int armorPoints, Sprite icon)
    {
        Item item = new Item(name, displayName, ItemType.Armor)
        {
            icon = icon,
            maxStackSize = 1,
            armorType = armorType,
            armorPoints = armorPoints
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
