using Mirror;
using UnityEngine;

public partial class Monster
{    
    void UpdateClient_ItemDrop()
    {
        if (EventDied())
        {
            if (collider.enabled)
            {
                collider.enabled = false;
            }
        }

        if (EventRespawnTimeElapsed())
        {
            if (!collider.enabled)
            {
                collider.enabled = true;
            }
        }
    }
#if ITEM_DROP_C
    void OnDeath_ItemDrop()
    {
        if (AddonItemDrop.ItemPrefab == null)
            return;

        if (gold > 0)
        {
            if (ScriptableItem.All.TryGetValue(AddonItemDrop.goldHashCode, out var data))
            {
                inventory.Add(new ItemSlot(new Item(data)));
            }
        }

        Vector2 center = transform.position;

        for (int i = 0; i < inventory.Count; i++)
        {
            ScriptableItem itemData = inventory[i].item.data;

            if (itemData.image != null)
            {
                if (!(itemData is PetItem) && !(itemData is MountItem))
                {
                    if (ItemDropSettings.Settings.individualLoot)
                    {
                        if (AddonItemDrop.RandomPoint2D(center, out var point))
                        {
                            // spawn an item on client only and without generating a unique ID
                            RpcDropItem(itemData.name, itemData.gold, gold, center, point);
                        }
                    }
                    else
                    {
                        if (AddonItemDrop.RandomPoint2D(center, out var point))
                        {
                            // spawn an item without generating a unique ID
                            ItemDrop loot = AddonItemDrop.GenerateLoot(itemData.name, itemData.gold, gold, center, point);
                            NetworkServer.Spawn(loot.gameObject);
                        }
                    }
                }
            }
        }        
    }
#endif
    [ClientRpc]
    public void RpcDropItem(string itemName, bool currency, long gold, Vector2 center, Vector2 point)
    {
        AddonItemDrop.GenerateLoot(itemName, currency, gold, center, point);
    }
}
#if ITEM_DROP_R
public partial class MonsterInventory
{
    void OnDeath_ItemDrop()
    {
        if (AddonItemDrop.ItemPrefab == null)
            return;

        if (monster.gold > 0)
        {
            if (ScriptableItem.All.TryGetValue(AddonItemDrop.goldHashCode, out var data))
            {
                slots.Add(new ItemSlot(new Item(data)));
            }
        }

        Vector2 center = transform.position;

        for (int i = 0; i < slots.Count; i++)
        {
            ScriptableItem itemData = slots[i].item.data;

            if (itemData.image != null)
            {
                if (!(itemData is PetItem) && !(itemData is MountItem))
                {
                    if (ItemDropSettings.Settings.individualLoot)
                    {
                        if (AddonItemDrop.RandomPoint2D(center, out var point))
                        {
                            // spawn an item on client only and without generating a unique ID
                            monster.RpcDropItem(itemData.name, itemData.gold, monster.gold, center, point);
                        }
                    }
                    else
                    {
                        if (AddonItemDrop.RandomPoint2D(center, out var point))
                        {
                            // spawn an item without generating a unique ID
                            ItemDrop loot = AddonItemDrop.GenerateLoot(itemData.name, itemData.gold, monster.gold, center, point);
                            NetworkServer.Spawn(loot.gameObject);
                        }
                    }
                }
            }
        }
    }
}
#endif