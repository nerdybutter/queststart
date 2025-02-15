using UnityEngine;
using Mirror;
using System.Collections;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(MonsterInventory))]
[RequireComponent(typeof(NetworkNavMeshAgent2D))]
public partial class Monster : Entity
{
    [Header("Components")]
    public MonsterInventory inventory;

    [Header("Movement")]
    [Range(0, 1)] public float moveProbability = 0.1f; // chance per second
    public float moveDistance = 3;
    public float followDistance = 5;
    [Range(0.1f, 1)] public float attackToMoveRangeRatio = 0.8f;

    [Header("Experience Reward")]
    public long rewardExperience = 10;
    public long rewardSkillExperience = 2;

    [Header("Respawn")]
    public float deathTime = 30f; // enough for animation & looting
    double deathTimeEnd; // using double for long term precision
    public bool respawn = true;
    // Note: the fixed respawnTime field is replaced by a random range below.
    double respawnTimeEnd; // using double for long term precision

    [Header("Random Respawn")]
    public float minRespawnTime = 5f;  // minimum respawn time (in seconds)
    public float maxRespawnTime = 15f; // maximum respawn time (in seconds)

    [SyncVar, HideInInspector] public Player lastAggressor;
    // Save the start position for random movement and respawning.
    Vector2 startPosition;
    [SyncVar(hook = nameof(OnHealthBarChanged))]
    private bool showHealthBar = false;

    private MonsterUIHandler uiHandler;

    [Server]
    public void ShowHealthBar()
    {
        showHealthBar = true; // SyncVar automatically updates on clients
        StartCoroutine(HideHealthBarAfterDelay());
    }

