// Note: this script has to be on an always-active UI parent, so that we can
// always find it from other code. (GameObject.Find doesn't find inactive ones)
using UnityEngine;
using UnityEngine.UI;

public partial class UINpcDialogue : MonoBehaviour
{
    public static UINpcDialogue singleton;
    public GameObject panel;
    public Text welcomeText;
    public Transform offerPanel;
    public GameObject offerButtonPrefab;

    private DialogueNode currentDialogue;  // Holds the current dialogue being displayed
    private Npc currentNpc;  // Track the current NPC for service handling

    public UINpcDialogue()
    {
        // Ensure singleton only once
        if (singleton == null) singleton = this;
    }

    void Update()
    {
        Player player = Player.localPlayer;

        // Ensure the player is near the NPC before keeping the dialogue panel open
        if (player != null && panel.activeSelf && player.target != null && player.target is Npc npc &&
            Utils.ClosestDistance(player, player.target) <= player.interactionRange)
        {
            // If we haven't started the dialogue, start it from the NPC's starting node
            if (currentDialogue == null)
            {
                currentDialogue = npc.startingDialogue;  // Start the dialogue from NPC's starting node
                currentNpc = npc;  // Set the current NPC
                DisplayDialogue(currentDialogue, npc);  // Display the first dialogue immediately
            }
        }
        else
        {
            panel.SetActive(false);  // Hide the panel when the player is too far
            currentDialogue = null; // Reset currentDialogue to stop displaying it
        }
    }

    // Start a new dialogue sequence with the provided DialogueNode and NPC
    public void StartDialogue(DialogueNode dialogue, Npc npc)
    {
        if (currentDialogue != dialogue) // Only reset if the dialogue has changed
        {
            panel.SetActive(true); // Ensure the panel is shown
            currentDialogue = dialogue;  // Set the current dialogue
            DisplayDialogue(dialogue, npc);  // Display the new dialogue
        }
    }

    // Displays the NPC's dialogue choices and triggers NPC services when selected
    private void DisplayDialogue(DialogueNode node, Npc npc)
    {
        welcomeText.text = node.dialogueText;  // Display the NPC's dialogue text
        Debug.Log("Displaying Dialogue: " + node.dialogueText);  // Log current dialogue text

        // Balance Prefabs to match the number of choices
        UIUtils.BalancePrefabs(offerButtonPrefab, node.choices.Count, offerPanel);

        // Loop through the choices and set up buttons
        for (int i = 0; i < node.choices.Count; i++)
        {
            DialogueChoice choice = node.choices[i];
            Button choiceButton = offerPanel.GetChild(i).GetComponent<Button>();

            // Ensure button is active
            choiceButton.gameObject.SetActive(true);

            // Set button text
            choiceButton.GetComponentInChildren<Text>().text = choice.responseText;

            // Remove previous listeners to prevent stacking
            choiceButton.onClick.RemoveAllListeners();

            // If this choice triggers a service, handle that
            if (choice.isServiceOption)
            {
                choiceButton.onClick.AddListener(() => HandleNpcService(choice, npc));
            }
            else
            {
                // Otherwise, continue the dialogue
                choiceButton.onClick.AddListener(() => SelectChoice(choice, npc));
            }
        }
    }



    // Handles when a player selects a dialogue choice
    private void SelectChoice(DialogueChoice choice, Npc npc)
    {
        if (choice.isServiceOption)
        {
            // This option triggers a service, no need to move to the next node
            HandleNpcService(choice, npc);
        }
        else if (choice.nextNode != null)
        {
            Debug.Log("Next Node: " + choice.nextNode.dialogueText);  // Log next node info
            DisplayDialogue(choice.nextNode, npc);  // Show the next dialogue node
        }
        else
        {
            Debug.Log("Dialogue Ended");
            EndDialogue();  // End the dialogue if there's no next node
        }
    }



    // Handles NPC service activation (Shop, Quest, Teleport, etc.) when chosen
    private void HandleNpcService(DialogueChoice choice, Npc npc)
    {
        if (choice.responseText == "Shop" && npc.trading != null)
        {
            npc.trading.OnSelect(Player.localPlayer);  // Open the NPC's shop
        }
        else if (choice.responseText == "Quest" && npc.quests != null)
        {
            npc.quests.OnSelect(Player.localPlayer);  // Open the NPC's quest panel
        }
        else if (choice.responseText == "Teleport" && npc.teleport != null)
        {
            npc.teleport.OnSelect(Player.localPlayer);  // Execute teleportation
        }
        else if (choice.responseText == "Revive" && npc.revive != null)
        {
            UINpcRevive.singleton.panel.SetActive(true);  // Open revive panel
        }
        else if (choice.responseText == "Guild Management" && npc.guildManagement != null)
        {
            UINpcGuildManagement.singleton.panel.SetActive(true);  // Open guild management panel
        }
        else
        {
            Debug.LogWarning("No service found for choice: " + choice.responseText);
        }

        // End the dialogue after service is triggered
        EndDialogue();
    }

    // Ends the conversation and hides the panel
    public void EndDialogue()
    {
        panel.SetActive(false);
        currentDialogue = null;
        currentNpc = null;  // Reset the NPC reference when the dialogue ends
    }
}
