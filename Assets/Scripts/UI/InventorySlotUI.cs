using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

/// <summary>
/// UI component for a single inventory slot
/// Displays item icon, count, and handles durability bar for tools
/// </summary>
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI References")]
    public Image iconImage;
    public TextMeshProUGUI countText;
    public Image durabilityBar;
    public Image selectionHighlight;
    public Image backgroundImage;
    public Image hoverOverlay; // New hover overlay

    [Header("Settings")]
    public Color emptySlotColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    public Color filledSlotColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
    public Color selectedSlotColor = new Color(1f, 1f, 1f, 1f);
    public Color hoverColor = new Color(1f, 1f, 1f, 0.2f); // Light white overlay
    public bool enableSelectionHighlight = true; // Control if this slot can show selection highlight

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

    private void Awake()
    {
        // Auto-assign references if missing to prevent visual glitches
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        
        if (iconImage == null)
        {
            // Look for a child named "Icon" or similar
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Icon") || child.name.Contains("Item"))
                {
                    iconImage = child.GetComponent<Image>();
                    break;
                }
            }
        }

        if (countText == null) countText = GetComponentInChildren<TextMeshProUGUI>(true);
        if (durabilityBar == null)
        {
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Durability"))
                {
                    durabilityBar = child.GetComponent<Image>();
                    break;
                }
            }
        }
    }

    public void Initialize(int index, SlotType type = SlotType.Inventory)
    {
        slotIndex = index;
        slotType = type;
        
        // Create hover overlay if not assigned
        if (hoverOverlay == null)
        {
            CreateHoverOverlay();
        }
        
        SetEmpty();
    }

    private void CreateHoverOverlay()
    {
        GameObject overlayObj = new GameObject("HoverOverlay");
        overlayObj.transform.SetParent(transform, false);
        
        RectTransform rt = overlayObj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        
        hoverOverlay = overlayObj.AddComponent<Image>();
        hoverOverlay.color = hoverColor;
        hoverOverlay.raycastTarget = false;
        hoverOverlay.enabled = false;
        
        // Move to back so it doesn't cover the item icon
        overlayObj.transform.SetAsFirstSibling();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnSlotClicked?.Invoke(slotIndex, slotType, eventData.button);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverOverlay != null)
        {
            hoverOverlay.enabled = true;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverOverlay != null)
        {
            hoverOverlay.enabled = false;
        }
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
            if (stack.item.icon != null)
            {
                iconImage.enabled = true;
                iconImage.sprite = stack.item.icon;
            }
            else
            {
                // Hide icon if sprite is missing to avoid white square
                iconImage.enabled = false;
            }
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = filledSlotColor;
            backgroundImage.enabled = true; // Ensure background is visible
            backgroundImage.raycastTarget = true; // Ensure it catches clicks
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
            backgroundImage.enabled = true; // Ensure background is visible
            backgroundImage.raycastTarget = true; // Ensure it catches clicks
        }
    }

    /// <summary>
    /// Set slot as selected (highlight)
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (selectionHighlight != null)
        {
            if (!enableSelectionHighlight)
            {
                selectionHighlight.enabled = false;
                return;
            }

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
