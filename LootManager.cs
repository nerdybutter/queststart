using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LootManager : MonoBehaviour
{
    public static LootManager instance;

    public Label labelPrefab;

    bool highlightLoot;
    LootHighlighter _lootHighlighter;
    readonly HashSet<ItemDrop> loots = new HashSet<ItemDrop>();
    readonly HashSet<ItemDrop> _items = new HashSet<ItemDrop>();
    LabelPool lootLabelPool;
    Label label;

    IEnumerator coroutine;

    Camera mainCamera;
    Player player;

    public Player GetPlayer() => player;
    public LootHighlighter GetLootHighlighter() => _lootHighlighter;

    public IReadOnlyCollection<ItemDrop> Items => _items;

    public bool HighlightLoot
    {
        get => highlightLoot;
        set
        {
            if (value != highlightLoot)
            {
                highlightLoot = value;

                HideLabel(); // hide a single label

                SetPlayer();

                if (highlightLoot)
                {
                    coroutine = ShowLabels();
                    StartCoroutine(coroutine);
                }
                else
                {
                    loots.Clear();
                    _lootHighlighter.Show(loots);
                }
            }
        }
    }

    void Awake()
    {
        instance = this;
        lootLabelPool = new LabelPool(transform);
        _lootHighlighter = new LootHighlighter(lootLabelPool);

        label = Create();

        mainCamera = Camera.main;
    }

    public Label Create()
    {
        return Instantiate(labelPrefab, transform);
    }

    void SetPlayer()
    {
        if (!player)
        {
            player = Player.localPlayer;
        }
    }

    void FindVisibleLoots()
    {        
        if (mainCamera != null)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
            foreach (ItemDrop loot in instance.Items)
            {              
                if (GeometryUtility.TestPlanesAABB(planes, loot.ItemBounds))
                {
                    loots.Add(loot);
                }             
            }
        }              
    }

    public void Add(ItemDrop item)
    {
        _items.Add(item);

        if (highlightLoot)
        {
            if (highlightLoot)
            {
#if ITEM_DROP_C
                if (!player.IsMoving())
                {
                    loots.Clear();
                    FindVisibleLoots();
                }
#endif
#if ITEM_DROP_R
                if (!player.movement.IsMoving())
                {
                    loots.Clear();
                    FindVisibleLoots();
                }
#endif
            }
        }
    }

    public void Remove(ItemDrop item)
    {
        _items.Remove(item);

        if (highlightLoot)
        {
#if ITEM_DROP_C
            if (!player.IsMoving())
            {
                loots.Clear();
                FindVisibleLoots();
            }
#endif
#if ITEM_DROP_R
            if (!player.movement.IsMoving())
            {
                loots.Clear();
                FindVisibleLoots();
            }
#endif
        }
        HideLabel();
    }

    public void ShowLabel(ItemDrop item)
    {
        Vector2 labelPosition = item.transform.position + (Vector3)item.TitleOffset;
        label.Show(labelPosition, item.Title);
    }
      
    public void HideLabel()
    {
        if (label != null)
        {
            label.Hide();
        }
    }

    IEnumerator ShowLabels()
    {
#if ITEM_DROP_C
        if (!player.IsMoving())
        {
            loots.Clear();
            FindVisibleLoots();
        }
#endif
#if ITEM_DROP_R
        if (!player.movement.IsMoving())
        {
            loots.Clear();
            FindVisibleLoots();
        }
#endif
        while (highlightLoot)
        {
#if ITEM_DROP_C
            if (player.IsMoving())
            {
                loots.Clear();
                FindVisibleLoots();
            }
#endif
#if ITEM_DROP_R
            if (player.movement.IsMoving())
            {
                loots.Clear();
                FindVisibleLoots();
            }
#endif
            _lootHighlighter.Show(loots);
            yield return null;
        }
    }
}
