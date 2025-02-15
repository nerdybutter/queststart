// All player logic was put into this class. We could also split it into several
// smaller components, but this would result in many GetComponent calls and a
// more complex syntax.
//
// The default Player class takes care of the basic player logic like the state
// machine and some properties like damage and defense.
//
// The Player class stores the maximum experience for each level in a simple
// array. So the maximum experience for level 1 can be found in expMax[0] and
// the maximum experience for level 2 can be found in expMax[1] and so on. The
// player's health and mana are also level dependent in most MMORPGs, hence why
// there are hpMax and mpMax arrays too. We can find out a players's max health
// in level 1 by using hpMax[0] and so on.
//
// The class also takes care of selection handling, which detects 3D world
// clicks and then targets/navigates somewhere/interacts with someone.
//
// Animations are not handled by the NetworkAnimator because it's still very
// buggy and because it can't really react to movement stops fast enough, which
// results in moonwalking. Not synchronizing animations over the network will
// also save us bandwidth
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Mirror;

[Serializable] public class UnityEventPlayer : UnityEvent<Player> {}

//[RequireComponent(typeof(Animator))] <- on ChildRoot/Sprite now
[RequireComponent(typeof(Experience))]
[RequireComponent(typeof(Strength))]
[RequireComponent(typeof(Intelligence))]
[RequireComponent(typeof(PlayerChat))]
[RequireComponent(typeof(PlayerCrafting))]
[RequireComponent(typeof(PlayerCooking))]
[RequireComponent(typeof(PlayerBlacksmithing))]
[RequireComponent(typeof(PlayerIndicator))]
[RequireComponent(typeof(PlayerInventory))]
[RequireComponent(typeof(PlayerItemMall))]
[RequireComponent(typeof(PlayerLooting))]
[RequireComponent(typeof(PlayerMountControl))]
[RequireComponent(typeof(PlayerNpcRevive))]
[RequireComponent(typeof(PlayerNpcTeleport))]
[RequireComponent(typeof(PlayerNpcTrading))]
[RequireComponent(typeof(PlayerParty))]
[RequireComponent(typeof(PlayerPetControl))]
[RequireComponent(typeof(PlayerQuests))]
[RequireComponent(typeof(PlayerSkillbar))]
[RequireComponent(typeof(PlayerTrading))]
[RequireComponent(typeof(NetworkName))]
[RequireComponent(typeof(NetworkNavMeshAgentRubberbanding2D))]
public partial class Player : Entity
{
    [Header("Components")]
    public Experience experience;
    public Strength strength;
    public Intelligence intelligence;
    public PlayerChat chat;
    public PlayerCrafting crafting;
    public PlayerCooking cooking;
    public PlayerBlacksmithing Blacksmithing;
    public PlayerGuild guild;
    public PlayerIndicator indicator;
    public PlayerInventory inventory;
    public PlayerItemMall itemMall;
    public PlayerLooting looting;
    public PlayerMountControl mountControl;
    public PlayerNpcRevive npcRevive;
    public PlayerNpcTeleport npcTeleport;
    public PlayerNpcTrading npcTrading;
    public PlayerParty party;
    public PlayerPetControl petControl;
    public PlayerQuests quests;
    public PlayerSkillbar skillbar;
    public PlayerTrading trading;
    public NetworkNavMeshAgentRubberbanding2D rubberbanding;
    public Entity lastAggressor;
    public ItemSlot ammoSlot;
    public GameObject PVPFlag;
    [Header("Text Meshes")]
    public TextMesh nameOverlay;
    public Color nameOverlayDefaultColor = Color.white;
    public Color nameOverlayOffenderColor = Color.magenta;
    public Color nameOverlayMurdererColor = Color.red;
    public Color nameOverlayPartyColor = new Color(0.341f, 0.965f, 0.702f);

    [Header("Icons")]
    public Sprite classIcon; // for character selection
    public Sprite portraitIcon; // for top left portrait

    // some meta info
    [HideInInspector] public string account = "";
    [HideInInspector] public string className = "";
    [HideInInspector] public string hair = "";
    [SyncVar] public Vector3 respawnPoint; // Player's current respawn point
    [SyncVar(hook = nameof(OnBioChanged))]

    public string lastAttackUsed = "";
    public string bio = "I haven't edited my profile yet.";
    // localPlayer singleton for easier access from UI scripts etc.
    public static Player localPlayer;
    public bool IsAdmin()
    {
        return Database.singleton.IsAdminAccount(account);
    }

    public ItemSlot GetEquippedAmmo()
    {
        return ammoSlot; // Ensure you define `ammoSlot` as a part of your equipment slots
    }

    public bool ConsumeAmmo()
    {
        if (ammoSlot.amount > 0)
        {
            ammoSlot.DecreaseAmount(1);
            return true;
        }

        return false;
    }

    public ItemSlot GetAmmoSlot()
    {
        if (equipment.slots != null && equipment.slots.Count > 9)
        {
            return equipment.slots[9];  // Slot 9 is your ammo slot
        }
        return new ItemSlot();  // Return an empty slot if out of bounds
    }

    // speed
    public override float speed
    {
        get
        {
            // Base movement speed from Entity.cs
            float baseSpeed = base.speed;

            // If mounted, use mount speed instead
            if (mountControl.activeMount != null && mountControl.activeMount.health.current > 0)
                return mountControl.activeMount.speed;

            // Calculate total movement speed bonus from equipped items
            float equipmentSpeedBonus = 0f;
            foreach (var slot in equipment.slots)
            {
                if (slot.amount > 0 && slot.item.data is EquipmentItem equipItem)
                {
                    equipmentSpeedBonus += equipItem.movementSpeedBonus;
                }
            }

            // Apply the movement speed bonus: 1.0f (base) + bonus percentage
            return baseSpeed * (1 + equipmentSpeedBonus);
        }
    }


    void OnBioChanged(string oldBio, string newBio)
    {
        if (UICharacterInfo.localInstance != null)
        {
            UICharacterInfo.localInstance.UpdateBioText(newBio);
        }
    }




    // item cooldowns
    // it's based on a 'cooldownCategory' that can be set in ScriptableItems.
    // -> they can use their own name for a cooldown that only applies to them
    // -> they can use a category like 'HealthPotion' for a shared cooldown
    //    amongst all health potions
    // => we could use hash(category) as key to significantly reduce bandwidth,
    //    but we don't anymore because it makes database saving easier.
    //    otherwise we would have to find the category from a hash.
    // => IMPORTANT: cooldowns need to be saved in database so that long
    //    cooldowns can't be circumvented by logging out and back in again.
    internal readonly SyncDictionary<string, double> itemCooldowns = new SyncDictionary<string, double>();

    [Header("Interaction")]
    public float interactionRange = 1;
    public bool localPlayerClickThrough = true; // click selection goes through localplayer. feels best.
    public KeyCode cancelActionKey = KeyCode.Escape;

    [Header("PvP")]
    public BuffSkill offenderBuff;
    public BuffSkill murdererBuff;

