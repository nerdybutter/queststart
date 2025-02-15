using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Mirror;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class TargetlessProjectileSkillEffect : SkillEffect
{
    [Header("Components")]
    public Animator animator;
    private Rigidbody2D rb;

    [Header("Properties")]
    public float speed = 5;
    [HideInInspector] public int damage = 1; // set by skill
    [HideInInspector] public float stunChance; // set by skill
    [HideInInspector] public float stunTime; // set by skill

    [SyncVar, HideInInspector] public Vector2 direction;

    public float autoDestroyDistance = 10;

    Vector2 initialPosition;

    public UnityEvent onSetInitialPosition;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();

        // Remember start position for distance checks
        initialPosition = transform.position;

        // Move via Rigidbody into synced direction on server & client
        rb.velocity = direction * speed;
    }

    public override void OnStartClient()
    {
        SetInitialPosition();
    }

    void SetInitialPosition()
    {
        if (caster != null)
        {
            transform.position = caster.skills.effectMount.position;
            onSetInitialPosition.Invoke();
        }
    }

    void FixedUpdate()
    {
        if (caster != null)
        {
            Vector3 currentPosition = transform.position;
            Vector3 nextPosition = currentPosition + (Vector3)(direction * speed * Time.fixedDeltaTime);

            // Use NavMesh.Raycast to check if the next position is blocked
            NavMeshHit navHit;
            if (!NavMesh.Raycast(currentPosition, nextPosition, out navHit, NavMesh.AllAreas))
            {
                // Check distance and destroy projectile if too far
                if (isServer && Vector2.Distance(initialPosition, currentPosition) >= autoDestroyDistance)
                {
                    NetworkServer.Destroy(gameObject);
                }
            }
            else
            {
                // Projectile hit a blocked area, stop and destroy it
                Debug.Log("Projectile hit a blocked NavMesh area. Stopping projectile.");
                StopProjectile();
            }
        }
        else if (isServer)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    [ServerCallback]
    void OnTriggerEnter2D(Collider2D co)
    {
        Entity entity = co.GetComponentInParent<Entity>();

        // Hit something valid that is not the caster
        if (entity != null && entity != caster && caster.CanAttack(entity))
        {
            if (entity.health.current > 0)
            {
                caster.combat.DealDamageAt(entity, caster.combat.damage + damage, stunChance, stunTime);
            }
            NetworkServer.Destroy(gameObject);
        }
    }

    void StopProjectile()
    {
        // Stop movement and animations
        if (rb != null) rb.velocity = Vector2.zero;

        // Stop animation if any
        if (animator != null)
        {
            animator.SetFloat("DirectionX", 0);
            animator.SetFloat("DirectionY", 0);
        }

        // Disable any particle effects
        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in particleSystems)
        {
            ps.Stop();
        }

        // Immediately destroy the object
        if (isServer)
        {
            NetworkServer.Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    [ClientCallback]
    void Update()
    {
        if (animator != null)
        {
            animator.SetFloat("DirectionX", direction.x);
            animator.SetFloat("DirectionY", direction.y);
        }
    }
}
