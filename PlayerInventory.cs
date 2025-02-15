using UnityEngine;
using Mirror;
using UnityEditor;

[RequireComponent(typeof(PlayerTrading))]
public partial class PlayerInventory : Inventory
{
    [Header("Components")]
    public Player player;

    [Header("Inventory")]
    public int size = 30;
    public ScriptableItemAndAmount[] defaultItems;
    public KeyCode[] splitKeys = { KeyCode.LeftShift, KeyCode.RightShift };

    [Header("Trash")]
    [SyncVar] public ItemSlot trash;

    // are inventory operations like swap, merge, split allowed at the moment?
    // -> trading offers are inventory indices. we don't allow any inventory
    //    operations while trading to guarantee the trade offer indices don't
    //    get messed up when swapping items with one of the indices.
    public bool InventoryOperationsAllowed()
    {
        return player.state == "IDLE" ||
               player.state == "MOVING" ||
               player.state == "CASTING";
    }

    public bool HasEnough(Item item, int amount)
    {
        foreach (var slot in slots)
        {
            // Skip empty slots
            if (slot.amount == 0 || slot.item.name == string.Empty)
                continue;

            // Check if the item matches and has enough quantity
            if (slot.item.hash == item.hash && slot.amount >= amount)
            {
                return true;
            }
        }

        return false;
    }

    public int CountTotal(ScriptableItem item)
    {
        int total = 0;
        foreach (ItemSlot slot in slots)
        {
            if (slot.amount > 0 && slot.item.data == item)
            {
                total += slot.amount;
            }
        }
        return total;
    }


    [Command]
public void RemoveItem(Item item, int amount)
{
    if (!isServer) return;

    // Proceed with item removal logic
    for (int i = 0; i < slots.Count; i++)
    {
        ItemSlot slot = slots[i];

        if (slot.item.hash == item.hash)
        {
            if (slot.amount >= amount)
            {
                slot.DecreaseAmount(amount);
                if (slot.amount == 0)
                {
                    slots[i] = new ItemSlot();  // Clear the slot
                }
                else
                {
                    slots[i] = slot;  // Update the slot
                }

                // Save changes to the inventory
                Database.singleton.SavePlayerInventory(this);
                return;
            }
            else
            {
                return;  // Not enough items to remove
            }
        }
    }

    // Item not found in inventory
}


    [ClientRpc]
    public void RpcUpdateInventorySlot(int slotIndex, ItemSlot updatedSlot)
    {
        if (slotIndex >= 0 && slotIndex < slots.Count)
        {
            slots[slotIndex] = updatedSlot;
            Debug.Log($"[CLIENT] Inventory slot {slotIndex} updated.");
        }
    }



