using System.Collections.Generic;
using UnityEngine;

public class LootHighlighter
{
    Dictionary<ItemDrop, Label> labels = new Dictionary<ItemDrop, Label>();
    Dictionary<GameObject, ItemDrop> lootsByGameObjects = new Dictionary<GameObject, ItemDrop>();
    List<ItemDrop> toRemove = new List<ItemDrop>();
    LabelPool labelPool;
    readonly LootLabelLayout layout = new LootLabelLayout(1);
    
    public LootHighlighter(LabelPool labelPool)
    {
        this.labelPool = labelPool;
    }
    
    public void Show(ISet<ItemDrop> loots)
    {
        CreateLabels(loots); 
        layout.Arrange(labels);              
    }

    void CreateLabels(ISet<ItemDrop> loots)
    {
        toRemove.Clear();
        foreach (ItemDrop loot in labels.Keys)
        {
            if (!loots.Contains(loot))
                toRemove.Add(loot);
        }
        foreach (ItemDrop loot in toRemove)
        {
            Label label = labels[loot];
            labels.Remove(loot);
            lootsByGameObjects.Remove(label.gameObject);
            labelPool.Return(label);
        }
        foreach (ItemDrop loot in loots)
        {
            if (!labels.ContainsKey(loot))
            {
                Label label = labelPool.Get();
                labels.Add(loot, label);
                lootsByGameObjects.Add(label.gameObject, loot);
            }
        }
    }

    public ItemDrop SelectLoot(GameObject gameObject)
    {
        return lootsByGameObjects.GetValueOrDefault(gameObject);
    }
}
