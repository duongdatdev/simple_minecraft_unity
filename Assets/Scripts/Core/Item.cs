using UnityEngine;

/// <summary>
/// Base class representing an item type in the game.
/// Items can be blocks, tools, food, weapons, etc.
/// </summary>
[System.Serializable]
public class Item
{
    public string itemName;
    public string displayName;
    public Sprite icon;
    public ItemType itemType;
    public int maxStackSize = 64;
    
    // For block items
    public BlockType blockType = BlockType.Air;
    
    // For tool items
    public ToolType toolType = ToolType.None;
    public ToolTier toolTier = ToolTier.None;
    public int durability = 0;
    public int maxDurability = 0;
    
    // For consumable items (food)
    public bool isConsumable = false;
    public int healAmount = 0;
    public int hungerAmount = 0;

    // For armor items
    public ArmorType armorType = ArmorType.None;
    public int armorPoints = 0;

    public Item(string name, string display, ItemType type)
    {
        itemName = name;
        displayName = display;
        itemType = type;
    }

    public Item Clone()
    {
        return new Item(itemName, displayName, itemType)
        {
            icon = this.icon,
            maxStackSize = this.maxStackSize,
            blockType = this.blockType,
            toolType = this.toolType,
            toolTier = this.toolTier,
            durability = this.durability,
            maxDurability = this.maxDurability,
            isConsumable = this.isConsumable,
            healAmount = this.healAmount,
            hungerAmount = this.hungerAmount,
            armorType = this.armorType,
            armorPoints = this.armorPoints
        };
    }
}

/// <summary>
/// Defines the category of the item
/// </summary>
public enum ItemType
{
    Block,      // Placeable blocks
    Tool,       // Pickaxe, Axe, Shovel, Hoe
    Weapon,     // Sword, Bow
    Food,       // Consumable items
    Material,   // Crafting materials (sticks, coal, etc)
    Armor,      // Armor items
    Misc        // Other items
}

/// <summary>
/// Armor types
/// </summary>
public enum ArmorType
{
    None,
    Helmet,
    Chestplate,
    Leggings,
    Boots
}

/// <summary>
/// Tool types for mining/interaction
/// </summary>
public enum ToolType
{
    None,
    Pickaxe,
    Axe,
    Shovel,
    Hoe,
    Sword
}

/// <summary>
/// Tool tiers define mining speed and durability
/// </summary>
public enum ToolTier
{
    None,
    Wood,
    Stone,
    Iron,
    Gold,
    Diamond
}
