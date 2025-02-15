// for targetless projectiles that are fired into a general direction.
using UnityEngine;
using Mirror;

[CreateAssetMenu(menuName = "uMMORPG Skill/Targetless Projectile", order = 999)]
public class TargetlessProjectileSkill : DamageSkill
{
    [Header("Projectile")]
    public TargetlessProjectileSkillEffect projectile; // Arrows, Bullets, Fireballs, ...

    bool HasRequiredWeaponAndAmmo(Entity caster)
    {
        if (caster is Player player)
        {
            // Check if a bow is equipped
            int weaponIndex = player.equipment.GetEquippedWeaponIndex();

            if (weaponIndex != -1)
            {
                var weaponSlot = player.equipment.slots[weaponIndex];

                // Debug: Log weapon information
                Debug.Log($"Weapon equipped in slot {weaponIndex}: {weaponSlot.item.data.name}");

                if (weaponSlot.item.data is WeaponItem)
                {
                    // Check if there is ammo in the ammo slot
                    if (player.ammoSlot.amount > 0)
                    {
                        // Debug: Log ammo information
                        Debug.Log($"Ammo equipped: {player.ammoSlot.item.data.name}, Amount: {player.ammoSlot.amount}");
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning("No ammo equipped or ammo amount is 0.");
                        return false;
                    }
                }
                else
                {
                    Debug.LogWarning($"Equipped weapon is not a valid WeaponItem: {weaponSlot.item.data.name}");
                }
            }
            else
            {
                Debug.LogWarning("No weapon equipped.");
            }
        }
        else
        {
            Debug.LogWarning("Entity is not a Player.");
        }

        return false; // No valid weapon or entity is not a player
    }



    void ConsumeRequiredWeaponsAmmo(Entity caster)
    {
        if (caster is Player player)
        {
            // Consume one unit of ammo if available
            if (player.ammoSlot.amount > 0)
            {
                player.ammoSlot.DecreaseAmount(1);
                Debug.Log("Consumed 1 ammo from the ammo slot.");
            }
        }
    }


    public override bool CheckSelf(Entity caster, int skillLevel)
    {
        // check base and ammo
        return base.CheckSelf(caster, skillLevel) &&
               HasRequiredWeaponAndAmmo(caster);
    }

    public override bool CheckTarget(Entity caster)
    {
        // no target necessary, but still set to self so that LookAt(target)
        // doesn't cause the player to look at a target that doesn't even matter
        caster.target = caster;
        return true;
    }

    public override bool CheckDistance(Entity caster, int skillLevel, out Vector2 destination)
    {
        // can cast anywhere
        destination = (Vector2)caster.transform.position + caster.lookDirection;
        return true;
    }

    public override void Apply(Entity caster, int skillLevel, Vector2 direction)
    {
        Debug.Log($"Applying TargetlessProjectileSkill for {caster.name}...");

        // Consume ammo if needed
        ConsumeRequiredWeaponsAmmo(caster);

        if (projectile != null)
        {
            Debug.Log($"Spawning projectile: {projectile.name}...");
            GameObject go = Instantiate(projectile.gameObject, caster.skills.effectMount.position, caster.skills.effectMount.rotation);
            TargetlessProjectileSkillEffect effect = go.GetComponent<TargetlessProjectileSkillEffect>();

            if (effect == null)
            {
                Debug.LogError("Projectile prefab does not have TargetlessProjectileSkillEffect component!");
                return;
            }

            effect.target = caster.target;
            effect.caster = caster;
            effect.damage = damage.Get(skillLevel);
            effect.stunChance = stunChance.Get(skillLevel);
            effect.stunTime = stunTime.Get(skillLevel);
            effect.direction = direction; // Fly in the given direction
            NetworkServer.Spawn(go);
        }
        else
        {
            Debug.LogWarning($"{name}: missing projectile prefab!");
        }
    }


}
