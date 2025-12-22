using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HUDManager : MonoBehaviour
{
    [Header("References")]
    public PlayerController player;
    
    [Header("Health")]
    public Transform healthBarParent;
    public GameObject heartPrefab; // Prefab with an Image component
    public Sprite fullHeart;
    public Sprite halfHeart;
    public Sprite emptyHeart;

    [Header("Hunger")]
    public Transform hungerBarParent;
    public GameObject foodPrefab; // Prefab with an Image component
    public Sprite fullFood;
    public Sprite halfFood;
    public Sprite emptyFood;

    private List<Image> healthIcons = new List<Image>();
    private List<Image> hungerIcons = new List<Image>();

    void Start()
    {
        if (player == null)
            player = Object.FindAnyObjectByType<PlayerController>();
            
        InitializeHUD();
    }

    void Update()
    {
        if (player != null)
        {
            UpdateHealth();
            UpdateHunger();
        }
    }

    void InitializeHUD()
    {
        // Create 10 hearts
        if (healthBarParent != null && heartPrefab != null)
        {
            for (int i = 0; i < 10; i++)
            {
                GameObject obj = Instantiate(heartPrefab, healthBarParent);
                healthIcons.Add(obj.GetComponent<Image>());
            }
        }

        // Create 10 food icons
        if (hungerBarParent != null && foodPrefab != null)
        {
            for (int i = 0; i < 10; i++)
            {
                GameObject obj = Instantiate(foodPrefab, hungerBarParent);
                hungerIcons.Add(obj.GetComponent<Image>());
            }
        }
    }

    void UpdateHealth()
    {
        if (healthIcons.Count == 0) return;

        float healthPerHeart = player.maxHealth / 10f;
        for (int i = 0; i < 10; i++)
        {
            float heartValue = (i + 1) * healthPerHeart;
            if (player.currentHealth >= heartValue)
            {
                healthIcons[i].sprite = fullHeart;
            }
            else if (player.currentHealth >= heartValue - (healthPerHeart / 2))
            {
                healthIcons[i].sprite = halfHeart;
            }
            else
            {
                healthIcons[i].sprite = emptyHeart;
            }
        }
    }

    void UpdateHunger()
    {
        if (hungerIcons.Count == 0) return;

        float foodPerIcon = player.maxHunger / 10f;
        for (int i = 0; i < 10; i++)
        {
            float iconValue = (i + 1) * foodPerIcon;
            if (player.currentHunger >= iconValue)
            {
                hungerIcons[i].sprite = fullFood;
            }
            else if (player.currentHunger >= iconValue - (foodPerIcon / 2))
            {
                hungerIcons[i].sprite = halfFood;
            }
            else
            {
                hungerIcons[i].sprite = emptyFood;
            }
        }
    }
}
