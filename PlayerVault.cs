using Mirror;
using System.Collections.Generic;
using UnityEngine;      // Required for attributes like Header
//using Mirror;           // For networking components


public class PlayerVault : NetworkBehaviour
{
    [Header("Vault")]
    public int size = 30;                      // Default vault size
    public readonly SyncList<ItemSlot> slots = new SyncList<ItemSlot>();


    private void Start()
    {
        if (isServer)
        {
            InitializeVault();
        }
    }

    [Server]
    private void InitializeVault()
    {
        if (slots.Count == 0)
        {
            for (int i = 0; i < size; ++i)
            {
                slots.Add(new ItemSlot());
            }
        }

        // Optionally load saved data from the database
        Database.singleton.LoadVault(this);
    }

    [Command]
    public void CmdRequestVaultData()
    {
        TargetSendVaultData(connectionToClient);
    }

    [TargetRpc]
    private void TargetSendVaultData(NetworkConnection target)
    {
        Debug.Log("Vault data sent to client.");
    }

    [Command]
    public void CmdMoveItemInVault(int fromIndex, int toIndex)
    {
        if (fromIndex >= 0 && fromIndex < slots.Count && toIndex >= 0 && toIndex < slots.Count)
        {
            ItemSlot temp = slots[fromIndex];
            slots[fromIndex] = slots[toIndex];
            slots[toIndex] = temp;

            Database.singleton.SaveVault(this);
        }
    }

    void OnDragAndDrop_Vault_Vault(int[] indices)
    {
        int fromIndex = indices[0];
        int toIndex = indices[1];

        if (fromIndex >= 0 && fromIndex < slots.Count && toIndex >= 0 && toIndex < slots.Count)
        {
            CmdMoveItemInVault(fromIndex, toIndex);
        }
    }
   
    [Command]
    public void CmdMoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex >= 0 && fromIndex < slots.Count && toIndex >= 0 && toIndex < slots.Count)
        {
            // Swap or move the item within the vault
            ItemSlot temp = slots[fromIndex];
            slots[fromIndex] = slots[toIndex];
            slots[toIndex] = temp;

            // Save the changes to the database
            Database.singleton.SaveVault(this);
        }
    }

}
