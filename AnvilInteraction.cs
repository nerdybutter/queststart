using UnityEngine;

public class AnvilInteraction : MonoBehaviour
{
    public UIBlacksmithing uiBlacksmithing;  // UI for blacksmithing
    public float interactionRange = 2f;       // Maximum distance to interact with the anvil
    public string requiredBlacksmithWeapon = "Blacksmithing Hammer";  // Name of the required weapon

    void OnMouseDown()
    {
        Player player = Player.localPlayer;
        if (player != null)
        {
            // Calculate the distance between the player and the anvil
            float distance = Vector3.Distance(transform.position, player.transform.position);

            // Check if the player is within interaction range
            if (distance > interactionRange)
            {
                Debug.Log("You need to be closer to the anvil to use it.");
                player.chat.TargetMsgInfo("You need to be closer to the anvil to use it.");
                return;
            }

            // Check if the player has the required weapon equipped
            if (!HasBlacksmithingHammerEquipped(player))
            {
                Debug.Log("You need a blacksmithing hammer to use the anvil.");
                player.chat.TargetMsgInfo("You need a blacksmithing hammer to use the anvil.");
                return;
            }

            // Open the blacksmithing UI
            uiBlacksmithing.panel.SetActive(true);
            Debug.Log("Blacksmithing UI opened.");
        }
    }

    private bool HasBlacksmithingHammerEquipped(Player player)
    {
        int weaponIndex = player.equipment.GetEquippedWeaponIndex();
        if (weaponIndex != -1 && player.equipment.slots.Count > weaponIndex)
        {
            string equippedWeaponName = player.equipment.slots[weaponIndex].item.data.name;
            Debug.Log($"Equipped weapon name: {equippedWeaponName}");
            return equippedWeaponName.Equals(requiredBlacksmithWeapon, System.StringComparison.OrdinalIgnoreCase);
        }

        Debug.Log("No weapon equipped or invalid weapon slot.");
        return false;
    }
}
