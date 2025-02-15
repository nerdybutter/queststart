using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "New Blacksmithing Recipe", menuName = "uMMORPG Blacksmithing Recipe", order = 1000)]
public class ScriptableBlacksmithingRecipe : ScriptableRecipe
{
    // Blacksmithing-specific fields
    public float BlacksmithingTime = 10f;

    public void UpdateBlacksmithingTime(Player player)
    {
        BlacksmithingTime = GetBlacksmithingTimeBasedOnLevel(player);
    }

    private float GetBlacksmithingTimeBasedOnLevel(Player player)
    {
        int BlacksmithingLevel = (player.skills as PlayerSkills)?.GetSkillLevel("Blacksmithing") ?? 0;

        if (BlacksmithingLevel > 90) return 1f; // 1 second
        if (BlacksmithingLevel > 80) return 2f; // 2 seconds
        if (BlacksmithingLevel > 70) return 3f; // 3 seconds
        if (BlacksmithingLevel > 60) return 4f; // 4 seconds
        if (BlacksmithingLevel > 50) return 5f; // 5 seconds
        if (BlacksmithingLevel > 40) return 6f; // 6 seconds
        if (BlacksmithingLevel > 30) return 7f; // 7 seconds
        if (BlacksmithingLevel > 20) return 8f; // 8 seconds
        if (BlacksmithingLevel > 10) return 9f; // 9 seconds
        return 10f; // Default 10 seconds
    }


    // Override the Find method to filter and return only Blacksmithing recipes
    public new static ScriptableBlacksmithingRecipe Find(List<ItemSlot> items)
    {
        foreach (var recipe in All.Values)
        {
            // Ensure the recipe is of type ScriptableBlacksmithingRecipe and matches the ingredients
            if (recipe is ScriptableBlacksmithingRecipe BlacksmithingRecipe && BlacksmithingRecipe.CanCraftWith(items))
                return BlacksmithingRecipe;
        }
        return null;
    }

    // Ensure CanCraftWith uses inherited logic but is available for customization
    public override bool CanCraftWith(List<ItemSlot> items)
    {
        // Use base method logic for default behavior, customize if needed for Blacksmithing-specific validation
        return base.CanCraftWith(items);
    }
}
