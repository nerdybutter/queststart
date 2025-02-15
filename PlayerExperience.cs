using System;
using UnityEngine;
using Mirror;

[RequireComponent(typeof(PlayerChat))]
[RequireComponent(typeof(PlayerParty))]
public class PlayerExperience : Experience
{
    [Header("Components")]
    public PlayerChat chat;
    public PlayerParty party;

    [Header("Death")]
    public string deathMessage = "You died and lost experience.";

    [Server]
    public override void OnDeath()
    {
        // call base logic
        base.OnDeath();

        // send an info chat message
        chat.TargetMsgInfo(deathMessage);
    }

    // events //////////////////////////////////////////////////////////////////
    [Server]
    public void OnKilledEnemy(Entity victim)
    {
        if (victim is Monster monster)
        {
            long adjustedExperience = monster.rewardExperience - level.current;
            adjustedExperience = Math.Max(adjustedExperience, 0);

            if (!party.InParty() || !party.party.shareExperience)
            {
                current += adjustedExperience;

                // Send the experience popup to the client
                TargetShowExperiencePopup(connectionToClient, (int)adjustedExperience);

                Player player = GetComponent<Player>();
                if (player != null)
                {
                    player.OnMonsterKilled(monster);
                }
            }
        }
    }

    [TargetRpc]
    void TargetShowExperiencePopup(NetworkConnection target, int amount)
    {
        // Instantiate popup on the client
        if (damagePopupPrefab != null)
        {
            Bounds bounds = GetComponent<Collider2D>().bounds;
            Vector2 position = new Vector2(bounds.center.x, bounds.max.y);

            GameObject popup = Instantiate(damagePopupPrefab, position, Quaternion.identity);
            TextMesh popupText = popup.GetComponentInChildren<TextMesh>();
            if (popupText != null)
            {
                popupText.text = $"+{amount} EXP";
                popupText.color = Color.green;
            }
        }
    }
}
