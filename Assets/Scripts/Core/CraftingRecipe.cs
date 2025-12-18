using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Recipe", menuName = "Minecraft/Crafting Recipe")]
public class CraftingRecipe : ScriptableObject
{
    // Grid size: 2 for 2x2, 3 for 3x3
    public int gridSize = 2;

    // Ingredients array. 
    // For 2x2: size 4 (indices 0-3)
    // For 3x3: size 9 (indices 0-8)
    [Tooltip("Items required for the recipe. Size must be 4 (for 2x2) or 9 (for 3x3).")]
    public Item[] ingredients;
    
    public Item result;
    public int resultCount = 1;

    public bool Matches(ItemStack[] craftingGrid, int currentGridSize)
    {
        if (craftingGrid == null) return false;
        
        // If recipe is 3x3 but we are in 2x2 mode, it can't match
        if (gridSize > currentGridSize) return false;

        // We need to map the recipe's grid to the crafting grid
        // Case 1: Recipe is 2x2, Grid is 2x2 -> Direct match (0-3)
        // Case 2: Recipe is 3x3, Grid is 3x3 -> Direct match (0-8)
        // Case 3: Recipe is 2x2, Grid is 3x3 -> We need to check if the 2x2 recipe exists ANYWHERE in the 3x3 grid?
        // For simplicity in this implementation:
        // - 2x2 recipes must be crafted in the 2x2 grid OR the top-left 2x2 of the 3x3 grid?
        // - Minecraft allows 2x2 recipes in any 2x2 area of the 3x3 grid.
        // - To keep it simple: 
        //   - If currentGridSize is 2, we only check indices 0-3 against a 2x2 recipe.
        //   - If currentGridSize is 3, we check indices 0-8 against a 3x3 recipe.
        //   - BUT, a 2x2 recipe should be craftable in a 3x3 grid.
        
        // SIMPLIFIED LOGIC:
        // If recipe is 2x2, we expect the ingredients to match the first 4 slots of the input grid IF the input grid is treated as 2x2.
        // If the input grid is 3x3, we need to be careful.
        
        // Let's enforce:
        // - 2x2 Inventory Grid uses indices 0,1,2,3.
        // - 3x3 Crafting Table Grid uses indices 0..8.
        
        // If we are in 3x3 mode, we can craft 3x3 recipes.
        // Can we craft 2x2 recipes in 3x3 mode? Yes, usually.
        // But implementing the "anywhere" logic is complex.
        // Let's stick to: 2x2 recipes must be placed in the top-left 2x2 of the 3x3 grid if we are in 3x3 mode.
        // Or better: The 2x2 Inventory Grid is a separate concept from the 3x3 Table Grid.
        
        int checkSize = gridSize * gridSize;
        if (ingredients.Length < checkSize) return false;

        // Check for exact match in the defined area
        for (int i = 0; i < checkSize; i++)
        {
            Item recipeItem = ingredients[i];
            
            // Map index i to the craftingGrid index
            // If both are same size, it's 1:1
            // If grid is 3x3 and recipe is 2x2:
            // Recipe: 0 1
            //         2 3
            // Grid:   0 1 2
            //         3 4 5
            //         6 7 8
            // Mapping 2x2 to top-left of 3x3:
            // 0->0, 1->1, 2->3, 3->4
            
            int gridIndex = i;
            if (currentGridSize == 3 && gridSize == 2)
            {
                // Map 2x2 index to 3x3 index (Top-Left alignment)
                if (i == 2) gridIndex = 3;
                else if (i == 3) gridIndex = 4;
            }

            if (gridIndex >= craftingGrid.Length) return false;

            ItemStack gridStack = craftingGrid[gridIndex];

            // Check match
            if (recipeItem == null)
            {
                if (gridStack != null && !gridStack.IsEmpty()) return false;
            }
            else
            {
                if (gridStack == null || gridStack.IsEmpty() || gridStack.item.itemName != recipeItem.itemName)
                {
                    return false;
                }
            }
        }

        // Ensure other slots are empty (if any)
        // This is tricky if we map 2x2 into 3x3. We need to ensure the REST of 3x3 is empty.
        if (currentGridSize == 3)
        {
            for (int i = 0; i < 9; i++)
            {
                // Check if 'i' was part of the recipe
                bool isRecipeSlot = false;
                if (gridSize == 3)
                {
                    isRecipeSlot = true; // All slots used in check
                }
                else // gridSize == 2
                {
                    // Indices 0, 1, 3, 4 are used
                    if (i == 0 || i == 1 || i == 3 || i == 4) isRecipeSlot = true;
                }

                if (!isRecipeSlot)
                {
                    if (!craftingGrid[i].IsEmpty()) return false;
                }
            }
        }

        return true;
    }
}
