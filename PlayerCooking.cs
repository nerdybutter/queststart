using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using Mirror;

public enum CookingState { None, InProgress, Success, Failed }

[RequireComponent(typeof(PlayerInventory))]
[DisallowMultipleComponent]
public class PlayerCooking : NetworkBehaviour
{
    [Header("Components")]
    public Player player;
    public PlayerInventory inventory;
    public AudioClip cookingSound;

    [Header("Cooking")]
    public List<int> indices = Enumerable.Repeat(-1, ScriptableCookingRecipe.recipeSize).ToList();
    [HideInInspector] public CookingState state = CookingState.None; // // client sided
    public ScriptableCookingRecipe currentRecipe; // currently Cooked recipe. cached to avoid searching ALL recipes in Cook()
    [SyncVar, HideInInspector] public double endTime; // double for long term precision
    [HideInInspector] public bool requestPending; // for state machine event
    public double cookingEndTime => endTime;

    // Cooking ////////////////////////////////////////////////////////////////
    // the Cooking system is designed to work with all kinds of commonly known
    // Cooking options:
    // - item combinations: wood + stone = axe
    // - weapon upgrading: axe + gem = strong axe
    // - recipe items: axerecipe(item) + wood(item) + stone(item) = axe(item)
    //
    // players can Cook at all times, not just at npcs, because that's the most
    // realistic option

    // Cook a recipe with the combination of items and put result into inventory
    // => we pass the recipe name so that we don't have to search ALL the
    //    recipes. this would slow down the server if we have lots of recipes.
    // => we just let the client do the searching!
    [Command]
    public void CmdCook(string recipeName, int[] clientIndices, bool auto)
    {
        // validate: between 1 and 6, all valid, no duplicates?
        // -> can be IDLE or MOVING (in which case we reset the movement)
        if ((player.state == "IDLE" || player.state == "MOVING") &&
            clientIndices.Length == ScriptableCookingRecipe.recipeSize)
        {
            // find valid indices that are not '-1' and make sure there are no
            // duplicates
            List<int> validIndices = clientIndices.Where(index => 0 <= index && index < inventory.slots.Count && inventory.slots[index].amount > 0).ToList();
            if (validIndices.Count > 0 && !validIndices.HasDuplicates())
            {
                // find recipe and cast to ScriptableCookingRecipe
                if (ScriptableCookingRecipe.All.TryGetValue(recipeName, out ScriptableRecipe baseRecipe) &&
                    baseRecipe is ScriptableCookingRecipe recipe && // Safe cast
                    recipe.result != null)
                {
                    // enough space?
                    Item result = new Item(recipe.result);
                    if (inventory.CanAdd(result, 1))
                    {
                        // cache recipe so we don't have to search for it again
                        // in Cook()
                        currentRecipe = recipe;

                        // store the Cooking indices on the server. no need for
                        // a SyncList and unnecessary broadcasting.
                        // we already have a 'CookingIndices' variable anyway.
                        indices = clientIndices.ToList();

                        // Play the cooking sound effect
                        RpcPlayCookingSound();

                        // start Cooking
                        requestPending = true;
                        endTime = NetworkTime.time + recipe.cookingTime; // Use the correct property

                        // Handle auto-cooking
                        if (auto)
                            StartCoroutine(AutoCook(recipe));
                    }
                }
            }
        }
    }

    // Play the sound effect on all clients
[ClientRpc]
void RpcPlayCookingSound()
{
    if (player != null && player.TryGetComponent(out AudioSource audioSource) && cookingSound != null)
    {
        audioSource.PlayOneShot(cookingSound);
    }
}

    [Server]
    public IEnumerator AutoCook(ScriptableCookingRecipe recipe)
    {
        while (true) // Loop indefinitely until stopped
        {
            // Wait for the current cooking process to finish
            yield return new WaitForSeconds((float)recipe.cookingTime);

            // Check conditions for continuing auto-cooking
            if (!inventory.CanAdd(new Item(recipe.result), 1))
            {
                Debug.Log("[AutoCook] Stopping: Not enough inventory space for the result.");
                break;
            }

            if (!ValidateIngredients(recipe))
            {
                Debug.Log("[AutoCook] Stopping: Not enough ingredients for the recipe.");
                break;
            }

            // Ensure player is still in the correct state for auto-cooking
            if (player.state != "COOKING")
            {
                Debug.Log("[AutoCook] Stopping: Player is no longer in the COOKING state.");
                break;
            }

            // Trigger the cooking logic again
            Debug.Log("[AutoCook] Starting next cooking cycle.");
            Cook();
        }
    }

