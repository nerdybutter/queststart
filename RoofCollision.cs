using UnityEngine;

[RequireComponent(typeof(PolygonCollider2D))]
public class RoofCollision : MonoBehaviour
{
    [Header("Roof Tilemaps")]
    public GameObject tilemap;
    public GameObject tilemap2;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Player localPlayer = Player.localPlayer;

        // Check if the local player enters the trigger
        if (localPlayer != null && other.GetComponent<Player>() == localPlayer)
        {
            SetRoofVisibility(false); // Hide the roof for the local player
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Player localPlayer = Player.localPlayer;

        // Check if the local player exits the trigger
        if (localPlayer != null && other.GetComponent<Player>() == localPlayer)
        {
            SetRoofVisibility(true); // Show the roof for the local player
        }
    }

    private void SetRoofVisibility(bool visible)
    {
        // Only affects the local player's view
        if (tilemap != null)
        {
            tilemap.GetComponent<Renderer>().enabled = visible;
        }

        if (tilemap2 != null)
        {
            tilemap2.GetComponent<Renderer>().enabled = visible;
        }
    }
}
