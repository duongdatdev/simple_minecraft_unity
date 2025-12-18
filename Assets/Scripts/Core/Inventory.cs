using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inventory system for storing items
/// - Main inventory: 27 slots (3x9 grid)
/// - Hotbar: 9 slots (bottom row, accessible via number keys)
/// - Total: 36 slots
/// </summary>
public class Inventory : MonoBehaviour
{
    public const int HOTBAR_SIZE = 9;
    public const int MAIN_INVENTORY_SIZE = 27;
    public const int TOTAL_SIZE = HOTBAR_SIZE + MAIN_INVENTORY_SIZE;

    [Header("Inventory Settings")]
    public int selectedHotbarSlot = 0; // 0-8

    // Inventory slots (0-8 = hotbar, 9-35 = main inventory)
    private ItemStack[] slots = new ItemStack[TOTAL_SIZE];

    [Header("Crafting & Armor")]
    public List<CraftingRecipe> recipes = new List<CraftingRecipe>();
    public ItemStack[] craftingSlots = new ItemStack[9]; // Increased to 9 for 3x3
    public ItemStack craftResultSlot = new ItemStack(null, 0);
    public ItemStack[] armorSlots = new ItemStack[4];
    public ItemStack shieldSlot = new ItemStack(null, 0); // Off-hand/Shield slot
    
    public int currentCraftingGridSize = 2; // 2 or 3

    // Events for UI updates
    public event Action<int, ItemStack> OnSlotChanged;
    public event Action<int> OnHotbarSelectionChanged;
    public event Action<int, ItemStack> OnCraftingSlotChanged;
    public event Action<ItemStack> OnCraftResultChanged;
    public event Action<int, ItemStack> OnArmorSlotChanged;
    public event Action<ItemStack> OnShieldSlotChanged;

    private void Awake()
    {
        // Initialize all slots as empty
        for (int i = 0; i < TOTAL_SIZE; i++)
        {
            slots[i] = new ItemStack(null, 0);
        }
        // Ensure craftingSlots has expected length and initialize
        if (craftingSlots == null || craftingSlots.Length < 9)
        {
            craftingSlots = new ItemStack[9];
        }
        for (int i = 0; i < craftingSlots.Length; i++)
        {
            craftingSlots[i] = craftingSlots[i] ?? new ItemStack(null, 0);
        }

        // Ensure armorSlots has expected length and initialize
        if (armorSlots == null || armorSlots.Length < 4)
        {
            armorSlots = new ItemStack[4];
        }
        for (int i = 0; i < armorSlots.Length; i++)
        {
            armorSlots[i] = armorSlots[i] ?? new ItemStack(null, 0);
        }

        shieldSlot = new ItemStack(null, 0);

        // Initialize default recipes at runtime if none are assigned in inspector
        InitializeDefaultRecipes();
    }

    /// <summary>
    /// Create some basic recipes at runtime when none are defined in the Inventory component.
    /// - Wood (1 in top-left of 2x2) -> 4 Planks
    /// - 2x2 Planks -> Crafting Table
    /// These are created as ScriptableObject instances and added to the recipes list.
    /// </summary>
    private void InitializeDefaultRecipes()
    {
        if (recipes == null) recipes = new List<CraftingRecipe>();
        if (recipes.Count > 0) return; // user provided recipes in inspector
        if (ItemDatabase.Instance == null) return;

        // Wood -> Planks (2x2 grid, top-left wood)
        var planksRecipe = ScriptableObject.CreateInstance<CraftingRecipe>();
        planksRecipe.gridSize = 2;
        planksRecipe.ingredients = new Item[4];
        planksRecipe.ingredients[0] = ItemDatabase.Instance.GetItem("wood");
        planksRecipe.ingredients[1] = null;
        planksRecipe.ingredients[2] = null;
        planksRecipe.ingredients[3] = null;
        planksRecipe.result = ItemDatabase.Instance.GetItem("planks");
        planksRecipe.resultCount = 4;
        recipes.Add(planksRecipe);

        // Planks x4 -> Crafting Table (2x2 full)
        var tableRecipe = ScriptableObject.CreateInstance<CraftingRecipe>();
        tableRecipe.gridSize = 2;
        tableRecipe.ingredients = new Item[4]
        {
            ItemDatabase.Instance.GetItem("planks"),
            ItemDatabase.Instance.GetItem("planks"),
            ItemDatabase.Instance.GetItem("planks"),
            ItemDatabase.Instance.GetItem("planks")
        };
        tableRecipe.result = ItemDatabase.Instance.GetItem("crafting_table");
        tableRecipe.resultCount = 1;
        recipes.Add(tableRecipe);
    }

    private void Update()
    {
        HandleHotbarInput();
    }

    /// <summary>
    /// Handle number keys (1-9) to select hotbar slots
    /// Mouse wheel to cycle through hotbar
    /// </summary>
    private void HandleHotbarInput()
    {
        // Number keys 1-9
        for (int i = 0; i < HOTBAR_SIZE; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SetSelectedHotbarSlot(i);
                return;
            }
        }

