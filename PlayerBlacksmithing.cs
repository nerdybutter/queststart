using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using Mirror;

public enum BlacksmithingState { None, InProgress, Success, Failed }

[RequireComponent(typeof(PlayerInventory))]
[DisallowMultipleComponent]
public class PlayerBlacksmithing : NetworkBehaviour
{
    [Header("Components")]
    public Player player;
    public PlayerInventory inventory;
    public AudioClip BlacksmithingSound;

    [Header("Blacksmithing")]
    public List<int> indices = Enumerable.Repeat(-1, ScriptableBlacksmithingRecipe.recipeSize).ToList();
    [HideInInspector] public BlacksmithingState state = BlacksmithingState.None; // // client sided
    public ScriptableBlacksmithingRecipe currentRecipe; // currently Blacksmithed recipe. cached to avoid searching ALL recipes in Blacksmith()
    [SyncVar, HideInInspector] public double endTime; // double for long term precision
    [HideInInspector] public bool requestPending; // for state machine event
    public double BlacksmithingEndTime => endTime;

    // Blacksmithing ////////////////////////////////////////////////////////////////
    // the Blacksmithing system is designed to work with all kinds of commonly known
    // Blacksmithing options:
    // - item combinations: wood + stone = axe
    // - weapon upgrading: axe + gem = strong axe
    // - recipe items: axerecipe(item) + wood(item) + stone(item) = axe(item)
    //
    // players can Blacksmith at all times, not just at npcs, because that's the most
    // realistic option

    // Blacksmith a recipe with the combination of items and put result into inventory
    // => we pass the recipe name so that we don't have to search ALL the
    //    recipes. this would slow down the server if we have lots of recipes.
    // => we just let the client do the searching!
    [Command]
    public void CmdBlacksmith(string recipeName, int[] clientIndices, bool auto)
    {
        // validate: between 1 and 6, all valid, no duplicates?
        // -> can be IDLE or MOVING (in which case we reset the movement)
        if ((player.state == "IDLE" || player.state == "MOVING") &&
            clientIndices.Length == ScriptableBlacksmithingRecipe.recipeSize)
        {
            // find valid indices that are not '-1' and make sure there are no
            // duplicates
            List<int> validIndices = clientIndices.Where(index => 0 <= index && index < inventory.slots.Count && inventory.slots[index].amount > 0).ToList();
            if (validIndices.Count > 0 && !validIndices.HasDuplicates())
            {
                // find recipe and cast to ScriptableBlacksmithingRecipe
                if (ScriptableBlacksmithingRecipe.All.TryGetValue(recipeName, out ScriptableRecipe baseRecipe) &&
                    baseRecipe is ScriptableBlacksmithingRecipe recipe && // Safe cast
                    recipe.result != null)
                {
                    // enough space?
                    Item result = new Item(recipe.result);
                    if (inventory.CanAdd(result, 1))
                    {
                        // cache recipe so we don't have to search for it again
                        // in Blacksmith()
                        currentRecipe = recipe;

                        // store the Blacksmithing indices on the server. no need for
                        // a SyncList and unnecessary broadcasting.
                        // we already have a 'BlacksmithingIndices' variable anyway.
                        indices = clientIndices.ToList();

                        // Play the Blacksmithing sound effect
                        RpcPlayBlacksmithingSound();

                        // start Blacksmithing
                        requestPending = true;
                        endTime = NetworkTime.time + recipe.BlacksmithingTime; // Use the correct property

                        // Handle auto-Blacksmithing
                        if (auto)
                            StartCoroutine(AutoBlacksmith(recipe));
                    }
                }
            }
        }
    }

    // Play the sound effect on all clients
[ClientRpc]
void RpcPlayBlacksmithingSound()
{
    if (player != null && player.TryGetComponent(out AudioSource audioSource) && BlacksmithingSound != null)
    {
        audioSource.PlayOneShot(BlacksmithingSound);
    }
}

