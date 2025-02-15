using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

[CreateAssetMenu(menuName = "uMMORPG Item/Equipment", order = 999)]
public class EquipmentItem : UsableItem
{
    [Header("Equipment")]
    public string category;
    public int healthBonus;
    public int manaBonus;
    public int damageBonus;
    public int randomDamageBonus;
    public int defenseBonus;
    [Range(0, 1)] public float blockChanceBonus;
    [Range(0, 1)] public float criticalChanceBonus;
    [Range(0, 1)] public float movementSpeedBonus; // Movement speed bonus

    [Header("Durability")]
    public int maxDurability = 100; // Maximum durability
    public int durability = 100;   // Current durability
    public bool isIndestructible = false; // Prevents durability loss

    [Header("Enhancement Rewards")]
    public ScriptableItem damagedVersion;

    // Animated equipment sprites. List instead of array so that we can pass it
    // around without copying the whole thing each time.
    public List<Sprite> sprites = new List<Sprite>();

    // Usage
    // -> Can we equip this into any slot?
    public override bool CanUse(Player player, int inventoryIndex)
    {
        return FindEquipableSlotFor(player, inventoryIndex) != -1;
    }

    // Can we equip this item into this specific equipment slot?
    public bool CanEquip(Player player, int inventoryIndex, int equipmentIndex)
    {
        string requiredCategory = ((PlayerEquipment)player.equipment).slotInfo[equipmentIndex].requiredCategory;
        return base.CanUse(player, inventoryIndex) &&
               requiredCategory != "" &&
               category.StartsWith(requiredCategory);
    }

    int FindEquipableSlotFor(Player player, int inventoryIndex)
    {
        for (int i = 0; i < player.equipment.slots.Count; ++i)
            if (CanEquip(player, inventoryIndex, i))
                return i;
        return -1;
    }

    public override void Use(Player player, int inventoryIndex)
    {
        // Always call base function too
        base.Use(player, inventoryIndex);

        // Find a slot that accepts this category, then equip it
        int slot = FindEquipableSlotFor(player, inventoryIndex);
        if (slot != -1)
        {
            // Reuse Player.SwapInventoryEquip function for validation etc.
            ((PlayerEquipment)player.equipment).SwapInventoryEquip(inventoryIndex, slot);
        }
    }

    // Tooltip
    public override string ToolTip()
    {
        StringBuilder tip = new StringBuilder(base.ToolTip());

        // Add static properties
        tip.Replace("{CATEGORY}", category);
        tip.Replace("{DAMAGEBONUS}", damageBonus.ToString());
        tip.Replace("{DEFENSEBONUS}", defenseBonus.ToString());
        tip.Replace("{HEALTHBONUS}", healthBonus.ToString());
        tip.Replace("{MANABONUS}", manaBonus.ToString());
        tip.Replace("{BLOCKCHANCEBONUS}", Mathf.RoundToInt(blockChanceBonus * 100).ToString());
        tip.Replace("{CRITICALCHANCEBONUS}", Mathf.RoundToInt(criticalChanceBonus * 100).ToString());

        // Display movement speed only if it provides a bonus
        if (movementSpeedBonus > 0)
            tip.Append($"\nMovement Speed Bonus: {Mathf.RoundToInt(movementSpeedBonus * 100)}%");

        // We don't append durability here; it will be handled by the dynamic call in ItemSlot.cs
        return tip.ToString();
    }





}
