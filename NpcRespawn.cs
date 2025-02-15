using UnityEngine;

public class NpcRespawn : NpcOffer
{
    public Transform respawnPoint; // The respawn point associated with this NPC

    public override bool HasOffer(Player player)
    {
        // Always show the "Set Respawn" option if a respawn point is set
        return respawnPoint != null;
    }

    public override string GetOfferName()
    {
        // The text for the menu option
        return "Set Respawn";
    }

    public override void OnSelect(Player player)
    {
        // Trigger the Respawn UI and pass the respawn point
        if (respawnPoint != null)
        {
            UISetRespawn.singleton.Show(this);
        }

        // Close the NPC dialogue menu when switching to the respawn menu
        UINpcDialogue.singleton.panel.SetActive(false);
    }
}