    private IEnumerator HideHealthBarAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        showHealthBar = false; // SyncVar will sync to clients
    }

    private void OnHealthBarChanged(bool _, bool newValue)
    {
        if (uiHandler != null)
        {
            uiHandler.SetHealthBarVisibility(newValue);
        }
    }

    protected override void Start()
    {
        base.Start();
        uiHandler = GetComponent<MonsterUIHandler>();
        startPosition = transform.position;
    }

    void LateUpdate()
    {
        if (isClient) // No need for animations on the server.
        {
            animator.SetBool("MOVING", state == "MOVING" && movement.GetVelocity() != Vector2.zero);
            animator.SetBool("CASTING", state == "CASTING");
            foreach (Skill skill in skills.skills)
                animator.SetBool(skill.name, skill.CastTimeRemaining() > 0);
            animator.SetBool("STUNNED", state == "STUNNED");
            animator.SetBool("DEAD", state == "DEAD");
            animator.SetFloat("LookX", lookDirection.x);
            animator.SetFloat("LookY", lookDirection.y);
        }
    }

    void OnDrawGizmos()
    {
        Vector2 startHelp = Application.isPlaying ? startPosition : (Vector2)transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(startHelp, moveDistance);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(startHelp, followDistance);
    }

    // finite state machine events /////////////////////////////////////////////
    bool EventDied() { return health.current == 0; }
    bool EventDeathTimeElapsed() { return state == "DEAD" && NetworkTime.time >= deathTimeEnd; }
    bool EventRespawnTimeElapsed() { return state == "DEAD" && respawn && NetworkTime.time >= respawnTimeEnd; }
    bool EventTargetDisappeared() { return target == null; }
    bool EventTargetDied() { return target != null && target.health.current == 0; }
    bool EventTargetTooFarToAttack()
    {
        return target != null &&
               0 <= skills.currentSkill && skills.currentSkill < skills.skills.Count &&
               !skills.CastCheckDistance(skills.skills[skills.currentSkill], out Vector2 destination);
    }
    bool EventTargetTooFarToFollow()
    {
        return target != null &&
               Vector2.Distance(startPosition, target.collider.ClosestPointOnBounds(transform.position)) > followDistance;
    }
    bool EventTargetEnteredSafeZone() { return target != null && target.inPvPZone; }
    bool EventAggro() { return target != null && target.health.current > 0; }
    bool EventSkillRequest() { return 0 <= skills.currentSkill && skills.currentSkill < skills.skills.Count; }
    bool EventSkillFinished()
    {
        return 0 <= skills.currentSkill && skills.currentSkill < skills.skills.Count &&
                                       skills.skills[skills.currentSkill].CastTimeRemaining() == 0;
    }
    bool EventMoveEnd() { return state == "MOVING" && !movement.IsMoving(); }
    bool EventMoveRandomly() { return Random.value <= moveProbability * Time.deltaTime; }
    bool EventStunned() { return NetworkTime.time <= stunTimeEnd; }

    // finite state machine - server ///////////////////////////////////////////
    [Server]
    string UpdateServer_IDLE()
    {
        if (EventDied())
            return "DEAD";
        if (EventStunned())
        {
            movement.Reset();
            return "STUNNED";
        }
        if (EventTargetDied())
        {
            target = null;
            skills.CancelCast();
            return "IDLE";
        }
        if (EventTargetTooFarToFollow())
        {
            target = null;
            skills.CancelCast();
            movement.Navigate(startPosition, 0);
            return "MOVING";
        }
        if (EventTargetTooFarToAttack())
        {
            movement.Navigate(target.collider.ClosestPointOnBounds(transform.position),
                                ((MonsterSkills)skills).CurrentCastRange() * attackToMoveRangeRatio);
            return "MOVING";
        }
        if (EventTargetEnteredSafeZone())
        {
            // If the target enters a safe zone, immediately kill the monster.
            base.OnDeath(); // no looting
            float randomizedRespawnTime = Random.Range(minRespawnTime, maxRespawnTime);
            respawnTimeEnd = NetworkTime.time + randomizedRespawnTime;
            return "DEAD";
        }
        if (EventSkillRequest())
        {
            Skill skill = skills.skills[skills.currentSkill];
            if (skills.CastCheckSelf(skill))
            {
                if (skills.CastCheckTarget(skill))
                {
                    skills.StartCast(skill);
                    return "CASTING";
                }
                else
                {
                    target = null;
                    skills.currentSkill = -1;
                    return "IDLE";
                }
            }
            else
            {
                skills.currentSkill = -1;
                return "IDLE";
            }
        }
        if (EventAggro())
        {
            if (skills.skills.Count > 0)
                skills.currentSkill = ((MonsterSkills)skills).NextSkill();
            else
                Debug.LogError(name + " has no skills to attack with.");
            return "IDLE";
        }
        if (EventMoveRandomly())
        {
            Vector2 circle2D = Random.insideUnitCircle * moveDistance;
            movement.Navigate(startPosition + circle2D, 0);
            return "MOVING";
        }
        // Other events (DeathTimeElapsed, RespawnTimeElapsed, etc.) are handled in their state.
        return "IDLE";
    }

    [Server]
    string UpdateServer_MOVING()
    {
        if (EventDied())
        {
            movement.Reset();
            return "DEAD";
        }
        if (EventStunned())
        {
            movement.Reset();
            return "STUNNED";
        }
        if (EventMoveEnd())
            return "IDLE";
        if (EventTargetDied())
        {
            target = null;
            skills.CancelCast();
            movement.Reset();
            return "IDLE";
        }
        if (EventTargetTooFarToFollow())
        {
            target = null;
            skills.CancelCast();
            movement.Navigate(startPosition, 0);
            return "MOVING";
        }
        if (EventTargetTooFarToAttack())
        {
            movement.Navigate(target.collider.ClosestPointOnBounds(transform.position),
                                ((MonsterSkills)skills).CurrentCastRange() * attackToMoveRangeRatio);
            return "MOVING";
        }
        if (EventTargetEnteredSafeZone())
        {
            base.OnDeath(); // no looting
            float randomizedRespawnTime = Random.Range(minRespawnTime, maxRespawnTime);
            respawnTimeEnd = NetworkTime.time + randomizedRespawnTime;
            return "DEAD";
        }
        if (EventAggro())
        {
            if (skills.skills.Count > 0)
                skills.currentSkill = ((MonsterSkills)skills).NextSkill();
            else
                Debug.LogError(name + " has no skills to attack with.");
            movement.Reset();
            return "IDLE";
        }
        return "MOVING";
    }

    [Server]
    string UpdateServer_CASTING()
    {
        if (EventDied())
            return "DEAD";
        if (EventStunned())
        {
            skills.CancelCast();
            movement.Reset();
            return "STUNNED";
        }
        if (EventTargetDisappeared())
        {
            if (skills.skills[skills.currentSkill].cancelCastIfTargetDied)
            {
                skills.CancelCast();
                target = null;
                return "IDLE";
            }
        }
        if (EventTargetDied())
        {
            if (skills.skills[skills.currentSkill].cancelCastIfTargetDied)
            {
                skills.CancelCast();
                target = null;
                return "IDLE";
            }
        }
        if (EventTargetEnteredSafeZone())
        {
            if (skills.skills[skills.currentSkill].cancelCastIfTargetDied)
            {
                base.OnDeath(); // no looting
                float randomizedRespawnTime = Random.Range(minRespawnTime, maxRespawnTime);
                respawnTimeEnd = NetworkTime.time + randomizedRespawnTime;
                return "DEAD";
            }
        }
        if (EventSkillFinished())
        {
            skills.FinishCast(skills.skills[skills.currentSkill]);
            if (target != null && target.health.current == 0)
                target = null;
            ((MonsterSkills)skills).lastSkill = skills.currentSkill;
            skills.currentSkill = -1;
            return "IDLE";
        }
        return "CASTING";
    }

    [Server]
    string UpdateServer_STUNNED()
    {
        if (EventDied())
            return "DEAD";
        if (EventStunned())
            return "STUNNED";

        return "IDLE";
    }

    [Server]
    string UpdateServer_DEAD()
    {
        if (EventRespawnTimeElapsed())
        {
            // Respawn: reset gold, clear loot, make visible, move to start, and revive.
            gold = 0;
            inventory.slots.Clear();
            Show();
            movement.Warp(startPosition);
            Revive();
            return "IDLE";
        }
        if (EventDeathTimeElapsed())
        {
            // After death time elapsed, hide (if respawnable) or destroy.
            if (respawn) Hide();
            else NetworkServer.Destroy(gameObject);
            return "DEAD";
        }
        return "DEAD";
    }

    [Server]
    protected override string UpdateServer()
    {
        if (state == "IDLE") return UpdateServer_IDLE();
        if (state == "MOVING") return UpdateServer_MOVING();
        if (state == "CASTING") return UpdateServer_CASTING();
        if (state == "STUNNED") return UpdateServer_STUNNED();
        if (state == "DEAD") return UpdateServer_DEAD();
        Debug.LogError("invalid state:" + state);
        return "IDLE";
    }

    // finite state machine - client ///////////////////////////////////////////
    [Client]
    protected override void UpdateClient()
    {
        UpdateClient_ItemDrop();
    }

    // aggro ///////////////////////////////////////////////////////////////////
    [ServerCallback]
    public override void OnAggro(Entity entity)
    {
        if (entity != null && CanAttack(entity))
        {
            if (target == null)
            {
                target = entity;
            }
            else if (entity != target)
            {
                float oldDistance = Vector2.Distance(transform.position, target.transform.position);
                float newDistance = Vector2.Distance(transform.position, entity.transform.position);
                if (newDistance < oldDistance * 0.8f) target = entity;
            }
        }
    }

    // death ///////////////////////////////////////////////////////////////////
    public override void OnDeath()
    {
        // Process any additional entity death logic.
        base.OnDeath();

        // Set the death end time.
        deathTimeEnd = NetworkTime.time + deathTime;
        // Calculate a random respawn delay.
        float randomizedRespawnTime = Random.Range(minRespawnTime, maxRespawnTime);
        // Set the respawn end time after the death delay.
        respawnTimeEnd = deathTimeEnd + randomizedRespawnTime;
    }

    // attack //////////////////////////////////////////////////////////////////
    public override bool CanAttack(Entity entity)
    {
        return base.CanAttack(entity) &&
               (entity is Player ||
                entity is Pet ||
                entity is Mount);
    }

    // interaction /////////////////////////////////////////////////////////////
    protected override void OnInteract()
    {
        Player player = Player.localPlayer;

        if (player.CanAttack(this) && player.skills.skills.Count > 0)
        {
            ((PlayerSkills)player.skills).TryUse(0);
        }
        else
        {
            Vector2 destination = Utils.ClosestPoint(this, player.transform.position);
            player.movement.Navigate(destination, player.interactionRange);
        }
    }
}