    // when moving into attack range of a target, we always want to move a
    // little bit closer than necessary to tolerate for latency and other
    // situations where the target might have moved away a little bit already.
    [Header("Movement")]
    [Range(0.1f, 1)] public float attackToMoveRangeRatio = 0.8f;

    // some commands should have delays to avoid DDOS, too much database usage
    // or brute forcing coupons etc. we use one riskyAction timer for all.
    [SyncVar, HideInInspector] public double nextRiskyActionTime = 0; // double for long term precision

    // the next target to be set if we try to set it while casting
    [SyncVar, HideInInspector] public Entity nextTarget;

    // Trophies field (synchronized across the network)
    [SyncVar]
    public List<int> trophies = new List<int>();



    // cache players to save lots of computations
    // (otherwise we'd have to iterate NetworkServer.objects all the time)
    // => on server: all online players
    // => on client: all observed players
    public static Dictionary<string, Player> onlinePlayers = new Dictionary<string, Player>();

    // first allowed logout time after combat
    public double allowedLogoutTime => lastCombatTime + ((NetworkManagerMMO)NetworkManager.singleton).combatLogoutDelay;
    public double remainingLogoutTime => NetworkTime.time < allowedLogoutTime ? (allowedLogoutTime - NetworkTime.time) : 0;

    // helper variable to remember which skill to use when we walked close enough
    [HideInInspector] public int useSkillWhenCloser = -1;

    // Camera.main calls FindObjectWithTag each time. cache it!
    Camera cam;

    // networkbehaviour ////////////////////////////////////////////////////////
    public override void OnStartLocalPlayer()
    {
        // set singleton
        localPlayer = this;

        // find main camera
        // only for local player. 'Camera.main' is expensive (FindObjectWithTag)
        cam = Camera.main;

        // make camera follow the local player. we don't just set .parent
        // because the player might be destroyed, but the camera never should be
        cam.GetComponent<CameraMMO2D>().target = transform;
        GameObject.FindWithTag("MinimapCamera").GetComponent<CopyPosition>().target = transform;
    }

    void SetStunned(bool isStunned)
    {
        if (stunnedOverlay != null)
        {
            stunnedOverlay.SetActive(isStunned); // Enable/Disable the stun effect
            Animator stunAnimator = stunnedOverlay.GetComponent<Animator>();
            if (stunAnimator != null)
                stunAnimator.SetBool("STUNNED", isStunned);
        }
    }



    [Server]
    public void OnMonsterKilled(Monster monster)
    {
        // Get the PlayerSkills component
        PlayerSkills playerSkills = GetComponent<PlayerSkills>();
        if (playerSkills == null) return;

        // Define skill requirements
        Dictionary<string, (string predecessor, int levelRequired, int rngChance)> skillRequirements = new Dictionary<string, (string, int, int)>
    {
        { "Thrust", ("Slash", 30, 40) },
        { "Swordsmanship", ("Slash", 50, 40) },
        { "Heavy Arms", ("Thrust", 60, 40) },
        { "Fencing", ("Swordsmanship", 20, 40) }
    };

        foreach (var skill in skillRequirements)
        {
            string skillName = skill.Key;
            string predecessor = skill.Value.predecessor;
            int levelRequired = skill.Value.levelRequired;
            int rngChance = skill.Value.rngChance;

            if (playerSkills.HasLearnedWithLevel(predecessor, levelRequired))
            {
                if (UnityEngine.Random.Range(0, rngChance) == 0)
                {
                    UpgradeSkillServer(skillName);
                }
            }
        }

        // Check what the player used to kill the monster
        int weaponIndex = GetComponent<Player>().equipment.GetEquippedWeaponIndex();

        if (weaponIndex == -1 || lastAttackUsed == "Punch") // If unarmed OR used Punch
        {
            if (UnityEngine.Random.Range(0, 20) == 0) // 1 in 20 chance
            {
                UpgradeSkillServer("Punch");
            }
            if (UnityEngine.Random.Range(0, 25) == 0) // 1 in 25 chance
            {
                UpgradeSkillServer("Martial Arts");
            }
        }
        else
        {
            // If the player used a weapon, only Slash levels up
            if (UnityEngine.Random.Range(0, 40) == 0)
            {
                UpgradeSkillServer("Slash");
            }
        }
    }


    [Server]
    private void UpgradeSkillServer(string skillName)
    {
        PlayerSkills playerSkills = GetComponent<PlayerSkills>();
        if (playerSkills != null)
        {
            playerSkills.LevelUpSkill(skillName);
        }
    }





    protected override void Start()
    {
        // do nothing if not spawned (=for character selection previews)
        if (!isServer && !isClient) return;

        base.Start();
        onlinePlayers[name] = this;
    }

    [Command]
    public void CmdSetRespawnPoint(Vector3 newRespawnPoint)
    {
        respawnPoint = newRespawnPoint;
        // Optionally: Send a message to the player confirming the respawn change
        Debug.Log("Respawn point set!");
    }

    [Command]
    public void CmdRequestOnlinePlayers()
    {
        List<string> onlinePlayers = FindObjectOfType<NetworkManagerMMO>().GetOnlinePlayers();
        Debug.Log($"[Server] Online players: {string.Join(", ", onlinePlayers)}");
        TargetSendOnlinePlayers(connectionToClient, onlinePlayers);
    }

    [TargetRpc]
    private void TargetSendOnlinePlayers(NetworkConnection target, List<string> players)
    {
        Debug.Log($"[Client] Received players list: {string.Join(", ", players)}");
        FindObjectOfType<WhoUI>().ShowOnlinePlayers(players);
    }

    // Method called when trophies are updated
    private void OnTrophiesUpdated(List<int> oldTrophies, List<int> newTrophies)
    {
        // Add logic if needed to handle trophy updates (e.g., refresh UI)
        Debug.Log("Trophies updated.");
    }

    // Method to update bio (can be called by the player)
    [Command]
    public void CmdUpdateBio(string newBio)
    {
        bio = newBio; // Update the bio on the server
    }

    // Method to add a trophy (e.g., when mastering a skill)
[Server]
public void AddTrophy(int trophyId)
{
    if (!trophies.Contains(trophyId))
    {
        trophies.Add(trophyId); // Add the trophy on the server
    }
}

    // Method to remove a trophy (if needed)
    [Command]
    public void CmdRemoveTrophy(int trophyId)
    {
        if (trophies.Contains(trophyId))
        {
            trophies.Remove(trophyId); // Remove the trophy on the server
        }
    }



