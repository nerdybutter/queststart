using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public partial class UIBlacksmithing : MonoBehaviour
{
    public GameObject panel;
    public UIBlacksmithingIngredientSlot BlacksmithingIngredientSlot;
    public Transform BlacksmithingingredientContent;
    public Image resultSlotImage;
    public UIShowToolTip resultSlotToolTip;
    public Button BlacksmithButton;
    public Slider progressSlider;
    public Text resultText;
    public Toggle autoToggle; // Add a toggle for auto-Blacksmithing
    public Color successColor = Color.green;
    public Color failedColor = Color.red;

    [Header("Durability Colors")]
    public Color brokenDurabilityColor = Color.red;
    public Color lowDurabilityColor = Color.magenta;
    [Range(0.01f, 0.99f)] public float lowDurabilityThreshold = 0.1f;

    [Header("Interaction Settings")]
    public float interactionRange = 5f; // Maximum distance to interact with a Anvil

    void Update()
    {
        Player player = Player.localPlayer;
        if (player)
        {
            // Check if the player is within range of any Anvil
            if (panel.activeSelf)
            {
                AnvilInteraction nearestAnvil = FindNearestAnvil(player);
                if (nearestAnvil == null || Vector3.Distance(player.transform.position, nearestAnvil.transform.position) > interactionRange)
                {
                    panel.SetActive(false); // Close the panel if the player moves away from the Anvil
                    return;
                }

                // instantiate/destroy enough slots
                UIUtils.BalancePrefabs(BlacksmithingIngredientSlot.gameObject, player.Blacksmithing.indices.Count, BlacksmithingingredientContent);

                // refresh all ingredient slots
                for (int i = 0; i < player.Blacksmithing.indices.Count; ++i)
                {
                    UIBlacksmithingIngredientSlot slot = BlacksmithingingredientContent.GetChild(i).GetComponent<UIBlacksmithingIngredientSlot>();
                    slot.dragAndDropable.name = i.ToString(); // drag and drop index
                    int itemIndex = player.Blacksmithing.indices[i];

                    if (0 <= itemIndex && itemIndex < player.inventory.slots.Count &&
                        player.inventory.slots[itemIndex].amount > 0)
                    {
                        ItemSlot itemSlot = player.inventory.slots[itemIndex];
                        slot.tooltip.enabled = true;
                        if (slot.tooltip.IsVisible())
                            slot.tooltip.text = itemSlot.ToolTip();
                        slot.dragAndDropable.dragable = true;

                        slot.image.color = Color.white; // reset for no-durability items
                        slot.image.sprite = itemSlot.item.image;

                        slot.amountOverlay.SetActive(itemSlot.amount > 1);
                        slot.amountText.text = itemSlot.amount.ToString();
                    }
                    else
                    {
                        // reset the index because it's invalid
                        player.Blacksmithing.indices[i] = -1;

                        // refresh invalid item
                        slot.tooltip.enabled = false;
                        slot.dragAndDropable.dragable = false;
                        slot.image.color = Color.clear;
                        slot.image.sprite = null;
                        slot.amountOverlay.SetActive(false);
                    }
                }

                // find valid indices => item templates => matching recipe
                List<int> validIndices = player.Blacksmithing.indices.Where(
                    index => 0 <= index && index < player.inventory.slots.Count &&
                             player.inventory.slots[index].amount > 0
                ).ToList();
                List<ItemSlot> items = validIndices.Select(index => player.inventory.slots[index]).ToList();
                ScriptableBlacksmithingRecipe recipe = ScriptableBlacksmithingRecipe.Find(items);
                if (recipe != null)
                {
                    // refresh valid recipe
                    Item item = new Item(recipe.result);
                    resultSlotToolTip.enabled = true;
                    if (resultSlotToolTip.IsVisible())
                        resultSlotToolTip.text = new ItemSlot(item).ToolTip();
                    resultSlotImage.color = Color.white;
                    resultSlotImage.sprite = recipe.result.image;

                    // show progress bar while Blacksmithing
                    progressSlider.gameObject.SetActive(player.state == "BLACKSMITHING");
                    double startTime = player.Blacksmithing.endTime - recipe.BlacksmithingTime;
                    double elapsedTime = NetworkTime.time - startTime;
                    progressSlider.value = recipe.BlacksmithingTime > 0 ? (float)elapsedTime / recipe.BlacksmithingTime : 1;
                }
                else
                {
                    // refresh invalid recipe
                    resultSlotToolTip.enabled = false;
                    resultSlotImage.color = Color.clear;
                    resultSlotImage.sprite = null;
                    progressSlider.gameObject.SetActive(false);
                }

                // Blacksmith result
                if (player.Blacksmithing.state == BlacksmithingState.Success)
                {
                    resultText.color = successColor;
                    resultText.text = "Success!";
                }
                else if (player.Blacksmithing.state == BlacksmithingState.Failed)
                {
                    resultText.color = failedColor;
                    resultText.text = "Failed :(";
                }
                else
                {
                    resultText.text = "";
                }

                // Blacksmith button with 'Try' prefix to let people know it might fail
                BlacksmithButton.GetComponentInChildren<Text>().text = recipe != null && recipe.probability < 1 ? "Repair" : "Repair";
                BlacksmithButton.interactable = recipe != null &&
                                           player.state != "BLACKSMITHING" &&
                                           player.Blacksmithing.state != BlacksmithingState.InProgress &&
                                           player.inventory.CanAdd(new Item(recipe.result), 1);
                BlacksmithButton.onClick.SetListener(() =>
                {
                    player.Blacksmithing.state = BlacksmithingState.InProgress;

                    // pass original array so server can copy it
                    player.Blacksmithing.CmdBlacksmith(recipe.name, player.Blacksmithing.indices.ToArray(), autoToggle.isOn);
                });
            }
        }
        else
        {
            panel.SetActive(false);
        }
    }

    // Helper method to find the nearest Anvil
    AnvilInteraction FindNearestAnvil(Player player)
    {
        AnvilInteraction[] Anvils = FindObjectsOfType<AnvilInteraction>();
        return Anvils.OrderBy(Anvil => Vector3.Distance(player.transform.position, Anvil.transform.position))
                     .FirstOrDefault();
    }
}
