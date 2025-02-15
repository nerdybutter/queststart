using System;
using UnityEngine;
using UnityEngine.Events;
using Mirror;
using System.Collections.Generic;

public enum DamageType { Normal, Block, Crit }

// inventory, attributes etc. can influence max health
public interface ICombatBonus
{
    int GetDamageBonus();
    int GetDefenseBonus();
    float GetCriticalChanceBonus();
    float GetBlockChanceBonus();
}

[Serializable] public class UnityEventIntDamageType : UnityEvent<int, DamageType> {}

[DisallowMultipleComponent]
public class Combat : NetworkBehaviour
{
    [Header("Components")]
    public Level level;
    public Entity entity;
    public new Collider2D collider;
    public GameObject BlacksmithingEffectPrefab; // Assign this in the Inspector

    [Header("Stats")]
    [SyncVar] public bool invincible = false; // GMs, Npcs, ...
    public LinearInt baseDamage = new LinearInt{baseValue=1};
    public LinearInt baseDefense = new LinearInt{baseValue=1};
    public LinearFloat baseBlockChance;
    public LinearFloat baseCriticalChance;

    [Header("Damage Popup")]
    public GameObject damagePopupPrefab;

    // events
    [Header("Events")]
    public UnityEventEntity onDamageDealtTo;
    public UnityEventEntity onKilledEnemy;
    public UnityEventEntityInt onServerReceivedDamage;
    public UnityEventIntDamageType onClientReceivedDamage;

    // cache components that give a bonus (attributes, inventory, etc.)
    ICombatBonus[] _bonusComponents;
    ICombatBonus[] bonusComponents =>
        _bonusComponents ?? (_bonusComponents = GetComponents<ICombatBonus>());

    void Start()
    {
        if (isServer)
        {
            onServerReceivedDamage.AddListener(OnReceivedDamageServer);
        }
    }

    [Server]
    private void OnReceivedDamageServer(Entity attacker, int amount)
    {
        // 1 in 8 chance to reduce durability
        if (UnityEngine.Random.Range(0, 8) == 0)
        {
            ReduceRandomEquipmentDurability();
        }
        // Get the current Parry level
        Player player = GetComponent<Player>();
        if (player != null)
        {
            int parryLevel = (player.skills as PlayerSkills)?.GetSkillLevel("Parry") ?? 1;

            // Use Parry level in the RNG check
            if (UnityEngine.Random.Range(0, parryLevel) == 0)
            {
                UpgradeSkillServer("Parry");
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


    // calculate damage
    public int damage
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            int bonus = 0;
            foreach (ICombatBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetDamageBonus();
            return baseDamage.Get(level.current) + bonus;
        }
    }

    // calculate defense
    public int defense
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            int bonus = 0;
            foreach (ICombatBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetDefenseBonus();
            return baseDefense.Get(level.current) + bonus;
        }
    }

    // calculate block
    public float blockChance
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            float bonus = 0;
            foreach (ICombatBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetBlockChanceBonus();
            return baseBlockChance.Get(level.current) + bonus;
        }
    }

    // calculate critical
    public float criticalChance
    {
        get
        {
            // sum up manually. Linq.Sum() is HEAVY(!) on GC and performance (190 KB/call!)
            float bonus = 0;
            foreach (ICombatBonus bonusComponent in bonusComponents)
                bonus += bonusComponent.GetCriticalChanceBonus();
            return baseCriticalChance.Get(level.current) + bonus;
        }
    }

    // combat //////////////////////////////////////////////////////////////////
    // deal damage at another entity
    // (can be overwritten for players etc. that need custom functionality)
    [Server]
    public virtual void DealDamageAt(Entity victim, int amount, float stunChance = 0, float stunTime = 0)
    {
        Combat victimCombat = victim.combat;
        int damageDealt = 0;
        DamageType damageType = DamageType.Normal;

        // don't deal any damage if entity is invincible
        if (!victimCombat.invincible)
        {
            // block? (we use < not <= so that block rate 0 never blocks)
            if (UnityEngine.Random.value < victimCombat.blockChance)
            {
                damageType = DamageType.Block;
            }
            // deal damage
            else
            {
                // subtract defense (but leave at least 1 damage, otherwise
                // it may be frustrating for weaker players)
                damageDealt = Mathf.Max(amount - victimCombat.defense, 1);

                // critical hit?
                if (UnityEngine.Random.value < criticalChance)
                {
                    damageDealt *= 2;
                    damageType = DamageType.Crit;
                }

                // deal the damage
                victim.health.current -= damageDealt;

                // Track the last aggressor if the victim is a Player
                if (victim is Player player)
                {
                    player.lastAggressor = this.entity; // Set the entity dealing damage as the last aggressor
                }

                // call OnServerReceivedDamage event on the target
                // -> can be used for monsters to pull aggro
                // -> can be used by equipment to decrease durability etc.
                victimCombat.onServerReceivedDamage.Invoke(entity, damageDealt);

                // stun?
                if (UnityEngine.Random.value < stunChance)
                {
                    // don't allow a short stun to overwrite a long stun
                    // => if a player is hit with a 10s stun, immediately
                    //    followed by a 1s stun, we don't want it to end in 1s!
                    double newStunEndTime = NetworkTime.time + stunTime;
                    victim.stunTimeEnd = Math.Max(newStunEndTime, entity.stunTimeEnd);
                }

                // Random durability reduction on the weapon
                ReduceWeaponDurability();
            }

            // call OnDamageDealtTo / OnKilledEnemy events
            onDamageDealtTo.Invoke(victim);
            if (victim.health.current == 0)
                onKilledEnemy.Invoke(victim);
        }

        // let's make sure to pull aggro in any case so that archers
        // are still attacked if they are outside of the aggro range
        victim.OnAggro(entity);

        // show effects on clients
        victimCombat.RpcOnReceivedDamaged(damageDealt, damageType);

        // reset last combat time for both
        entity.lastCombatTime = NetworkTime.time;
        victim.lastCombatTime = NetworkTime.time;
    }


