using UnityEngine;

[CreateAssetMenu(menuName = "uMMORPG Skill/Lumberjacking Skill", order = 999)]
public class LumberjackingSkill : DamageSkill
{
    public string requiredWeaponName = "Axe"; // Name of the required weapon for lumberjacking

    public override bool CheckTarget(Entity caster)
    {
        Player player = caster as Player;

        // Ensure the player has a weapon in the "WeaponSword" category equipped
        if (player != null)
        {
            // Check if the equipped weapon is the axe
            int weaponIndex = player.equipment.GetEquippedWeaponIndex();
            if (weaponIndex != -1 && player.equipment.slots[weaponIndex].item.data.name == requiredWeaponName)
            {
                caster.target = caster;
                return true;
            }

            // If not the axe, display an error message
            player.chat.TargetMsgInfo("You must have an axe equipped to chop trees!");
        }

        // No valid weapon equipped
        return false;
    }

    public override bool CheckDistance(Entity caster, int skillLevel, out Vector2 destination)
    {
        // Can chop in any direction within range
        destination = (Vector2)caster.transform.position + caster.lookDirection;
        return true;
    }

    public override void Apply(Entity caster, int skillLevel, Vector2 direction)
    {
        // Ensure the caster is a Player
        if (caster is Player player)
        {
            // Cast a box or circle to find nearby choppable trees
            float range = castRange.Get(skillLevel);
            Vector2 center = (Vector2)player.transform.position + direction * range / 2;
            Vector2 size = new Vector2(range, range);
            Collider2D[] colliders = Physics2D.OverlapBoxAll(center, size, 0);

            foreach (Collider2D co in colliders)
            {
                ChoppableTree tree = co.GetComponentInParent<ChoppableTree>();
                if (tree != null && tree.CanBeChopped())
                {
                    // Pass the player to the Chop method
                    tree.Chop(player);
                    break; // Only chop one tree per skill usage
                }
            }
        }
        else
        {
            Debug.LogWarning("Caster is not a Player, lumberjacking skill cannot be applied.");
        }
    }
}
