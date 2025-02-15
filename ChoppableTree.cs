using Mirror;
using UnityEngine;

public class ChoppableTree : NetworkBehaviour
{
    public int maxDurability = 18; // Default maximum durability for all trees
    public Sprite choppedSprite; // Sprite to use when the tree is chopped
    public float minRespawnTime = 5f; // Minimum respawn time in seconds
    public float maxRespawnTime = 15f; // Maximum respawn time in seconds
    public ItemDropChance[] dropChances; // List of possible item drops
    public AudioClip choppingSound; // Sound to play when hit by an axe
    public AudioClip fallSound; // Sound to play when the tree falls

    private SpriteRenderer spriteRenderer;
    private Sprite originalSprite; // Original sprite of the tree
    private AudioSource audioSource;
    private int currentDurability;
    // SyncVar to track the state of the rock
    [SyncVar(hook = nameof(OnTreeStateChanged))]
    private TreeState currentState = TreeState.Normal;

    private void Start()
    {
        // Initialize components
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();
        // Save the original sprite
        originalSprite = spriteRenderer.sprite;
        // Set initial durability
        currentDurability = maxDurability;
        // Initialize the state
        UpdateVisualState();
    }

    public enum TreeState
    {
        Normal,
        Chopped
    }

    private void OnTreeStateChanged(TreeState oldState, TreeState newState)
    {
        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (currentState == TreeState.Normal)
        {
            spriteRenderer.sprite = originalSprite;
            //if (navMeshObstacle2D != null) navMeshObstacle2D.enabled = true;
        }
        else if (currentState == TreeState.Chopped)
        {
            spriteRenderer.sprite = choppedSprite;
           // if (navMeshObstacle2D != null) navMeshObstacle2D.enabled = false;
        }
    }
    
    [Server]
    public void Chop(Player player)
    {
        if (currentDurability <= 0) return;

        // Validate player and play mining sound
        if (player == null) return;
        RpcPlayChoppingSound();

        // Calculate damage based on mining level
        int damage = GetDamageBasedOnLumberjackingLevel(player);
        currentDurability -= damage;

        // Check if rock is fully mined
        if (currentDurability <= 0)
        {
            // Random chance for upgrading the Mining skill (1 in 16 chance)
            if (Random.Range(0, 2) == 0)
            {
                UpgradeSkillServer(player, "Lumberjacking");
            }

            ChangeToChoppedState(player);
        }
    }

    [ClientRpc]
    private void RpcPlayChoppingSound()
    {
        if (audioSource != null && choppingSound != null)
        {
            audioSource.PlayOneShot(choppingSound);
        }
    }

    [ClientRpc]
    private void RpcPlayFallSound()
    {
        if (audioSource != null && fallSound != null)
        {
            audioSource.PlayOneShot(fallSound);
        }
    }

    private int GetDamageBasedOnLumberjackingLevel(Player player)
    {
        // Get the player's "Lumberjacking" skill level
        int lumberjackingLevel = (player.skills as PlayerSkills)?.GetSkillLevel("Lumberjacking") ?? 0;

        // Determine how many hits it should take to chop the tree
        if (lumberjackingLevel > 90) return maxDurability / 3; // 3 hits
        else if (lumberjackingLevel > 80) return maxDurability / 4; // 4 hits
        else if (lumberjackingLevel > 70) return maxDurability / 6; // 6 hits
        else if (lumberjackingLevel > 60) return maxDurability / 8; // 8 hits
        else if (lumberjackingLevel > 50) return maxDurability / 10; // 10 hits
        else if (lumberjackingLevel > 40) return maxDurability / 12; // 12 hits
        else if (lumberjackingLevel > 30) return maxDurability / 13; // 13 hits
        else if (lumberjackingLevel > 20) return maxDurability / 14; // 14 hits
        else if (lumberjackingLevel > 10) return maxDurability / 15; // 15 hits
        else return 1; // Minimum damage (e.g., Lumberjacking level <= 10)
    }
    
    [Server]
    private void ChangeToChoppedState(Player player)
    {
        // Play fall sound
        RpcPlayFallSound();
        currentState = TreeState.Chopped;
        // Drop items on the server
        if (NetworkServer.active)
        {
            DropItems();
        }

        // Start the respawn timer
        StartCoroutine(RespawnTree());
    }

    [Server]
    private void UpgradeSkillServer(Player player, string skillName)
    {
        if (player != null && player.skills is PlayerSkills playerSkills)
        {
            playerSkills.LevelUpSkill(skillName);
        }
        else
        {
            Debug.LogWarning("Invalid player or missing PlayerSkills component.");
        }
    }

    [Server]
    private System.Collections.IEnumerator RespawnTree()
    {
        // Wait for a random respawn time
        float respawnTime = Random.Range(minRespawnTime, maxRespawnTime);
        yield return new WaitForSeconds(respawnTime);

        // Reset the tree's state
        currentDurability = maxDurability;
        currentState = TreeState.Normal;
    }
    
    [Server]
    private void DropItems()
    {
        if (AddonItemDrop.ItemPrefab == null)
        {
            Debug.LogError("[ChoppableTree] ItemPrefab is null in AddonItemDrop settings.");
            return;
        }

        Vector2 center = transform.position;

        foreach (ItemDropChance itemDropChance in dropChances)
        {
            if (Random.value <= itemDropChance.probability)
            {
                if (AddonItemDrop.RandomPoint2D(center, out Vector2 point))
                {
                    // Spawn individual or shared loot based on settings
                    if (ItemDropSettings.Settings.individualLoot)
                    {
                        RpcDropItem(itemDropChance.item.name, false, 0, center, point);
                    }
                    else
                    {
                        ItemDrop loot = AddonItemDrop.GenerateLoot(itemDropChance.item.name, false, 0, center, point);
                        NetworkServer.Spawn(loot.gameObject);
                    }
                }
                else
                {
                    Debug.LogWarning($"[ChoppableTree] Failed to find a valid drop position for {itemDropChance.item.name}.");
                }
            }
        }
    }

    [ClientRpc]
    private void RpcDropItem(string itemName, bool currency, long gold, Vector2 center, Vector2 point)
    {
        AddonItemDrop.GenerateLoot(itemName, currency, gold, center, point);
    }

    public bool CanBeChopped()
    {
        // Check if the tree can be chopped
        return currentDurability > 0;
    }
}
