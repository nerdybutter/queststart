using Mirror;
using UnityEngine;

public class MiningRock : NetworkBehaviour
{
    public int maxDurability = 18; // Default maximum durability for all rocks
    public Sprite normalSprite; // Original rock sprite
    public Sprite minedSprite; // Sprite to use when the rock is mined
    public float minRespawnTime = 5f; // Minimum respawn time in seconds
    public float maxRespawnTime = 15f; // Maximum respawn time in seconds
    public ItemDropChance[] dropChances; // List of possible item drops
    public AudioClip miningSound; // Sound to play when hit by pickaxe
    public AudioClip crumbleSound; // Sound to play when the rock is mined

    private SpriteRenderer spriteRenderer;
    private NavMeshObstacle2D navMeshObstacle2D;
    private Sprite originalSprite; // Original sprite of the rock
    private AudioSource audioSource;
    private int currentDurability;

    // SyncVar to track the state of the rock
    [SyncVar(hook = nameof(OnRockStateChanged))]
    private RockState currentState = RockState.Normal;

    private void Start()
    {
        // Initialize components
        spriteRenderer = GetComponent<SpriteRenderer>();
        navMeshObstacle2D = GetComponent<NavMeshObstacle2D>();
        audioSource = GetComponent<AudioSource>();
        originalSprite = spriteRenderer.sprite;
        currentDurability = maxDurability; 
        // Set initial durability
        // Initialize the state
        UpdateVisualState();
    }

    private void OnRockStateChanged(RockState oldState, RockState newState)
    {
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (currentState == RockState.Normal)
        {
            spriteRenderer.sprite = normalSprite;
            if (navMeshObstacle2D != null) navMeshObstacle2D.enabled = true;
        }
        else if (currentState == RockState.Mined)
        {
            spriteRenderer.sprite = minedSprite;
            if (navMeshObstacle2D != null) navMeshObstacle2D.enabled = false;
        }
    }

    public bool CanBeMined()
    {
        // Check if the rock has durability left
        return currentDurability > 0;
    }

    public enum RockState
    {
        Normal,
        Mined
    }

    [Server]
    public void Mine(Player player)
    {
        if (currentDurability <= 0) return;

        // Validate player and play mining sound
        if (player == null) return;
        RpcPlayMiningSound();

        // Calculate damage based on mining level
        int damage = GetDamageBasedOnMiningLevel(player);
        currentDurability -= damage;

        // Check if rock is fully mined
        if (currentDurability <= 0)
        {
            // Random chance for upgrading the Mining skill (1 in 16 chance)
            if (Random.Range(0, 15) == 0)
            {
                UpgradeSkillServer(player, "Mining");
            }

            ChangeToMinedState(player);
        }
    }


    private int GetDamageBasedOnMiningLevel(Player player)
    {
        int miningLevel = (player.skills as PlayerSkills)?.GetSkillLevel("Mining") ?? 0;

        if (miningLevel > 90) return maxDurability / 3; // 3 hits
        if (miningLevel > 80) return maxDurability / 4; // 4 hits
        if (miningLevel > 70) return maxDurability / 6; // 6 hits
        if (miningLevel > 60) return maxDurability / 8; // 8 hits
        if (miningLevel > 50) return maxDurability / 10; // 10 hits
        if (miningLevel > 40) return maxDurability / 12; // 12 hits
        if (miningLevel > 30) return maxDurability / 13; // 13 hits
        if (miningLevel > 20) return maxDurability / 14; // 14 hits
        if (miningLevel > 10) return maxDurability / 15; // 15 hits
        return 1; // Default damage
    }

    [ClientRpc]
    private void RpcPlayMiningSound()
    {
        if (audioSource != null && miningSound != null)
        {
            audioSource.PlayOneShot(miningSound);
        }
    }

    [ClientRpc]
    private void RpcPlayCrumbleSound()
    {
        if (audioSource != null && crumbleSound != null)
        {
            audioSource.PlayOneShot(crumbleSound);
        }
    }

    [Server]
    private void ChangeToMinedState(Player player)
    {
        RpcPlayCrumbleSound();

        if (NetworkServer.active)
        {
            currentState = RockState.Mined;
            if (navMeshObstacle2D != null)
            {
                navMeshObstacle2D.enabled = false;
            }

            DropItems(player); // Pass the player object here
            StartCoroutine(RespawnRock());
        }
    }


    private System.Collections.IEnumerator RespawnRock()
    {
        float respawnTime = Random.Range(5f, 15f);
        yield return new WaitForSeconds(respawnTime);
        // Reset the rock's state
        currentDurability = maxDurability;
        // Reset state to Normal on the server
        currentState = RockState.Normal;
        if (NetworkServer.active)
        {
            RpcEnableNavMeshObstacle();
        }
    }

    // RPC to disable NavMeshObstacle on all clients
    [ClientRpc]
    private void RpcDisableNavMeshObstacle()
    {
        if (navMeshObstacle2D != null)
        {
            navMeshObstacle2D.enabled = false;
        }
    }

    [ClientRpc]
    private void RpcEnableNavMeshObstacle()
    {
        if (navMeshObstacle2D != null)
        {
            navMeshObstacle2D.enabled = true;
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



    [Server]
    private void DropItems(Player player)
    {
        if (!NetworkServer.active) return;

        Vector2 center = transform.position;

        foreach (ItemDropChance dropChance in dropChances)
        {
            if (Random.value <= dropChance.probability)
            {
                // Calculate the number of items to drop based on the player's mining level
                int numOfCrystals = GetCrystalQuantityBasedOnMiningLevel(player);

                for (int i = 0; i < numOfCrystals; i++)
                {
                    if (AddonItemDrop.RandomPoint2D(center, out Vector2 point))
                    {
                        ItemDrop loot = AddonItemDrop.GenerateLoot(dropChance.item.name, false, 0, center, point);
                        NetworkServer.Spawn(loot.gameObject);
                    }
                }
            }
        }
    }

    private int GetCrystalQuantityBasedOnMiningLevel(Player player)
    {
        int miningLevel = (player.skills as PlayerSkills)?.GetSkillLevel("Mining") ?? 0;

        if (miningLevel > 90) return Random.Range(1, 7); // 1 to 6
        if (miningLevel > 80) return Random.Range(1, 6); // 1 to 5
        if (miningLevel > 70) return Random.Range(1, 6); // 1 to 5
        if (miningLevel > 60) return Random.Range(1, 5); // 1 to 4
        if (miningLevel > 50) return Random.Range(1, 5); // 1 to 4
        if (miningLevel > 40) return Random.Range(1, 4); // 1 to 3
        if (miningLevel > 30) return Random.Range(1, 4); // 1 to 3
        if (miningLevel > 20) return Random.Range(1, 3); // 1 to 2
        if (miningLevel > 10) return Random.Range(1, 3); // 1 to 2
        return 1; // Default to 1
    }


}
