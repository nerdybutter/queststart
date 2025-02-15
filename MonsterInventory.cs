using UnityEngine;
using Mirror;

[DisallowMultipleComponent]
public partial class MonsterInventory : Inventory
{
    [Header("Components")]
    public Monster monster;

    [Header("Loot")]
    public int lootGoldMin = 0;
    public int lootGoldMax = 10;
    public ItemDropChance[] dropChances;
    //public ParticleSystem lootIndicator;
    // note: Items have a .valid property that can be used to 'delete' an item.
    //       it's better than .RemoveAt() because we won't run into index-out-of
    //       range issues

    [ClientCallback]
    void Update()
    {
        // show loot indicator on clients while it still has items
        /*if (lootIndicator != null)
        {
            // only set active once. we don't want to reset the particle
            // system all the time.
            bool hasLoot = HasLoot();
            if (hasLoot && !lootIndicator.isPlaying)
                lootIndicator.Play();
            else if (!hasLoot && lootIndicator.isPlaying)
                lootIndicator.Stop();
        }*/
    }

    // other scripts need to know if it still has valid loot (to show UI etc.)
    public bool HasLoot()
    {
        // any gold or valid items?
        return monster.gold > 0 || SlotsOccupied() > 0;
    }

    [Server]
    public void OnDeath()
    {
        // Get the player who dealt the killing blow
        Player killer = monster.lastAggressor as Player;
        if (killer == null) return; // Ensure valid player

        // generate gold drop
        monster.gold = Random.Range(lootGoldMin, lootGoldMax);

        // generate non-quest items (note: can't use Linq because of SyncList)
        foreach (ItemDropChance itemChance in dropChances)
        {
            // Ignore quest items here
            if (itemChance.item is QuestItem) continue;

            if (Random.value <= itemChance.probability)
            {
                slots.Add(new ItemSlot(new Item(itemChance.item)));
            }
        }

        // Generate quest item drops
        foreach (ItemDropChance itemChance in dropChances)
        {
            // Ensure it's a quest item
            QuestItem questItem = itemChance.item as QuestItem;
            if (questItem == null) continue;

            // Check if the player needs the quest item
            bool hasQuestActive = killer.quests.HasActive(questItem.linkedQuestName);
            int playerItemCount = killer.inventory.CountTotal(questItem);
            bool needsMore = playerItemCount < questItem.maxStack;

            // Only drop the item if the player is on the quest and still needs it
            if (hasQuestActive && needsMore && Random.value <= itemChance.probability)
            {
                slots.Add(new ItemSlot(new Item(questItem)));
            }
        }

        // Player Death Item Drop functionality
#if ITEM_DROP_R
        OnDeath_ItemDrop();
#endif
    }



}