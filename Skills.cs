// skill system for all entities (players, monsters, npcs, towers, ...)
using System;
using UnityEngine;
using UnityEngine.Events;
using Mirror;

// serializable events
[Serializable] public class UnityEventSkill : UnityEvent<Skill> { }

[DisallowMultipleComponent]
public abstract class Skills : NetworkBehaviour, IHealthBonus, IManaBonus, ICombatBonus
{
    [Header("Components")]
    public Entity entity;
    public Health health;
    public Mana mana;

    // 'skillTemplates' are the available skills (first one is default attack)
    // 'skills' are the loaded skills with cooldowns etc.
    [Header("Skills & Buffs")]
    public ScriptableSkill[] skillTemplates;
    public readonly SyncList<Skill> skills = new SyncList<Skill>();
    public readonly SyncList<Skill> spells = new SyncList<Skill>();
    public readonly SyncList<Buff> buffs = new SyncList<Buff>(); // active buffs

    // effect mount is where the arrows/fireballs/etc. are spawned
    // -> can be overwritten, e.g. for mages to set it to the weapon's effect
    //    mount
    // -> assign to right hand if in doubt!
#pragma warning disable CS0649 // Field is never assigned to
    [SerializeField] Transform _effectMount;
#pragma warning restore CS0649 // Field is never assigned to
    public virtual Transform effectMount
    {
        get { return _effectMount; }
        set { _effectMount = value; }
    }

    [Header("Events")]
    public UnityEventSkill onSkillCastStarted;
    public UnityEventSkill onSkillCastFinished;

    // current skill (synced because we need it as an animation parameter)
    [SyncVar, HideInInspector] public int currentSkill = -1;

    // get look direction for current skill.
    // == Entity.lookDirection for all server controlled entities.
    // only for players we need to pass it in Cmd. see CmdUse comments.
    protected virtual Vector2 currentSkillDirection => entity.lookDirection;

    // boni ////////////////////////////////////////////////////////////////////
    public int GetHealthBonus(int baseHealth)
    {
        // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
        int passiveBonus = 0;
        foreach (Skill skill in skills)
            if (skill.level > 0 && skill.data is PassiveSkill passiveSkill)
                passiveBonus += passiveSkill.healthMaxBonus.Get(skill.level);

        int buffBonus = 0;
        foreach (Buff buff in buffs)
            buffBonus += buff.healthMaxBonus;

        return passiveBonus + buffBonus;
    }

    public int GetHealthRecoveryBonus()
    {
        // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
        float passivePercent = 0;
        foreach (Skill skill in skills)
            if (skill.level > 0 && skill.data is PassiveSkill passiveSkill)
                passivePercent += passiveSkill.healthPercentPerSecondBonus.Get(skill.level);

        float buffPercent = 0;
        foreach (Buff buff in buffs)
            buffPercent += buff.healthPercentPerSecondBonus;

        return Convert.ToInt32(passivePercent * health.max) + Convert.ToInt32(buffPercent * health.max);
    }

    public int GetManaBonus(int baseMana)
    {
        // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
        int passiveBonus = 0;
        foreach (Skill skill in skills)
            if (skill.level > 0 && skill.data is PassiveSkill passiveSkill)
                passiveBonus += passiveSkill.manaMaxBonus.Get(skill.level);

        int buffBonus = 0;
        foreach (Buff buff in buffs)
            buffBonus += buff.manaMaxBonus;

        return passiveBonus + buffBonus;
    }

    public int GetManaRecoveryBonus()
    {
        // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
        float passivePercent = 0;
        foreach (Skill skill in skills)
            if (skill.level > 0 && skill.data is PassiveSkill passiveSkill)
                passivePercent += passiveSkill.manaPercentPerSecondBonus.Get(skill.level);

        float buffPercent = 0;
        foreach (Buff buff in buffs)
            buffPercent += buff.manaPercentPerSecondBonus;

        return Convert.ToInt32(passivePercent * mana.max) + Convert.ToInt32(buffPercent * mana.max);
    }
    
    public int GetDamageBonus()
    {
        int passiveBonus = 0;

        foreach (Skill skill in skills)
        {
            if (skill.level > 0 && skill.data is PassiveSkill passiveSkill)
            {
                passiveBonus += passiveSkill.damageBonus.Get(skill.level);
            }
        }

        int buffBonus = 0;
        foreach (Buff buff in buffs)
        {
            buffBonus += buff.damageBonus;
        }

        return passiveBonus + buffBonus;
    }


    public int GetDefenseBonus()
    {
        // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
        int passiveBonus = 0;
        foreach (Skill skill in skills)
            if (skill.level > 0 && skill.data is PassiveSkill passiveSkill)
                passiveBonus += passiveSkill.defenseBonus.Get(skill.level);

        int buffBonus = 0;
        foreach (Buff buff in buffs)
            buffBonus += buff.defenseBonus;

        return passiveBonus + buffBonus;
    }

