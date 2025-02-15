using Mirror;
using SQLite;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class Database
{
    class Items
    {
        public string Name { get; set; }
        [PrimaryKey] public string ID { get; set; }
        public int Stack { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }

    void Initialize_ItemSave()
    {
        connection.CreateTable<Items>();
    }

#if ITEM_DROP_R
#if !UMMORPG_3D_R && !UMMORPG_2D_R
    void Start()
    {
        StartItemDrop();
    }
#endif
    void StartItemDrop()
    {
        onConnected?.AddListener(Initialize_ItemSave);
    }
#endif

    // deleting an item from the database by unique ID
    public void ItemDelete(string uniqueId)
    {
        connection.BeginTransaction();
        connection.Execute("DELETE FROM Items WHERE ID=?", uniqueId);
        connection.Commit();
    }

    // deleting all items from the database
    public void ItemsDelete()
    {
        connection.BeginTransaction();
        connection.Execute("DELETE FROM Items");
        connection.Commit();
    }

    public GameObject ItemLoad(ItemDrop prefab)
    {
        foreach (Items row in connection.Query<Items>("SELECT * FROM Items"))
        {
            ItemDrop item = Instantiate(prefab);

            item.name = row.Name;
            item.uniqueId = row.ID;
            item.stack = row.Stack;

            Vector2 position = new Vector3(row.X, row.Y);

            item.endingPoint = position;
            item.transform.position = position;

            NetworkServer.Spawn(item.gameObject);
        }
        return null;
    }

    public void ItemSave(ItemDrop item, bool useTransaction = true)
    {
        // only use a transaction if not called within SaveMany transaction
        if (useTransaction) connection.BeginTransaction();
        connection.InsertOrReplace(new Items
        {
            Name = item.name,
            ID = item.uniqueId,
            Stack = item.stack,
            X = item.transform.position.x,
            Y = item.transform.position.y
        });
        if (useTransaction) connection.Commit();
    }

    // save multiple items at once (useful for ultra fast transactions)
    public void ItemSaveMany(List<ItemDrop> items)
    {
        connection.BeginTransaction();
        foreach (ItemDrop item in items)
        {
            ItemSave(item, false);
            item.isMarked = false;
        }
        connection.Commit();
    }
}

public partial class NetworkManagerMMO
{
    WaitForSeconds itemSaveCycle;
    IEnumerator itemSaveCoroutine;

    void OnStartServer_ItemDrop()
    {
        itemSaveCycle = new WaitForSeconds(ItemDropSettings.Settings.saveInterval);

        itemSaveCoroutine = ItemAutoSave();
        StartCoroutine(itemSaveCoroutine);

        AddonItemDrop.LoadItems();
    }

    void OnStopServer_ItemDrop()
    {
        StopCoroutine(itemSaveCoroutine);
    }

#if ITEM_DROP_R
#if !UMMORPG_3D_R && !UMMORPG_2D_R
    public override void Start()
    {
        base.Start();
        StartItemDrop();
    }
#endif
    void StartItemDrop()
    {
        onStartServer?.AddListener(OnStartServer_ItemDrop);
        onStopServer?.AddListener(OnStopServer_ItemDrop);
    }
#endif

    IEnumerator ItemAutoSave()
    {
        while (true)
        {
            yield return itemSaveCycle;
            AddonItemDrop.SaveItems();
        }
    }
}
