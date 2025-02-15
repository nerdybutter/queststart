using UnityEngine;
using UnityEngine.UI;

public class UIVault : MonoBehaviour
{
    public static UIVault singleton;
    public GameObject panel;
    public UIVaultSlot slotPrefab;
    public Transform content;
    public float interactionRange = 5f;  // Set the maximum distance for interaction

    private VaultObject currentVault;

    private void Awake()
    {
        if (singleton == null) singleton = this;
    }

    public void SetCurrentVault(VaultObject vault)
    {
        currentVault = vault;  // Set the active vault object
    }

    private void Update()
    {
        Player player = Player.localPlayer;

        if (player != null && player.GetComponent<PlayerVault>() != null)
        {
            // Only update the panel if it's active
            if (panel.activeSelf)
            {
                // Check if the player is still within interaction range of the vault
                if (currentVault != null)
                {
                    float distance = Vector3.Distance(player.transform.position, currentVault.transform.position);
                    if (distance > interactionRange)
                    {
                        Debug.Log("Player moved out of range. Closing vault UI.");
                        panel.SetActive(false);
                        currentVault = null;
                        return;
                    }
                }

                PlayerVault vault = player.GetComponent<PlayerVault>();

                // Ensure the slots match the vault size
                UIUtils.BalancePrefabs(slotPrefab.gameObject, vault.slots.Count, content);

                // Refresh each slot in the vault
                for (int i = 0; i < vault.slots.Count; ++i)
                {
                    UIVaultSlot slot = content.GetChild(i).GetComponent<UIVaultSlot>();
                    slot.dragAndDropable.name = i.ToString();  // Set drag-and-drop index
                    ItemSlot itemSlot = vault.slots[i];

                    if (itemSlot.amount > 0)
                    {
                        // Refresh valid item
                        int icopy = i;  // Needed for lambda
                        slot.button.onClick.RemoveAllListeners();
                        slot.button.onClick.AddListener(() =>
                        {
                            Debug.Log($"Clicked item {itemSlot.item.name} in vault slot {icopy}");
                            // Additional actions can be added here
                        });

                        slot.image.sprite = itemSlot.item.image;
                        slot.image.color = Color.white;  // Ensure visibility
                        slot.amountText.text = itemSlot.amount.ToString();
                        slot.amountOverlay.SetActive(itemSlot.amount > 1);
                        slot.tooltip.enabled = true;
                        slot.tooltip.text = itemSlot.ToolTip();
                        slot.dragAndDropable.dragable = true;

                        // Cooldown handling
                        if (itemSlot.item.data is UsableItem usable)
                        {
                            float cooldown = player.GetItemCooldown(usable.cooldownCategory);
                            slot.cooldownCircle.fillAmount = usable.cooldown > 0 ? cooldown / usable.cooldown : 0;
                        }
                        else
                        {
                            slot.cooldownCircle.fillAmount = 0;
                        }
                    }
                    else
                    {
                        // Clear invalid item
                        slot.button.onClick.RemoveAllListeners();
                        slot.tooltip.enabled = false;
                        slot.dragAndDropable.dragable = false;
                        slot.image.color = Color.clear;
                        slot.image.sprite = null;
                        slot.cooldownCircle.fillAmount = 0;
                        slot.amountOverlay.SetActive(false);
                    }
                }
            }
        }
        else
        {
            panel.SetActive(false);
        }
    }
}
