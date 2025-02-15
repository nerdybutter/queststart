using UnityEngine;

public class ShoulderEquipmentSorting : MonoBehaviour
{
    public SpriteRenderer shoulderRenderer; // Assign your shoulder SpriteRenderer here

    private void Update()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;

        if (input == Vector2.zero) return; // Skip if no movement input

        if (input.y < 0) // Facing down
        {
            shoulderRenderer.sortingOrder = -1;
        }
        else if (input.y > 0) // Facing up
        {
            shoulderRenderer.sortingOrder = 1;
        }
        else if (input.x != 0) // Facing left or right
        {
            shoulderRenderer.sortingOrder = 0;
        }
    }
}
