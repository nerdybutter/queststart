using UnityEngine;
using Mirror;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(Level))]
[RequireComponent(typeof(Movement))]
[RequireComponent(typeof(PlayerParty))]
[DisallowMultipleComponent]
public class PlayerSkills : Skills
{
    [Header("Components")]
    public Level level;
    public Movement movement;
    public PlayerParty party;
    private Skills skillsComponent;

    [Header("Skill Experience")]
    [SyncVar] public long skillExperience = 0;
   
    private void Awake()
    {
        // Reference the Skills component
        skillsComponent = GetComponent<Skills>();

    }
    // always store lookDirection at the time of casting.
    // this is the only 100% accurate way since player movement is only synced
    // in intervals and look direction from velocity is never 100% accurate.
    // fixes https://github.com/vis2k/uMMORPG2D/issues/19
    // => only necessary for players. all other entities are server controlled.
    Vector2 _currentSkillDirection = Vector2.down;
    protected override Vector2 currentSkillDirection => _currentSkillDirection;

    void Start()
    {
        if (!isServer && !isClient) return;
       // SeparateSpellsFromSkills();
        if (isServer)
        {
            for (int i = 0; i < buffs.Count; ++i)
                if (buffs[i].BuffTimeRemaining() > 0)
                    buffs[i].data.SpawnEffect(entity, entity);
        }
    }

    // IMPORTANT
    // for targetless skills we always need look direction at the exact moment
    // when the skill is casted on the client.
    //
    // using the look direction on server is never 100% accurate.
    // it's assumed from movement.velocity, but we only sync client position to
    // server every 'interval'. it's never 100%.
    //
    // for example, consider this movement on client:
    //    ----------|
    //              |
    //
    // which might be come this movement on server:
    //    --------\
    //             \
    //
    // if the server's destination is set to the last position while it hasn't
    // reached the second last position yet (it'll just go diagonal, hence not
    // change the move direction from 'right' to 'down'.
    //
    // the only 100% accurate solution is to always pass direction at the exact
    // moment of the cast.
    //
    // see also: https://github.com/vis2k/uMMORPG2D/issues/19
    [Command]
    public void CmdUse(int skillIndex, Vector2 direction)
    {
        // validate
        if ((entity.state == "IDLE" || entity.state == "MOVING" || entity.state == "CASTING") &&
            0 <= skillIndex && skillIndex < skills.Count)
        {
            // skill learned and can be casted?
            if (skills[skillIndex].level > 0 && skills[skillIndex].IsReady())
            {
                // set skill index to cast next.
                currentSkill = skillIndex;

                // set look direction to use when the cast starts.
                // DO NOT set entity.lookDirection instead. it would be over-
                // written by Entity.Update before the actual cast starts!
                // fixes https://github.com/vis2k/uMMORPG2D/issues/19
                _currentSkillDirection = direction;

                // let's set it anyway for visuals.
                // even if it might be overwritten.
                entity.lookDirection = direction;
            }
        }
    }



    public int GetSkillLevel(string skillName)
    {
        foreach (Skill skill in skills)
        {
            if (skill.name == skillName)
            {
                return skill.level;
            }
        }
        return 0; // Return 0 if the skill is not found
    }

