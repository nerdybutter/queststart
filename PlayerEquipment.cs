using System;
using UnityEngine;
using Mirror;

[Serializable]
public partial struct EquipmentInfo
{
    public string requiredCategory;
    public SubAnimation location;
    public ScriptableItemAndAmount defaultItem;
}

[RequireComponent(typeof(PlayerInventory))]
public partial class PlayerEquipment : Equipment
{
    [Header("Components")]
    public Player player;
    public PlayerInventory inventory;

    [Header("Avatar")]
    public Camera avatarCamera;

    [Header("Equipment Info")]
    public EquipmentInfo[] slotInfo;

    public override void OnStartClient()
    {
#pragma warning disable CS0618
        slots.Callback += OnEquipmentChanged;
#pragma warning restore CS0618

        for (int i = 0; i < slots.Count; ++i)
            RefreshLocation(i);
    }

    void OnEquipmentChanged(SyncList<ItemSlot>.Operation op, int index, ItemSlot oldSlot, ItemSlot newSlot)
    {
        RefreshLocation(index);

        // Handle slot clearing on server side only
        if (isServer && newSlot.amount == 0)
        {
            slots[index] = new ItemSlot();  // Modify on the server to ensure proper synchronization
        }
    }

    [Server]
    public void RemoveItemFromSlot(int index)
    {
        if (index >= 0 && index < slots.Count)
        {
            slots[index] = new ItemSlot();  // Properly clear the slot on the server
            Debug.Log($"Server: Removed item from slot {index}");
        }
    }

    [Command]
    public void CmdDropItemFromEquipmentSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < slots.Count && slots[slotIndex].amount > 0)
        {
            // Remove the item from the equipment slot
            ItemSlot slot = slots[slotIndex];
            RemoveItemFromSlot(slotIndex);

            // Instantiate the dropped item in the game world
            AddonItemDrop.GenerateItem(slot.item.data.name, slot.amount, player, player.transform.position, slot.item.durability);

            Debug.Log($"Dropped item from equipment slot {slotIndex}: {slot.item.data.name}");
        }
    }



    public void RefreshLocation(int index)
    {
        ItemSlot slot = slots[index];
        EquipmentInfo info = slotInfo[index];

        if (info.requiredCategory != "" && info.location != null)
            info.location.spritesToAnimate = slot.amount > 0 ? ((EquipmentItem)slot.item.data).sprites : null;
    }

    [Server]
    public void ReduceEquipmentDurability(int slotIndex, int amount)
    {
        if (slotIndex >= 0 && slotIndex < slots.Count)
        {
            ItemSlot slot = slots[slotIndex];

            if (slot.amount > 0 && slot.item.data is EquipmentItem equipmentItem && !equipmentItem.isIndestructible)
            {
                // Reduce durability and sync the updated slot
                slot.item.durability = Mathf.Max(slot.item.durability - amount, 0);
                slots[slotIndex] = slot;  // This triggers synchronization with clients

                Debug.Log($"Server: Reduced durability for {slot.item.data.name}, new durability: {slot.item.durability}");
            }
            else
            {
                Debug.Log("Item is either not an equipment item or is indestructible.");
            }
        }
    }




    [Command]
    public void CmdRequestReduceDurability(int slotIndex, int amount)
    {
        ReduceEquipmentDurability(slotIndex, amount);
    }

    [Server]
    public void SwapInventoryEquip(int inventoryIndex, int equipmentIndex)
    {
        if (inventory.InventoryOperationsAllowed() &&
            0 <= inventoryIndex && inventoryIndex < inventory.slots.Count &&
            0 <= equipmentIndex && equipmentIndex < slots.Count)
        {
            ItemSlot slot = inventory.slots[inventoryIndex];
            if (slot.amount == 0 || slot.item.data is EquipmentItem itemData &&
                                    itemData.CanEquip(player, inventoryIndex, equipmentIndex))
            {
                ItemSlot temp = slots[equipmentIndex];
                slots[equipmentIndex] = slot;
                inventory.slots[inventoryIndex] = temp;
            }
        }
    }

    [Command]
    public void CmdSwapInventoryEquip(int inventoryIndex, int equipmentIndex)
    {
        SwapInventoryEquip(inventoryIndex, equipmentIndex);
    }

    [Server]
    public void MergeInventoryEquip(int inventoryIndex, int equipmentIndex)
    {
        if (inventory.InventoryOperationsAllowed() &&
            0 <= inventoryIndex && inventoryIndex < inventory.slots.Count &&
            0 <= equipmentIndex && equipmentIndex < slots.Count)
        {
            ItemSlot slotFrom = inventory.slots[inventoryIndex];
            ItemSlot slotTo = slots[equipmentIndex];
            if (slotFrom.amount > 0 && slotTo.amount > 0 && slotFrom.item.Equals(slotTo.item))
            {
                int put = slotTo.IncreaseAmount(slotFrom.amount);
                slotFrom.DecreaseAmount(put);

                inventory.slots[inventoryIndex] = slotFrom;
                slots[equipmentIndex] = slotTo;
            }
        }
    }

    [Command]
    public void CmdMergeInventoryEquip(int inventoryIndex, int equipmentIndex)
    {
        MergeInventoryEquip(inventoryIndex, equipmentIndex);
    }

    [Command]
    public void CmdMergeEquipInventory(int equipmentIndex, int inventoryIndex)
    {
        if (inventory.InventoryOperationsAllowed() &&
            0 <= inventoryIndex && inventoryIndex < inventory.slots.Count &&
            0 <= equipmentIndex && equipmentIndex < slots.Count)
        {
            ItemSlot slotFrom = slots[equipmentIndex];
            ItemSlot slotTo = inventory.slots[inventoryIndex];
            if (slotFrom.amount > 0 && slotTo.amount > 0 && slotFrom.item.Equals(slotTo.item))
            {
                int put = slotTo.IncreaseAmount(slotFrom.amount);
                slotFrom.DecreaseAmount(put);

                slots[equipmentIndex] = slotFrom;
                inventory.slots[inventoryIndex] = slotTo;
            }
        }
    }

    void OnDragAndDrop_InventorySlot_EquipmentSlot(int[] slotIndices)
    {
        if (inventory.slots[slotIndices[0]].amount > 0 && slots[slotIndices[1]].amount > 0 &&
            inventory.slots[slotIndices[0]].item.Equals(slots[slotIndices[1]].item))
        {
            CmdMergeInventoryEquip(slotIndices[0], slotIndices[1]);
        }
        else
        {
            CmdSwapInventoryEquip(slotIndices[0], slotIndices[1]);
        }
    }

    void OnDragAndDrop_EquipmentSlot_InventorySlot(int[] slotIndices)
    {
        if (slots[slotIndices[0]].amount > 0 && inventory.slots[slotIndices[1]].amount > 0 &&
            slots[slotIndices[0]].item.Equals(inventory.slots[slotIndices[1]].item))
        {
            CmdMergeEquipInventory(slotIndices[0], slotIndices[1]);
        }
        else
        {
            CmdSwapInventoryEquip(slotIndices[1], slotIndices[0]);
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        for (int i = 0; i < slotInfo.Length; ++i)
            if (slotInfo[i].defaultItem.item != null && slotInfo[i].defaultItem.amount == 0)
                slotInfo[i].defaultItem.amount = 1;
    }
}
