using System.Collections;
using UnityEngine;
using Mirror;

public class SlotMachine : NetworkBehaviour
{
    public Sprite[] slotIcons = new Sprite[4]; // Pig, Slime, Gazer, Orc sprites in the Inspector
    public int[] betAmounts = { 10, 50, 100 };

    [SyncVar] private int slot1, slot2, slot3;

    private float interactionDistance = 3f; // How close a player must be

    [Server]
    public void ServerStartSlotMachine(Player player, int bet)
    {
        if (Vector2.Distance(player.transform.position, transform.position) > interactionDistance)
        {
            player.chat.TargetMsgInfo("You are too far away.");
            TargetStopSlotAnimation(player.connectionToClient); // Stop animation
            return;
        }

        if (player.gold < bet)
        {
            player.chat.TargetMsgInfo("You don't have enough gold to play!");
            TargetStopSlotAnimation(player.connectionToClient); // Stop animation
            return;
        }

        player.gold -= bet; // Deduct gold on the server
        StartCoroutine(RollSlots(player, bet));
    }

    [TargetRpc]
    void TargetStopSlotAnimation(NetworkConnection target)
    {
        SlotMachineUI.instance.StopRollingAnimation();
    }


    [Server]
    private IEnumerator RollSlots(Player player, int bet)
    {
        yield return new WaitForSeconds(2f); // Simulate slot roll delay

        slot1 = Random.Range(0, slotIcons.Length);
        slot2 = Random.Range(0, slotIcons.Length);
        slot3 = Random.Range(0, slotIcons.Length);

        TargetUpdateSlots(player.connectionToClient, slot1, slot2, slot3);

        if (slot1 == slot2 && slot2 == slot3) // Player wins
        {
            int winnings = bet * 5; // Example payout multiplier
            player.gold += winnings;
            player.chat.TargetMsgInfo($"You won " + winnings + " gold!");
        }
        else // Player loses
        {
            player.chat.TargetMsgInfo($"You lost, try again!");
        }
    }

    [TargetRpc]
    void TargetUpdateSlots(NetworkConnection target, int s1, int s2, int s3)
    {
        SlotMachineUI.instance.UpdateSlotGraphics(s1, s2, s3);
    }

    private void OnMouseDown()
    {
        if (Player.localPlayer != null)
        {
            Player.localPlayer.CmdOpenSlotMachine(this);
        }
    }
}
