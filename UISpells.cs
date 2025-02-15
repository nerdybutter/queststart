using UnityEngine;
using UnityEngine.UI;
using Mirror;

public partial class UISpells : MonoBehaviour
{
    public KeyCode hotKey = KeyCode.K;
    public GameObject panel;
    public UISkillSlot slotPrefab;
    public Transform content;

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
                // Filter spell skills: only show learned spells (those with level > 0)
                var spellSkills = player.skills.skills.FindAll(skill =>
                    (skill.data.isSpell || player.skills.skills.IndexOf(skill) >= 34) && skill.level > 0);

                // Instantiate/destroy enough slots for spell skills
                UIUtils.BalancePrefabs(slotPrefab.gameObject, spellSkills.Count, content);

                // Refresh all spell slots
                for (int i = 0; i < spellSkills.Count; ++i)
                {
                    UISkillSlot slot = content.GetChild(i).GetComponent<UISkillSlot>();
                    Skill spell = spellSkills[i];

                    // Set state
                    slot.dragAndDropable.name = (i + 34).ToString();  // Correct the slot index to match actual skill slot number
                    slot.dragAndDropable.dragable = true;

                    // Can we cast it? Checks mana, cooldown, etc.
                    bool canCast = player.skills.CastCheckSelf(spell);

                    // Click event
                    slot.button.interactable = canCast;

                    int slotIndex = i + 34;  // Ensure we use the correct slot index
                    slot.button.onClick.SetListener(() =>
                    {
                        // Try to use the spell based on the corrected slot index
                        ((PlayerSkills)player.skills).TryUse(slotIndex);
                    });

                    // Image
                    slot.image.color = Color.white;
                    slot.image.sprite = spell.image;

                    // Description
                    slot.descriptionText.text = spell.ToolTip(showRequirements: false);

                    // Cooldown overlay
                    float cooldown = spell.CooldownRemaining();
                    slot.cooldownOverlay.SetActive(cooldown > 0);
                    slot.cooldownText.text = cooldown > 0 ? cooldown.ToString("F0") : "";
                    slot.cooldownCircle.fillAmount = spell.cooldown > 0 ? cooldown / spell.cooldown : 0;
                }

                // Spell experience (if needed)
                // spellExperienceText.text = ((PlayerSkills)player.skills).spellExperience.ToString();
            }
        }
        else
        {
            panel.SetActive(false);
        }
    }
}
