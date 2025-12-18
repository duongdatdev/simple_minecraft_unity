using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

/// <summary>
/// UI component for a single inventory slot
/// Displays item icon, count, and handles durability bar for tools
/// </summary>
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI countText;
    public Image durabilityBar;
    public Image selectionHighlight;
    public Image backgroundImage;

    [Header("Settings")]
    public Color emptySlotColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    public Color filledSlotColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
    public Color selectedSlotColor = new Color(1f, 1f, 1f, 1f);

    private ItemStack currentStack;
    private int slotIndex;
    private SlotType slotType;

    public event Action<int, SlotType, PointerEventData.InputButton> OnSlotClicked;

    public enum SlotType
    {
        Inventory, // Main + Hotbar
        Crafting,
        Result,
        Armor,
        Shield
    }

    public void Initialize(int index, SlotType type = SlotType.Inventory)
    {
        slotIndex = index;
        slotType = type;
        SetEmpty();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnSlotClicked?.Invoke(slotIndex, slotType, eventData.button);
    }

    /// <summary>
    /// Update slot display with item stack
    /// </summary>
    public void UpdateSlot(ItemStack stack)
    {
        currentStack = stack;

        if (stack == null || stack.IsEmpty())
        {
            SetEmpty();
            return;
        }

        // Show item
        if (iconImage != null)
        {
            iconImage.enabled = true;
            iconImage.sprite = stack.item.icon;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = filledSlotColor;
        }

        // Show count (if more than 1 and not a tool)
        if (countText != null)
        {
            if (stack.count > 1 && stack.item.itemType != ItemType.Tool && stack.item.itemType != ItemType.Weapon)
            {
                countText.enabled = true;
                countText.text = stack.count.ToString();
            }
            else
            {
                countText.enabled = false;
            }
        }

        // Show durability bar for tools
        if (durabilityBar != null)
        {
            if ((stack.item.itemType == ItemType.Tool || stack.item.itemType == ItemType.Weapon) && stack.item.maxDurability > 0)
            {
                durabilityBar.enabled = true;
                float durabilityPercent = (float)stack.currentDurability / stack.item.maxDurability;
                durabilityBar.fillAmount = durabilityPercent;
                
                // Color based on durability (green -> yellow -> red)
                if (durabilityPercent > 0.5f)
                    durabilityBar.color = Color.green;
                else if (durabilityPercent > 0.25f)
                    durabilityBar.color = Color.yellow;
                else
                    durabilityBar.color = Color.red;
            }
            else
            {
                durabilityBar.enabled = false;
            }
        }
    }

    /// <summary>
    /// Set slot as empty
    /// </summary>
    public void SetEmpty()
    {
        if (iconImage != null)
        {
            iconImage.enabled = false;
        }

        if (countText != null)
        {
            countText.enabled = false;
        }

        if (durabilityBar != null)
        {
            durabilityBar.enabled = false;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = emptySlotColor;
        }
    }

    /// <summary>
    /// Set slot as selected (highlight)
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null)
        {
            selectionHighlight.enabled = selected;
            selectionHighlight.color = selectedSlotColor;
        }
    }

    public ItemStack GetItemStack()
    {
        return currentStack;
    }

    public int GetSlotIndex()
    {
        return slotIndex;
    }
}
