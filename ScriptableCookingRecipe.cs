using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "New Cooking Recipe", menuName = "uMMORPG Cooking Recipe", order = 1000)]
public class ScriptableCookingRecipe : ScriptableRecipe
{
    // Cooking-specific fields
    public float cookingTime = 10f;

    public void UpdateCookingTime(Player player)
    {
        cookingTime = GetCookingTimeBasedOnLevel(player);
    }

    private float GetCookingTimeBasedOnLevel(Player player)
    {
        int cookingLevel = (player.skills as PlayerSkills)?.GetSkillLevel("Cooking") ?? 0;

        if (cookingLevel > 90) return 1f; // 1 second
        if (cookingLevel > 80) return 2f; // 2 seconds
        if (cookingLevel > 70) return 3f; // 3 seconds
        if (cookingLevel > 60) return 4f; // 4 seconds
        if (cookingLevel > 50) return 5f; // 5 seconds
        if (cookingLevel > 40) return 6f; // 6 seconds
        if (cookingLevel > 30) return 7f; // 7 seconds
        if (cookingLevel > 20) return 8f; // 8 seconds
        if (cookingLevel > 10) return 9f; // 9 seconds
        return 10f; // Default 10 seconds
    }


    // Override the Find method to filter and return only cooking recipes
    public new static ScriptableCookingRecipe Find(List<ItemSlot> items)
    {
        foreach (var recipe in All.Values)
        {
            // Ensure the recipe is of type ScriptableCookingRecipe and matches the ingredients
            if (recipe is ScriptableCookingRecipe cookingRecipe && cookingRecipe.CanCraftWith(items))
                return cookingRecipe;
        }
        return null;
    }

    // Ensure CanCraftWith uses inherited logic but is available for customization
    public override bool CanCraftWith(List<ItemSlot> items)
    {
        // Use base method logic for default behavior, customize if needed for cooking-specific validation
        return base.CanCraftWith(items);
    }
}
