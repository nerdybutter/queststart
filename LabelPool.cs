using System.Collections.Generic;
using UnityEngine;

public class LabelPool
{
    readonly Transform parent;
    readonly List<Label> pool = new List<Label>();

    public LabelPool(Transform parent)
    {
        this.parent = parent;
    }
    
    public Label Get()
    {
        if (pool.Count == 0)
        {
            return parent.GetComponent<LootManager>().Create();
        }

        Label label = pool[pool.Count - 1];
        label.SetColor(ItemDropSettings.Settings.label);
        pool.RemoveAt(pool.Count - 1);
        return label;
    }
    
    public void Return(Label label)
    {
        label.Hide();
        pool.Add(label);
    }
}