        // Mouse wheel scroll
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            int direction = scroll > 0 ? -1 : 1; // scroll up = previous, down = next
            int newSlot = (selectedHotbarSlot + direction + HOTBAR_SIZE) % HOTBAR_SIZE;
            SetSelectedHotbarSlot(newSlot);
        }
    }

    /// <summary>
    /// Set the selected hotbar slot (0-8)
    /// </summary>
    public void SetSelectedHotbarSlot(int slot)
    {
        if (slot < 0 || slot >= HOTBAR_SIZE) return;
        
        selectedHotbarSlot = slot;
        OnHotbarSelectionChanged?.Invoke(selectedHotbarSlot);
    }

    /// <summary>
    /// Get currently selected item stack
    /// </summary>
    public ItemStack GetSelectedItemStack()
    {
        return slots[selectedHotbarSlot];
    }

    /// <summary>
    /// Get item stack at slot index
    /// </summary>
    public ItemStack GetSlot(int index)
    {
        if (index < 0 || index >= TOTAL_SIZE) return null;
        return slots[index];
    }

    /// <summary>
    /// Set item stack at slot index
    /// </summary>
    public void SetSlot(int index, ItemStack stack)
    {
        if (index < 0 || index >= TOTAL_SIZE) return;
        
        slots[index] = stack ?? new ItemStack(null, 0);
        OnSlotChanged?.Invoke(index, slots[index]);
    }

    /// <summary>
    /// Add item to inventory (auto-stack and find empty slots)
    /// Returns true if all items were added, false if inventory is full
    /// </summary>
    public bool AddItem(Item item, int count = 1)
    {
        if (item == null || count <= 0) return false;

        int remaining = count;

        // First pass: try to stack with existing items
        for (int i = 0; i < TOTAL_SIZE && remaining > 0; i++)
        {
            ItemStack slot = slots[i];
            
            // Skip empty slots in first pass
            if (slot.IsEmpty()) continue;
            
            // Try to stack
            if (slot.item.itemName == item.itemName && slot.count < item.maxStackSize)
            {
                int canAdd = Mathf.Min(remaining, item.maxStackSize - slot.count);
                slot.count += canAdd;
                remaining -= canAdd;
                OnSlotChanged?.Invoke(i, slot);
            }
        }

        // Second pass: fill empty slots
        for (int i = 0; i < TOTAL_SIZE && remaining > 0; i++)
        {
            if (slots[i].IsEmpty())
            {
                int addCount = Mathf.Min(remaining, item.maxStackSize);
                slots[i] = new ItemStack(item, addCount);
                remaining -= addCount;
                OnSlotChanged?.Invoke(i, slots[i]);
            }
        }

        return remaining == 0; // true if everything was added
    }

    /// <summary>
    /// Remove item from inventory
    /// Returns true if item was found and removed
    /// </summary>
    public bool RemoveItem(string itemName, int count = 1)
    {
        int remaining = count;

        for (int i = 0; i < TOTAL_SIZE && remaining > 0; i++)
        {
            ItemStack slot = slots[i];
            
            if (slot.IsEmpty() || slot.item.itemName != itemName) continue;

            int removeCount = Mathf.Min(remaining, slot.count);
            slot.RemoveCount(removeCount);
            remaining -= removeCount;
            
            OnSlotChanged?.Invoke(i, slot);
        }

        return remaining == 0;
    }

    /// <summary>
    /// Check if inventory contains item
    /// </summary>
    public bool HasItem(string itemName, int count = 1)
    {
        int found = 0;
        
        for (int i = 0; i < TOTAL_SIZE; i++)
        {
            if (!slots[i].IsEmpty() && slots[i].item.itemName == itemName)
            {
                found += slots[i].count;
                if (found >= count) return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Get total count of item in inventory
    /// </summary>
    public int GetItemCount(string itemName)
    {
        int count = 0;
        
        for (int i = 0; i < TOTAL_SIZE; i++)
        {
            if (!slots[i].IsEmpty() && slots[i].item.itemName == itemName)
            {
                count += slots[i].count;
            }
        }
        
        return count;
    }

    /// <summary>
    /// Clear entire inventory
    /// </summary>
    public void ClearInventory()
    {
        for (int i = 0; i < TOTAL_SIZE; i++)
        {
            slots[i].Clear();
            OnSlotChanged?.Invoke(i, slots[i]);
        }
    }

    /// <summary>
    /// Swap two slots
    /// </summary>
    public void SwapSlots(int slotA, int slotB)
    {
        if (slotA < 0 || slotA >= TOTAL_SIZE || slotB < 0 || slotB >= TOTAL_SIZE) return;

        ItemStack temp = slots[slotA].Clone();
        slots[slotA] = slots[slotB].Clone();
        slots[slotB] = temp;

        OnSlotChanged?.Invoke(slotA, slots[slotA]);
        OnSlotChanged?.Invoke(slotB, slots[slotB]);
    }

    /// <summary>
    /// Use/consume item in selected hotbar slot
    /// </summary>
    public void UseSelectedItem()
    {
        ItemStack selectedStack = GetSelectedItemStack();
        if (selectedStack.IsEmpty()) return;

        Item item = selectedStack.item;

        // Handle consumable items (food)
        if (item.isConsumable)
        {
            // TODO: Apply heal/hunger effects
            selectedStack.RemoveCount(1);
            OnSlotChanged?.Invoke(selectedHotbarSlot, selectedStack);
        }
        
        // Tool/weapon durability is handled by the tool usage code
    }

    /// <summary>
    /// Reduce durability of selected tool
    /// </summary>
    public bool DamageSelectedTool(int damage = 1)
    {
        ItemStack selectedStack = GetSelectedItemStack();
        if (selectedStack.IsEmpty()) return false;

        bool broke = selectedStack.ReduceDurability(damage);
        OnSlotChanged?.Invoke(selectedHotbarSlot, selectedStack);
        
        return broke; // true if tool broke
    }

    /// <summary>
    /// Set item in crafting slot
    /// </summary>
    public void SetCraftingSlot(int index, ItemStack stack)
    {
        if (index < 0 || index >= 9) return;
        craftingSlots[index] = stack ?? new ItemStack(null, 0);
        OnCraftingSlotChanged?.Invoke(index, craftingSlots[index]);
        CheckCrafting();
    }

    /// <summary>
    /// Check if current crafting grid matches any recipe
    /// </summary>
    private void CheckCrafting()
    {
        ItemStack result = null;

        foreach (var recipe in recipes)
        {
            if (recipe.Matches(craftingSlots, currentCraftingGridSize))
            {
                result = new ItemStack(recipe.result, recipe.resultCount);
                break;
            }
        }

        craftResultSlot = result ?? new ItemStack(null, 0);
        OnCraftResultChanged?.Invoke(craftResultSlot);
    }

    /// <summary>
    /// Craft the item (consume ingredients)
    /// </summary>
    public void CraftItem()
    {
        if (craftResultSlot.IsEmpty()) return;

        // Find the recipe that matched to know how to consume
        CraftingRecipe matchedRecipe = null;
        foreach (var recipe in recipes)
        {
            if (recipe.Matches(craftingSlots, currentCraftingGridSize))
            {
                matchedRecipe = recipe;
                break;
            }
        }

        if (matchedRecipe == null) return;

        // Consume ingredients
        int gridSize = matchedRecipe.gridSize;
        int checkSize = gridSize * gridSize;

        for (int i = 0; i < checkSize; i++)
        {
            // Map recipe index to grid index
            int gridIndex = i;
            if (currentCraftingGridSize == 3 && gridSize == 2)
            {
                if (i == 2) gridIndex = 3;
                else if (i == 3) gridIndex = 4;
            }

            if (gridIndex < craftingSlots.Length && !craftingSlots[gridIndex].IsEmpty())
            {
                craftingSlots[gridIndex].RemoveCount(1);
                OnCraftingSlotChanged?.Invoke(gridIndex, craftingSlots[gridIndex]);
            }
        }

        // Re-check crafting (result might change or disappear)
        CheckCrafting();
    }

    public void SetCraftingGridSize(int size)
    {
        if (size != 2 && size != 3) return;

        // Ensure the craftingSlots array exists and is at least size 9
        if (craftingSlots == null || craftingSlots.Length < 9)
        {
            var newSlots = new ItemStack[9];
            if (craftingSlots != null)
            {
                for (int i = 0; i < craftingSlots.Length && i < 9; i++)
                {
                    newSlots[i] = craftingSlots[i] ?? new ItemStack(null, 0);
                }
            }
            for (int i = 0; i < 9; i++) if (newSlots[i] == null) newSlots[i] = new ItemStack(null, 0);
            craftingSlots = newSlots;
        }

        // If switching size, maybe clear slots or return items to inventory?
        // For now, just clear to avoid losing items in hidden slots
        for (int i = 0; i < craftingSlots.Length; i++)
        {
            if (craftingSlots[i] != null && !craftingSlots[i].IsEmpty())
            {
                AddItem(craftingSlots[i].item, craftingSlots[i].count);
                craftingSlots[i] = new ItemStack(null, 0);
                OnCraftingSlotChanged?.Invoke(i, craftingSlots[i]);
            }
        }

        currentCraftingGridSize = size;
        CheckCrafting();
    }

    public void SetArmorSlot(int index, ItemStack stack)
    {
        if (index < 0 || index >= 4) return;
        armorSlots[index] = stack ?? new ItemStack(null, 0);
        OnArmorSlotChanged?.Invoke(index, armorSlots[index]);
    }

    public void SetShieldSlot(ItemStack stack)
    {
        shieldSlot = stack ?? new ItemStack(null, 0);
        OnShieldSlotChanged?.Invoke(shieldSlot);
    }
}