    public float GetCriticalChanceBonus()
    {
        // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
        float passiveBonus = 0;
        foreach (Skill skill in skills)
            if (skill.level > 0 && skill.data is PassiveSkill passiveSkill)
                passiveBonus += passiveSkill.criticalChanceBonus.Get(skill.level);

        float buffBonus = 0;
        foreach (Buff buff in buffs)
            buffBonus += buff.criticalChanceBonus;

        return passiveBonus + buffBonus;
    }

    public float GetBlockChanceBonus()
    {
        // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
        float passiveBonus = 0;
        foreach (Skill skill in skills)
            if (skill.level > 0 && skill.data is PassiveSkill passiveSkill)
                passiveBonus += passiveSkill.blockChanceBonus.Get(skill.level);

        float buffBonus = 0;
        foreach (Buff buff in buffs)
            buffBonus += buff.blockChanceBonus;

        return passiveBonus + buffBonus;
    }

    ////////////////////////////////////////////////////////////////////////////
    void Update()
    {
        // only update if it's worth updating (see IsWorthUpdating comments)
        if (isServer && entity.IsWorthUpdating())
            CleanupBuffs();
    }

    // helper function to find a skill index
    public int GetSkillIndexByName(string skillName)
    {
        // (avoid FindIndex to minimize allocations)
        for (int i = 0; i < skills.Count; ++i)
            if (skills[i].name == skillName)
                return i;
        return -1;
    }

    // helper function to find a buff index
    public int GetBuffIndexByName(string buffName)
    {
        // (avoid FindIndex to minimize allocations)
        for (int i = 0; i < buffs.Count; ++i)
            if (buffs[i].name == buffName)
                return i;
        return -1;
    }

    // the first check validates the caster
    // (the skill won't be ready if we check self while casting it. so the
    //  checkSkillReady variable can be used to ignore that if needed)
    // has a weapon (important for projectiles etc.), no cooldown, hp, mp?
    public bool CastCheckSelf(Skill skill, bool checkSkillReady = true) =>
        skill.CheckSelf(entity, checkSkillReady);

    // the second check validates the target and corrects it for the skill if
    // necessary (e.g. when trying to heal an npc, it sets target to self first)
    // (skill shots that don't need a target will just return true if the user
    //  wants to cast them at a valid position)
    public bool CastCheckTarget(Skill skill) =>
        skill.CheckTarget(entity);

    // the third check validates the distance between the caster and the target
    // (target entity or target position in case of skill shots)
    // note: castchecktarget already corrected the target (if any), so we don't
    //       have to worry about that anymore here
    public bool CastCheckDistance(Skill skill, out Vector2 destination) =>
        skill.CheckDistance(entity, out destination);

    // starts casting
    [Server]
    public void StartCast(Skill skill)
    {
        // Perform item checks only for players (ignore monsters or NPCs)
        if (entity is Player player)
        {
            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                CancelCast();
                return;
            }

            // Check for required items
            if (skill.data.requiredItem != null && skill.data.requiredItemQuantity > 0)
            {
                Item requiredItem = new Item(skill.data.requiredItem);

                if (!inventory.HasEnough(requiredItem, skill.data.requiredItemQuantity))
                {
                    string message = $"You need {skill.data.requiredItemQuantity} {skill.data.requiredItem.name} to cast this skill.";
                    PlayerChat playerChat = GetComponent<PlayerChat>();
                    if (playerChat != null)
                    {
                        playerChat.TargetMsgLocal(player.name, message);
                    }

                    // Cancel the cast and notify the client
                    CancelCast();
                    RpcNotifyClientToCancelCast();
                    return;
                }

                // Remove the required items from the inventory on the server
                RpcRequestClientToRemoveItems(skill.data.requiredItem.name.GetStableHashCode(), skill.data.requiredItemQuantity);
            }
        }

        // Start casting and set the casting end time
        skill.castTimeEnd = NetworkTime.time + skill.castTime;
        skills[currentSkill] = skill;

