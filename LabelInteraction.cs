using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class LabelInteraction : MonoBehaviour
{
    public Label label;
    ItemDrop item;

    void OnMouseDown()
    {
        item = LootManager.instance.GetLootHighlighter().SelectLoot(label.gameObject);
        //Debug.Log($"Selected Item: {label.text.text}, {item}");

        LootManager.instance.GetPlayer()?.SetTargetItem(item);
    }
    
    void OnMouseEnter()
    {
        label.SetColor(ItemDropSettings.Settings.highlightedLabel);
    }
    
    void OnMouseExit()
    {
        label.SetColor(ItemDropSettings.Settings.label);
    }    
}