    void LateUpdate()
    {
        // pass parameters to animation state machine
        // => passing the states directly is the most reliable way to avoid all
        //    kinds of glitches like movement sliding, attack twitching, etc.
        // => make sure to import all looping animations like idle/run/attack
        //    with 'loop time' enabled, otherwise the client might only play it
        //    once
        // => MOVING state is set to local IsMovement result directly. otherwise
        //    we would see animation latencies for rubberband movement if we
        //    have to wait for MOVING state to be received from the server
        // => MOVING checks if !CASTING because there is a case in UpdateMOVING
        //    -> SkillRequest where we still slide to the final position (which
        //    is good), but we should show the casting animation then.
        // => skill names are assumed to be boolean parameters in animator
        //    so we don't need to worry about an animation number etc.
        if (isClient) // no need for animations on the server
        {
            animator.SetBool("MOVING", movement.IsMoving() && state != "CASTING" && !mountControl.IsMounted());
            animator.SetBool("CASTING", state == "CASTING");
            foreach (Skill skill in skills.skills)
                if (skill.level > 0 && !(skill.data is PassiveSkill))
                    animator.SetBool(skill.name, skill.CastTimeRemaining() > 0);
            animator.SetBool("STUNNED", state == "STUNNED");
            animator.SetBool("DEAD", state == "DEAD");
            animator.SetFloat("LookX", lookDirection.x);
            animator.SetFloat("LookY", lookDirection.y);
        }
    }

    void OnDestroy()
    {
        // try to remove from onlinePlayers first, NO MATTER WHAT
        // -> we can not risk ever not removing it. do this before any early
        //    returns etc.
        // -> ONLY remove if THIS object was saved. this avoids a bug where
        //    a host selects a character preview, then joins the game, then
        //    only after the end of the frame the preview is destroyed,
        //    OnDestroy is called and the preview would actually remove the
        //    world player from onlinePlayers. hence making guild management etc
        //    impossible.
        if (onlinePlayers.TryGetValue(name, out Player entry) && entry == this)
            onlinePlayers.Remove(name);

        // do nothing if not spawned (=for character selection previews)
        if (!isServer && !isClient) return;

        if (isLocalPlayer) // requires at least Unity 5.5.1 bugfix to work
        {
            localPlayer = null;
        }
    }

    // finite state machine events - status based //////////////////////////////
    // status based events
    // Event for Cooking
    bool EventCookingStarted()
    {
        bool result = cooking.requestPending;
        cooking.requestPending = false;
        return result;
    }

    bool EventBlacksmithingStarted()
    {
        bool result = Blacksmithing.requestPending;
        Blacksmithing.requestPending = false;
        return result;
    }

    bool EventCookingDone()
    {
        return state == "COOKING" && NetworkTime.time > cooking.cookingEndTime;
    }

    bool EventBlacksmithingDone()
    {
        return state == "BLACKSMITHING" && NetworkTime.time > Blacksmithing.BlacksmithingEndTime;
    }

    bool EventDied()
    {
        return health.current == 0;
    }

    bool EventTargetDisappeared()
    {
        return target == null;
    }

    bool EventTargetDied()
    {
        return target != null && target.health.current == 0;
    }

    bool EventSkillRequest()
    {
        return 0 <= skills.currentSkill && skills.currentSkill < skills.skills.Count;
    }

    bool EventSkillFinished()
    {
        return 0 <= skills.currentSkill && skills.currentSkill < skills.skills.Count &&
               skills.skills[skills.currentSkill].CastTimeRemaining() == 0;
    }

    bool EventMoveStart()
    {
        return state != "MOVING" && movement.IsMoving(); // only fire when started moving
    }

    bool EventMoveEnd()
    {
        return state == "MOVING" && !movement.IsMoving(); // only fire when stopped moving
    }

    bool EventTradeStarted()
    {
        // did someone request a trade? and did we request a trade with him too?
        Player player = trading.FindPlayerFromInvitation();
        return player != null && player.trading.requestFrom == name;
    }

    bool EventTradeDone()
    {
        // trade canceled or finished?
        return state == "TRADING" && trading.requestFrom == "";
    }

    bool EventCraftingStarted()
    {
        bool result = crafting.requestPending;
        crafting.requestPending = false;
        return result;
    }

    bool EventCraftingDone()
    {
        return state == "CRAFTING" && NetworkTime.time > crafting.endTime;
    }

    bool EventStunned()
    {
        return NetworkTime.time <= stunTimeEnd;
    }

    // finite state machine events - command based /////////////////////////////
    // client calls command, command sets a flag, event reads and resets it
    // => we use a set so that we don't get ultra long queues etc.
    // => we use set.Return to read and clear values
    HashSet<string> cmdEvents = new HashSet<string>();

    [Command]
    public void CmdRespawn() { cmdEvents.Add("Respawn"); }
    bool EventRespawn() { return cmdEvents.Remove("Respawn"); }

    [Command]
    public void CmdCancelAction() { cmdEvents.Add("CancelAction"); }
    bool EventCancelAction() { return cmdEvents.Remove("CancelAction"); }

    // finite state machine - server ///////////////////////////////////////////

    [Server]
    string UpdateServer_IDLE()
    {
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied())
        {
            // we died.
            return "DEAD";
        }
        if (EventStunned())
        {
            rubberbanding.ResetMovement();
            return "STUNNED";
        }
        if (EventCancelAction())
        {
            // the only thing that we can cancel is the target
            target = null;
            return "IDLE";
        }
        if (EventTradeStarted())
        {
            // cancel casting (if any), set target, go to trading
            skills.CancelCast(); // just in case
            target = trading.FindPlayerFromInvitation();
            return "TRADING";
        }
        if (EventCraftingStarted())
        {
            // cancel casting (if any), go to crafting
            skills.CancelCast(); // just in case
            return "CRAFTING";
        }
        if (EventCookingStarted())
        {
            // cancel casting (if any), go to crafting
            skills.CancelCast(); // just in case
            return "COOKING";
        }
        if (EventBlacksmithingStarted())
        {
            // cancel casting (if any), go to crafting
            skills.CancelCast(); // just in case
            return "BLACKSMITHING";
        }
        if (EventMoveStart())
        {
            // cancel casting (if any)
            skills.CancelCast();
            return "MOVING";
        }
        if (EventSkillRequest())
        {
            // don't cast while mounted
            // (no MOUNTED state because we'd need MOUNTED_STUNNED, etc. too)
            if (!mountControl.IsMounted())
            {
                // user wants to cast a skill.
                // check self (alive, mana, weapon etc.) and target and distance
                Skill skill = skills.skills[skills.currentSkill];
                nextTarget = target; // return to this one after any corrections by CastCheckTarget
                Vector2 destination;
                if (skills.CastCheckSelf(skill) &&
                    skills.CastCheckTarget(skill) &&
                    skills.CastCheckDistance(skill, out destination))
                {
                    // start casting and cancel movement in any case
                    // (player might move into attack range * 0.8 but as soon as we
                    //  are close enough to cast, we fully commit to the cast.)
                    rubberbanding.ResetMovement();
                    skills.StartCast(skill);
                    return "CASTING";
                }
                else
                {
                    // checks failed. reset the attempted current skill.
                    skills.currentSkill = -1;
                    nextTarget = null; // nevermind, clear again (otherwise it's shown in UITarget)
                    return "IDLE";
                }
            }
        }
        if (EventSkillFinished()) {} // don't care
        if (EventMoveEnd()) {} // don't care
        if (EventTradeDone()) {} // don't care
        if (EventCraftingDone()) {} // don't care
        if (EventRespawn()) {} // don't care
        if (EventTargetDied()) {} // don't care
        if (EventTargetDisappeared()) {} // don't care

