using UnityEngine;
using Mirror;

public class PlayerScoreManager : NetworkBehaviour
{
    [SyncVar] public int playerScore; // The player's score (synchronized to all clients)

    private PlayerSkills playerSkills; // Reference to the PlayerSkills component
    private Health playerHealth;       // Reference to the Health component
    private Mana playerMana;           // Reference to the Mana component

    // Called when the object is spawned on the server
    public override void OnStartServer()
    {
        base.OnStartServer();

        // Get references to required components
        Player player = GetComponent<Player>();
        playerSkills = player.skills as PlayerSkills; // Cast the skills to PlayerSkills
        playerHealth = player.health; // Access inherited health
        playerMana = player.mana;     // Access inherited mana

        CalculatePlayerScore(); // Ensure the score is calculated when the server starts
    }

    [Server]
    public void CalculatePlayerScore()
    {
        int totalSkillLevels = 0;

        // Sum up all skill levels
        if (playerSkills != null && playerSkills.skills != null)
        {
            foreach (Skill skill in playerSkills.skills)
            {
                totalSkillLevels += skill.level;
            }
        }

        // Get the player's max health and max mana
        int maxHealth = playerHealth != null ? playerHealth.max : 0;
        int maxMana = playerMana != null ? playerMana.max : 0;

        // Calculate the score
        playerScore = totalSkillLevels + maxHealth + maxMana;

    }

    [Server]
    public void UpdateScoreInDatabase()
    {
        if (Database.singleton == null)
        {
            Debug.LogError("[PlayerScoreManager] Database instance is null. Make sure the database is initialized.");
            return;
        }

        string playerName = GetComponent<Player>().name; // Get the player's name
        Database.singleton.UpdateCharacterScore(playerName, playerScore);
    }

    [ClientRpc]
    public void RpcUpdateScoreUI()
    {
        UISkills uiSkills = FindObjectOfType<UISkills>();
        if (uiSkills != null)
        {
            Player player = GetComponent<Player>(); // Get the Player component
            if (player != null)
            {
                uiSkills.UpdateScoreDisplay(player); // Pass the Player object
            }
        }
        else
        {
            Debug.LogWarning("UISkills component not found!");
        }
    }

    // Optional: Call this method whenever a skill level, health, or mana changes
    [Server]
    public void OnSkillHealthManaChanged()
    {
        CalculatePlayerScore(); // Recalculate the player's score
        UpdateScoreInDatabase(); // Save the score to the database
        RpcUpdateScoreUI(); // Update the UI on all clients
    }
}
