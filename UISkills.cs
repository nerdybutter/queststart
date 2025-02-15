// Note: this script has to be on an always-active UI parent, so that we can
// always react to the hotkey.
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public partial class UISkills : MonoBehaviour
{
    public KeyCode hotKey = KeyCode.R;
    public GameObject panel;
    public UISkillSlot slotPrefab;
    public Transform content;
    public Text skillExperienceText;
    public Text scoreText; // UI element to display the player's score

    void Update()
    {
        Player player = Player.localPlayer;
        if (player)
        {
            // hotkey (not while typing in chat, etc.)
            if (Input.GetKeyDown(hotKey) && !UIUtils.AnyInputActive())
                panel.SetActive(!panel.activeSelf);

            // only update the panel if it's active
            if (panel.activeSelf)
            {
                // Update score display
                UpdateScoreDisplay(player);

                // Filter non-spell skills and limit to 34 slots
                var nonSpellSkills = player.skills.skills.FindAll(skill => !skill.data.isSpell).GetRange(0, Mathf.Min(34, player.skills.skills.Count));

                // Instantiate/destroy enough slots for non-spell skills
                UIUtils.BalancePrefabs(slotPrefab.gameObject, nonSpellSkills.Count, content);

                // Refresh all skill slots
                for (int i = 0; i < nonSpellSkills.Count; ++i)
                {
                    UISkillSlot slot = content.GetChild(i).GetComponent<UISkillSlot>();
                    Skill skill = nonSpellSkills[i];
                    bool isPassive = skill.data is PassiveSkill;

                    // Set state
                    slot.dragAndDropable.name = i.ToString();
                    slot.dragAndDropable.dragable = skill.level > 0 && !isPassive;

                    // Can we cast it? Checks mana, cooldown, etc.
                    bool canCast = player.skills.CastCheckSelf(skill);

                    // If movement does NOT support navigation, check distance too
                    if (!player.movement.CanNavigate())
                        canCast &= player.skills.CastCheckDistance(skill, out Vector2 _);

                    // Click event
                    slot.button.interactable = skill.level > 0 && !isPassive && canCast;

                    int icopy = i;
                    slot.button.onClick.SetListener(() =>
                    {
                        // Try to use the skill or walk closer if needed
                        ((PlayerSkills)player.skills).TryUse(icopy);
                    });

                    // Image
                    if (skill.level > 0)
                    {
                        slot.image.color = Color.white;
                        slot.image.sprite = skill.image;
                    }
                    else
                    {
                        slot.image.color = Color.gray;
                    }

                    // Description
                    slot.descriptionText.text = skill.ToolTip(showRequirements: skill.level == 0);

                    // Cooldown overlay
                    float cooldown = skill.CooldownRemaining();
                    slot.cooldownOverlay.SetActive(skill.level > 0 && cooldown > 0);
                    slot.cooldownText.text = cooldown > 0 ? cooldown.ToString("F0") : "";
                    slot.cooldownCircle.fillAmount = skill.cooldown > 0 ? cooldown / skill.cooldown : 0;
                }

                // Skill experience
                skillExperienceText.text = ((PlayerSkills)player.skills).skillExperience.ToString();
            }
        }
        else
        {
            panel.SetActive(false);
        }
    }

    public void UpdateScoreDisplay(Player player)
    {
        PlayerScoreManager scoreManager = player.GetComponent<PlayerScoreManager>();
        if (scoreManager != null)
        {
            scoreText.text = $"Score: {scoreManager.playerScore}";
        }
        else
        {
            scoreText.text = "Score: N/A";
        }
    }
}
