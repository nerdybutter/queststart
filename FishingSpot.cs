using Mirror;
using UnityEngine;

public class FishingSpot : NetworkBehaviour
{
    public AudioClip fishingSound; // Sound to play when fishing

    public ItemDropChance[] rawFishDrops;   // Drop chances for Raw Fish
    public ItemDropChance[] troutDrops;    // Drop chances for Trout
    public ItemDropChance[] catfishDrops;  // Drop chances for Catfish
    public ItemDropChance[] swordfishDrops;// Drop chances for Swordfish
    public ItemDropChance[] squidDrops;    // Drop chances for Squid
    public ItemDropChance[] octopusDrops;    // Drop chances for Squid

    private AudioSource audioSource;
    public Color gizmoColor = new Color(1, 0, 0, 0.25f);
    public Color gizmoWireColor = new Color(1, 0, 0, 0.8f);

    void OnDrawGizmos()
    {
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.color = gizmoColor;
        Gizmos.DrawCube(collider.offset, collider.size);
        Gizmos.color = gizmoWireColor;
        Gizmos.DrawWireCube(collider.offset, collider.size);
        Gizmos.matrix = Matrix4x4.identity;
    }

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public bool CanBeFished()
    {
        // Always allow fishing unless respawning
        return true;
    }

    [Server]
    public void Fish(Player player)
    {
        if (!CanBeFished()) return;

        // Play fishing sound
        RpcPlayFishingSound();
        TryUpgradeFishingSkill(player);
        // Check fishing level and determine possible drops
        int fishingLevel = (player.skills as PlayerSkills)?.GetSkillLevel("Fishing") ?? 0;
        ItemDropChance[] possibleDrops = GetPossibleDrops(fishingLevel);

        if (possibleDrops != null && possibleDrops.Length > 0)
        {
            // Random chance to catch a fish
            if (Random.value <= 0.7f) // 70% chance to catch a fish
            {
                DropFish(player, possibleDrops);
            }
        }
    }

    private ItemDropChance[] GetPossibleDrops(int fishingLevel)
    {
        if (fishingLevel >= 100)
            return squidDrops;
        if (fishingLevel >= 80)
            return swordfishDrops;
        if (fishingLevel >= 40)
            return catfishDrops;
        if (fishingLevel >= 15)
            return troutDrops;

        return rawFishDrops; // Default to Raw Fish
    }

    [Server]
    private void DropFish(Player player, ItemDropChance[] dropChances)
    {
        Vector2 dropPosition = transform.position;

        foreach (ItemDropChance dropChance in dropChances)
        {
            if (Random.value <= dropChance.probability)
            {
                // Spawn the fish item
                ItemDrop fish = AddonItemDrop.GenerateLoot(dropChance.item.name, false, 0, dropPosition, dropPosition);
                NetworkServer.Spawn(fish.gameObject);
                return;
            }
        }
    }

    [Server]
    private void TryUpgradeFishingSkill(Player player)
    {
        // Random chance to upgrade fishing skill (1 in 16 chance)
        if (Random.Range(0, 16) == 0)
        {
            UpgradeSkillServer(player, "Fishing");
        }
    }

    [ClientRpc]
    private void RpcPlayFishingSound()
    {
        if (audioSource != null && fishingSound != null)
        {
            audioSource.PlayOneShot(fishingSound);
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
}