    [Server]
    public IEnumerator AutoBlacksmith(ScriptableBlacksmithingRecipe recipe)
    {
        while (true) // Loop indefinitely until stopped
        {
            // Wait for the current Blacksmithing process to finish
            yield return new WaitForSeconds((float)recipe.BlacksmithingTime);

            // Check conditions for continuing auto-Blacksmithing
            if (!inventory.CanAdd(new Item(recipe.result), 1))
            {
                Debug.Log("[AutoBlacksmith] Stopping: Not enough inventory space for the result.");
                break;
            }

            if (!ValidateIngredients(recipe))
            {
                Debug.Log("[AutoBlacksmith] Stopping: Not enough ingredients for the recipe.");
                break;
            }

            // Ensure player is still in the correct state for auto-Blacksmithing
            if (player.state != "BLACKSMITHING")
            {
                Debug.Log("[AutoBlacksmith] Stopping: Player is no longer in the BlacksmithING state.");
                break;
            }

            // Trigger the Blacksmithing logic again
            Debug.Log("[AutoBlacksmith] Starting next Blacksmithing cycle.");
            Blacksmith();
        }
    }

    // Helper method to validate ingredients for auto-Blacksmithing
    private bool ValidateIngredients(ScriptableBlacksmithingRecipe recipe)
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



    // finish the Blacksmithing
    [Server]
    public void Blacksmith()
    {
        // Should only be called while BLACKSMITHING and if recipe is still valid.
        if (player.state == "BLACKSMITHING" &&
            currentRecipe != null &&
            currentRecipe.result != null)
        {
            // Enough space?
            Item result = new Item(currentRecipe.result);

            if (inventory.CanAdd(result, 1))
            {
                // Remove the ingredients from inventory in any case.
                foreach (ScriptableItemAndAmount ingredient in currentRecipe.ingredients)
                {
                    if (ingredient.amount > 0 && ingredient.item != null)
                        inventory.Remove(new Item(ingredient.item), ingredient.amount);
                }

                // Roll the dice to decide if we add the result or not.
                if (new System.Random().NextDouble() < currentRecipe.probability)
                {
                    // Apply enhancement reward.
                    ScriptableItem finalResult = currentRecipe.ApplyEnhancementReward();
                    result = new Item(finalResult);

                    // Add result item to inventory.
                    inventory.Add(result, 1);

                    // Send success message to player.
                    string itemName = finalResult.name;
                    player.chat.TargetMsgInfo($"Your {itemName} is ready!");

                    // Trigger success feedback.
                    TargetBlacksmithingSuccess();

                    // Random chance to upgrade the Blacksmithing skill (1 in 16 chance).
                    if (Random.Range(0, 8) == 0)
                    {
                        UpgradeSkillServer(player, "Blacksmithing");
                    }
                }
                else
                {
                    // Trigger failure feedback.
                    TargetBlacksmithingFailed();
                }

                // Clear indices afterwards.
                if (!isLocalPlayer)
                {
                    for (int i = 0; i < ScriptableBlacksmithingRecipe.recipeSize; ++i)
                        indices[i] = -1;
                }

                // Clear recipe.
                currentRecipe = null;
            }
        }
    }


    // two rpcs for results to save 1 byte for the actual result
    [TargetRpc] // only send to one client
    public void TargetBlacksmithingSuccess()
    {
        state = BlacksmithingState.Success;
    }

    [TargetRpc] // only send to one client
    public void TargetBlacksmithingFailed()
    {
        state = BlacksmithingState.Failed;
    }

    // drag & drop /////////////////////////////////////////////////////////////
    void OnDragAndDrop_InventorySlot_BlacksmithingIngredientSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        // only if not Blacksmithing right now
        if (state != BlacksmithingState.InProgress)
        {
            if (!indices.Contains(slotIndices[0]))
            {
                indices[slotIndices[1]] = slotIndices[0];
                state = BlacksmithingState.None; // reset state
            }
        }
    }

    void OnDragAndDrop_BlacksmithingIngredientSlot_BlacksmithingIngredientSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        if (state != BlacksmithingState.InProgress)
        {
            // just swap them client-sided
            int temp = indices[slotIndices[0]];
            indices[slotIndices[0]] = indices[slotIndices[1]];
            indices[slotIndices[1]] = temp;
            state = BlacksmithingState.None; // reset state
        }
    }



    void OnDragAndClear_BlacksmithingIngredientSlot(int slotIndex)
    {
        // only if not Blacksmithing right now
        if (state != BlacksmithingState.InProgress)
        {
            indices[slotIndex] = -1;
            state = BlacksmithingState.None; // reset state
        }
    }
}