        return "IDLE"; // nothing interesting happened
    }

    [Server]
    string UpdateServer_MOVING()
    {
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied())
        {
            // we died.
            return "DEAD";
        }
        if (EventStunned())
        {
            rubberbanding.ResetMovement();
            return "STUNNED";
        }
        if (EventMoveEnd())
        {
            // finished moving. do whatever we did before.
            return "IDLE";
        }
        if (EventCancelAction())
        {
            // cancel casting (if any) and stop moving
            skills.CancelCast();
            //rubberbanding.ResetMovement(); <- done locally. doing it here would reset localplayer to the slightly behind server position otherwise
            return "IDLE";
        }
        if (EventTradeStarted())
        {
            // cancel casting (if any), stop moving, set target, go to trading
            skills.CancelCast();
            rubberbanding.ResetMovement();
            target = trading.FindPlayerFromInvitation();
            return "TRADING";
        }
        if (EventCraftingStarted())
        {
            // cancel casting (if any), stop moving, go to crafting
            skills.CancelCast();
            rubberbanding.ResetMovement();
            return "CRAFTING";
        }
        if (EventCookingStarted())
        {
            // cancel casting (if any), stop moving, go to crafting
            skills.CancelCast();
            rubberbanding.ResetMovement();
            return "COOKING";
        }
        if (EventBlacksmithingStarted())
        {
            // cancel casting (if any), stop moving, go to crafting
            skills.CancelCast();
            rubberbanding.ResetMovement();
            return "BLACKSMITHING";
        }
        // SPECIAL CASE: Skill Request while doing rubberband movement
        // -> we don't really need to react to it
        // -> we could just wait for move to end, then react to request in IDLE
        // -> BUT player position on server always lags behind in rubberband movement
        // -> SO there would be a noticeable delay before we start to cast
        //
        // SOLUTION:
        // -> start casting as soon as we are in range
        // -> BUT don't ResetMovement. instead let it slide to the final position
        //    while already starting to cast
        // -> NavMeshAgentRubberbanding won't accept new positions while casting
        //    anyway, so this is fine
        if (EventSkillRequest())
        {
            // don't cast while mounted
            // (no MOUNTED state because we'd need MOUNTED_STUNNED, etc. too)
            if (!mountControl.IsMounted())
            {
                Vector2 destination;
                Skill skill = skills.skills[skills.currentSkill];
                if (skills.CastCheckSelf(skill) &&
                    skills.CastCheckTarget(skill) &&
                    skills.CastCheckDistance(skill, out destination))
                {
                    //Debug.Log("MOVING->EventSkillRequest: early cast started while sliding to destination...");
                    // rubberbanding.ResetMovement(); <- DO NOT DO THIS.
                    skills.StartCast(skill);
                    return "CASTING";
                }
            }
        }
        if (EventMoveStart()) {} // don't care
        if (EventSkillFinished()) {} // don't care
        if (EventTradeDone()) {} // don't care
        if (EventCraftingDone()) {} // don't care
        if (EventRespawn()) {} // don't care
        if (EventTargetDied()) {} // don't care
        if (EventTargetDisappeared()) {} // don't care

        return "MOVING"; // nothing interesting happened
    }

    void UseNextTargetIfAny()
    {
        // use next target if the user tried to target another while casting
        // (target is locked while casting so skill isn't applied to an invalid
        //  target accidentally)
        if (nextTarget != null)
        {
            target = nextTarget;
            nextTarget = null;
        }
    }

    [Server]
    string UpdateServer_CASTING()
    {
        // events sorted by priority (e.g. target doesn't matter if we died)
        //
        // IMPORTANT: nextTarget might have been set while casting, so make sure
        // to handle it in any case here. it should definitely be null again
        // after casting was finished.
        // => this way we can reliably display nextTarget on the client if it's
        //    != null, so that UITarget always shows nextTarget>target
        //    (this just feels better)
        if (EventDied())
        {
            // we died.
            UseNextTargetIfAny(); // if user selected a new target while casting
            return "DEAD";
        }
        if (EventStunned())
        {
            skills.CancelCast();
            rubberbanding.ResetMovement();
            return "STUNNED";
        }
        if (EventMoveStart())
        {
            // we do NOT cancel the cast if the player moved, and here is why:
            // * local player might move into cast range and then try to cast.
            // * server then receives the Cmd, goes to CASTING state, then
            //   receives one of the last movement updates from the local player
            //   which would cause EventMoveStart and cancel the cast.
            // * this is the price for rubberband movement.
            // => if the player wants to cast and got close enough, then we have
            //    to fully commit to it. there is no more way out except via
            //    cancel action. any movement in here is to be rejected.
            //    (many popular MMOs have the same behaviour too)
            //

            // we do NOT reset movement either. allow sliding to final position.
            // (NavMeshAgentRubberbanding doesn't accept new ones while CASTING)
            //rubberbanding.ResetMovement(); <- DO NOT DO THIS

            // we do NOT return "CASTING". EventMoveStart would constantly fire
            // while moving for skills that allow movement. hence we would
            // always return "CASTING" here and never get to the castfinished
            // code below.
            //return "CASTING";
        }
        if (EventCancelAction())
        {
            // cancel casting
            skills.CancelCast();
            UseNextTargetIfAny(); // if user selected a new target while casting
            return "IDLE";
        }
        if (EventTradeStarted())
        {
            // cancel casting (if any), stop moving, set target, go to trading
            skills.CancelCast();
            rubberbanding.ResetMovement();

            // set target to trade target instead of next target (clear that)
            target = trading.FindPlayerFromInvitation();
            nextTarget = null;
            return "TRADING";
        }
        if (EventTargetDisappeared())
        {
            // cancel if the target matters for this skill
            if (skills.skills[skills.currentSkill].cancelCastIfTargetDied)
            {
                skills.CancelCast();
                UseNextTargetIfAny(); // if user selected a new target while casting
                return "IDLE";
            }
        }
        if (EventTargetDied())
        {
            // cancel if the target matters for this skill
            if (skills.skills[skills.currentSkill].cancelCastIfTargetDied)
            {
                skills.CancelCast();
                UseNextTargetIfAny(); // if user selected a new target while casting
                return "IDLE";
            }
        }
        if (EventSkillFinished())
        {
            // apply the skill after casting is finished
            // note: we don't check the distance again. it's more fun if players
            //       still cast the skill if the target ran a few steps away
            Skill skill = skills.skills[skills.currentSkill];

            // apply the skill on the target
            skills.FinishCast(skill);

            // clear current skill for now
            skills.currentSkill = -1;

            // use next target if the user tried to target another while casting
            UseNextTargetIfAny();

            // go back to IDLE
            return "IDLE";
        }
        if (EventMoveEnd()) {} // don't care
        if (EventTradeDone()) {} // don't care
        if (EventCraftingStarted()) {} // don't care
        if (EventCraftingDone()) {} // don't care
        if (EventRespawn()) {} // don't care
        if (EventSkillRequest()) {} // don't care

        return "CASTING"; // nothing interesting happened
    }

    [Server]
    string UpdateServer_STUNNED()
    {
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied())
        {
            // we died.
            SetStunned(false); // Remove stun overlay
            return "DEAD";
        }
        if (EventStunned())
        {
            SetStunned(true); // Activate stun overlay
            return "STUNNED";
        }

        // go back to idle if we aren't stunned anymore
        SetStunned(false); // Remove stun overlay when no longer stunned
        return "IDLE";
    }


    [Server]
    string UpdateServer_TRADING()
    {
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied())
        {
            // we died, stop trading. other guy will receive targetdied event.
            trading.Cleanup();
            return "DEAD";
        }
        if (EventStunned())
        {
            // stop trading
            skills.CancelCast();
            rubberbanding.ResetMovement();
            trading.Cleanup();
            return "STUNNED";
        }
        if (EventMoveStart())
        {
            // reject movement while trading
            rubberbanding.ResetMovement();
            return "TRADING";
        }
        if (EventCancelAction())
        {
            // stop trading
            trading.Cleanup();
            return "IDLE";
        }
        if (EventTargetDisappeared())
        {
            // target disconnected, stop trading
            trading.Cleanup();
            return "IDLE";
        }
        if (EventTargetDied())
        {
            // target died, stop trading
            trading.Cleanup();
            return "IDLE";
        }
        if (EventTradeDone())
        {
            // someone canceled or we finished the trade. stop trading
            trading.Cleanup();
            return "IDLE";
        }
        if (EventMoveEnd()) {} // don't care
        if (EventSkillFinished()) {} // don't care
        if (EventCraftingStarted()) {} // don't care
        if (EventCraftingDone()) {} // don't care
        if (EventRespawn()) {} // don't care
        if (EventTradeStarted()) {} // don't care
        if (EventSkillRequest()) {} // don't care

        return "TRADING"; // nothing interesting happened
    }

    [Server]
    string UpdateServer_CRAFTING()
    {

        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied())
        {
            // we died, stop crafting
            return "DEAD";
        }
        if (EventStunned())
        {
            // stop crafting
            rubberbanding.ResetMovement();
            return "STUNNED";
        }
        if (EventMoveStart())
        {
            // reject movement while crafting
            rubberbanding.ResetMovement();
            return "CRAFTING";
        }
        if (EventCraftingDone())
        {
            // finish crafting
            crafting.Craft();
            return "IDLE";
        }
        if (EventCancelAction()) {} // don't care. user pressed craft, we craft.
        if (EventTargetDisappeared()) {} // don't care
        if (EventTargetDied()) {} // don't care
        if (EventMoveEnd()) {} // don't care
        if (EventSkillFinished()) {} // don't care
        if (EventRespawn()) {} // don't care
        if (EventTradeStarted()) {} // don't care
        if (EventTradeDone()) {} // don't care
        if (EventCraftingStarted()) {} // don't care
        if (EventSkillRequest()) {} // don't care

        return "CRAFTING"; // nothing interesting happened
    }

    [Server]
    string UpdateServer_COOKING()
    {
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied())
        {
            // we died, stop cooking
            return "DEAD";
        }
        if (EventStunned())
        {
            // stop cooking
            rubberbanding.ResetMovement();
            return "STUNNED";
        }
        if (EventMoveStart())
        {
            // reject movement while cooking
            rubberbanding.ResetMovement();
            return "COOKING";
        }
        if (EventCookingDone())
        {
            // finish cooking
            cooking.Cook();

            // Check if auto-cooking is enabled and continue if possible
            if (cooking.currentRecipe != null && cooking.requestPending)
            {
                StartCoroutine(cooking.AutoCook(cooking.currentRecipe));
            }

            // Return to IDLE if auto-cooking doesn't continue
            return cooking.currentRecipe == null || !cooking.requestPending ? "IDLE" : "COOKING";
        }

        if (EventCancelAction())
        {
            // cancel action
            cooking.requestPending = false; // Reset request pending
            return "IDLE";
        }
        if (EventTargetDisappeared()) { } // don't care
        if (EventTargetDied()) { } // don't care
        if (EventMoveEnd()) { } // don't care
        if (EventSkillFinished()) { } // don't care
        if (EventRespawn()) { } // don't care
        if (EventTradeStarted()) { } // don't care
        if (EventTradeDone()) { } // don't care
        if (EventCraftingStarted()) { } // don't care
        if (EventCookingStarted()) { } // don't care
        if (EventSkillRequest()) { } // don't care

        return "COOKING"; // nothing interesting happened
    }

    [Server]
    string UpdateServer_BLACKSMITHING()
    {
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventDied())
        {
            // we died, stop BLACKSMITHING
            return "DEAD";
        }
        if (EventStunned())
        {
            // stop BLACKSMITHING
            rubberbanding.ResetMovement();
            return "STUNNED";
        }
        if (EventMoveStart())
        {
            // reject movement while BLACKSMITHING
            rubberbanding.ResetMovement();
            return "BLACKSMITHING";
        }
        if (EventBlacksmithingDone())
        {
            // finish BLACKSMITHING
            Blacksmithing.Blacksmith();

            // Check if auto-BLACKSMITHING is enabled and continue if possible
            if (Blacksmithing.currentRecipe != null && Blacksmithing.requestPending)
            {
                //StartCoroutine(Blacksmithing.AutoCook(Blacksmithing.currentRecipe));
            }

            // Return to IDLE if auto-cooking doesn't continue
            return cooking.currentRecipe == null || !cooking.requestPending ? "IDLE" : "BLACKSMITHING";
        }

        if (EventCancelAction())
        {
            // cancel action
            cooking.requestPending = false; // Reset request pending
            return "IDLE";
        }
        if (EventTargetDisappeared()) { } // don't care
        if (EventTargetDied()) { } // don't care
        if (EventMoveEnd()) { } // don't care
        if (EventSkillFinished()) { } // don't care
        if (EventRespawn()) { } // don't care
        if (EventTradeStarted()) { } // don't care
        if (EventTradeDone()) { } // don't care
        if (EventCraftingStarted()) { } // don't care
        if (EventCookingStarted()) { } // don't care
        if (EventBlacksmithingStarted()) { } // don't care
        if (EventSkillRequest()) { } // don't care

        return "BLACKSMITHING"; // nothing interesting happened
    }


    [Server]
    string UpdateServer_DEAD()
    {
        // events sorted by priority (e.g. target doesn't matter if we died)
        if (EventRespawn())
        {
            // Revive at the set respawn point, with 50% health, then go to idle
            if (respawnPoint != Vector3.zero) // Ensure a valid respawn point exists
            {
                movement.Warp(respawnPoint); // Warp player to their saved respawn location
            }
            else
            {
                // Fallback to the default spawn position if no respawn point is set
                Transform start = NetworkManagerMMO.GetNearestStartPosition(transform.position);
                movement.Warp(start.position);
            }

            // Revive the player with 50% health
            Revive(0.5f);

            return "IDLE";
        }

        if (EventMoveStart())
        {
            // this should never happen, rubberband should prevent from moving
            // while dead.
            Debug.LogWarning("Player " + name + " moved while dead. This should not happen.");
            return "DEAD";
        }
        if (EventMoveEnd()) {} // don't care
        if (EventSkillFinished()) {} // don't care
        if (EventDied()) {} // don't care
        if (EventCancelAction()) {} // don't care
        if (EventTradeStarted()) {} // don't care
        if (EventTradeDone()) {} // don't care
        if (EventCraftingStarted()) {} // don't care
        if (EventCraftingDone()) {} // don't care
        if (EventTargetDisappeared()) {} // don't care
        if (EventTargetDied()) {} // don't care
        if (EventSkillRequest()) {} // don't care

        return "DEAD"; // nothing interesting happened
    }

    [Server]
    protected override string UpdateServer()
    {
        if (state == "IDLE") return UpdateServer_IDLE();
        if (state == "MOVING") return UpdateServer_MOVING();
        if (state == "CASTING") return UpdateServer_CASTING();
        if (state == "STUNNED") return UpdateServer_STUNNED();
        if (state == "TRADING") return UpdateServer_TRADING();
        if (state == "CRAFTING") return UpdateServer_CRAFTING();
        if (state == "COOKING") return UpdateServer_COOKING();
        if (state == "BLACKSMITHING") return UpdateServer_BLACKSMITHING();
        if (state == "DEAD") return UpdateServer_DEAD();
        Debug.LogError("Invalid state: " + state);
        return "IDLE";
    }

    // finite state machine - client ///////////////////////////////////////////
    [Client]
    protected override void UpdateClient()
    {
        UpdateClient_HighlightLoot();

        if (state == "IDLE" || state == "MOVING")
        {
            if (isLocalPlayer)
            {
                if (Input.GetKeyDown(ItemDropSettings.Settings.itemPickupKey))
                {
                    if (!Utils.IsCursorOverUserInterface() && Input.touchCount <= 1)
                    {
                        FindNearestItem();
                    }
                }

                if (EventMoveEnd())
                {
                    if (targetItem != null)
                    {
                        // check distance between character and target item
                        if (Utils.ClosestDistance(collider, targetItem.itemCollider) <= 1)
                        {
                            ItemPickup(targetItem);
                        }
                    }
                }

                // trying to cast a skill on a monster that wasn't in range?
                // then check if we walked into attack range by now
                if (useSkillWhenCloser != -1)
                {
                    // can we still attack the target? maybe it was switched.
                    if (CanAttack(target))
                    {
                        // in range already?
                        float range = skills.skills[useSkillWhenCloser].castRange * attackToMoveRangeRatio;
                        if (Utils.ClosestDistance(collider, target.collider) <= range)
                        {
                            // then stop moving and start attacking
                            ((PlayerSkills)skills).CmdUse(useSkillWhenCloser, lookDirection);

                            // reset
                            useSkillWhenCloser = -1;
                        }
                        else
                        {
                            // keep adjusting the destination
                            movement.Navigate(target.collider.ClosestPointOnBounds(transform.position), range);
                        }
                    }
                    else useSkillWhenCloser = -1;
                }
            }
        }
        else if (state == "CASTING")
        {
            if (isLocalPlayer)
            {
                // simply reset any client-sided movement
                movement.Reset();

                // cancel action if escape key was pressed
                if (Input.GetKeyDown(cancelActionKey)) CmdCancelAction();
            }
        }
        else if (state == "STUNNED")
        {
            if (isLocalPlayer)
            {
                // simply reset any client-sided movement
                movement.Reset();

                // cancel action if escape key was pressed
                if (Input.GetKeyDown(cancelActionKey)) CmdCancelAction();
            }
        }
        else if (state == "TRADING") { }
        else if (state == "CRAFTING") { }
        else if (state == "COOKING")
        {
            if (isLocalPlayer)
            {
                // Prevent movement while cooking
                movement.Reset();

                // cancel action if escape key was pressed
                if (Input.GetKeyDown(cancelActionKey)) CmdCancelAction();
            }
        }
        else if (state == "BLACKSMITHING")
        {
            if (isLocalPlayer)
            {
                // Prevent movement while blacksmithing
                movement.Reset();

                // cancel action if escape key was pressed
                if (Input.GetKeyDown(cancelActionKey)) CmdCancelAction();
            }
        }
        else if (state == "DEAD") { }
        else Debug.LogError("invalid state:" + state);
    }

    // overlays ////////////////////////////////////////////////////////////////
    protected override void UpdateOverlays()
    {
        base.UpdateOverlays();

        if (nameOverlay != null)
        {
            // Only players need to copy names to the name overlay. It never changes for monsters/NPCs.
            nameOverlay.text = name;

            // Find local player (null while in character selection)
            if (localPlayer != null)
            {
                // Note: murderer has higher priority (a player can be a murderer and an offender at the same time)
                if (IsMurderer())
                {
                    nameOverlay.color = nameOverlayMurdererColor;
                    if (PVPFlag != null)
                        PVPFlag.SetActive(true); // Enable the PVP flag when a murderer
                }
                else if (IsOffender())
                {
                    nameOverlay.color = nameOverlayOffenderColor;
                    if (PVPFlag != null)
                        PVPFlag.SetActive(false); // Hide the PVP flag if just an offender
                }
                // Member of the same party
                else if (localPlayer.party.InParty() && localPlayer.party.party.Contains(name))
                {
                    nameOverlay.color = nameOverlayPartyColor;
                    if (PVPFlag != null)
                        PVPFlag.SetActive(false); // Hide the PVP flag for party members
                }
                // Otherwise, default
                else
                {
                    nameOverlay.color = nameOverlayDefaultColor;
                    if (PVPFlag != null)
                        PVPFlag.SetActive(false); // Hide the PVP flag for neutral players
                }
            }
        }
    }


    // skill finished event & pending actions //////////////////////////////////
    // pending actions while casting. to be applied after cast.
    [HideInInspector] public int pendingSkill = -1;
