using UnityEngine;
using Mirror;

public class PlayerInteraction : MonoBehaviour
{
    public float interactionRange = 2f;

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))  // Left-click
        {
            RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
            if (hit.collider != null)
            {
                // Handle SpikeTrap interaction
                SpikeTrap trap = hit.collider.GetComponent<SpikeTrap>();
                if (trap != null && trap.isActive)
                {
                    float distance = Vector2.Distance(transform.position, trap.transform.position);
                    if (distance <= interactionRange)
                    {
                        Player.localPlayer.CmdAttemptDisarm(trap.GetComponent<NetworkIdentity>());
                    }
                    else
                    {
                        Player.localPlayer.CmdSendTooFarMessage();
                    }
                    return;
                }

                // Handle Slot Machine interaction
                SlotMachine slotMachine = hit.collider.GetComponent<SlotMachine>();
                if (slotMachine != null)
                {
                    float distance = Vector2.Distance(transform.position, slotMachine.transform.position);
                    if (distance <= interactionRange)
                    {
                        SlotMachineUI.instance.ToggleUI(true);
                    }
                    else
                    {
                        Player.localPlayer.CmdSendTooFarMessage();
                    }
                }
            }
        }
    }
}
