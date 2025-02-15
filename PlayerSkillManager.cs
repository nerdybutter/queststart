using UnityEngine;
using Mirror;

public class PlayerSkillManager : NetworkBehaviour
{
    [Header("Skill Settings")]
    public string slashSkillName = "Slash";
    public string thrustSkillName = "Thrust";
    public string heavyArmsSkillName = "Heavy Arms";
    public string swordsmanshipSkillName = "Swordsmanship";
    public string fencingSkillName = "Fencing";

    [Header("RNG Chances (0 to 1)")]
    [Range(0f, 1f)] public float slashLevelUpChance = 0.5f; // 50% chance to level up
    [Range(0f, 1f)] public float thrustLevelUpChance = 0.3f; // 30% chance
    [Range(0f, 1f)] public float heavyArmsLevelUpChance = 0.2f; // 20% chance
    [Range(0f, 1f)] public float swordsmanshipLevelUpChance = 0.4f; // 40% chance
    [Range(0f, 1f)] public float fencingLevelUpChance = 0.1f; // 10% chance

    [Server]
    public void OnMonsterKilled(Player player, Monster monster)
    {
        if (player == null || monster == null) return;

        // Randomly level up Slash
        TryLevelUpSkill(player, slashSkillName, slashLevelUpChance);

        // Check and level up Thrust
        if (GetSkillLevel(player, slashSkillName) >= 30)
        {
            TryLevelUpSkill(player, thrustSkillName, thrustLevelUpChance);
        }

        // Check and level up Heavy Arms
        if (GetSkillLevel(player, thrustSkillName) >= 60)
        {
            TryLevelUpSkill(player, heavyArmsSkillName, heavyArmsLevelUpChance);
        }

        // Check and level up Swordsmanship
        if (GetSkillLevel(player, slashSkillName) >= 50)
        {
            TryLevelUpSkill(player, swordsmanshipSkillName, swordsmanshipLevelUpChance);
        }

        // Check and level up Fencing
        if (GetSkillLevel(player, swordsmanshipSkillName) >= 20)
        {
            TryLevelUpSkill(player, fencingSkillName, fencingLevelUpChance);
        }
    }

    [Server]
    private void TryLevelUpSkill(Player player, string skillName, float chance)
    {
        if (Random.value <= chance)
        {
            int currentLevel = GetSkillLevel(player, skillName);
            if (currentLevel < 100)
            {
                // Level up the skill
                LevelUpSkill(player, skillName);

                // Add bonus damage every 10 levels
                if ((currentLevel + 1) % 10 == 0)
                {
                    AddBonusDamage(player, 1); // Add +1 damage every 10 levels
                }
            }
        }
    }

    [Server]
    private void LevelUpSkill(Player player, string skillName)
    {
        if (player.skills is PlayerSkills playerSkills)
        {
            int skillIndex = playerSkills.GetSkillIndexByName(skillName);
            if (skillIndex != -1)
            {
                Skill skill = playerSkills.skills[skillIndex];
                skill.level++;
                playerSkills.skills[skillIndex] = skill; // SyncList update
                Debug.Log($"Skill {skillName} leveled up to {skill.level}");
            }
        }
    }

    [Server]
    private void AddBonusDamage(Player player, int amount)
    {
        if (player.combat is Combat playerCombat)
        {
            // Modify baseDamage
            playerCombat.baseDamage.baseValue += amount; // Increment the base value
            Debug.Log($"Bonus damage added. New base damage: {playerCombat.baseDamage.baseValue}");
        }
    }



    [Server]
    private int GetSkillLevel(Player player, string skillName)
    {
        if (player.skills is PlayerSkills playerSkills)
        {
            return playerSkills.GetSkillLevel(skillName);
        }
        return 0;
    }
}