[HideInInspector] public Vector2 pendingDestination;
[HideInInspector] public bool pendingDestinationValid;

// client event when skill cast finished on server
// -> useful for follow up attacks etc.
//    (doing those on server won't really work because the target might have
//     moved, in which case we need to follow, which we need to do on the
//     client)
[Client]
public void OnSkillCastFinished(Skill skill)
{
if (!isLocalPlayer) return;

// tried to click move somewhere?
if (pendingDestinationValid)
{
movement.Navigate(pendingDestination, 0);
}
// user pressed another skill button?
else if (pendingSkill != -1)
{
((PlayerSkills)skills).TryUse(pendingSkill, true);
}
// otherwise do follow up attack if no interruptions happened
else if (skill.followupDefaultAttack)
{
((PlayerSkills)skills).TryUse(0, true);
}

// clear pending actions in any case
pendingSkill = -1;
pendingDestinationValid = false;
}

    // combat //////////////////////////////////////////////////////////////////
    [Server]
    public void OnDamageDealtTo(Entity victim)
    {
        // Track the last aggressor if the victim is a monster
        if (victim is Monster monster)
        {
            // Ensure the attacker is a player
            if (this is Player player)
            {
                monster.lastAggressor = player; // Track the last player who attacked

                monster.ShowHealthBar();
            }
        }

        // Attacked an innocent player
        if (victim is Player && ((Player)victim).IsInnocent())
        {
            // Start offender status if not a murderer yet
            if (!IsMurderer()) StartOffender();
        }
        // Attacked a pet with an innocent owner
        else if (victim is Pet && ((Pet)victim).owner.IsInnocent())
        {
            // Start offender status if not a murderer yet
            if (!IsMurderer()) StartOffender();
        }
    }



    [Server]