    [Client]
    public void RemoveItemClient(Item item, int amount)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].item.hash == item.hash)
            {
                if (slots[i].amount >= amount)
                {
                    slots[i].DecreaseAmount(amount);

                    if (slots[i].amount == 0)
                    {
                        slots[i] = new ItemSlot();  // Clear the slot
                    }

                    return;
                }
            }
        }

        Debug.LogWarning($"Failed to remove item: {item.name} on the client.");
    }






    [Command]
    public void CmdSwapInventoryTrash(int inventoryIndex)
    {
        // dragging an inventory item to the trash always overwrites the trash
        if (InventoryOperationsAllowed() &&
            0 <= inventoryIndex && inventoryIndex < slots.Count)
        {
            // inventory slot has to be valid and destroyable and not summoned
            ItemSlot slot = slots[inventoryIndex];
            if (slot.amount > 0 && slot.item.destroyable && !slot.item.summoned)
            {
                // overwrite trash
                trash = slot;

                // clear inventory slot
                slot.amount = 0;
                slots[inventoryIndex] = slot;
            }
        }
    }

    [Command]
    public void CmdMoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex >= 0 && fromIndex < slots.Count && toIndex >= 0 && toIndex < slots.Count)
        {
            // Swap or move the item within the inventory
            ItemSlot temp = slots[fromIndex];
            slots[fromIndex] = slots[toIndex];
            slots[toIndex] = temp;

            // Save changes if necessary (modify according to your data persistence logic)
            Database.singleton.SavePlayerInventory(this);
        }
    }

    [Command]
    public void CmdSwapTrashInventory(int inventoryIndex)
    {
        if (InventoryOperationsAllowed() &&
            0 <= inventoryIndex && inventoryIndex < slots.Count)
        {
            // inventory slot has to be empty or destroyable
            ItemSlot slot = slots[inventoryIndex];
            if (slot.amount == 0 || slot.item.destroyable)
            {
                // swap them
                slots[inventoryIndex] = trash;
                trash = slot;
            }
        }
    }

    [Command]
    public void CmdSwapInventoryInventory(int fromIndex, int toIndex)
    {
        // note: should never send a command with complex types!
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if (InventoryOperationsAllowed() &&
            0 <= fromIndex && fromIndex < slots.Count &&
            0 <= toIndex && toIndex < slots.Count &&
            fromIndex != toIndex)
        {
            // swap them
            ItemSlot temp = slots[fromIndex];
            slots[fromIndex] = slots[toIndex];
            slots[toIndex] = temp;
        }
    }

    [Command]
    public void CmdInventorySplit(int fromIndex, int toIndex)
    {
        // note: should never send a command with complex types!
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if (InventoryOperationsAllowed() &&
            0 <= fromIndex && fromIndex < slots.Count &&
            0 <= toIndex && toIndex < slots.Count &&
            fromIndex != toIndex)
        {
            // slotFrom needs at least two to split, slotTo has to be empty
            ItemSlot slotFrom = slots[fromIndex];
            ItemSlot slotTo = slots[toIndex];
            if (slotFrom.amount >= 2 && slotTo.amount == 0)
            {
                // split them serversided (has to work for even and odd)
                slotTo = slotFrom; // copy the value

                slotTo.amount = slotFrom.amount / 2;
                slotFrom.amount -= slotTo.amount; // works for odd too

                // put back into the list
                slots[fromIndex] = slotFrom;
                slots[toIndex] = slotTo;
            }
        }
    }

    [Command]
    public void CmdInventoryMerge(int fromIndex, int toIndex)
    {
        if (InventoryOperationsAllowed() &&
            0 <= fromIndex && fromIndex < slots.Count &&
            0 <= toIndex && toIndex < slots.Count &&
            fromIndex != toIndex)
        {
            // both items have to be valid
            ItemSlot slotFrom = slots[fromIndex];
            ItemSlot slotTo = slots[toIndex];
            if (slotFrom.amount > 0 && slotTo.amount > 0)
            {
                // make sure that items are the same type
                // note: .Equals because name AND dynamic variables matter (petLevel etc.)
                if (slotFrom.item.Equals(slotTo.item))
                {
                    // merge from -> to
                    // put as many as possible into 'To' slot
                    int put = slotTo.IncreaseAmount(slotFrom.amount);
                    slotFrom.DecreaseAmount(put);

                    // put back into the list
                    slots[fromIndex] = slotFrom;
                    slots[toIndex] = slotTo;
                }
            }
        }
    }

    [ClientRpc]
    public void RpcUsedItem(Item item)
    {
        // validate
        if (item.data is UsableItem usable)
        {
            usable.OnUsed(player);
        }
    }

    // Updated Command in PlayerInventory to use the spell book
    [Command]
    public void CmdUseItem(int index)
    {
        if (InventoryOperationsAllowed() && 0 <= index && index < slots.Count && slots[index].amount > 0)
        {
            Item item = slots[index].item;

            // Check if the item is a SpellBookItem
            if (item.data is SpellBookItem spellBook)
            {
                PlayerSkills playerSkills = player.GetComponent<PlayerSkills>();
                if (playerSkills != null)
                {
                    if (!playerSkills.HasLearned(spellBook.skillToLearn.name))
                    {
                        // Learn the skill
                        playerSkills.LevelUpSkill(spellBook.skillToLearn.name);

                        // Notify the player
                        string message = $"You have learned {spellBook.skillToLearn.name}!";
                        PlayerChat playerChat = player.GetComponent<PlayerChat>();
                        if (playerChat != null)
                        {
                            playerChat.TargetMsgLocal(player.name, message);
                        }

                        // Request item removal on the server
                        RpcRequestClientToRemoveItems(item.name.GetStableHashCode(), 1);

                        // Notify other clients that the item was used
                        RpcUsedItem(item);
                    }
                    else
                    {
                        // Player already knows the skill
                        string message = "You already know this skill.";
                        PlayerChat playerChat = player.GetComponent<PlayerChat>();
                        if (playerChat != null)
                        {
                            playerChat.TargetMsgLocal(player.name, message);
                        }
                    }
                }
            }
            else if (item.data is EquipmentItem equipmentItem)
            {
                // Handle EquipmentItem
                if (equipmentItem.CanUse(player, index))
                {
                    equipmentItem.Use(player, index);
                    RpcUsedItem(item);
                }
            }
            else if (item.data is UsableItem usable)
            {
                // Handle UsableItem
                if (usable.CanUse(player, index))
                {
                    usable.Use(player, index);
                    RpcUsedItem(item);
                }
            }
        }
    }

    [ClientRpc]
    public void RpcRequestClientToRemoveItems(int itemHash, int amount)
    {
        PlayerInventory inventory = GetComponent<PlayerInventory>();
        if (inventory == null) return;

        // Retrieve the item from the hash
        if (!ScriptableItem.All.TryGetValue(itemHash, out ScriptableItem itemData))
        {
            Debug.LogError($"Item with hash {itemHash} not found.");
            return;
        }

        // Convert to Item and remove from inventory
        Item item = new Item(itemData);
        inventory.RemoveItem(item, amount);
    }






    // drag & drop /////////////////////////////////////////////////////////////
    void OnDragAndDrop_InventorySlot_InventorySlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo

        // merge? check Equals because name AND dynamic variables matter (petLevel etc.)
        if (slots[slotIndices[0]].amount > 0 && slots[slotIndices[1]].amount > 0 &&
            slots[slotIndices[0]].item.Equals(slots[slotIndices[1]].item))
        {
            CmdInventoryMerge(slotIndices[0], slotIndices[1]);
        }
        // split?
        else if (Utils.AnyKeyPressed(splitKeys))
        {
            CmdInventorySplit(slotIndices[0], slotIndices[1]);
        }
        // swap?
        else
        {
            CmdSwapInventoryInventory(slotIndices[0], slotIndices[1]);
        }
    }

    void OnDragAndDrop_InventorySlot_TrashSlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        CmdSwapInventoryTrash(slotIndices[0]);
    }

    void OnDragAndDrop_TrashSlot_InventorySlot(int[] slotIndices)
    {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        CmdSwapTrashInventory(slotIndices[1]);
    }

    // validation
    protected override void OnValidate()
    {
        base.OnValidate();

        // defaultItems is null when first adding the component. avoid error.
        if (defaultItems != null)
        {
            // it's easy to set a default item and forget to set amount from 0 to 1
            // -> let's do this automatically.
            for (int i = 0; i < defaultItems.Length; ++i)
                if (defaultItems[i].item != null && defaultItems[i].amount == 0)
                    defaultItems[i].amount = 1;
        }

        // force syncMode to observers for now.
        // otherwise trade offer items aren't shown when trading with someone
        // else, because we can't see the other person's inventory slots.
        if (syncMode != SyncMode.Observers)
        {
            syncMode = SyncMode.Observers;
#if UNITY_EDITOR
            Undo.RecordObject(this, name + " " + GetType() + " component syncMode changed to Observers.");
#endif
        }
    }
}
