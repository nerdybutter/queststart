// The Item struct only contains the dynamic item properties, so that the static
// properties can be read from the scriptable object.
//
// Items have to be structs in order to work with SyncLists.
//
// Use .Equals to compare two items. Comparing the name is NOT enough for cases
// where dynamic stats differ. E.g. two pets with different levels shouldn't be
// merged.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Mirror;

[Serializable]
public partial struct Item
{
    // hashcode used to reference the real ScriptableItem (can't link to data
    // directly because synclist only supports simple types). and syncing a
    // string's hashcode instead of the string takes WAY less bandwidth.
    public int hash;
    public int durability; // Instance-specific durability

    // dynamic stats (cooldowns etc. later)
    public NetworkIdentity summoned; // summonable that's currently summoned
    public int summonedHealth; // stored in item while summonable unsummoned
    public int summonedLevel; // stored in item while summonable unsummoned
    public long summonedExperience; // stored in item while summonable unsummoned

    // constructors
    public Item(ScriptableItem data)
    {
        hash = data.name.GetStableHashCode();
        durability = (data is EquipmentItem equipmentItem) ? equipmentItem.maxDurability : 0;
        summoned = null;
        summonedHealth = data is SummonableItem ? ((SummonableItem)data).summonPrefab.health.max : 0;
        summonedLevel = data is SummonableItem ? 1 : 0;
        summonedExperience = 0;
    }

    // wrappers for easier access
    public ScriptableItem data
    {
        get
        {
            // show a useful error message if the key can't be found
            // note: ScriptableItem.OnValidate 'is in resource folder' check
            //       causes Unity SendMessage warnings and false positives.
            //       this solution is a lot better.
            if (!ScriptableItem.All.ContainsKey(hash))
                throw new KeyNotFoundException("There is no ScriptableItem with hash=" + hash + ". Make sure that all ScriptableItems are in the Resources folder so they are loaded properly.");
            return ScriptableItem.All[hash];
        }
    }
    
    // Check if the item has durability
    public bool HasDurability()
    {
        return data is EquipmentItem;
    }

    // Serialize item data for storage or networking
    public string Serialize()
    {
        return $"{hash},{durability}";
    }
    // Deserialize item data
    public static Item Deserialize(string serializedData)
    {
        string[] parts = serializedData.Split(',');
        int hash = int.Parse(parts[0]);
        int durability = int.Parse(parts[1]);
        ScriptableItem data = ScriptableItem.All[hash];

        Item item = new Item(data);
        item.durability = durability;
        return item;
    }

    public string name => data.name;
    public int maxStack => data.maxStack;
    public long buyPrice => data.buyPrice;
    public long sellPrice => data.sellPrice;
    public long itemMallPrice => data.itemMallPrice;
    public bool sellable => data.sellable;
    public bool tradable => data.tradable;
    public bool destroyable => data.destroyable;
    public Sprite image => data.image;

    // tooltip
    public string ToolTip()
    {
        // we use a StringBuilder so that addons can modify tooltips later too
        // ('string' itself can't be passed as a mutable object)
        StringBuilder tip = new StringBuilder(data.ToolTip());
        tip.Replace("{SUMMONEDHEALTH}", summonedHealth.ToString());
        tip.Replace("{SUMMONEDLEVEL}", summonedLevel.ToString());
        tip.Replace("{SUMMONEDEXPERIENCE}", summonedExperience.ToString());

        // addon system hooks
        Utils.InvokeMany(typeof(Item), this, "ToolTip_", tip);

        return tip.ToString();
    }
}