public void OnKilledEnemy(Entity victim)
{
// killed an innocent player
if (victim is Player && ((Player)victim).IsInnocent())
{
StartMurderer();
}
// killed a pet with an innocent owner
else if (victim is Pet && ((Pet)victim).owner.IsInnocent())
{
StartMurderer();
}
}

// aggro ///////////////////////////////////////////////////////////////////
// this function is called by entities that attack us
[ServerCallback]
public override void OnAggro(Entity entity)
{
// forward to pet if it's supposed to defend us
if (petControl.activePet != null && petControl.activePet.defendOwner)
petControl.activePet.OnAggro(entity);
}

// movement ////////////////////////////////////////////////////////////////
// check if movement is currently allowed
// -> not in Movement.cs because we would have to add it to each player
//    movement system. (can't use an abstract PlayerMovement.cs because
//    PlayerNavMeshMovement needs to inherit from NavMeshMovement already)
public bool IsMovementAllowed()
{
// some skills allow movement while casting
bool castingAndAllowed = state == "CASTING" &&
                 skills.currentSkill != -1 &&
                 skills.skills[skills.currentSkill].allowMovement;

// in a state where movement is allowed?
// and if local player: not typing in an input?
// (fix: only check for local player. checking in all cases means that
//       no player could move if host types anything in an input)
bool isLocalPlayerTyping = isLocalPlayer && UIUtils.AnyInputActive();
return (state == "IDLE" || state == "MOVING" || castingAndAllowed) &&
!isLocalPlayerTyping;
}

    // death ///////////////////////////////////////////////////////////////////
    public override void OnDeath()
    {
        base.OnDeath();
        rubberbanding.ResetMovement();
        OnDeath_ItemDrop();
        

        // Broadcast death message if killed by a monster
        if (lastAggressor != null && lastAggressor is Monster monster)
        {
            string deathMessage = $"{name} was just killed by {monster.name}.";
            foreach (var player in Player.onlinePlayers.Values)
            {
                player.chat.TargetMsgDeath(deathMessage);
            }
        }

        lastAggressor = null; // Reset aggressor
    }
    public void OnDragAndDrop_Inventory_Vault(int[] indices)
    {
        int fromIndex = indices[0];
        int toIndex = indices[1];

        // Move the item from inventory to vault
        PlayerInventory inventory = GetComponent<PlayerInventory>();
        PlayerVault vault = GetComponent<PlayerVault>();

        if (inventory != null && vault != null)
        {
            ItemSlot itemSlot = inventory.slots[fromIndex];
            if (itemSlot.amount > 0)
            {
                // Update locally
                vault.slots[toIndex] = itemSlot;
                inventory.slots[fromIndex] = new ItemSlot();

                // Sync with server
                vault.CmdMoveItem(fromIndex, toIndex);
            }
        }
    }
    public void OnDragAndDrop_InventorySlot_Vault(int[] indices)
    {
        int fromIndex = indices[0];
        int toIndex = indices[1];

        PlayerInventory inventory = GetComponent<PlayerInventory>();
        PlayerVault vault = GetComponent<PlayerVault>();

        if (inventory != null && vault != null)
        {
            ItemSlot itemSlot = inventory.slots[fromIndex];
            if (itemSlot.amount > 0)
            {
                // Call the server to handle item transfer
                CmdTransferItemInventoryToVault(fromIndex, toIndex);
            }
        }
    }

    [Command]
    public void CmdAttemptDisarm(NetworkIdentity trapIdentity)
    {
        if (trapIdentity == null) return;

        SpikeTrap trap = trapIdentity.GetComponent<SpikeTrap>();
        if (trap == null || !trap.isActive) return;

        PlayerInventory inventory = GetComponent<PlayerInventory>();
        if (inventory == null) return;

        // Check if the player has the required item
        if (!inventory.HasEnough(new Item(trap.requiredItem), 1))
        {
            string message = $"You need a Disarming Kit to disarm this trap.";
            PlayerChat playerChat = GetComponent<PlayerChat>();
            if (playerChat != null)
            {
                playerChat.TargetMsgLocal(name, message);
            }
            return;  // Exit if the required item is missing
        }

        // Proceed with disarming the trap and removing the item
        trap.DisarmTrap(this);
    }

    [Command]
    public void CmdRequestRemoveItem(int itemHash, int amount, int inventoryIndex)
    {
        // Check if the item exists in the player's inventory
        if (!inventory.HasEnough(new Item(ScriptableItem.All[itemHash]), amount))
        {
            Debug.LogWarning("[SERVER] Player does not have enough of the item to remove.");
            return;
        }

        // Remove the item from inventory
        inventory.RemoveItem(new Item(ScriptableItem.All[itemHash]), amount);

        // Update the inventory slot on all clients
        ItemSlot updatedSlot = inventory.slots[inventoryIndex];
        RpcUpdateInventorySlot(inventoryIndex, updatedSlot);
    }

    [ClientRpc]
    void RpcUpdateInventorySlot(int index, ItemSlot slot)
    {
        if (index >= 0 && index < inventory.slots.Count)
        {
            inventory.slots[index] = slot;
        }
    }


    [Command]
    public void CmdTransferItemInventoryToVault(int fromIndex, int toIndex)
    {
        PlayerInventory inventory = GetComponent<PlayerInventory>();
        PlayerVault vault = GetComponent<PlayerVault>();

        if (inventory != null && vault != null)
        {
            if (fromIndex >= 0 && fromIndex < inventory.slots.Count && toIndex >= 0 && toIndex < vault.slots.Count)
            {
                // Perform the transfer on the server
                ItemSlot itemSlot = inventory.slots[fromIndex];
                vault.slots[toIndex] = itemSlot;
                inventory.slots[fromIndex] = new ItemSlot();

                // Save both inventory and vault states
                Database.singleton.SavePlayerInventory(inventory);
                Database.singleton.SaveVault(vault);
            }
        }
    }

    [Command]
    public void CmdOpenSlotMachine(SlotMachine machine)
    {
        if (machine == null) return;
        if (Vector2.Distance(transform.position, machine.transform.position) > 0.5f)
        {
            return;
        }


        SlotMachineUI.instance.ToggleUI(true);
    }

    [Command]
    public void CmdStartSlotMachine(int bet)
    {
        SlotMachine machine = FindObjectOfType<SlotMachine>();
        if (machine != null)
        {
            machine.ServerStartSlotMachine(this, bet);
        }
    }

    [TargetRpc]
    public void TargetMsgLocal(string message)
    {
        PlayerChat playerChat = GetComponent<PlayerChat>();
        if (playerChat != null)
        {
            playerChat.TargetMsgLocal(name, message);
        }
    }

    [Command]
    public void CmdSendTooFarMessage()
    {
        PlayerChat playerChat = GetComponent<PlayerChat>();
        if (playerChat != null)
        {
            playerChat.TargetMsgLocal(name, "You are too far away to interact with this.");
        }
    }

    public void OnDragAndDrop_Vault_Inventory(int[] indices)
    {
        int fromIndex = indices[0];
        int toIndex = indices[1];

        // Move the item from vault to inventory
        PlayerVault vault = GetComponent<PlayerVault>();
        PlayerInventory inventory = GetComponent<PlayerInventory>();

        if (vault != null && inventory != null)
        {
            ItemSlot itemSlot = vault.slots[fromIndex];
            if (itemSlot.amount > 0)
            {
                inventory.slots[toIndex] = itemSlot;
                vault.slots[fromIndex] = new ItemSlot();  // Clear original slot

                // Sync changes with the server
                inventory.CmdMoveItem(fromIndex, toIndex);
            }
        }
    }

    public void OnDragAndDrop_Vault_Vault(int[] indices)
    {
        int fromIndex = indices[0];
        int toIndex = indices[1];

        // Get the vault component
        PlayerVault vault = GetComponent<PlayerVault>();

        if (vault != null)
        {
            // Validate indices to prevent out-of-bounds errors
            if (fromIndex >= 0 && fromIndex < vault.slots.Count && toIndex >= 0 && toIndex < vault.slots.Count)
            {
                // Swap the items within the vault
                ItemSlot temp = vault.slots[fromIndex];
                vault.slots[fromIndex] = vault.slots[toIndex];
                vault.slots[toIndex] = temp;

                // Sync the change with the server
                vault.CmdMoveItemInVault(fromIndex, toIndex);

                // Refresh the UI (optional)
                //UIVault.singleton?.RefreshUI(vault);
            }
        }
    }

    [Command]
    public void CmdClearPlayerTargets()
    {
        // Clear targets on the server
        target = null;
        nextTarget = null;

        // Notify all clients to update their UI
        RpcClearPlayerTargets();
    }

    [ClientRpc]
    void RpcClearPlayerTargets()
    {
        if (this == Player.localPlayer)
        {
            // Ensure the UI is updated on the client side
            UITarget uiTarget = FindObjectOfType<UITarget>();
            if (uiTarget != null)
            {
                uiTarget.HideTargetPanel();
            }
        }
    }




    public void OnDragAndDrop_Vault_InventorySlot(int[] indices)
    {
        int fromIndex = indices[0];
        int toIndex = indices[1];

        PlayerVault vault = GetComponent<PlayerVault>();
        PlayerInventory inventory = GetComponent<PlayerInventory>();

        if (vault != null && inventory != null)
        {
            ItemSlot itemSlot = vault.slots[fromIndex];
            if (itemSlot.amount > 0)
            {
                // Call the server to handle item transfer
                CmdTransferItemVaultToInventory(fromIndex, toIndex);
            }
        }
    }

    [Command]
    public void CmdTransferItemVaultToInventory(int fromIndex, int toIndex)
    {
        PlayerVault vault = GetComponent<PlayerVault>();
        PlayerInventory inventory = GetComponent<PlayerInventory>();

        if (vault != null && inventory != null)
        {
            if (fromIndex >= 0 && fromIndex < vault.slots.Count && toIndex >= 0 && toIndex < inventory.slots.Count)
            {
                // Perform the transfer on the server
                ItemSlot itemSlot = vault.slots[fromIndex];
                inventory.slots[toIndex] = itemSlot;
                vault.slots[fromIndex] = new ItemSlot();

                // Save both inventory and vault states
                Database.singleton.SaveVault(vault);
                Database.singleton.SavePlayerInventory(inventory);
            }
        }
    }


    // item cooldowns //////////////////////////////////////////////////////////
    // get remaining item cooldown, or 0 if none
    public float GetItemCooldown(string cooldownCategory)
{
// find cooldown for that category
if (itemCooldowns.TryGetValue(cooldownCategory, out double cooldownEnd))
{
return NetworkTime.time >= cooldownEnd ? 0 : (float)(cooldownEnd - NetworkTime.time);
}

// none found
return 0;
}

