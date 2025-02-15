using UnityEngine;
using Mirror;
using System.Collections;

[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(NetworkName))]
public class ItemDrop : NetworkBehaviour
{
    [HideInInspector] public ScriptableItem data;
    [HideInInspector, SyncVar] public int stack;
    [HideInInspector, SyncVar] public Vector2 endingPoint;
    [HideInInspector, SyncVar] public int durability; // Add durability here
    [HideInInspector] public string uniqueId;
    [HideInInspector] public bool isMarked;

    [Header("Components")]
    public CircleCollider2D itemCollider;

    SpriteRenderer itemSprite;

    Vector2 startingPoint;
    Vector2 controlPoint;
    float count;

    readonly Color normalColor = new Color32(128, 128, 128, 255);      // Gray 808080
    readonly Color highlightedColor = new Color32(255, 255, 255, 255); // White FFFFFF

    string _title = null;
    public string Title
    {
        set { _title = value; }
        get { return _title ?? name; }
    }

    public Bounds ItemBounds => itemSprite.bounds;
    public Vector2 TitleOffset => Vector2.up;
    public bool IsVisible => itemSprite.isVisible;
    public bool IsMoving => (Vector2)transform.position != endingPoint;

    IEnumerator coroutineDrop;
    IEnumerator coroutineDestroy;
    WaitForSeconds updateInterval;

    void Awake()
    {
        itemSprite = GetComponent<SpriteRenderer>();
        coroutineDrop = Drop();
        coroutineDestroy = DestroyAfter();

        updateInterval = new WaitForSeconds(ItemDropSettings.Settings.decayTime);
    }

    void Start()
    {
        if (ScriptableItem.All.TryGetValue(name.GetStableHashCode(), out data))
        {
            if (data.image != null)
            {
                startingPoint = transform.position;
                itemSprite.sprite = data.image;

                // Handle server-specific behaviors
                if (isServer)
                {
                    if (string.IsNullOrEmpty(uniqueId))
                    {
                        StartCoroutine(coroutineDestroy);
                    }
                }

                // Disable sprite if server-only
                if (isServerOnly)
                {
                    itemSprite.enabled = false;
                }

                LootManager.instance.Add(this);

                // Set the title/tooltip
                if (data is EquipmentItem equipmentItem)
                {
                    // Check if item is indestructible before appending durability
                    if (!equipmentItem.isIndestructible)
                    {
                        // Use the current durability value set on the item
                        Title = $"{data.GetTitle(stack)}\nDurability: {durability}/100";
                    }
                    else
                    {
                        Title = data.GetTitle(stack); // No durability for indestructible items
                    }
                }
                else
                {
                    Title = data.GetTitle(stack);
                }

                StartCoroutine(coroutineDrop);
            }
        }
    }




    IEnumerator Drop()
    {
        while (IsMoving)
        {
            float step = ItemDropSettings.Settings.itemDropSpeed * Time.deltaTime;

            if (!isMarked)
            {
                // define a point that controls where the curve is made
                controlPoint = startingPoint + (endingPoint - startingPoint) / 2 + Vector2.up * 2.0f;
                isMarked = true;
            }

            if (count < 1.0f)
            {
                count += step;

                // move item along a curve to make a nice looking drop effect
                transform.position = AddonItemDrop.GetPoint(startingPoint, controlPoint, endingPoint, count);
                yield return null;
            }
        }
    }

    IEnumerator DestroyAfter()
    {
        yield return updateInterval;
        NetworkServer.Destroy(gameObject);
    }

    [Client]
    void OnMouseEnter()
    {
        if (Utils.IsCursorOverUserInterface())
            return;

        if (!LootManager.instance.HighlightLoot)
        {
            LootManager.instance.ShowLabel(this);
            itemSprite.color = highlightedColor;
        }
    }

    [Client]
    void OnMouseExit()
    {
        LootManager.instance.HideLabel();
        itemSprite.color = normalColor;
    }

    void OnDestroy()
    {
        LootManager.instance.Remove(this);
    }

    /// <summary>
    /// Initializes the dropped item.
    /// </summary>

    public void Initialize(ScriptableItem itemData, int itemStack, int itemDurability, Vector2 targetPosition)
    {
        data = itemData;
        stack = itemStack;
        durability = itemDurability; // Properly set durability here
        endingPoint = targetPosition;

        Title = $"{itemData.GetTitle(itemStack)}\nDurability: {durability}/{((EquipmentItem)itemData).maxDurability}";
    }

}

public struct Loot
{
    public int hashCode;
    public string uniqueId;
    public int stack;
    public int durability; // Add durability to the Loot struct

    public Loot(int _hashCode, string _uniqueId, int _stack, int _durability)
    {
        hashCode = _hashCode;
        uniqueId = _uniqueId;
        stack = _stack;
        durability = _durability;
    }
}