    [Server]
    private void ReduceWeaponDurability()
    {
        PlayerEquipment equipment = GetComponent<PlayerEquipment>();
        if (equipment == null) return;  // No PlayerEquipment found

        // Get the equipped weapon index
        int weaponIndex = equipment.GetEquippedWeaponIndex();

        // Ensure the weapon slot is valid
        if (weaponIndex != -1 && ScriptableItem.All.ContainsKey(equipment.slots[weaponIndex].item.hash))
        {
            // Access the item slot and its item instance
            ItemSlot slot = equipment.slots[weaponIndex];
            Item weaponItem = slot.item;

            // Ensure the item is an EquipmentItem
            if (weaponItem.data is EquipmentItem equipmentItem && weaponItem.HasDurability() && !equipmentItem.isIndestructible)
            {
                // 1 in 8 chance for durability reduction
                if (UnityEngine.Random.Range(0, 8) == 0)
                {
                    // Reduce the durability on the item instance
                    weaponItem.durability = Mathf.Max(weaponItem.durability - 1, 0);

                    // Update the slot to sync changes
                    slot.item = weaponItem;
                    equipment.slots[weaponIndex] = slot;  // SyncList change triggers network sync
                }
            }
        }
    }



    // no need to instantiate damage popups on the server
    // -> calculating the position on the client saves server computations and
    //    takes less bandwidth (4 instead of 12 byte)
    [Client]
    void ShowDamagePopup(int amount, DamageType damageType)
    {
        // spawn the damage popup (if any) and set the text
        if (damagePopupPrefab != null)
        {
            // showing it above their head looks best, and we don't have to use
            // a custom shader to draw world space UI in front of the entity
            Bounds bounds = collider.bounds;
            Vector2 position = new Vector2(bounds.center.x, bounds.max.y);

            GameObject popup = Instantiate(damagePopupPrefab, position, Quaternion.identity);
            if (damageType == DamageType.Normal)
                popup.GetComponentInChildren<TextMesh>().text = amount.ToString();
            else if (damageType == DamageType.Block)
                popup.GetComponentInChildren<TextMesh>().text = "<i>Block!</i>";
            else if (damageType == DamageType.Crit)
                popup.GetComponentInChildren<TextMesh>().text = amount + " Crit!";
        }
    }

    [ClientRpc]
    public void RpcOnReceivedDamaged(int amount, DamageType damageType)
    {
        // Show damage popup on all clients
        ShowDamagePopup(amount, damageType);

        // Call OnClientReceivedDamage event
        onClientReceivedDamage.Invoke(amount, damageType);
    }

    [Server]
    public void ApplyDamageAndReduceDurability(int amount, DamageType damageType)
    {
        // Handle durability reduction logic on the server
        if (UnityEngine.Random.Range(0, 8) == 0)  // 1 in 8 chance
        {
            ReduceRandomEquipmentDurability();
        }

        // Notify all clients of the damage received
        RpcOnReceivedDamaged(amount, damageType);
    }

    [Server]
    private void ReduceRandomEquipmentDurability()
    {
        PlayerEquipment equipment = GetComponent<PlayerEquipment>();
        if (equipment == null)
        {
            return;
        }

        int weaponIndex = equipment.GetEquippedWeaponIndex();

        // Collect valid equipment items with durability
        List<int> durabilityItemIndices = new List<int>();
        for (int i = 0; i < equipment.slots.Count; i++)
        {
            if (i == weaponIndex)
            {
                continue; // Skip weapon slot
            }

            ItemSlot slot = equipment.slots[i];

            // Check for items with durability that are not indestructible
            if (slot.item.hash != 0 && ScriptableItem.All.ContainsKey(slot.item.hash))
            {
                if (slot.item.HasDurability() && slot.item.data is EquipmentItem eqItem && !eqItem.isIndestructible)
                {
                    durabilityItemIndices.Add(i);
                }
            }
        }

        // If no valid items, do nothing
        if (durabilityItemIndices.Count == 0)
        {
            return;
        }

        // Pick a random item to reduce durability
        int randomIndex = UnityEngine.Random.Range(0, durabilityItemIndices.Count);
        int slotIndex = durabilityItemIndices[randomIndex];
        ItemSlot selectedSlot = equipment.slots[slotIndex];

        // Reduce durability
        selectedSlot.item.durability = Mathf.Max(selectedSlot.item.durability - 1, 0);
        equipment.slots[slotIndex] = selectedSlot;  // Update slot to trigger sync

        // Check for durability reaching 0 and handle item destruction
        if (selectedSlot.item.durability == 0 && selectedSlot.item.data is EquipmentItem equipmentItem)
        {
            // Spawn visual effect
            GameObject effect = Instantiate(BlacksmithingEffectPrefab, transform.position, Quaternion.identity);
            NetworkServer.Spawn(effect);
            Destroy(effect, 1f);

            // Replace with damaged version if available
            if (equipmentItem.damagedVersion != null)
            {
                selectedSlot.item = new Item(equipmentItem.damagedVersion);
                equipment.slots[slotIndex] = selectedSlot;  // Update the slot again to sync
                GetComponent<PlayerChat>().TargetMsgInfo($"Your {equipmentItem.name} has shattered.");
            }
        }
    }







}
