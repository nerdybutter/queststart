using UnityEngine;

[System.Serializable]
public class DialogueChoice
{
    [TextArea(2, 5)]
    public string responseText;  // What the player says (e.g., "Yes, let's trade.")

    public DialogueNode nextNode;  // The next dialogue node if the conversation continues

    public bool isServiceOption;  // Whether this choice triggers a service (like Shop, Quest, etc.)

    // Constructor to initialize a DialogueChoice with or without a service option
    public DialogueChoice(string responseText, DialogueNode nextNode = null, bool isServiceOption = false)
    {
        this.responseText = responseText;
        this.nextNode = nextNode;
        this.isServiceOption = isServiceOption;
    }
}
