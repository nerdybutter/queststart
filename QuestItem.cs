using UnityEngine;

[CreateAssetMenu(menuName = "uMMORPG Item/QuestItem", order = 998)]
public class QuestItem : ScriptableItem
{
    [Header("Quest Link")]
    public string linkedQuestName;  // The name of the quest this item belongs to

    public ScriptableItem GetScriptableItem()
    {
        return this; // Since QuestItem inherits from ScriptableItem, it can return itself
    }

    public override string ToolTip()
    {
        return $"{base.ToolTip()}\nRequired for Quest: {linkedQuestName}";
    }

}