    // Helper method to validate ingredients for auto-cooking
    private bool ValidateIngredients(ScriptableCookingRecipe recipe)
    {
        foreach (var ingredient in recipe.ingredients)
        {
            if (!inventory.HasEnough(new Item(ingredient.item), ingredient.amount))
            {
                Debug.Log($"[ValidateIngredients] Missing ingredient: {ingredient.item.name}, required: {ingredient.amount}");
                return false;
            }
        }
        return true;
    }


    [Server]
    private void UpgradeSkillServer(Player player, string skillName)
    {
        if (player != null && player.skills is PlayerSkills playerSkills)
        {
            playerSkills.LevelUpSkill(skillName);
        }
    }



    // finish the Cooking
    [Server]
    public void Cook()
    {
        // Should only be called while COOKING and if the recipe is still valid.
        if (player.state == "COOKING" &&
            currentRecipe != null &&
            currentRecipe.result != null)
        {
            // Enough space?
            Item result = new Item(currentRecipe.result);
            if (inventory.CanAdd(result, 1))
            {
                // Remove the ingredients from inventory.
                foreach (ScriptableItemAndAmount ingredient in currentRecipe.ingredients)
                {
                    if (ingredient.amount > 0 && ingredient.item != null)
                        inventory.Remove(new Item(ingredient.item), ingredient.amount);
                }

                // Roll the dice to decide if the cooking succeeds.
                if (new System.Random().NextDouble() < currentRecipe.probability)
                {
                    // Apply enhancement reward, if available.
                    ScriptableItem finalResult = currentRecipe.ApplyEnhancementReward();
                    result = new Item(finalResult);

                    // Add result item to inventory.
                    inventory.Add(result, 1);

                    // Send success message to player.
                    string itemName = finalResult.name;
                    player.chat.TargetMsgInfo($"Your {itemName} is ready!");

                    // Trigger success feedback.
                    TargetCookingSuccess();

                    // Random chance to upgrade the Cooking skill (1 in 10 chance).
                    if (Random.Range(0, 10) == 0)
                    {
                        UpgradeSkillServer(player, "Cooking");
                    }
                }
                else
                {
                    // Send failure message to player.
                    string itemName = currentRecipe.result.name;
                    player.chat.TargetMsgInfo($"You burnt the {itemName}!");

                    // Trigger failure feedback.
                    TargetCookingFailed();
                }

                // Clear indices afterwards.
                if (!isLocalPlayer)
                {
                    for (int i = 0; i < ScriptableCookingRecipe.recipeSize; ++i)
                        indices[i] = -1;
                }

                // Clear recipe.
                currentRecipe = null;
            }
        }
    }


    // two rpcs for results to save 1 byte for the actual result
    [TargetRpc] // only send to one client
    public void TargetCookingSuccess()
    {
        state = CookingState.Success;
    }

    [TargetRpc] // only send to one client
    public void TargetCookingFailed()
    {
        state = CookingState.Failed;
    }

    // drag & drop /////////////////////////////////////////////////////////////
    void OnDragAndDrop_InventorySlot_CookingIngredientSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        // only if not Cooking right now
        if (state != CookingState.InProgress)
        {
            if (!indices.Contains(slotIndices[0]))
            {
                indices[slotIndices[1]] = slotIndices[0];
                state = CookingState.None; // reset state
            }
        }
    }

    void OnDragAndDrop_CookingIngredientSlot_CookingIngredientSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        if (state != CookingState.InProgress)
        {
            // just swap them client-sided
            int temp = indices[slotIndices[0]];
            indices[slotIndices[0]] = indices[slotIndices[1]];
            indices[slotIndices[1]] = temp;
            state = CookingState.None; // reset state
        }
    }



    void OnDragAndClear_CookingIngredientSlot(int slotIndex)
    {
        // only if not Cooking right now
        if (state != CookingState.InProgress)
        {
            indices[slotIndex] = -1;
            state = CookingState.None; // reset state
        }
    }
}
