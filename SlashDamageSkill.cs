using UnityEngine;
using Mirror;

[CreateAssetMenu(menuName = "uMMORPG Skill/Slash Damage", order = 999)]
public class SlashDamageSkill : DamageSkill
{
    public string requiredMiningWeapon = "Pickaxe";
    public string requiredFishingWeapon = "Fishing Pole";
    public string bowWeapon = "Sun Bow";
    public GameObject FishingEffectPrefab;

    public override bool CheckTarget(Entity caster)
    {
        caster.target = caster;
        return true;
    }

    public override bool CheckDistance(Entity caster, int skillLevel, out Vector2 destination)
    {
        destination = (Vector2)caster.transform.position + caster.lookDirection;
        return true;
    }

    public override void Apply(Entity caster, int skillLevel, Vector2 direction)
    {
        if (!(caster is Player player)) return;

        // If unarmed, execute Punch Attack
        if (player.equipment.GetEquippedWeaponIndex() == -1)
        {
            PerformPunchAttack(player, direction); // Pass direction argument
            return;
        }

        if (HasBowEquipped(caster))
        {
            FireProjectileWithEquippedArrow(caster, skillLevel, direction);
            return;
        }

        float range = castRange.Get(skillLevel);
        Vector2 center = (Vector2)caster.transform.position + direction * range / 2;
        Vector2 size = new Vector2(range, range);

        Collider2D[] colliders = Physics2D.OverlapBoxAll(center, size, 0);
        foreach (Collider2D co in colliders)
        {
            if (HasMiningWeapon(caster))
            {
                MiningRock rock = co.GetComponentInParent<MiningRock>();
                if (rock != null && rock.CanBeMined())
                {
                    if (NetworkServer.active)
                    {
                        rock.Mine(player);
                    }
                    break;
                }
            }
            else if (HasFishingWeapon(caster))
            {
                FishingSpot fish = co.GetComponentInParent<FishingSpot>();
                if (fish != null && fish.CanBeFished())
                {
                    if (NetworkServer.active)
                    {
                        GameObject fishingEffect = Instantiate(FishingEffectPrefab, fish.transform.position, Quaternion.identity);
                        NetworkServer.Spawn(fishingEffect);
                        Destroy(fishingEffect, 0.5f);
                        fish.Fish(player);
                    }
                    break;
                }
            }

            Entity candidate = co.GetComponentInParent<Entity>();
            if (candidate != null && caster.CanAttack(candidate))
            {
                caster.combat.DealDamageAt(candidate, caster.combat.damage + damage.Get(skillLevel));
            }
        }
    }

    [Server] // Ensures this method only runs on the server
    private void PerformPunchAttack(Player player, Vector2 direction)
    {
        float range = 0.5f; // Define punch range
        Vector2 center = (Vector2)player.transform.position + direction * range / 2;
        Vector2 size = new Vector2(range, range);

        Collider2D[] colliders = Physics2D.OverlapBoxAll(center, size, 0);
        foreach (Collider2D co in colliders)
        {
            Entity target = co.GetComponentInParent<Entity>();
            if (target != null && player.CanAttack(target))
            {
                // Get Punch and Martial Arts skill levels
                int punchLevel = (player.skills as PlayerSkills)?.GetSkillLevel("Punch") ?? 0;
                int martialArtsLevel = (player.skills as PlayerSkills)?.GetSkillLevel("Martial Arts") ?? 0;

                // Base attack power is random 1-3
                int attackPower = Random.Range(1, 4); // 1, 2, or 3

                // Apply Punch bonuses
                if (punchLevel > 5) attackPower += 1;
                if (punchLevel > 10) attackPower += 1;
                if (punchLevel > 30) attackPower += 1;
                if (punchLevel > 40) attackPower += 1;
                if (punchLevel > 60) attackPower += 1;
                if (punchLevel > 70) attackPower += 1;
                if (punchLevel > 90) attackPower += 1;

                // Apply Martial Arts bonuses
                if (martialArtsLevel > 20) attackPower += 1;
                if (martialArtsLevel > 40) attackPower += 1;
                if (martialArtsLevel > 60) attackPower += 1;
                if (martialArtsLevel > 70) attackPower += 1;
                if (martialArtsLevel > 90) attackPower += 1;
                if (martialArtsLevel > 98) attackPower += 1;

                // Deal Punch damage
                player.combat.DealDamageAt(target, attackPower);

                // Store that the player used Punch (for tracking in OnMonsterKilled)
                player.lastAttackUsed = "Punch";
            }
        }
    }



