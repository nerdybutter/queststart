using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "New Recipe", menuName = "uMMORPG Recipe", order = 999)]
public class ScriptableRecipe : ScriptableObject
{
    // fixed ingredient size for all recipes
    public static int recipeSize = 6;

    // ingredients and result
    public List<ScriptableItemAndAmount> ingredients = new List<ScriptableItemAndAmount>(6);
    public ScriptableItem result;

    // crafting time in seconds
    public float craftingTime = 1;

    // probability of success
    [Range(0, 1)] public float probability = 1;

    // List of possible enhancement rewards
    public List<EnhancementReward> enhancementRewards = new List<EnhancementReward>();

    // Helper function to check if an item slot list has at least one valid item
    bool IngredientsNotEmpty()
    {
        foreach (ScriptableItemAndAmount slot in ingredients)
            if (slot.amount > 0 && slot.item != null)
                return true;
        return false;
    }

    int FindMatchingStack(List<ItemSlot> items, ScriptableItemAndAmount ingredient)
    {
        for (int i = 0; i < items.Count; ++i)
            if (items[i].amount >= ingredient.amount &&
                items[i].item.data == ingredient.item)
                return i;
        return -1;
    }

    // check if the list of items works for this recipe
    public virtual bool CanCraftWith(List<ItemSlot> items)
    {
        items = new List<ItemSlot>(items);

        if (IngredientsNotEmpty())
        {
            foreach (ScriptableItemAndAmount ingredient in ingredients)
            {
                if (ingredient.amount > 0 && ingredient.item != null)
                {
                    int index = FindMatchingStack(items, ingredient);
                    if (index != -1)
                        items.RemoveAt(index);
                    else
                        return false;
                }
            }
            return items.Count == 0;
        }
        else return false;
    }

    // Apply enhancement rewards based on probability
    public ScriptableItem ApplyEnhancementReward()
    {
        foreach (var reward in enhancementRewards)
        {
            if (Random.Range(0f, 1f) <= reward.chance)
            {
                return reward.enhancedVersion; // Return the enhanced version
            }
        }
        return result; // Return the default result if no enhancement is applied
    }

    // caching /////////////////////////////////////////////////////////////////
    static Dictionary<string, ScriptableRecipe> cache;
    public static Dictionary<string, ScriptableRecipe> All
    {
        get
        {
            if (cache == null)
            {
                ScriptableRecipe[] recipes = Resources.LoadAll<ScriptableRecipe>("");

                List<string> duplicates = recipes.ToList().FindDuplicates(recipe => recipe.name);
                if (duplicates.Count == 0)
                {
                    cache = recipes.ToDictionary(recipe => recipe.name, recipe => recipe);
                }
                else
                {
                    foreach (string duplicate in duplicates)
                        Debug.LogError("Resources folder contains multiple ScriptableRecipes with the name " + duplicate + ".");
                }
            }
            return cache;
        }
    }

    // find a recipe based on item slots
    public static ScriptableRecipe Find(List<ItemSlot> items)
    {
        foreach (ScriptableRecipe recipe in All.Values)
            if (recipe.CanCraftWith(items))
                return recipe;
        return null;
    }

    // validation //////////////////////////////////////////////////////////////
    void OnValidate()
    {
        for (int i = ingredients.Count; i < recipeSize; ++i)
            ingredients.Add(new ScriptableItemAndAmount());

        ingredients.RemoveRange(recipeSize, ingredients.Count - recipeSize);
    }
}

// Enhancement reward structure
[System.Serializable]
public class EnhancementReward
{
    public ScriptableItem enhancedVersion; // The enhanced version of the item
    [Range(0f, 1f)] public float chance;   // Probability to receive this reward
}
