using UnityEngine;
using Mirror;

public class SpikeTrap : NetworkBehaviour
{
    [Header("Trap Settings")]
    [SyncVar(hook = nameof(OnIsActiveChanged))]
    public bool isActive = true;
    public float defaultDisarmedDuration = 10f;

    [Header("Sprites")]
    public Sprite activeSprite;
    public Sprite disarmedSprite;

    [Header("Required Item")]
    public ScriptableItem requiredItem;

    [Header("Sound Effects")]
    public AudioClip disarmSoundEffect;  // New field for disarm sound

    private AudioSource audioSource;

    [Header("Animator")]
    public Animator trapAnimator;

    private SpriteRenderer spriteRenderer;
    private Collider2D trapCollider;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        trapCollider = GetComponent<Collider2D>();

        if (trapAnimator == null)
            trapAnimator = GetComponent<Animator>();

        UpdateTrapState();
    }

    private void OnIsActiveChanged(bool oldValue, bool newValue)
    {
        UpdateTrapState();
    }

    private void UpdateTrapState()
    {
        if (spriteRenderer != null)
            spriteRenderer.sprite = isActive ? activeSprite : disarmedSprite;

        if (trapAnimator != null)
            trapAnimator.enabled = isActive;

        if (trapCollider != null)
            trapCollider.enabled = isActive;
    }

    [Server]
    public void DisarmTrap()
    {
        if (!isActive) return;

        isActive = false;
        UpdateTrapState();
        RpcPlayDisarmSound();
        RpcRequestClientToRemoveItems(requiredItem.name.GetStableHashCode(), 1);

        Invoke(nameof(RearmTrap), defaultDisarmedDuration);
    }

    [ClientRpc]
    private void RpcPlayDisarmSound()
    {
        if (disarmSoundEffect != null && audioSource != null)
        {
            audioSource.PlayOneShot(disarmSoundEffect);
            Debug.Log("[CLIENT] Playing disarm sound effect.");
        }
        else
        {
            Debug.LogWarning("[CLIENT] No sound effect or audio source available.");
        }
    }

    [Server]
    public void DisarmTrap(Player player)
    {
        DisarmTrap();  // Call the parameterless version
    }


    [ClientRpc]
    public void RpcRequestClientToRemoveItems(int itemHash, int amount)
    {
        PlayerInventory inventory = Player.localPlayer.GetComponent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogError("[CLIENT] PlayerInventory component not found!");
            return;
        }

        // Retrieve the item from the hash
        if (!ScriptableItem.All.TryGetValue(itemHash, out ScriptableItem itemData))
        {
            Debug.LogError($"[CLIENT] Item with hash {itemHash} not found.");
            return;
        }

        // Convert ScriptableItem to Item and request removal
        Item item = new Item(itemData);
        inventory.RemoveItem(item, amount);
    }


    [Server]
    private void RearmTrap()
    {
        isActive = true;
        UpdateTrapState();

        // Refresh the collider by temporarily disabling it and then enabling it
        if (trapCollider != null)
        {
            trapCollider.enabled = false;  // Disable briefly
            Invoke(nameof(ReenableCollider), 0.1f);  // Re-enable after a short delay
        }
    }

    private void ReenableCollider()
    {
        if (trapCollider != null)
        {
            trapCollider.enabled = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;

        Player player = other.GetComponent<Player>();
        if (player != null && player.isServer)
        {
            player.health.current = 0;
            player.OnDeath();
        }
    }
}
