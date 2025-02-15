using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class WhoUI : MonoBehaviour
{
    public static WhoUI singleton; // Singleton for global access
    public KeyCode hotKey = KeyCode.O; // Hotkey to toggle the panel
    public GameObject panel; // Panel to show the UI
    public Transform content; // Content container in the Scroll View
    public GameObject playerNamePrefab; // Prefab for player name (TextMeshProUGUI)

    void Awake()
    {
        // Assign singleton for global access
        if (singleton == null) singleton = this;
    }

    void Start()
    {
        panel.SetActive(false); // Hide the panel by default
    }

    void Update()
    {
        // Hotkey to toggle the panel (ignore if typing in chat or UI input is active)
        if (Player.localPlayer != null && Input.GetKeyDown(hotKey) && !UIUtils.AnyInputActive())
        {
            ToggleOnlinePlayers();
        }
    }

    // Method to toggle the online players panel
    public void ToggleOnlinePlayers()
    {
        if (!panel.activeSelf)
        {
            // Request the online players list from the server
            Player.localPlayer.CmdRequestOnlinePlayers();
        }
        else
        {
            panel.SetActive(false); // Hide the panel
        }
    }

    // Method to display the list of online players
    public void ShowOnlinePlayers(List<string> players)
    {
        Debug.Log($"Populating WhoUI with {players.Count} players.");

        // Clear existing entries
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }

        // Add a new entry for each player
        foreach (string player in players)
        {
            Debug.Log($"Adding player: {player}");
            GameObject playerEntry = Instantiate(playerNamePrefab, content);

            // Check parent
            Debug.Log($"Prefab parent set to: {playerEntry.transform.parent.name}");

            // Set the player name in the prefab
            TextMeshProUGUI textComponent = playerEntry.GetComponent<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = player;
                Debug.Log($"Player name set to: {textComponent.text}");
            }
            else
            {
                Debug.LogError("TextMeshProUGUI component not found on PlayerNamePrefab!");
            }

            // Debugging prefab position and size
            RectTransform rect = playerEntry.GetComponent<RectTransform>();
            Debug.Log($"Prefab instantiated at: {rect.anchoredPosition}, Width: {rect.rect.width}, Height: {rect.rect.height}");
        }

        // **Force Layout Rebuild**
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content.GetComponent<RectTransform>());
        Debug.Log("Layout updated for Content.");

        panel.SetActive(true); // Show the panel
    }


}
