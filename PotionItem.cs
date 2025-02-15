using System.Collections;
using System.Text;
using UnityEngine;

[CreateAssetMenu(menuName = "uMMORPG Item/Potion", order = 999)]
public class PotionItem : UsableItem
{
    [Header("Potion")]
    public int usageHealth;       // Amount of HP restored instantly
    public int usageMana;         // Amount of MP restored instantly
    public int usageExperience;   // Amount of EXP gained on use
    public int usagePetHealth;    // Amount of HP restored for pet

    [Header("Healing Over Time (Optional)")]
    public int healOverTimeAmount; // Total HP restored over duration
    public float healDuration = 10f; // How long healing lasts
    public float healTickInterval = 2f; // How often healing applies

    [Header("Mana Regeneration Over Time (Optional)")]
    public int manaOverTimeAmount; // Total MP restored over duration
    public float manaDuration = 10f; // How long mana restoration lasts
    public float manaTickInterval = 2f; // How often mana restoration applies

    // usage ///////////////////////////////////////////////////////////////////
    public override void Use(Player player, int inventoryIndex)
    {
        // Always call base function too
        base.Use(player, inventoryIndex);

        // Instant healing effects
        if (usageHealth > 0)
            player.health.current += usageHealth;
        if (usageMana > 0)
            player.mana.current += usageMana;
        if (player.petControl.activePet != null && usagePetHealth > 0)
            player.petControl.activePet.health.current += usagePetHealth;

        // Healing Over Time
        if (healOverTimeAmount > 0 && healDuration > 0 && healTickInterval > 0)
            player.StartCoroutine(ApplyHealingOverTime(player));

        // Mana Regeneration Over Time
        if (manaOverTimeAmount > 0 && manaDuration > 0 && manaTickInterval > 0)
            player.StartCoroutine(ApplyManaOverTime(player));

        // Decrease amount of potion used
        ItemSlot slot = player.inventory.slots[inventoryIndex];
        slot.DecreaseAmount(1);
        player.inventory.slots[inventoryIndex] = slot;
    }

    // Applies healing over time by restoring HP at fixed intervals
    private IEnumerator ApplyHealingOverTime(Player player)
    {
        float timePassed = 0f;
        int healPerTick = Mathf.CeilToInt(healOverTimeAmount / (healDuration / healTickInterval));

        while (timePassed < healDuration)
        {
            player.health.current += healPerTick;
            yield return new WaitForSeconds(healTickInterval);
            timePassed += healTickInterval;
        }
    }

    // Applies mana regeneration over time at fixed intervals
    private IEnumerator ApplyManaOverTime(Player player)
    {
        float timePassed = 0f;
        int manaPerTick = Mathf.CeilToInt(manaOverTimeAmount / (manaDuration / manaTickInterval));

        while (timePassed < manaDuration)
        {
            player.mana.current += manaPerTick;
            yield return new WaitForSeconds(manaTickInterval);
            timePassed += manaTickInterval;
        }
    }

    // tooltip /////////////////////////////////////////////////////////////////
    public override string ToolTip()
    {
        StringBuilder tip = new StringBuilder(base.ToolTip());
        tip.Replace("{USAGEHEALTH}", usageHealth.ToString());
        tip.Replace("{USAGEMANA}", usageMana.ToString());
        tip.Replace("{USAGEEXPERIENCE}", usageExperience.ToString());
        tip.Replace("{USAGEPETHEALTH}", usagePetHealth.ToString());

        // Display Healing Over Time effect in tooltip
        if (healOverTimeAmount > 0)
            tip.AppendLine($"Restores {healOverTimeAmount} HP over {healDuration} sec ({healOverTimeAmount / (healDuration / healTickInterval)} HP per {healTickInterval} sec).");

        // Display Mana Regeneration Over Time effect in tooltip
        if (manaOverTimeAmount > 0)
            tip.AppendLine($"Restores {manaOverTimeAmount} MP over {manaDuration} sec ({manaOverTimeAmount / (manaDuration / manaTickInterval)} MP per {manaTickInterval} sec).");

        return tip.ToString();
    }
}
