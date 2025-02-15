using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;

[Serializable]
public struct SkillbarEntry
{
    public string reference;
    public KeyCode hotKey;
}

[RequireComponent(typeof(PlayerEquipment))]
//[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerSkills))]
public class PlayerSkillbar : NetworkBehaviour
{

    [Header("Components")]
    public PlayerEquipment equipment;
    public PlayerInventory inventory;
    public PlayerSkills skills;

    [Header("Skillbar")]
    public SkillbarEntry[] slots =
    {

    };

    public override void OnStartLocalPlayer()
    {
        // load skillbar after player data was loaded
        Load();
    }

    public override void OnStopClient()
    {
        if (isLocalPlayer)
            Save();
    }

    // skillbar ////////////////////////////////////////////////////////////////
    //[Client] <- disabled while UNET OnDestroy isLocalPlayer bug exists
    void Save()
    {
        // save skillbar to player prefs (based on player name, so that
        // each character can have a different skillbar)
        for (int i = 0; i < slots.Length; ++i)
            PlayerPrefs.SetString(name + "_skillbar_" + i, slots[i].reference);

        // force saving playerprefs, otherwise they aren't saved for some reason
        PlayerPrefs.Save();
    }

    void Update()
    {
        Player player = Player.localPlayer;
        if (player)
        {
            if (Input.GetKeyDown(KeyCode.L)) // Press "L" to log skillbar state
            {
                UpdateSkillbarDebug();
            }
            // Check for Tab key press
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                // Ensure there is at least one skill in the skill list
                if (player.skills.skills.Count > 0)
                {
                    // Get the first skill
                    Skill firstSkill = player.skills.skills[0];

                    // Use the skill if it's valid
                    if (firstSkill.level > 0)
                    {
                        ((PlayerSkills)player.skills).CmdUse(0, player.lookDirection); // Use the first skill
                    }
                }
            }
        }
    }





    [Client]
    void Load()
    {
        Debug.Log("loading skillbar for " + name);
        List<Skill> learned = skills.skills.Where(skill => skill.level > 0).ToList();
        for (int i = 0; i < slots.Length; ++i)
        {
            // try loading an existing entry
            if (PlayerPrefs.HasKey(name + "_skillbar_" + i))
            {
                string entry = PlayerPrefs.GetString(name + "_skillbar_" + i, "");

                // is this a valid item/equipment/learned skill?
                // (might be an old character's playerprefs)
                // => only allow learned skills (in case it's an old character's
                //    skill that we also have, but haven't learned yet)
                if (skills.HasLearned(entry) ||
                    inventory.GetItemIndexByName(entry) != -1 ||
                    equipment.GetItemIndexByName(entry) != -1)
                {
                    slots[i].reference = entry;
                }
            }
            // otherwise fill with default skills for a better first impression
            else if (i < learned.Count)
            {
                slots[i].reference = learned[i].name;
            }
        }
    }

    // drag & drop /////////////////////////////////////////////////////////////
    void OnDragAndDrop_InventorySlot_SkillbarSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        slots[slotIndices[1]].reference = inventory.slots[slotIndices[0]].item.name; // just save it clientsided
    }

    void OnDragAndDrop_EquipmentSlot_SkillbarSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        slots[slotIndices[1]].reference = equipment.slots[slotIndices[0]].item.name; // just save it clientsided
    }

    void OnDragAndDrop_SkillsSlot_SkillbarSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        slots[slotIndices[1]].reference = skills.skills[slotIndices[0]].name; // just save it clientsided
    }

    void OnDragAndDrop_SpellsSlot_SkillbarSlot(int[] slotIndices)
    {
        Debug.Log($"🔄 Dragging from SpellSlot {slotIndices[0]} to SkillbarSlot {slotIndices[1]}");

        // ✅ Convert index to match skills
        int skillIndex = 34 + slotIndices[0];

        if (skillIndex >= 34 && skillIndex < skills.skills.Count)
        {
            slots[slotIndices[1]].reference = skills.skills[skillIndex].name;
            Debug.Log($"✅ Successfully added spell '{skills.skills[skillIndex].name}' to Skillbar slot {slotIndices[1]}");
        }
        else
        {
            Debug.LogError($"❌ ERROR: Invalid SpellSlot index {skillIndex}, Total Skills: {skills.skills.Count}");
        }
    }


    void UpdateSkillbarDebug()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            Debug.Log($"Slot {i}: {slots[i].reference}");
        }
    }


    void OnDragAndDrop_SkillbarSlot_SkillbarSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        // just swap them clientsided
        string temp = slots[slotIndices[0]].reference;
        slots[slotIndices[0]].reference = slots[slotIndices[1]].reference;
        slots[slotIndices[1]].reference = temp;
    }

    void OnDragAndClear_SkillbarSlot(int slotIndex)
    {
        slots[slotIndex].reference = "";
    }

    public void TryUseSpell(string spellName)
    {
        Player player = Player.localPlayer;
        if (!player) return;

        PlayerSkills playerSkills = player.GetComponent<PlayerSkills>();
        if (playerSkills == null)
        {
            Debug.LogError("❌ PlayerSkills component is missing.");
            return;
        }

        int spellIndex = playerSkills.spells.FindIndex(spell => spell.name == spellName);
        if (spellIndex != -1)
        {
            playerSkills.TryUse(spellIndex + playerSkills.skills.Count);
        }
        else
        {
            Debug.LogError($"❌ Spell '{spellName}' not found in player's spell list.");
        }
    }

}
