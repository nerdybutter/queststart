using UnityEngine;

public class StoveInteraction : MonoBehaviour
{
    public UICooking uiCooking;
    public float interactionRange = 2f; // Maximum distance to interact with the stove

    void OnMouseDown()
    {
        Player player = Player.localPlayer;
        if (player != null)
        {
            // Calculate the distance between the player and the stove
            float distance = Vector3.Distance(transform.position, player.transform.position);

            // Check if the player is within interaction range
            if (distance <= interactionRange)
            {
                uiCooking.panel.SetActive(true); // Open the cooking UI
            }
            else
            {
                Debug.Log("You need to be closer to the stove to use it.");
                // Optionally, show a message to the player
                // UIMessage.Show("You need to be closer to the stove to use it.");
            }
        }
    }
}
