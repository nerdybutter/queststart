﻿// Inventories need a slot type to hold Item + Amount. This is better than
// storing .amount in 'Item' because then we can use Item.Equals properly
// any workarounds to ignore the .amount.
//
// Note: always check .amount > 0 before accessing .item.
//       set .amount=0 to clear it.
using System;
using System.Text;
using UnityEngine;

[Serializable]
public partial struct ItemSlot
{
    public Item item;
    public int amount;

    // constructors
    public ItemSlot(Item item, int amount=1)
    {
        this.item = item;
        this.amount = amount;
    }

    // helper functions to increase/decrease amount more easily
    // -> returns the amount that we were able to increase/decrease by
    public int DecreaseAmount(int reduceBy)
    {
        // as many as possible
        int limit = Mathf.Clamp(reduceBy, 0, amount);
        amount -= limit;
        return limit;
    }

    public int IncreaseAmount(int increaseBy)
    {
        // as many as possible
        int limit = Mathf.Clamp(increaseBy, 0, item.maxStack - amount);
        amount += limit;
        return limit;
    }

    // tooltip
    public string ToolTip()
    {
        if (amount == 0) return "";

        // Generate tooltip from static data
        StringBuilder tip = new StringBuilder(item.ToolTip());

        // Inject dynamic durability if applicable
        if (item.data is EquipmentItem equipmentItem && !equipmentItem.isIndestructible)
        {
            tip.AppendLine();
            tip.AppendLine($"Durability: {item.durability}/{equipmentItem.maxDurability}");
        }

        // Add amount information
        tip.Replace("{AMOUNT}", amount.ToString());

        return tip.ToString();
    }

}
