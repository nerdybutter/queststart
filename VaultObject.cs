using UnityEngine;

public class VaultObject : MonoBehaviour
{
    public GameObject vaultUIPanel;    // Reference to the vault UI panel
    public float interactionRange = 2f; // Maximum distance to interact with the vault

    private void OnMouseDown()
    {
        Player player = Player.localPlayer;
        if (player != null)
        {
            // Calculate the distance between the player and the vault
            float distance = Vector3.Distance(transform.position, player.transform.position);

            // Check if the player is within interaction range
            if (distance <= interactionRange)
            {
                OpenVault(player);  // Open the vault if within range
            }
            else
            {
                Debug.Log("You need to be closer to the vault to use it.");
                // Optionally, show a message to the player
                // UIMessage.Show("You need to be closer to the vault to use it.");
            }
        }
    }

    private void OpenVault(Player player)
    {
        if (vaultUIPanel != null)
        {
            vaultUIPanel.SetActive(true);
            UIVault.singleton.SetCurrentVault(this);  // Set the active vault for range checking
        }

        PlayerVault playerVault = player.GetComponent<PlayerVault>();
        if (playerVault != null)
        {
            playerVault.CmdRequestVaultData();  // Request server to sync vault data
        }
    }
}