    [Server]
    private void FireProjectileWithEquippedArrow(Entity caster, int skillLevel, Vector2 direction)
    {
        if (caster is Player player)
        {
            // Ensure the ammo slot is valid and has ammo
            if (player.equipment.slots.Count > 9 && player.equipment.slots[9].amount > 0)
            {
                // Get the equipped arrow item from slot 9
                ItemSlot ammoSlot = player.equipment.slots[9];
                Item arrowItem = ammoSlot.item;

                // Check if the arrow item has a projectile effect
                if (arrowItem.data is AmmoItem ammoData && ammoData.projectileEffectPrefab != null)
                {
                    // Calculate the damage with Archery passive skill bonus
                    int bonusDamage = GetArcheryBonusDamage(player);
                    int finalDamage = damage.Get(skillLevel) + bonusDamage;

                    // Instantiate and configure the projectile
                    GameObject projectileInstance = Instantiate(ammoData.projectileEffectPrefab, caster.transform.position, Quaternion.identity);
                    TargetlessProjectileSkillEffect effect = projectileInstance.GetComponent<TargetlessProjectileSkillEffect>();

                    if (effect != null)
                    {
                        effect.caster = caster;
                        effect.damage = finalDamage;
                        effect.direction = direction;
                        NetworkServer.Spawn(projectileInstance);

                        // Decrease the ammo count
                        ammoSlot.DecreaseAmount(1);
                        player.equipment.slots[9] = ammoSlot;  // Update the slot after decreasing

                        // Request the client to try leveling up the Archery skill
                        if (Random.Range(0, 16) == 0)
                        {
                            UpgradeSkillServer(player, "Archery");
                        }
                    }
                }
            }
        }
    }

    [Server]
    private void UpgradeSkillServer(Player player, string skillName)
    {
        if (player != null && player.skills is PlayerSkills playerSkills)
        {
            playerSkills.LevelUpSkill(skillName);
        }
    }

    private int GetArcheryBonusDamage(Player player)
    {
        // Ensure the player has the "Archery" skill
        if (player.skills is PlayerSkills playerSkills)
        {
            int archeryLevel = playerSkills.GetSkillLevel("Archery");  // GetSkillLevel returns an int
                                                                       // Bonus: 1 extra damage for every 10 levels
            return archeryLevel / 10;  // 1 damage for every 10 levels
        }

        return 0;  // No bonus if the skill doesn't exist
    }

    private bool HasBowEquipped(Entity caster)
    {
        return HasSpecificWeapon(caster, bowWeapon);
    }

    private bool HasMiningWeapon(Entity caster)
    {
        return HasSpecificWeapon(caster, requiredMiningWeapon);
    }

    private bool HasFishingWeapon(Entity caster)
    {
        return HasSpecificWeapon(caster, requiredFishingWeapon);
    }

    private bool HasSpecificWeapon(Entity caster, string weaponName)
    {
        if (caster is Player player)
        {
            int weaponIndex = player.equipment.GetEquippedWeaponIndex();
            if (weaponIndex != -1 && player.equipment.slots != null)
            {
                string equippedWeaponName = player.equipment.slots[weaponIndex].item.data.name.ToLowerInvariant();
                return equippedWeaponName.Contains(weaponName.ToLowerInvariant());
            }
        }
        return false;
    }
}
