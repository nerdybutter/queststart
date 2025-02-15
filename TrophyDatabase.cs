using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TrophyDatabase", menuName = "Databases/TrophyDatabase")]
public class TrophyDatabase : ScriptableObject
{
    public static TrophyDatabase Instance; // Singleton for easy access

    [System.Serializable]
    public class Trophy
    {
        public int id; // Unique ID for the trophy
        public Sprite icon; // The sprite/icon for the trophy
    }

    public List<Trophy> trophies = new List<Trophy>(); // List of all trophies

    private void OnEnable()
    {
        // Assign the singleton instance
        Instance = this;
    }

    public Sprite GetSprite(int trophyId)
    {
        // Find the trophy by ID and return its sprite
        foreach (Trophy trophy in trophies)
        {
            if (trophy.id == trophyId)
                return trophy.icon;
        }

        Debug.LogWarning($"Trophy with ID {trophyId} not found!");
        return null; // Return null if not found
    }
}
