// The Entity class is rather simple. It contains a few basic entity properties
// like health, mana and level that all inheriting classes like Players and
// Monsters can use.
//
// Entities also have a _target_ Entity that can't be synchronized with a
// SyncVar. Instead we created a EntityTargetSync component that takes care of
// that for us.
//
// Entities use a deterministic finite state machine to handle IDLE/MOVING/DEAD/
// CASTING etc. states and events. Using a deterministic FSM means that we react
// to every single event that can happen in every state (as opposed to just
// taking care of the ones that we care about right now). This means a bit more
// code, but it also means that we avoid all kinds of weird situations like 'the
// monster doesn't react to a dead target when casting' etc.
// The next state is always set with the return value of the UpdateServer
// function. It can never be set outside of it, to make sure that all events are
// truly handled in the state machine and not outside of it. Otherwise we may be
// tempted to set a state in CmdBeingTrading etc., but would likely forget of
// special things to do depending on the current state.
//
// Entities also need a kinematic Rigidbody so that OnTrigger functions can be
// called. Note that there is currently a Unity bug that slows down the agent
// when having lots of FPS(300+) if the Rigidbody's Interpolate option is
// enabled. So for now it's important to disable Interpolation - which is a good
// idea in general to increase performance.
using System;
using UnityEngine;
using UnityEngine.Events;
using Mirror;

[Serializable] public class UnityEventEntity : UnityEvent<Entity> {}
[Serializable] public class UnityEventEntityInt : UnityEvent<Entity, int> {}

// note: no animator required, towers, dummies etc. may not have one
[RequireComponent(typeof(Level))]
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(Mana))]
[RequireComponent(typeof(Combat))]
//[RequireComponent(typeof(Equipment))] // not required. monsters don't have one.
//[RequireComponent(typeof(Movement))] // not required. mounts don't have one.
[RequireComponent(typeof(Skills))]
[RequireComponent(typeof(Rigidbody2D))] // kinematic, only needed for OnTrigger
[RequireComponent(typeof(AudioSource))]
public abstract partial class Entity : NetworkBehaviour
{
    [Header("Components")]
    public Level level;
    public Health health;
    public Mana mana;
    public Combat combat;
    public Equipment equipment;
    public Movement movement;
    public Skills skills;
    public Animator animator;
#pragma warning disable CS0109 // member does not hide accessible member
    public new Collider2D collider;
#pragma warning restore CS0109 // member does not hide accessible member
    public AudioSource audioSource;

    // finite state machine
    // -> state only writable by entity class to avoid all kinds of confusion
    [Header("State")]
    [SyncVar, SerializeField] string _state = "IDLE";
    public string state => _state;

    // it's useful to know an entity's last combat time (did/was attacked)
    // e.g. to prevent logging out for x seconds after combat
    [SyncVar] public double lastCombatTime;

    [Header("Target")]
    [SyncVar, HideInInspector] public Entity target;

    [Header("Speed")]
    [SerializeField] protected LinearFloat _speed = new LinearFloat{baseValue=3};
    public virtual float speed
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            float passiveBonus = 0;
            foreach (Skill skill in skills.skills)
                if (skill.level > 0 && skill.data is PassiveSkill)
                    passiveBonus += ((PassiveSkill)skill.data).speedBonus.Get(skill.level);

            float buffBonus = 0;
            foreach (Buff buff in skills.buffs)
                buffBonus += buff.speedBonus;

