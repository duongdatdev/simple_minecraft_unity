using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Main UI controller for inventory display
/// - Hotbar: bottom 9 slots (always visible)
/// - Main inventory: 27 slots (toggle with 'E' key)
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    public Inventory playerInventory;
    public GameObject hotbarPanel;
    public GameObject mainInventoryPanel;
    public GameObject inventorySlotPrefab;

    [Header("Hotbar Settings")]
    public Transform hotbarSlotsParent;
    public Transform internalHotbarSlotsParent; // New: For hotbar inside inventory panel
    public KeyCode toggleInventoryKey = KeyCode.E;

    [Header("Main Inventory Settings")]
    public Transform mainInventorySlotsParent;
    public int inventoryRows = 3;
    public int inventoryColumns = 9;

    [Header("Crafting UI")]
    public GameObject inventoryCraftingPanel; // Panel containing 2x2 grid
    public Transform craftingSlotsParent; // 2x2 parent
    public Transform resultSlotParent; // 2x2 result
    
    [Header("Crafting Table UI")]
    public GameObject craftingTablePanel; // Panel containing 3x3 grid
    public Transform crafting3x3SlotsParent;
    public Transform crafting3x3ResultSlotParent;

    [Header("Armor UI")]
    public Transform armorSlotsParent;
    public Transform shieldSlotParent; // New shield slot parent
    public GameObject characterViewPlaceholder; // Placeholder for character view

    [Header("Cursor")]
    public Image cursorItemImage;
    public TMPro.TextMeshProUGUI cursorItemCount;

    private List<InventorySlotUI> hotbarSlots = new List<InventorySlotUI>();
    private List<InventorySlotUI> internalHotbarSlots = new List<InventorySlotUI>(); // New list
    private List<InventorySlotUI> mainInventorySlots = new List<InventorySlotUI>();
    
    private List<InventorySlotUI> craftingSlotsUI = new List<InventorySlotUI>(); // 2x2
    private InventorySlotUI resultSlotUI; // 2x2
    
    private List<InventorySlotUI> crafting3x3SlotsUI = new List<InventorySlotUI>(); // 3x3
    private InventorySlotUI result3x3SlotUI; // 3x3

    private List<InventorySlotUI> armorSlotsUI = new List<InventorySlotUI>();
    private InventorySlotUI shieldSlotUI; // Shield slot UI
    
    private ItemStack cursorStack = new ItemStack(null, 0);
    private bool isMainInventoryOpen = false;
    private bool isCraftingTableOpen = false;

    private void Start()
    {
        if (playerInventory == null)
        {
            playerInventory = FindObjectOfType<Inventory>();
        }

        // Ensure HUD hotbar panel is visible so HUD hotbar doesn't disappear
        if (hotbarPanel != null && !hotbarPanel.activeSelf)
        {
            hotbarPanel.SetActive(true);
        }

        CreateHotbarSlots();
        CreateMainInventorySlots();
        CreateCraftingSlots(); // 2x2
        CreateResultSlot(); // 2x2
        CreateCrafting3x3Slots(); // 3x3
        CreateResult3x3Slot(); // 3x3
        CreateArmorSlots();
        CreateShieldSlot(); // Create shield slot

        // Subscribe to inventory events
        if (playerInventory != null)
        {
            playerInventory.OnSlotChanged += OnInventorySlotChanged;
            playerInventory.OnHotbarSelectionChanged += OnHotbarSelectionChanged;
            playerInventory.OnCraftingSlotChanged += OnCraftingSlotChanged;
            playerInventory.OnCraftResultChanged += OnCraftResultChanged;
            playerInventory.OnArmorSlotChanged += OnArmorSlotChanged;
            playerInventory.OnShieldSlotChanged += OnShieldSlotChanged;
        }

        // Start with main inventory closed
        CloseMainInventory();
        CloseCraftingTable();
        
        // Initial update
        RefreshAllSlots();
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (playerInventory != null)
        {
            playerInventory.OnSlotChanged -= OnInventorySlotChanged;
            playerInventory.OnHotbarSelectionChanged -= OnHotbarSelectionChanged;
            playerInventory.OnCraftingSlotChanged -= OnCraftingSlotChanged;
            playerInventory.OnCraftResultChanged -= OnCraftResultChanged;
            playerInventory.OnArmorSlotChanged -= OnArmorSlotChanged;
            playerInventory.OnShieldSlotChanged -= OnShieldSlotChanged;
        }
    }

    private void Update()
    {
        // Toggle main inventory with E key
        if (Input.GetKeyDown(toggleInventoryKey))
        {
            if (isCraftingTableOpen)
            {
                CloseCraftingTable();
            }
            else
            {
                ToggleMainInventory();
            }
        }

        // Update cursor item position
        if (cursorItemImage != null && cursorItemImage.enabled)
        {
            cursorItemImage.transform.position = Input.mousePosition;
        }
    }

    /// <summary>
    /// Create hotbar slots UI (9 slots)
    /// </summary>
    private void CreateHotbarSlots()
    {
        if (inventorySlotPrefab == null) return;

        // If no explicit parent was assigned for HUD hotbar slots, try to use/create one under hotbarPanel
        if (hotbarSlotsParent == null && hotbarPanel != null)
        {
            Transform existing = hotbarPanel.transform.Find("HotbarSlots");
            if (existing != null)
            {
                hotbarSlotsParent = existing;
            }
            else
            {
                GameObject go = new GameObject("HotbarSlots", typeof(RectTransform));
                go.transform.SetParent(hotbarPanel.transform, false);
                hotbarSlotsParent = go.transform;
            }
        }

        // Create HUD Hotbar Slots
        if (hotbarSlotsParent != null)
        {
            for (int i = 0; i < Inventory.HOTBAR_SIZE; i++)
            {
                GameObject slotObj = Instantiate(inventorySlotPrefab, hotbarSlotsParent);
                InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
                
                if (slotUI != null)
                {
                    slotUI.Initialize(i, InventorySlotUI.SlotType.Inventory);
                    slotUI.OnSlotClicked += HandleSlotClick;
                    hotbarSlots.Add(slotUI);
                }
            }
        }

        // Create Internal Hotbar Slots (inside Inventory Panel)
        // Avoid creating duplicate slots if both parents point to the same transform
        if (internalHotbarSlotsParent != null && internalHotbarSlotsParent != hotbarSlotsParent)
        {
            for (int i = 0; i < Inventory.HOTBAR_SIZE; i++)
            {
                GameObject slotObj = Instantiate(inventorySlotPrefab, internalHotbarSlotsParent);
                InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
                
                if (slotUI != null)
                {
                    slotUI.Initialize(i, InventorySlotUI.SlotType.Inventory);
                    slotUI.OnSlotClicked += HandleSlotClick;
                    internalHotbarSlots.Add(slotUI);
                }
            }
        }

        // Highlight first slot by default
        OnHotbarSelectionChanged(0);
    }

    /// <summary>
    /// Create main inventory slots UI (27 slots = 3 rows x 9 columns)
    /// </summary>
    private void CreateMainInventorySlots()
    {
        if (mainInventorySlotsParent == null || inventorySlotPrefab == null) return;

        for (int i = 0; i < Inventory.MAIN_INVENTORY_SIZE; i++)
        {
            GameObject slotObj = Instantiate(inventorySlotPrefab, mainInventorySlotsParent);
            InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
            
            if (slotUI != null)
            {
                slotUI.Initialize(Inventory.HOTBAR_SIZE + i, InventorySlotUI.SlotType.Inventory); // offset by hotbar size
                slotUI.OnSlotClicked += HandleSlotClick;
                mainInventorySlots.Add(slotUI);
            }
        }
    }

    private void CreateCraftingSlots()
    {
        if (craftingSlotsParent == null || inventorySlotPrefab == null) return;

        for (int i = 0; i < 4; i++)
        {
            GameObject slotObj = Instantiate(inventorySlotPrefab, craftingSlotsParent);
            InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
            if (slotUI != null)
            {
                slotUI.Initialize(i, InventorySlotUI.SlotType.Crafting);
                slotUI.OnSlotClicked += HandleSlotClick;
                craftingSlotsUI.Add(slotUI);
            }
        }
    }

    private void CreateResultSlot()
    {
        if (resultSlotParent == null || inventorySlotPrefab == null) return;

        GameObject slotObj = Instantiate(inventorySlotPrefab, resultSlotParent);
        resultSlotUI = slotObj.GetComponent<InventorySlotUI>();
        if (resultSlotUI != null)
        {
            resultSlotUI.Initialize(0, InventorySlotUI.SlotType.Result);
            resultSlotUI.OnSlotClicked += HandleSlotClick;
        }
    }

    private void CreateCrafting3x3Slots()
    {
        if (crafting3x3SlotsParent == null || inventorySlotPrefab == null) return;

        for (int i = 0; i < 9; i++)
        {
            GameObject slotObj = Instantiate(inventorySlotPrefab, crafting3x3SlotsParent);
            InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
            if (slotUI != null)
            {
                slotUI.Initialize(i, InventorySlotUI.SlotType.Crafting);
                slotUI.OnSlotClicked += HandleSlotClick;
                crafting3x3SlotsUI.Add(slotUI);
            }
        }
    }

    private void CreateResult3x3Slot()
    {
        if (crafting3x3ResultSlotParent == null || inventorySlotPrefab == null) return;

        GameObject slotObj = Instantiate(inventorySlotPrefab, crafting3x3ResultSlotParent);
        result3x3SlotUI = slotObj.GetComponent<InventorySlotUI>();
        if (result3x3SlotUI != null)
        {
            result3x3SlotUI.Initialize(0, InventorySlotUI.SlotType.Result);
            result3x3SlotUI.OnSlotClicked += HandleSlotClick;
        }
    }

    private void CreateArmorSlots()
    {
        if (armorSlotsParent == null || inventorySlotPrefab == null) return;

        for (int i = 0; i < 4; i++)
        {
            GameObject slotObj = Instantiate(inventorySlotPrefab, armorSlotsParent);
            InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
            if (slotUI != null)
            {
                slotUI.Initialize(i, InventorySlotUI.SlotType.Armor);
                slotUI.OnSlotClicked += HandleSlotClick;
                armorSlotsUI.Add(slotUI);
            }
        }
    }

    private void CreateShieldSlot()
    {
        if (shieldSlotParent == null || inventorySlotPrefab == null) return;

        GameObject slotObj = Instantiate(inventorySlotPrefab, shieldSlotParent);
        shieldSlotUI = slotObj.GetComponent<InventorySlotUI>();
        if (shieldSlotUI != null)
        {
            shieldSlotUI.Initialize(0, InventorySlotUI.SlotType.Shield);
            shieldSlotUI.OnSlotClicked += HandleSlotClick;
        }
    }

    private void HandleSlotClick(int index, InventorySlotUI.SlotType type, PointerEventData.InputButton button)
    {
        if (!isMainInventoryOpen) return;

        if (type == InventorySlotUI.SlotType.Result)
        {
            HandleResultSlotClick(button);
            return;
        }

        ItemStack clickedStack = null;
        switch (type)
        {
            case InventorySlotUI.SlotType.Inventory:
                clickedStack = playerInventory.GetSlot(index);
                break;
            case InventorySlotUI.SlotType.Crafting:
                clickedStack = playerInventory.craftingSlots[index];
                break;
            case InventorySlotUI.SlotType.Armor:
                clickedStack = playerInventory.armorSlots[index];
                break;
            case InventorySlotUI.SlotType.Shield:
                clickedStack = playerInventory.shieldSlot;
                break;
        }

        if (button == PointerEventData.InputButton.Left)
        {
            // Logic for swapping/stacking with cursor (Left Click)
            if (cursorStack.IsEmpty())
            {
                if (clickedStack != null && !clickedStack.IsEmpty())
                {
                    // Pick up item
                    cursorStack = clickedStack;
                    SetSlot(type, index, new ItemStack(null, 0));
                }
            }
            else
            {
                if (clickedStack == null || clickedStack.IsEmpty())
                {
                    // Place item
                    SetSlot(type, index, cursorStack);
                    cursorStack = new ItemStack(null, 0);
                }
                else if (clickedStack.CanStackWith(cursorStack))
                {
                    // Stack items
                    int remaining = clickedStack.AddCount(cursorStack.count);
                    if (remaining > 0)
                    {
                        cursorStack.count = remaining;
                    }
                    else
                    {
                        cursorStack = new ItemStack(null, 0);
                    }
                    SetSlot(type, index, clickedStack);
                }
                else
                {
                    // Swap items
                    ItemStack temp = clickedStack;
                    SetSlot(type, index, cursorStack);
                    cursorStack = temp;
                }
            }
        }
        else if (button == PointerEventData.InputButton.Right)
        {
            // Logic for splitting/placing one (Right Click)
            if (cursorStack.IsEmpty())
            {
                if (clickedStack != null && !clickedStack.IsEmpty())
                {
                    // Pick up half
                    int total = clickedStack.count;
                    int half = Mathf.CeilToInt(total / 2.0f);
                    int remaining = total - half;
                    
                    cursorStack = new ItemStack(clickedStack.item, half);
                    cursorStack.currentDurability = clickedStack.currentDurability;
                    
                    if (remaining > 0)
                    {
                        clickedStack.count = remaining;
                        SetSlot(type, index, clickedStack);
                    }
                    else
                    {
                        SetSlot(type, index, new ItemStack(null, 0));
                    }
                }
            }
            else
            {
                // Place one item
                if (type == InventorySlotUI.SlotType.Armor) return; // Prevent right-click placing in armor slots

                if (clickedStack == null || clickedStack.IsEmpty())
                {
                    // Place one into empty slot
                    ItemStack oneItem = new ItemStack(cursorStack.item, 1);
                    oneItem.currentDurability = cursorStack.currentDurability;
                    SetSlot(type, index, oneItem);
                    
                    cursorStack.RemoveCount(1);
                }
                else if (clickedStack.CanStackWith(cursorStack))
                {
                    // Add one to stack
                    if (clickedStack.count < clickedStack.item.maxStackSize)
                    {
                        clickedStack.AddCount(1);
                        SetSlot(type, index, clickedStack);
                        cursorStack.RemoveCount(1);
                    }
                }
                else
                {
                    // Swap items
                    ItemStack temp = clickedStack;
                    SetSlot(type, index, cursorStack);
                    cursorStack = temp;
                }
            }
        }

        UpdateCursorUI();
    }

    private void HandleResultSlotClick(PointerEventData.InputButton button)
    {
        if (playerInventory.craftResultSlot.IsEmpty()) return;

        if (cursorStack.IsEmpty())
        {
            // Pick up crafted item
            cursorStack = playerInventory.craftResultSlot;
            playerInventory.CraftItem();
        }
        else if (cursorStack.CanStackWith(playerInventory.craftResultSlot))
        {
            // Stack crafted item
            if (cursorStack.count + playerInventory.craftResultSlot.count <= cursorStack.item.maxStackSize)
            {
                cursorStack.AddCount(playerInventory.craftResultSlot.count);
                playerInventory.CraftItem();
            }
        }
        
        UpdateCursorUI();
    }

    private void SetSlot(InventorySlotUI.SlotType type, int index, ItemStack stack)
    {
        switch (type)
        {
            case InventorySlotUI.SlotType.Inventory:
                playerInventory.SetSlot(index, stack);
                break;
            case InventorySlotUI.SlotType.Crafting:
                playerInventory.SetCraftingSlot(index, stack);
                break;
            case InventorySlotUI.SlotType.Armor:
                playerInventory.SetArmorSlot(index, stack);
                break;
            case InventorySlotUI.SlotType.Shield:
                playerInventory.SetShieldSlot(stack);
                break;
        }
    }

    private void UpdateCursorUI()
    {
        if (cursorItemImage != null)
        {
            if (!cursorStack.IsEmpty())
            {
                cursorItemImage.enabled = true;
                cursorItemImage.sprite = cursorStack.item.icon;
                if (cursorItemCount != null)
                {
                    cursorItemCount.text = cursorStack.count > 1 ? cursorStack.count.ToString() : "";
                }
            }
            else
            {
                cursorItemImage.enabled = false;
                if (cursorItemCount != null) cursorItemCount.text = "";
            }
        }
    }

    private void OnCraftingSlotChanged(int index, ItemStack stack)
    {
        // Update 2x2 UI
        if (index < craftingSlotsUI.Count)
        {
            craftingSlotsUI[index].UpdateSlot(stack);
        }
        
        // Update 3x3 UI
        if (index < crafting3x3SlotsUI.Count)
        {
            crafting3x3SlotsUI[index].UpdateSlot(stack);
        }
    }

    private void OnCraftResultChanged(ItemStack stack)
    {
        if (resultSlotUI != null)
        {
            resultSlotUI.UpdateSlot(stack);
        }
        if (result3x3SlotUI != null)
        {
            result3x3SlotUI.UpdateSlot(stack);
        }
    }

    private void OnArmorSlotChanged(int index, ItemStack stack)
    {
        if (index < armorSlotsUI.Count)
        {
            armorSlotsUI[index].UpdateSlot(stack);
        }
    }

    private void OnShieldSlotChanged(ItemStack stack)
    {
        if (shieldSlotUI != null)
        {
            shieldSlotUI.UpdateSlot(stack);
        }
    }

    /// <summary>
    /// Called when inventory slot changes
    /// </summary>
    private void OnInventorySlotChanged(int slotIndex, ItemStack stack)
    {
        UpdateSlotUI(slotIndex, stack);
    }

    /// <summary>
    /// Called when hotbar selection changes
    /// </summary>
    private void OnHotbarSelectionChanged(int selectedSlot)
    {
        // Update selection highlights HUD
        for (int i = 0; i < hotbarSlots.Count; i++)
        {
            hotbarSlots[i].SetSelected(i == selectedSlot);
        }
        
        // Update selection highlights Internal
        for (int i = 0; i < internalHotbarSlots.Count; i++)
        {
            internalHotbarSlots[i].SetSelected(i == selectedSlot);
        }
    }

    /// <summary>
    /// Update specific slot UI
    /// </summary>
    private void UpdateSlotUI(int slotIndex, ItemStack stack)
    {
        if (slotIndex < Inventory.HOTBAR_SIZE)
        {
            // Hotbar slot
            if (slotIndex < hotbarSlots.Count)
            {
                hotbarSlots[slotIndex].UpdateSlot(stack);
            }
            // Internal Hotbar slot
            if (slotIndex < internalHotbarSlots.Count)
            {
                internalHotbarSlots[slotIndex].UpdateSlot(stack);
            }
        }
        else
        {
            // Main inventory slot
            int mainIndex = slotIndex - Inventory.HOTBAR_SIZE;
            if (mainIndex < mainInventorySlots.Count)
            {
                mainInventorySlots[mainIndex].UpdateSlot(stack);
            }
        }
    }

    /// <summary>
    /// Refresh all slots (useful for initialization)
    /// </summary>
    private void RefreshAllSlots()
    {
        if (playerInventory == null) return;

        // Update hotbar
        for (int i = 0; i < Inventory.HOTBAR_SIZE; i++)
        {
            ItemStack stack = playerInventory.GetSlot(i);
            if (i < hotbarSlots.Count)
            {
                hotbarSlots[i].UpdateSlot(stack);
            }
            if (i < internalHotbarSlots.Count)
            {
                internalHotbarSlots[i].UpdateSlot(stack);
            }
        }

        // Update main inventory
        for (int i = 0; i < Inventory.MAIN_INVENTORY_SIZE; i++)
        {
            ItemStack stack = playerInventory.GetSlot(Inventory.HOTBAR_SIZE + i);
            if (i < mainInventorySlots.Count)
            {
                mainInventorySlots[i].UpdateSlot(stack);
            }
        }

        // Update hotbar selection
        OnHotbarSelectionChanged(playerInventory.selectedHotbarSlot);
    }

    /// <summary>
    /// Toggle main inventory panel
    /// </summary>
    public void ToggleMainInventory()
    {
        if (isCraftingTableOpen)
        {
            CloseCraftingTable();
        }
        else if (isMainInventoryOpen)
        {
            CloseMainInventory();
        }
        else
        {
            OpenMainInventory();
        }
    }

    /// <summary>
    /// Open main inventory (2x2 crafting)
    /// </summary>
    public void OpenMainInventory()
    {
        isMainInventoryOpen = true;
        isCraftingTableOpen = false;
        
        if (mainInventoryPanel != null) mainInventoryPanel.SetActive(true);
        if (inventoryCraftingPanel != null) inventoryCraftingPanel.SetActive(true);
        if (craftingTablePanel != null) craftingTablePanel.SetActive(false);

        if (playerInventory != null) playerInventory.SetCraftingGridSize(2);

        // Unlock cursor when inventory is open
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshAllSlots();
    }

    /// <summary>
    /// Close main inventory
    /// </summary>
    public void CloseMainInventory()
    {
        isMainInventoryOpen = false;
        isCraftingTableOpen = false;
        
        if (mainInventoryPanel != null) mainInventoryPanel.SetActive(false);
        if (inventoryCraftingPanel != null) inventoryCraftingPanel.SetActive(false);
        if (craftingTablePanel != null) craftingTablePanel.SetActive(false);

        // Lock cursor when inventory is closed
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Return cursor item to inventory if any
        if (playerInventory != null)
        {
            if (!cursorStack.IsEmpty())
            {
                playerInventory.AddItem(cursorStack.item, cursorStack.count);
                cursorStack = new ItemStack(null, 0);
                UpdateCursorUI();
            }

            // Return crafting items to inventory
            for (int i = 0; i < playerInventory.craftingSlots.Length; i++)
            {
                if (playerInventory.craftingSlots[i] != null && !playerInventory.craftingSlots[i].IsEmpty())
                {
                    playerInventory.AddItem(playerInventory.craftingSlots[i].item, playerInventory.craftingSlots[i].count);
                    playerInventory.SetCraftingSlot(i, new ItemStack(null, 0));
                }
            }
        }
    }

    public void OpenCraftingTable()
    {
        isMainInventoryOpen = true;
        isCraftingTableOpen = true;

        if (mainInventoryPanel != null) mainInventoryPanel.SetActive(true);
        if (inventoryCraftingPanel != null) inventoryCraftingPanel.SetActive(false);
        if (craftingTablePanel != null) craftingTablePanel.SetActive(true);

        if (playerInventory != null) playerInventory.SetCraftingGridSize(3);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        RefreshAllSlots();
    }

    public void CloseCraftingTable()
    {
        CloseMainInventory();
    }

    public bool IsInventoryOpen()
    {
        return isMainInventoryOpen;
    }
}
