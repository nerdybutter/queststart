using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;
using System.Text;

public static class AddonItemDrop
{
    public static int goldHashCode = "Gold".GetStableHashCode();

    static ItemDrop cache;
    public static ItemDrop ItemPrefab => cache ?? (cache = ItemDropSettings.Settings.itemPrefab.GetComponent<ItemDrop>());

    public static string Color(string itemName, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{itemName}</color>";
    public static string ColorStack(string itemName, int stack, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{itemName} x{stack}</color>";

    public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue = default)
    {
        return dictionary.TryGetValue(key, out TValue value) ? value : defaultValue;
    }

    #region Database
    public static bool SaveItems()
    {
        List<ItemDrop> items = (from entry in LootManager.instance.Items where entry.uniqueId != "" && entry.isMarked select entry).ToList();

        if (items.Count > 0)
        {
            Database.singleton.ItemSaveMany(items);
            //Debug.Log($"{items.Count} items have been saved.");
            return true;
        }
        return false;
    }

    public static void LoadItems()
    {
        if (ItemPrefab != null)
        {
            Database.singleton.ItemLoad(ItemPrefab);
        }
        else Debug.LogWarning("Unable to find the Item.prefab!");
    }
    
    public static void DeleteItems()
    {
        if (ItemDropSettings.Settings.individualLoot)
        {
            foreach (ItemDrop item in LootManager.instance.Items)
            {
                if (item.uniqueId != "")
                {
                    NetworkServer.Destroy(item.gameObject);
                }
            }

            foreach (Player player in Player.onlinePlayers.Values)
            {
                player.TargetDeleteItems();
            }
        }
        else
        {
            foreach (ItemDrop item in LootManager.instance.Items)
            {
                NetworkServer.Destroy(item.gameObject);
            }
        }

        Database.singleton.ItemsDelete();
    }

    public static void DeleteItem(string uniqueId)
    {
        if (uniqueId != "")
        {
            Database.singleton.ItemDelete(uniqueId);
        }
    }
    #endregion

    #region Monster
    /// <summary>
    /// Returns an item created without a unique ID.
    /// </summary>
    public static ItemDrop GenerateLoot(string itemName, bool currency, long gold, Vector2 center, Vector2 point)
    {
        ItemDrop clone = UnityEngine.Object.Instantiate(ItemPrefab, center, Quaternion.identity);
        clone.name = itemName;
        clone.stack = currency ? Convert.ToInt32(gold) : 1;
        clone.endingPoint = point;
        return clone;
    }

    /// <summary>
    /// Returns a random point inside a circle on the reachable NavMesh around the center position.
    /// </summary>
    public static bool RandomPoint2D(Vector2 center, out Vector2 result)
    {
        bool blocked = true;

        while (blocked)
        {
            Vector2 randomPoint = (UnityEngine.Random.insideUnitCircle * ItemDropSettings.Settings.itemDropRadius) + center;
            blocked = NavMesh2D.Raycast(center, randomPoint, out var hit, 1);

            if (!blocked)
            {
                //Debug.Log("ok");
                result = randomPoint;
                return true;
            }
            //Debug.Log("find another place");
        }
        //Debug.Log("nothing");
        result = Vector2.zero;
        return false;
    }
    #endregion

    #region Player
    /// <summary>
    /// Returns an item created with a unique ID (can be saved).
    /// </summary>
    public static ItemDrop GenerateItem(string itemName, int amount, Player player, Vector2 point, int durability)
    {
        ItemDrop clone = UnityEngine.Object.Instantiate(ItemPrefab, player.transform.position, Quaternion.identity);
        clone.name = itemName;
        clone.uniqueId = Guid.NewGuid().ToString();
        clone.stack = amount;
        clone.durability = durability; // Set durability here
        clone.endingPoint = CheckTargetPoint(player, point);
        return clone;
    }



    /// <summary>
    /// Looks for a place where the player can drop an item.
    /// </summary>
    public static Vector2 CheckTargetPoint(Player player, Vector2 targetPoint)
    {
        Vector2 center = player.transform.position;

        bool blocked = NavMesh2D.Raycast(center, targetPoint, out var hit, 1);
        // check if the path from the source to target is unobstructed
        if (!blocked)
        {
            // check if this position is not occupied
            RaycastHit2D raycastHit = Physics2D.Raycast(targetPoint, Vector2.zero);
            if (!raycastHit.collider)
            {
                // check if this position is on the NavMesh
                if (NavMesh2D.SamplePosition(targetPoint, out var navMeshHit, 1, 1))
                {
                    return navMeshHit.position;
                }
            }
        }
        // find another position randomly
        RandomPoint2D(center, out Vector2 result);
        return result;
    }
    #endregion

    /// <summary>
    /// Quadratic Bezier Curve (point1 as a control point).
    /// </summary>
    public static Vector3 GetPoint(Vector3 point0, Vector3 point1, Vector3 point2, float t)
    {
        return Vector3.Lerp(Vector3.Lerp(point0, point1, t), Vector3.Lerp(point1, point2, t), t);
    }

    public static List<T> GetRandomElements<T>(IEnumerable<T> list, int elementsCount)
    {
        return list.OrderBy(x => Guid.NewGuid()).Take(elementsCount).ToList();
    }

    public static int GetLayerIndex(int layerMaskValue)
    {
        return (int)Mathf.Log(layerMaskValue, 2);
    }

    public static LayerMask DisableLayers(LayerMask layerMask)
    {
        layerMask &= ~(1 << GetLayerIndex(ItemDropSettings.Settings.GetItemMask()));
        layerMask &= ~(1 << GetLayerIndex(ItemDropSettings.Settings.GetLabelMask()));
        return layerMask;
    }
}

public partial class ScriptableItem
{
    [Header("Item Drop")]
    public Rarity rarity;
    public enum Rarity
    {
        Basic,
        Rare,
        Unique
    }

    public bool gold;

    public string GetTitle(int stack = 1)
    {
        StringBuilder stringBuilder = new StringBuilder();
        Color color = ItemDropSettings.Settings.gold;

        if (!gold)
        {
            switch (rarity)
            {
                case Rarity.Unique: color = ItemDropSettings.Settings.uniqueItem; break;
                case Rarity.Rare: color = ItemDropSettings.Settings.rareItem; break;
                case Rarity.Basic: color = ItemDropSettings.Settings.normalItem; break;
            }
        }

        if (stack == 1)
        {
            stringBuilder.Append(AddonItemDrop.Color(name, color));
        }
        else
        {
            stringBuilder.Append(AddonItemDrop.ColorStack(name, stack, color));
        }
        return stringBuilder.ToString();
    }
}