            // base + passives + buffs
            return _speed.Get(level.current) + passiveBonus + buffBonus;
        }
    }

    // all entities should have gold, not just the player
    // useful for monster loot, chests etc.
    // note: int is not enough (can have > 2 mil. easily)
    [Header("Gold")]
    [SyncVar, SerializeField] long _gold = 0;
    public long gold { get { return _gold; } set { _gold = Math.Max(value, 0); } }

    // 3D text mesh for name above the entity's head
    [Header("Text Meshes")]
    public GameObject stunnedOverlay;

    [Header("Events")]
    public UnityEventEntity onAggro;
    public UnityEvent onSelect; // called when clicking it the first time
    public UnityEvent onInteract; // called when clicking it the second time
    public UnityEvent<Entity> OnKilledEnemy;
    // every entity can be stunned by setting stunTimeEnd
    [HideInInspector] public double stunTimeEnd;

    // safe zone flag
    // -> needs to be in Entity because both player and pet need it
    [HideInInspector] public bool inPvPZone;

    // look direction for animations and targetless skills
    // (NavMeshAgent itself just moves without actually looking anywhere)
    // => should always be normalized so that the animator doesn't do blending
    public Vector2 lookDirection = Vector2.down; // down by default

    // networkbehaviour ////////////////////////////////////////////////////////
    public override void OnStartServer()
    {
        // dead if spawned without health
        if (health.current == 0) _state = "DEAD";
    }

    protected virtual void Start()
    {
        // disable animator on server. this is a huge performance boost and
        // definitely worth one line of code (1000 monsters: 22 fps => 32 fps)
        // (!isClient because we don't want to do it in host mode either)
        // (OnStartServer doesn't know isClient yet, Start is the only option)
        if (!isClient) animator.enabled = false;
    }

    // server function to check which entities need to be updated.
    // monsters, npcs etc. don't have to be updated if no player is around
    // checking observers is enough, because lonely players have at least
    // themselves as observers, so players will always be updated
    // and dead monsters will respawn immediately in the first update call
    // even if we didn't update them in a long time (because of the 'end'
    // times)
    // -> update only if:
    //    - has observers
    //    - if the entity is hidden, otherwise it would never be updated again
    //      because it would never get new observers
    // -> can be overwritten if necessary (e.g. pets might be too far from
    //    observers but should still be updated to run to owner)
    // => only call this on server. client should always update!
    [Server]
    public virtual bool IsWorthUpdating() =>
        netIdentity.observers.Count > 0 ||
        IsHidden();

    // entity logic will be implemented with a finite state machine
    // -> we should react to every state and to every event for correctness
    // -> we keep it functional for simplicity
    // note: can still use LateUpdate for Updates that should happen in any case
    void Update()
    {
        // always apply speed to movement system
        // (if any. mounts don't have one)
        if (movement != null)
            movement.SetSpeed(speed);

        // always update all the objects that the client sees
        if (isClient)
        {
            UpdateClient();
        }

        // on server, only update if worth updating
        // (see IsWorthUpdating comments)
        // -> we also clear the target if it's hidden, so that players don't
        //    keep hidden (respawning) monsters as target, hence don't show them
        //    as target again when they are shown again
        if (isServer && IsWorthUpdating())
        {
            if (target != null && target.IsHidden()) target = null;
            _state = UpdateServer();
        }

        // update look direction on server and client (saves a SyncVar)
        // -> always look at move or target direction (if any), otherwise
        //    use the last one when IDLE
        // -> always orthonormal like (0,1) etc. and never (0, 0.5) so the blend
        //    tree doesn't actually blend between sprite animations
        // -> with default value so that default is played instead of nothing for
        //    Vector2.zero cases
        Vector2 velocity = movement.GetVelocity();
        if (velocity != Vector2.zero)
            lookDirection = Utils.OrthonormalVector2(velocity, lookDirection);
        else if (target != null)
            lookDirection = Utils.OrthonormalVector2(velocity, lookDirection);

        // update overlays in any case, except on server-only mode
        // (also update for character selection previews etc. then)
        if (!isServerOnly) UpdateOverlays();
    }

    // update for server. should return the new state.
    protected abstract string UpdateServer();

    // update for client.
    protected abstract void UpdateClient();

    // can be overwritten for more overlays
    protected virtual void UpdateOverlays()
    {
        if (stunnedOverlay != null)
        {
            bool isStunned = state == "STUNNED";
            stunnedOverlay.gameObject.SetActive(isStunned);

            // Get Animator component
            Animator stunAnimator = stunnedOverlay.GetComponent<Animator>();

            // Check if the Animator exists AND the GameObject is active
            if (stunAnimator != null && stunnedOverlay.activeInHierarchy)
            {
                stunAnimator.SetBool("STUNNED", isStunned);
            }
        }
    }



    // visibility //////////////////////////////////////////////////////////////
    // hide a entity
    // note: using SetActive won't work because its not synced and it would
    //       cause inactive objects to not receive any info anymore
    // note: this won't be visible on the server as it always sees everything.
    [Server]
    public void Hide() => netIdentity.visibility = Visibility.ForceHidden;

    [Server]
    public void Show() => netIdentity.visibility = Visibility.Default;

    // is the entity currently hidden?
    // note: usually the server is the only one who uses forceHidden, the
    //       client usually doesn't know about it and simply doesn't see the
    //       GameObject.
    public bool IsHidden() => netIdentity.visibility == Visibility.ForceHidden;

    public float VisRange() => ((SpatialHashingInterestManagement)NetworkServer.aoi).visRange;

    // revive //////////////////////////////////////////////////////////////////
    [Server]
    public void Revive(float healthPercentage = 1)
    {
        health.current = Mathf.RoundToInt(health.max * healthPercentage);
    }
    public float HealthPercent()
    {
        return (health.current != 0 && health.max != 0) ? (float)health.current / (float)health.max : 0;
    }

    // aggro ///////////////////////////////////////////////////////////////////
    // this function is called by the AggroArea (if any) on clients and server
    public virtual void OnAggro(Entity entity)
    {
        // addon system hooks
        onAggro.Invoke(entity);
    }

    // attack //////////////////////////////////////////////////////////////////
    // we need a function to check if an entity can attack another.
    // => overwrite to add more cases like 'monsters can only attack players'
    //    or 'player can attack pets but not own pet' etc.
    // => raycast NavMesh to prevent attacks through walls, while allowing
    //    attacks through steep hills etc. (unlike Physics.Raycast). this is
    //    very important to prevent exploits where someone might try to attack a
    //    boss monster through a dungeon wall, etc.
    public virtual bool CanAttack(Entity entity)
    {
        // Players can only attack other players inside PvP zones.
        if (this is Player attacker && entity is Player target)
        {
            return attacker.inPvPZone && target.inPvPZone;
        }
        return health.current > 0 && entity.health.current > 0 && entity != this;
    }

    // death ///////////////////////////////////////////////////////////////////
    // universal OnDeath function that takes care of all the Entity stuff.
    // should be called by inheriting classes' finite state machine on death.
    [Server]
    public virtual void OnDeath()
    {
        // clear target
        target = null;
    }

    // selection & interaction /////////////////////////////////////////////////
    // use Unity's OnMouseDown function. no need for raycasts.
    void OnMouseDown()
    {
        // joined world yet? (not character selection?)
        // not over UI? (avoid targeting through windows)
        // and in a state where local player can select things?
        if (Player.localPlayer != null &&
            !Utils.IsCursorOverUserInterface() &&
            (Player.localPlayer.state == "IDLE" ||
             Player.localPlayer.state == "MOVING" ||
             Player.localPlayer.state == "CASTING" ||
             Player.localPlayer.state == "STUNNED"))
        {
            // clear requested skill in any case because if we clicked
            // somewhere else then we don't care about it anymore
            Player.localPlayer.useSkillWhenCloser = -1;

            // set indicator in any case
            // (not just the first time, because we might have clicked on the
            //  ground in the mean time. always set it when selecting.)
            Player.localPlayer.indicator.SetViaParent(transform);

            // clicked for the first time: SELECT
            if (Player.localPlayer.target != this)
            {
                // target it
                Player.localPlayer.CmdSetTarget(netIdentity);

                // call OnSelect + hook
                OnSelect();
                onSelect.Invoke();
            }
            // clicked for the second time: INTERACT
            else
            {
                // call OnInteract + hook
                OnInteract();
                onInteract.Invoke();
            }
        }
    }

    protected virtual void OnSelect() {}
    protected abstract void OnInteract();

    // ontrigger ///////////////////////////////////////////////////////////////
    // protected so that inheriting classes can use OnTrigger too, while also
    // calling those here via base.OnTriggerEnter/Exit
    protected virtual void OnTriggerEnter2D(Collider2D col)
    {
        if (col.isTrigger && col.GetComponent<PvPZone>())
            inPvPZone = true;
    }

    protected virtual void OnTriggerExit2D(Collider2D col)
    {
        if (col.isTrigger && col.GetComponent<PvPZone>())
            inPvPZone = false;
    }
}
