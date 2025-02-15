using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "uMMORPG Quest/Quest Recipe", order = 999)]
public class ScriptableQuestRecipe : ScriptableObject
{
    [Header("Required Quest Items")]
    public List<ScriptableItem> requiredItems; // The items needed for merging.

    [Header("Resulting Item")]
    public ScriptableItem resultingItem; // The final item after merging.

    [Header("Requirements")]
    public int requiredLevel = 0; // Optional: Player must be this level.
    public string requiredQuestName = ""; // Optional: Must be on this quest.

    // Check if the player meets all the requirements
    public bool MeetsRequirements(Player player)
    {
        // Check if player is high enough level
        if (player.level.current < requiredLevel)
            return false;

        // Check if the required quest is active
        if (!string.IsNullOrEmpty(requiredQuestName) && !player.quests.HasActive(requiredQuestName))
            return false;

        // Check if player has all required items
        foreach (var item in requiredItems)
        {
            if (!player.inventory.HasEnough(new Item(item), 1))
                return false;
        }

        return true;
    }
}
