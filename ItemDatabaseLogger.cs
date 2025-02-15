using UnityEngine;
using System.Collections.Generic;

public class ItemDatabaseLogger : MonoBehaviour
{
    public static void LogAllItems()
    {
        Debug.Log("Logging all items in the item database...");

        // Check if the database is a dictionary (KeyValuePair<int, ScriptableItem>)
        foreach (KeyValuePair<int, ScriptableItem> kvp in ScriptableItem.All)
        {
            ScriptableItem item = kvp.Value;  // Access the item
            Debug.Log($"Item: {item.name}, Hash: {item.name.GetStableHashCode()}, Type: {item.GetType().Name}");
        }

        Debug.Log("Finished logging all items.");
    }

    // Optional: Call this on start for testing
    private void Start()
    {
        LogAllItems();
    }
}
