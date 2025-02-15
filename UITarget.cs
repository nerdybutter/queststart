// Note: this script has to be on an always-active UI parent, so that we can
// always find it from other code. (GameObject.Find doesn't find inactive ones)
using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;

public partial class UITarget : MonoBehaviour
{
    public GameObject panel;
    public Slider healthSlider;
    public Text nameText;
    public Transform buffsPanel;
    public UIBuffSlot buffSlotPrefab;
    public Button tradeButton;
    public Button guildInviteButton;
    public Button partyInviteButton;

    public GameObject bioPanel; // Reference to the BioWindow panel
    public TMP_Text bioText; // Text field to display the player's bio
    public Transform trophyContainer; // Container for trophies
    public GameObject trophyPrefab; // Prefab for trophies

    void Update()
    {
        Player player = Player.localPlayer;
        if (player != null)
        {
            // Get the target (nextTarget > target)
            Entity target = player.nextTarget ?? player.target;

            // Skip self-targeting
            if (target == player)
            {
                // Hide the target panel and bioPanel if self-targeted
                panel.SetActive(false);
                if (bioPanel != null)
                    bioPanel.SetActive(false);
                return;
            }

            if (target != null)
            {
                float distance = Utils.ClosestDistance(player.collider, target.collider);

                // Show target panel
                panel.SetActive(true);
                healthSlider.value = target.health.Percent();
                nameText.text = target.name;

                // Handle buffs
                UIUtils.BalancePrefabs(buffSlotPrefab.gameObject, target.skills.buffs.Count, buffsPanel);
                for (int i = 0; i < target.skills.buffs.Count; ++i)
                {
                    UIBuffSlot slot = buffsPanel.GetChild(i).GetComponent<UIBuffSlot>();
                    slot.image.color = Color.white;
                    slot.image.sprite = target.skills.buffs[i].image;
                    if (slot.tooltip.IsVisible())
                        slot.tooltip.text = target.skills.buffs[i].ToolTip();
                    slot.slider.maxValue = target.skills.buffs[i].buffTime;
                    slot.slider.value = target.skills.buffs[i].BuffTimeRemaining();
                }

                // Handle trade button
                if (target is Player targetPlayer)
                {
                    tradeButton.gameObject.SetActive(true);
                    tradeButton.interactable = player.trading.CanStartTradeWith(target);
                    tradeButton.onClick.SetListener(() =>
                    {
                        player.trading.CmdSendRequest();
                    });

                    // Show BioWindow for the target player
                    if (bioPanel != null)
                    {
                        bioPanel.SetActive(true);

                        // Update Bio and Trophies
                        if (bioText != null)
                            bioText.text = targetPlayer.bio; // Update the player's bio

                        if (trophyContainer != null && trophyPrefab != null)
                        {
                            UIUtils.BalancePrefabs(trophyPrefab, targetPlayer.trophies.Count, trophyContainer);
                            for (int i = 0; i < targetPlayer.trophies.Count; i++)
                            {
                                int trophyId = targetPlayer.trophies[i];
                                Transform slot = trophyContainer.GetChild(i);
                                Image trophyImage = slot.GetComponent<Image>();
                                trophyImage.sprite = GetTrophySprite(trophyId);
                                trophyImage.color = Color.white;
                            }
                        }
                    }
                }
                else
                {
                    tradeButton.gameObject.SetActive(false);
                    if (bioPanel != null)
                        bioPanel.SetActive(false); // Hide bioPanel for non-players
                }

                // Guild invite button logic
                if (target is Player && player.guild.InGuild())
                {
                    guildInviteButton.gameObject.SetActive(true);
                    guildInviteButton.interactable = !((Player)target).guild.InGuild() &&
                                                     player.guild.guild.CanInvite(player.name, target.name) &&
                                                     NetworkTime.time >= player.nextRiskyActionTime &&
                                                     distance <= player.interactionRange;
                    guildInviteButton.onClick.SetListener(() =>
                    {
                        player.guild.CmdInviteTarget();
                    });
                }
                else guildInviteButton.gameObject.SetActive(false);

                // Party invite button logic
                if (target is Player)
                {
                    partyInviteButton.gameObject.SetActive(true);
                    partyInviteButton.interactable = (!player.party.InParty() || !player.party.party.IsFull()) &&
                                                     !((Player)target).party.InParty() &&
                                                     NetworkTime.time >= player.nextRiskyActionTime &&
                                                     distance <= player.interactionRange;
                    partyInviteButton.onClick.SetListener(() =>
                    {
                        player.party.CmdInvite(target.name);
                    });
                }
                else partyInviteButton.gameObject.SetActive(false);
            }
            else
            {
                // Hide everything when no target
                panel.SetActive(false);
                if (bioPanel != null)
                    bioPanel.SetActive(false);
            }
        }
        else
        {
            // Hide everything when no local player
            panel.SetActive(false);
            if (bioPanel != null)
                bioPanel.SetActive(false);
        }
    }

    // Method to close both the BioWindow and UITarget panel
    public void CloseBioWindow()
    {
        Player player = Player.localPlayer;
        if (player != null)
        {
            if (bioPanel != null && bioPanel.activeSelf)
            {
                bioPanel.SetActive(false);
                panel.SetActive(false);

                // Call the player's command to clear targets
                player.CmdClearPlayerTargets();
            }
        }
    }

    // Add a helper method to hide the target panel
    public void HideTargetPanel()
    {
        panel.SetActive(false);
        if (bioPanel != null)
            bioPanel.SetActive(false);
    }





    private Sprite GetTrophySprite(int trophyId)
    {
        // Fetch the sprite from your trophy database
        return TrophyDatabase.Instance?.GetSprite(trophyId);
    }
}