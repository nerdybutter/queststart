// Define a new ScriptableItem for spell books
using UnityEngine;
using Mirror;
using System.Text;
using System.Linq;

[CreateAssetMenu(menuName = "uMMORPG Item/Spell Book", order = 1000)]
public class SpellBookItem : UsableItem
{
    [Header("Spell Book Properties")]
    public ScriptableSkill skillToLearn; // Skill this book teaches

    // Override the tooltip to show what skill it teaches
    public override string ToolTip()
    {
        StringBuilder tip = new StringBuilder(base.ToolTip());
        tip.Append($"\n<color=yellow>Teaches: {skillToLearn.name}</color>");
        return tip.ToString();
    }

    public override void Use(Player player, int inventoryIndex)
    {
        Debug.Log($"SpellBookItem.Use called for item in inventory index: {inventoryIndex}");

        if (inventoryIndex < 0 || inventoryIndex >= player.inventory.slots.Count)
        {
            Debug.LogError("Invalid inventory index!");
            return;
        }

        ItemSlot slot = player.inventory.slots[inventoryIndex];
        Debug.Log($"Initial slot info - Item: {(!string.IsNullOrEmpty(slot.item.name) ? slot.item.name : "Empty")}, Amount: {slot.amount}");

        // Check if the player already knows the skill
        if (player.skills.skillTemplates.Any(x => x == skillToLearn) &&
            !((PlayerSkills)player.skills).HasLearned(skillToLearn.name))
        {
            Debug.Log($"Player does not know the skill: {skillToLearn.name}. Proceeding to learn...");

            // Always call base function
            base.Use(player, inventoryIndex);

            // Learn the skill
            int skillIndex = player.skills.skills.FindIndex(s => s.name == skillToLearn.name);
            Skill skill = player.skills.skills[skillIndex];
            ++skill.level;
            player.skills.skills[skillIndex] = skill;

            // Notify the player
            PlayerChat playerChat = player.GetComponent<PlayerChat>();
            if (playerChat != null)
            {
                playerChat.TargetMsgLocal(player.name, $"You have learned {skillToLearn.name}!");
            }

            // Request server to remove item from inventory
            player.inventory.RemoveItem(slot.item, 1);

            // Update inventory on the client for immediate feedback
            RpcUpdateInventory(player, inventoryIndex, player.inventory.slots[inventoryIndex]);
        }
        else
        {
            Debug.Log("Player already knows this skill or skill is not available.");
            PlayerChat playerChat = player.GetComponent<PlayerChat>();
            if (playerChat != null)
            {
                playerChat.TargetMsgLocal(player.name, "You already know this skill.");
            }
        }
    }


    [ClientRpc]
    void RpcUpdateInventory(Player player, int index, ItemSlot slot)
    {
        if (index >= 0 && index < player.inventory.slots.Count)
        {
            player.inventory.slots[index] = slot;
            Debug.Log($"Inventory slot updated on client: Index {index}, Item: {(slot.amount > 0 ? slot.item.name : "Empty")}, Amount: {slot.amount}");
        }
    }
}
