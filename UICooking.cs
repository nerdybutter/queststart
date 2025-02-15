using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public partial class UICooking : MonoBehaviour
{
    public GameObject panel;
    public UICookingIngredientSlot CookingIngredientSlot;
    public Transform CookingingredientContent;
    public Image resultSlotImage;
    public UIShowToolTip resultSlotToolTip;
    public Button cookButton;
    public Slider progressSlider;
    public Text resultText;
    public Toggle autoToggle; // Add a toggle for auto-cooking
    public Color successColor = Color.green;
    public Color failedColor = Color.red;

    [Header("Durability Colors")]
    public Color brokenDurabilityColor = Color.red;
    public Color lowDurabilityColor = Color.magenta;
    [Range(0.01f, 0.99f)] public float lowDurabilityThreshold = 0.1f;

    [Header("Interaction Settings")]
    public float interactionRange = 5f; // Maximum distance to interact with a stove

    void Update()
    {
        Player player = Player.localPlayer;
        if (player)
        {
            // Check if the player is within range of any stove
            if (panel.activeSelf)
            {
                StoveInteraction nearestStove = FindNearestStove(player);
                if (nearestStove == null || Vector3.Distance(player.transform.position, nearestStove.transform.position) > interactionRange)
                {
                    panel.SetActive(false); // Close the panel if the player moves away from the stove
                    return;
                }

                // instantiate/destroy enough slots
                UIUtils.BalancePrefabs(CookingIngredientSlot.gameObject, player.cooking.indices.Count, CookingingredientContent);

                // refresh all ingredient slots
                for (int i = 0; i < player.cooking.indices.Count; ++i)
                {
                    UICookingIngredientSlot slot = CookingingredientContent.GetChild(i).GetComponent<UICookingIngredientSlot>();
                    slot.dragAndDropable.name = i.ToString(); // drag and drop index
                    int itemIndex = player.cooking.indices[i];

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
                        player.cooking.indices[i] = -1;

                        // refresh invalid item
                        slot.tooltip.enabled = false;
                        slot.dragAndDropable.dragable = false;
                        slot.image.color = Color.clear;
                        slot.image.sprite = null;
                        slot.amountOverlay.SetActive(false);
                    }
                }

                // find valid indices => item templates => matching recipe
                List<int> validIndices = player.cooking.indices.Where(
                    index => 0 <= index && index < player.inventory.slots.Count &&
                             player.inventory.slots[index].amount > 0
                ).ToList();
                List<ItemSlot> items = validIndices.Select(index => player.inventory.slots[index]).ToList();
                ScriptableCookingRecipe recipe = ScriptableCookingRecipe.Find(items);
                if (recipe != null)
                {
                    // refresh valid recipe
                    Item item = new Item(recipe.result);
                    resultSlotToolTip.enabled = true;
                    if (resultSlotToolTip.IsVisible())
                        resultSlotToolTip.text = new ItemSlot(item).ToolTip();
                    resultSlotImage.color = Color.white;
                    resultSlotImage.sprite = recipe.result.image;

                    // show progress bar while cooking
                    progressSlider.gameObject.SetActive(player.state == "COOKING");
                    double startTime = player.cooking.endTime - recipe.cookingTime;
                    double elapsedTime = NetworkTime.time - startTime;
                    progressSlider.value = recipe.cookingTime > 0 ? (float)elapsedTime / recipe.cookingTime : 1;
                }
                else
                {
                    // refresh invalid recipe
                    resultSlotToolTip.enabled = false;
                    resultSlotImage.color = Color.clear;
                    resultSlotImage.sprite = null;
                    progressSlider.gameObject.SetActive(false);
                }

                // cook result
                if (player.cooking.state == CookingState.Success)
                {
                    resultText.color = successColor;
                    resultText.text = "Success!";
                }
                else if (player.cooking.state == CookingState.Failed)
                {
                    resultText.color = failedColor;
                    resultText.text = "Failed :(";
                }
                else
                {
                    resultText.text = "";
                }

                // cook button with 'Try' prefix to let people know it might fail
                cookButton.GetComponentInChildren<Text>().text = recipe != null && recipe.probability < 1 ? "Cook" : "Cook";
                cookButton.interactable = recipe != null &&
                                           player.state != "COOKING" &&
                                           player.cooking.state != CookingState.InProgress &&
                                           player.inventory.CanAdd(new Item(recipe.result), 1);
                cookButton.onClick.SetListener(() =>
                {
                    player.cooking.state = CookingState.InProgress;

                    // pass original array so server can copy it
                    player.cooking.CmdCook(recipe.name, player.cooking.indices.ToArray(), autoToggle.isOn);
                });
            }
        }
        else
        {
            panel.SetActive(false);
        }
    }

    // Helper method to find the nearest stove
    StoveInteraction FindNearestStove(Player player)
    {
        StoveInteraction[] stoves = FindObjectsOfType<StoveInteraction>();
        return stoves.OrderBy(stove => Vector3.Distance(player.transform.position, stove.transform.position))
                     .FirstOrDefault();
    }
}
