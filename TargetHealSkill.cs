using UnityEngine;

[CreateAssetMenu(menuName = "uMMORPG Skill/Target Heal", order = 999)]
public class TargetHealSkill : HealSkill
{
    public bool canHealSelf = true;
    public bool canHealOthers = false;

    // Helper function to determine bonus healing power based on Blessing level
    int CalculateHealingBonus(Entity caster)
    {
        int blessingLevel = 0;

        // Check if the caster has learned the Blessing skill and get its level
        if (caster is Player player)
        {
            blessingLevel = (player.skills as PlayerSkills)?.GetSkillLevel("Blessing") ?? 0;

            // Apply bonus increments based on the Blessing level
            int bonus = 0;
            if (blessingLevel > 10) bonus += 1;
            if (blessingLevel > 20) bonus += 1;
            if (blessingLevel > 30) bonus += 1;
            if (blessingLevel > 50) bonus += 1;
            if (blessingLevel > 70) bonus += 1;
            if (blessingLevel > 80) bonus += 1;
            if (blessingLevel > 90) bonus += 1;
            if (blessingLevel > 97) bonus += 1;

            return bonus;
        }

        return 0;  // No bonus if Blessing isn't learned
    }

    // Helper function to determine the target that the skill will be cast on
    Entity CorrectedTarget(Entity caster)
    {
        if (caster.target == null) return canHealSelf ? caster : null;
        if (caster.target == caster) return canHealSelf ? caster : null;
        if (caster.target.GetType() == caster.GetType()) return canHealOthers ? caster.target : (canHealSelf ? caster : null);
        return canHealSelf ? caster : null;
    }

    public override bool CheckTarget(Entity caster)
    {
        caster.target = CorrectedTarget(caster);
        return caster.target != null && caster.target.health.current > 0;
    }

    public override bool CheckDistance(Entity caster, int skillLevel, out Vector2 destination)
    {
        Entity target = CorrectedTarget(caster);
        if (target != null)
        {
            destination = target.collider.ClosestPointOnBounds(caster.transform.position);
            return Utils.ClosestDistance(caster.collider, target.collider) <= castRange.Get(skillLevel);
        }

        destination = caster.transform.position;
        return false;
    }

    public override void Apply(Entity caster, int skillLevel, Vector2 direction)
    {
        if (caster.target != null && caster.target.health.current > 0)
        {
            // Calculate base healing power
            int healingPower = healsHealth.Get(skillLevel);

            // Add bonus healing power from Blessing
            healingPower += CalculateHealingBonus(caster);

            // Apply healing
            caster.target.health.current += healingPower;
            caster.target.mana.current += healsMana.Get(skillLevel);

            // Show effect on target
            SpawnEffect(caster, caster.target);
        }
    }
}
