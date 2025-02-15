using UnityEngine;
using UnityEngine.UI;

public class UISetRespawn : MonoBehaviour
{
    public static UISetRespawn singleton;
    public GameObject panel;
    public Button confirmButton;
    public Button cancelButton;

    private NpcRespawn currentRespawn; // Reference to the NPC's Respawn component

    void Awake()
    {
        singleton = this;
        panel.SetActive(false);
    }

    public void Show(NpcRespawn respawn)
    {
        currentRespawn = respawn;
        panel.SetActive(true); // Show the respawn dialog
    }

    public void Hide()
    {
        currentRespawn = null;
        panel.SetActive(false); // Hide the respawn dialog
    }

    public void Confirm()
    {
        if (currentRespawn != null && Player.localPlayer != null)
        {
            // Set the player's respawn point on the server
            Player.localPlayer.CmdSetRespawnPoint(currentRespawn.respawnPoint.position);
            Hide();
        }
    }
}
