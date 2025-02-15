using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Dialogue/Node", order = 1)]
public class DialogueNode : ScriptableObject
{
    [Header("NPC Dialogue Line")]
    [TextArea(3, 10)]
    public string dialogueText;

    [Header("Player Responses")]
    public List<DialogueChoice> choices;  // List of DialogueChoices the player can select from
}
