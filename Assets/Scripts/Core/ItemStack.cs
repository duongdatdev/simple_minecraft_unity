using UnityEngine;

/// <summary>
/// Represents a stack of items (item + quantity + durability for tools)
/// Used in inventory slots
/// </summary>
[System.Serializable]
public class ItemStack
{
    public Item item;
    public int count;
    public int currentDurability; // for tools

    public ItemStack(Item item, int count = 1)
    {
        this.item = item?.Clone();
        this.count = Mathf.Max(1, count);
        this.currentDurability = item?.maxDurability ?? 0;
    }

    /// <summary>
    /// Check if this stack is empty (no item or count <= 0)
    /// </summary>
    public bool IsEmpty()
    {
        return item == null || count <= 0;
    }

    /// <summary>
    /// Check if this stack can be merged with another
    /// </summary>
    public bool CanStackWith(ItemStack other)
    {
        if (other == null || other.IsEmpty()) return false;
        if (IsEmpty()) return false;
        
        // Same item and not a tool (tools don't stack due to durability)
        return item.itemName == other.item.itemName && 
               item.itemType != ItemType.Tool &&
               item.itemType != ItemType.Weapon;
    }

    /// <summary>
    /// Add items to this stack (returns overflow count)
    /// </summary>
    public int AddCount(int amount)
    {
        if (item == null) return amount;
        
        int maxAdd = item.maxStackSize - count;
        int actualAdd = Mathf.Min(maxAdd, amount);
        count += actualAdd;
        return amount - actualAdd; // return overflow
    }

    /// <summary>
    /// Remove items from this stack
    /// </summary>
    public void RemoveCount(int amount)
    {
        count -= amount;
        if (count <= 0)
        {
            Clear();
        }
    }

    /// <summary>
    /// Clear this stack (make it empty)
    /// </summary>
    public void Clear()
    {
        item = null;
        count = 0;
        currentDurability = 0;
    }

    /// <summary>
    /// Reduce durability for tools (returns true if tool breaks)
    /// </summary>
    public bool ReduceDurability(int amount = 1)
    {
        if (item == null || (item.itemType != ItemType.Tool && item.itemType != ItemType.Weapon))
            return false;

        currentDurability -= amount;
        
        if (currentDurability <= 0)
        {
            Clear(); // tool breaks
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Clone this item stack
    /// </summary>
    public ItemStack Clone()
    {
        if (IsEmpty()) return new ItemStack(null, 0);
        
        var clone = new ItemStack(item, count);
        clone.currentDurability = this.currentDurability;
        return clone;
    }

    /// <summary>
    /// Get display string for UI (name + count)
    /// </summary>
    public string GetDisplayString()
    {
        if (IsEmpty()) return "";
        
        if (count > 1)
            return $"{item.displayName} x{count}";
        else if (item.itemType == ItemType.Tool || item.itemType == ItemType.Weapon)
            return $"{item.displayName} ({currentDurability}/{item.maxDurability})";
        else
            return item.displayName;
    }
}
