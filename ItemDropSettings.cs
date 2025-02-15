using System;
using UnityEngine;

[HelpURL("https://docs.google.com/document/d/1waOpYRYIFORlBYVqhHm-0VvrNjI6kMV2J8WMbqbeY6c/edit")]
public class ItemDropSettings : ScriptableObject
{
    static ItemDropSettings cache;
    public static ItemDropSettings Settings => cache ?? (cache = Resources.Load<ItemDropSettings>("ItemDropSettings"));

#if UNITY_EDITOR
    [HideInInspector]
    public bool isInstalled;
#endif

    [Header("Item Color")]
    public Color uniqueItem = new Color32(218, 165, 32, 255);       // #DAA520 GoldenRod
    public Color rareItem = new Color32(30, 144, 255, 255);         // #1E90FF DodgerBlue
    public Color normalItem = Color.white;                          // #FFFFFF White
    public Color gold = new Color32(105, 105, 105, 255);            // #808080 Gray

    [Header("Label Color")]
    public Color label = new Color32(0, 0, 0, 185);                 // #000000 Black
    public Color highlightedLabel = new Color32(25, 25, 112, 255);  // #191970 MidnightBlue
   
    [Header("Tag & Layer Masks")]
    [TagSelector] public string itemTag = "Untagged";
    public LayerMask itemMask;
    public LayerMask labelMask;

    [Header("Key Bindings")]
    [Tooltip("Holding down the selected key highlights any items on the ground that you can pick up.")]
    public KeyCode itemHighlightKey = KeyCode.LeftAlt;

    [Tooltip("When pressed picks up the nearest item.")]
    public KeyCode itemPickupKey = KeyCode.F;

    [Tooltip("Holding down the selected key with clicking on an item in the inventory window will put a link to it in your chat window.")]
    public KeyCode itemLinkKey = KeyCode.LeftControl;

    [Header("Timer")]
    [Min(600f)] public float decayTime = 3600f;     // 1 hour
    [Min(600f)] public float saveInterval = 1800f;  // 30 minutes     

    [Header("Prefabs")]
    public GameObject lootManager;
    public GameObject itemPrefab;
    public GameObject itemLabel;

    [Header("Others")]
    [Min(2f)] public float itemDropSpeed = 2f;
    [Min(1f)] public float itemDropRadius = 1.5f;

    [Tooltip("The number of pixels per unit, as in your sprites.")]
    [Min(1f)] public float pixelsPerUnit = 34;
    public bool fitLabelsIntoScreen = true;
    public bool showMessages = true;

    [Header("Extras")]
    [Tooltip("All items dropped by monsters are assigned to the individual player.")]
    public bool individualLoot;

    [Tooltip("Characters drop a percentage of their gold when they die.")]
    public GoldLoss goldLoss;
    [Serializable]
    public struct GoldLoss
    {
        public bool isActive;
        [Range(1, 100)] public int percentageLoss;
    }

    [Tooltip("Characters drop a random item of their inventory when they die.")]
    public ItemLoss itemLoss;
    [Serializable]
    public struct ItemLoss
    {
        public bool isActive;
        [Range(1, 5)] public int amount;
    }

    public int GetItemMask() => itemMask.value;
    public int GetLabelMask() => labelMask.value;

#if UNITY_EDITOR 
    public void DefaultSettings()
    {
        uniqueItem = new Color32(218, 165, 32, 255);        // #DAA520 GoldenRod
        rareItem = new Color32(30, 144, 255, 255);          // #1E90FF DodgerBlue
        normalItem = Color.white;                           // #FFFFFF White
        gold = new Color32(105, 105, 105, 255);             // #808080 Gray

        label = new Color32(0, 0, 0, 185);                  // #000000 Black
        highlightedLabel = new Color32(25, 25, 112, 255);   // #191970 MidnightBlue

        SetTagsAndLayers();

        itemHighlightKey = KeyCode.LeftAlt;
        itemPickupKey = KeyCode.F;
        itemLinkKey = KeyCode.LeftControl;

        decayTime = 3600f;      // 1 hour
        saveInterval = 1800f;   // 30 minutes

        itemDropSpeed = 2f;
        itemDropRadius = 1.5f;
        pixelsPerUnit = 34f;

        fitLabelsIntoScreen = true;
        showMessages = true;

        individualLoot = false;

        goldLoss.isActive = false;
        goldLoss.percentageLoss = 50;

        itemLoss.isActive = false;
        itemLoss.amount = 1;
    }

    string FindTag(string tag)
    {
        string[] tags = UnityEditorInternal.InternalEditorUtility.tags;
        for (int i = 0; i < tags.Length; i++)
        {
            if (tags[i].Equals(tag))
            {
                return tag;
            }
        }
        return "Untagged";
    }

    LayerMask FindLayer(string layer)
    {
        string[] layers = UnityEditorInternal.InternalEditorUtility.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i].Equals(layer))
            {
                return LayerMask.GetMask(layer);
            }
        }
        return LayerMask.GetMask("Default");
    }

    public void SetTagsAndLayers()
    {
        itemTag = FindTag("Item");
        itemMask = FindLayer("Item");
        labelMask = FindLayer("ItemLabel");
    }
#endif
}