        RpcCastStarted(skill);
    }



    [ClientRpc]
    private void RpcNotifyClientToCancelCast()
    {
        if (isLocalPlayer)
        {
            Player player = entity.GetComponent<Player>();
            player.CmdCancelAction(); // Simulate pressing Escape by triggering CmdCancelAction
            
        }
    }

    [ClientRpc]
    public void RpcRequestClientToRemoveItems(int itemHash, int amount)
    {
        
        PlayerInventory inventory = entity.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogError("PlayerInventory component not found on client.");
            return;
        }

        // Retrieve the item from the hash
        if (!ScriptableItem.All.TryGetValue(itemHash, out ScriptableItem itemData))
        {
            Debug.LogError($"Item with hash {itemHash} not found on the client.");
            return;
        }

        // Convert ScriptableItem to Item for inventory operations
        Item item = new Item(itemData);

        // Call the command to remove items
        inventory.RemoveItem(item, amount);
    }





    [TargetRpc]
    public void TargetRemoveItemsAndContinue(NetworkConnection target, Skill skill)
    {
        PlayerInventory inventory = entity.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogError("PlayerInventory component not found.");
            return;
        }

        // Remove the required item on the client
        if (skill.data.requiredItem != null && skill.data.requiredItemQuantity > 0)
        {
            Item requiredItem = new Item(skill.data.requiredItem);
            inventory.RemoveItemClient(requiredItem, skill.data.requiredItemQuantity);
        }

        // After removing items, proceed to finish cast
        CmdFinishCast(skill);
    }

    [Command]
    public void CmdFinishCast(Skill skill)
    {
        FinishCast(skill);
    }


    // cancel a skill cast properly
    [Server]
    public void CancelCast(bool resetCurrentSkill = true)
    {
        // reset cast time, otherwise if a buff has a 10s cast time and we
        // cancel the cast after 1s, then we would have to wait 9 more seconds
        // before we can attempt to cast it again.
        // -> we cancel it in any case. players will have to wait for 'casttime'
        //    when attempting another cast anyway.
        if (currentSkill != -1)
        {
            Skill skill = skills[currentSkill];
            skill.castTimeEnd = NetworkTime.time - skill.castTime;
            skills[currentSkill] = skill;

            // reset current skill
            if (resetCurrentSkill)
                currentSkill = -1;
        }
    }

    // finishes casting. casting and waiting has to be done in the state machine
    [Server]
    public void FinishCast(Skill skill)
    {
        if (CastCheckSelf(skill, false) && CastCheckTarget(skill))
        {
            // All checks passed: apply the skill and play the sound
            skill.Apply(entity, currentSkillDirection);  // This will play the cast sound and apply the effect
            RpcCastFinished(skill);

            // Decrease mana and start cooldown
            mana.current -= skill.manaCosts;
            skill.cooldownEnd = NetworkTime.time + skill.cooldown;

            // Save modifications
            skills[currentSkill] = skill;

            // Skill leveling logic
            if (entity is Player player)
            {
                PlayerSkills playerSkills = player.GetComponent<PlayerSkills>();
                if (playerSkills != null)
                {
                    foreach (SkillLevelingInfo levelingInfo in skill.data.levelingSkills)
                    {
                        // Use the new overloaded method to check if the skill is learned and get the level
                        if (playerSkills.HasLearned(levelingInfo.skill.name, out int skillLevel))
                        {
                            // Determine RNG chance based on the setting
                            int chance = levelingInfo.useSkillLevelForRng ? skillLevel : levelingInfo.rngChance;

                            // Apply RNG to determine if the skill levels up
                            if (UnityEngine.Random.Range(0, chance) == 0)
                            {
                                playerSkills.LevelUpSkill(levelingInfo.skill.name);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            currentSkill = -1;  // Cancel the skill if checks fail
        }
    }




    // skill cast started rpc for client sided effects
    // note: pass Skill to avoid sync race conditions with indices etc.
    [ClientRpc]
    public void RpcCastStarted(Skill skill)
    {
        // validate: still alive?
        if (health.current > 0)
        {
            // call scriptableskill event
            skill.data.OnCastStarted(entity);

            // call event
            onSkillCastStarted.Invoke(skill);
        }
    }

    // skill cast finished rpc for client sided effects
    // note: pass Skill to avoid sync race conditions with indices etc.
    [ClientRpc]
    public void RpcCastFinished(Skill skill)
    {
        // validate: still alive?
        if (health.current > 0)
        {
            // call scriptableskill event
            skill.data.OnCastFinished(entity);

            // call event
            onSkillCastFinished.Invoke(skill);
        }
    }

    // helper function to add or refresh a buff
    public void AddOrRefreshBuff(Buff buff)
    {
        // reset if already in buffs list, otherwise add
        int index = GetBuffIndexByName(buff.name);
        if (index != -1) buffs[index] = buff;
        else buffs.Add(buff);
    }

    // helper function to remove all buffs that ended
    public void CleanupBuffs()
    {
        for (int i = 0; i < buffs.Count; ++i)
        {
            if (buffs[i].BuffTimeRemaining() == 0)
            {
                buffs.RemoveAt(i);
                --i;
            }
        }
    }


    [Server]
    public void RemoveRequiredItems(Skill skill)
    {
        if (skill.data.requiredItem != null && skill.data.requiredItemQuantity > 0)
        {
            PlayerInventory inventory = entity.GetComponent<PlayerInventory>();

            // Find and remove the required item
            for (int i = 0; i < inventory.slots.Count; i++)
            {
                if (inventory.slots[i].item.name == skill.data.requiredItem.name)
                {
                    inventory.slots[i].DecreaseAmount(skill.data.requiredItemQuantity);

                    // Clear the slot if the amount reaches zero
                    if (inventory.slots[i].amount == 0)
                    {
                        inventory.slots[i] = new ItemSlot();  // Empty slot
                    }

                    // Save changes to the player's inventory on the server
                    Database.singleton.SavePlayerInventory(inventory);
                    break;
                }
            }
        }
    }




    [Server]
    public void OnDeath()
    {
        // clear buffs that shouldn't remain after death
        for (int i = 0; i < buffs.Count; ++i)
        {
            if (!buffs[i].remainAfterDeath)
            {
                buffs.RemoveAt(i);
                --i;
            }
        }

        // reset currently casted skill
        CancelCast();
    }
}