// reset item cooldown
public void SetItemCooldown(string cooldownCategory, float cooldown)
{
// save end time
itemCooldowns[cooldownCategory] = NetworkTime.time + cooldown;
}

// attack //////////////////////////////////////////////////////////////////
// we use 'is' instead of 'GetType' so that it works for inherited types too
public override bool CanAttack(Entity entity)
{
return base.CanAttack(entity) &&
(entity is Monster ||
entity is Player ||
(entity is Pet && entity != petControl.activePet) ||
(entity is Mount && entity != mountControl.activeMount));

}

// pvp murder system ///////////////////////////////////////////////////////
// attacking someone innocent results in Offender status
//   (can be attacked without penalty for a short time)
// killing someone innocent results in Murderer status
//   (can be attacked without penalty for a long time + negative buffs)
// attacking/killing a Offender/Murderer has no penalty
//
// we use buffs for the offender/status because buffs have all the features
// that we need here.
public bool IsOffender()
{
return offenderBuff != null && skills.GetBuffIndexByName(offenderBuff.name) != -1;
}

public bool IsMurderer()
{
return murdererBuff != null && skills.GetBuffIndexByName(murdererBuff.name) != -1;
}

public bool IsInnocent()
{
return !IsOffender() && !IsMurderer();
}

public void StartOffender()
{
if (offenderBuff != null) skills.AddOrRefreshBuff(new Buff(offenderBuff, 1));
}

public void StartMurderer()
{
if (murdererBuff != null) skills.AddOrRefreshBuff(new Buff(murdererBuff, 1));
}

// selection handling //////////////////////////////////////////////////////
[Command]
public void CmdSetTarget(NetworkIdentity ni)
{
// validate
if (ni != null)
{
// can directly change it, or change it after casting?string UpdateServer_STUNNED()
if (state == "IDLE" || state == "MOVING" || state == "STUNNED")
target = ni.GetComponent<Entity>();
else if (state == "CASTING")
nextTarget = ni.GetComponent<Entity>();
}
}

// interaction /////////////////////////////////////////////////////////////
protected override void OnInteract()
{
// not local player?
if (this != localPlayer)
{
// attackable and has skills? => attack
if (localPlayer.CanAttack(this) && localPlayer.skills.skills.Count > 0)
{
// then try to use that one
((PlayerSkills)localPlayer.skills).TryUse(0);
}
// otherwise just walk there
// (e.g. if clicking on it in a safe zone where we can't attack)
else
{
// use collider point(s) to also work with big entities
Vector2 destination = Utils.ClosestPoint(this, localPlayer.transform.position);
localPlayer.movement.Navigate(destination, localPlayer.interactionRange);
}
}
}
}