    // helper function: try to use a skill and walk into range if necessary
    [Client]
    public void TryUse(int skillIndex, bool ignoreState = false)
    {
        // only if not casting already
        // (might need to ignore that when coming from pending skill where
        //  CASTING is still true)
        if (entity.state != "CASTING" || ignoreState)
        {
            Skill skill = skills[skillIndex];

            // fix skill auto-recasts:
            // Server calls Skills::RpcCastFinished when castTimeRemaining==0.
            // Rpc may arrive before the SyncList or NetworkTime updates,
            // so when trying to auto re-cast, CastTimeRemaining is still a bit >0.
            // => RpcCastFinished is only ever called exactly when castRemaining==0,
            //    so let's simply ignore the ready check here by passing 'ignoreState',
            //    which is 'true' when auto recasting the next skill after one was finished.
            bool checkSelf = CastCheckSelf(skill, !ignoreState);
            bool checkTarget = CastCheckTarget(skill);
            if (checkSelf && checkTarget)
            {
                // check distance between self and target
                Vector2 destination;
                if (CastCheckDistance(skill, out destination))
                {
                    // cast
                    CmdUse(skillIndex, ((Player)entity).lookDirection);
                }
                else
                {
                    // move to the target first
                    // (use collider point(s) to also work with big entities)
                    float stoppingDistance = skill.castRange * ((Player)entity).attackToMoveRangeRatio;
                    movement.Navigate(destination, stoppingDistance);

                    // use skill when there
                    ((Player)entity).useSkillWhenCloser = skillIndex;
                }
            }
        }
        else
        {
            ((Player)entity).pendingSkill = skillIndex;
        }
    }

    public bool HasLearned(string skillName, out int level)
    {
        foreach (Skill skill in skills)
        {
            if (skill.name == skillName && skill.level > 0)
            {
                level = skill.level;
                return true;
            }
        }

        level = 0;
        return false;
    }


    public bool HasLearned(string skillName)
    {
        // has this skill with at least level 1 (=learned)?
        return HasLearnedWithLevel(skillName, 1);
    }



    public bool HasLearnedWithLevel(string skillName, int skillLevel)
    {
        // (avoid Linq because it is HEAVY(!) on GC and performance)
        foreach (Skill skill in skills)
            if (skill.level >= skillLevel && skill.name == skillName)
                return true;
        return false;
    }

    [Command]
    public void CmdLevelUpSkill(string skillName)
    {
        if (isServer)
        {
            LevelUpSkill(skillName);
        }
    }

    [Server]
    public void LevelUpSkill(string skillName)
    {
        if (skillsComponent.skills == null || skillsComponent.skills.Count == 0) return;

        int skillIndex = GetSkillIndexByName(skillName);
        if (skillIndex != -1)
        {
            Skill skill = skillsComponent.skills[skillIndex];

            if (skill.level < skill.data.maxLevel)
            {
                skill.level++;
                skillsComponent.skills[skillIndex] = skill; // Update the SyncList

                // Send chat message to the player
                string message = $"You have advanced in the art of {skillName}! (Level {skill.level})";
                PlayerChat playerChat = GetComponent<PlayerChat>();
                if (playerChat != null)
                {
                    playerChat.TargetMsgLocal(name, message);
                }


                // Check if the skill is mastered
                if (skill.level == skill.data.maxLevel)
                {
                    // Send mastery message
                    string masteryMessage = $"Congratulations! You have mastered the art of {skillName}!";
                    if (playerChat != null)
                    {
                        playerChat.TargetMsgLocal(name, masteryMessage);
                    }
                    else
                    {
                        Debug.LogWarning("PlayerChat component not found on this player.");
                    }

                    // Award the trophy for mastering this skill
                    Player player = GetComponent<Player>();
                    if (player != null)
                    {
                        int trophyId = skill.data.trophyId; // Assuming ScriptableSkill has a `trophyId` field
                        player.AddTrophy(trophyId); // Call the new server method instead of CmdAddTrophy

                    }
                    else
                    {
                        Debug.LogWarning("Player component not found.");
                    }
                }

                // Update the player's score in the database
                PlayerScoreManager scoreManager = GetComponent<PlayerScoreManager>();
                if (scoreManager != null)
                {
                    string playerName = GetComponent<Player>().name; // Assuming the player's name is accessible here
                    scoreManager.CalculatePlayerScore(); // Ensure the score is recalculated
                    int totalScore = scoreManager.playerScore; // Get the updated score
                    Database.singleton.UpdateCharacterScore(playerName, totalScore);
                }
                else
                {
                    Debug.LogWarning("PlayerScoreManager component not found.");
                }
            }
        }
    }


    [Command]
    private void CmdUpdateScore(string playerName, int score)
    {
        Database.singleton.UpdateCharacterScore(playerName, score);
    }

}
