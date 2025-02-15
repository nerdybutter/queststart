using UnityEngine;

public class PlayerUIHandler : MonoBehaviour
{
    public GameObject nameOverlayPosition; // Reference to the player's name overlay

    private void Start()
    {
        // Ensure the name overlay is hidden by default
        if (nameOverlayPosition != null)
            nameOverlayPosition.SetActive(false);
    }

    private void OnMouseEnter()
    {
        // Show the name overlay when the mouse is over the player
        if (nameOverlayPosition != null)
            nameOverlayPosition.SetActive(true);
    }

    private void OnMouseExit()
    {
        // Hide the name overlay when the mouse exits the player
        if (nameOverlayPosition != null)
            nameOverlayPosition.SetActive(false);
    }
}
