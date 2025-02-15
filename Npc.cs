// The Npc class is rather simple. It contains state Update functions that do
// nothing at the moment, because Npcs are supposed to stand around all day.
//
// Npcs first show the welcome text and then have options for item trading, 
// quests, teleporting, reviving, and guild management.
using UnityEngine;
using Mirror;

[RequireComponent(typeof(NetworkNavMeshAgent2D))]
public partial class Npc : Entity
{
    [Header("Components")]
    public NpcGuildManagement guildManagement;
    public NpcQuests quests;
    public NpcRevive revive;
    public NpcTrading trading;
    public NpcTeleport teleport;

    [Header("Dialogue")]
    public DialogueNode startingDialogue; // Keeps NPC dialogue functionality

    private NpcOffer[] offers; // Keeps track of NPC offers (shop, quests, etc.)

    void Awake()
    {
        offers = GetComponents<NpcOffer>(); // Ensure NPC offers are initialized
    }

    // finite state machine states /////////////////////////////////////////////
    [Server] protected override string UpdateServer() { return state; }
    [Client] protected override void UpdateClient() { }

    // skills //////////////////////////////////////////////////////////////////
    public override bool CanAttack(Entity entity) { return false; }

    // interaction /////////////////////////////////////////////////////////////
    protected override void OnInteract()
    {
        Player player = Player.localPlayer;

        // Ensure NPC is alive and the player is within interaction range
        if (health.current > 0 &&
            Utils.ClosestDistance(player, this) <= player.interactionRange)
        {
            // If this NPC has dialogue, use branching dialogue system
            if (startingDialogue != null)
            {
                UINpcDialogue.singleton.StartDialogue(startingDialogue, this);
            }
            else
            {
                // If no dialogue, fallback to default NPC interactions (Shops, Quests, etc.)
                if (quests != null && quests.quests.Length > 0) // Fixed: Use .Length instead of .Count
                {
                    UINpcQuests.singleton.panel.SetActive(true); // Fixed: Use panel activation
                }
                else if (trading != null && trading.saleItems.Length > 0) // Fixed: Use correct property name
                {
                    UINpcTrading.singleton.panel.SetActive(true);
                }
                else if (revive != null)
                {
                    UINpcRevive.singleton.panel.SetActive(true);
                }
                else if (teleport != null && teleport.destination != null)
                {
                    player.npcTeleport.CmdNpcTeleport();
                }
                else if (guildManagement != null)
                {
                    UINpcGuildManagement.singleton.panel.SetActive(true);
                }
                else
                {
                    Debug.Log($"{name} has no dialogue or interaction options.");
                }
            }
        }
        else
        {
            // Otherwise, move the player closer for interaction
            Vector2 destination = Utils.ClosestPoint(this, player.transform.position);
            player.movement.Navigate(destination, player.interactionRange);
        }
    }
}
