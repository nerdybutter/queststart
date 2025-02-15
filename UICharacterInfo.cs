using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public partial class UICharacterInfo : MonoBehaviour
{
    public KeyCode hotKey = KeyCode.T;
    public GameObject panel;
    public Text damageText;
    public Text healthText;
    public Text manaText;
    public Text criticalChanceText;
    public Text blockChanceText;
    public Text levelText;
    public TMP_Text timePlayedText;


    // New fields for Bio and Trophies
    public TMP_InputField bioInputField;
    public Transform trophyContainer;
    public GameObject trophyPrefab;
    public Button closeButton; // Add this at the top of your script
    private string previousBio;
    public static UICharacterInfo localInstance;
    public UICharacterInfo bioPanel;

    void Start()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() =>
            {
                Player player = Player.localPlayer;
                if (player != null)
                {
                    SaveBio(player); // Save the bio before closing the panel
                    panel.SetActive(false); // Close the panel
                }
            });
        }
    }

    public void UpdateBioText(string newBio)
    {
        if (bioInputField != null)
        {
            bioInputField.text = newBio; // Update the InputField's text
        }
        else
        {
            Debug.LogWarning("Bio Input Field is not assigned in UICharacterInfo.");
        }
    }


    void Awake()
    {
        // Assign the local instance
        if (localInstance == null)
            localInstance = this;
        else
            Debug.LogWarning("Multiple UICharacterInfo instances found. Only one instance is allowed.");
    }

    public void ClosePanel()
    {
        Player player = Player.localPlayer;
        if (player != null)
        {
            SaveBio(player); // Save the bio
        }
        panel.SetActive(false); // Close the panel
    }


    void Update()
    {
        Player player = Player.localPlayer;
        if (player)
        {
            // Hotkey to toggle panel (not while typing in chat, etc.)
            if (Input.GetKeyDown(hotKey) && !UIUtils.AnyInputActive())
            {
                if (panel.activeSelf)
                {
                    SaveBio(player); // Save the bio when closing the panel
                }

                panel.SetActive(!panel.activeSelf);

                // Only update bioInputField when opening the panel
                if (panel.activeSelf && bioInputField != null && player.isLocalPlayer)
                {
                    bioInputField.interactable = true; // Enable editing for local player
                    bioInputField.text = player.bio;  // Load the current bio
                }
            }

            // Only refresh the panel while it's active
            if (panel.activeSelf)
            {
                UpdateStats(player);
                UpdateTrophies(player);
            }
            else
            {
                panel.SetActive(false);
            }
        }
        else
        {
            panel.SetActive(false);
        }
    }








    public void UpdateStats(Player player)
    {
        damageText.text = player.combat.damage.ToString();
        healthText.text = player.health.max.ToString();
        manaText.text = player.mana.max.ToString();
        criticalChanceText.text = (player.combat.criticalChance * 100).ToString("F0") + "%";
        blockChanceText.text = (player.combat.blockChance * 100).ToString("F0") + "%";
        levelText.text = player.level.current.ToString();

        PlayerTime playerTime = player.GetComponent<PlayerTime>();
        if (timePlayedText != null && playerTime != null)
        {
            timePlayedText.text = playerTime.GetFormattedPlayTime();
        }
    }


    public void UpdateTrophies(Player player)
    {
        if (trophyContainer != null && trophyPrefab != null)
        {
            // Clear existing trophies in the container
            UIUtils.BalancePrefabs(trophyPrefab, player.trophies.Count, trophyContainer);

            // Loop through each trophy ID in the player's trophies list
            for (int i = 0; i < player.trophies.Count; i++)
            {
                int trophyId = player.trophies[i]; // Get the trophy ID
                Transform slot = trophyContainer.GetChild(i); // Get the slot for the trophy

                // Fetch the Image component for the trophy slot
                Image trophyImage = slot.GetComponent<Image>();
                if (trophyImage != null)
                {
                    // Fetch the sprite for the trophy ID
                    trophyImage.sprite = GetTrophySprite(trophyId);

                    // Set the trophy image to visible
                    trophyImage.color = Color.white;
                }
                else
                {
                    Debug.LogWarning("Trophy slot is missing an Image component.");
                }
            }
        }
        else
        {
            Debug.LogWarning("Trophy container or trophy prefab is not assigned.");
        }
    }

    private Sprite GetTrophySprite(int trophyId)
    {
        // Fetch the sprite for the given trophy ID from the TrophyDatabase
        // Assuming TrophyDatabase.Instance.GetSprite(trophyId) returns the sprite
        if (TrophyDatabase.Instance != null)
        {
            Sprite trophySprite = TrophyDatabase.Instance.GetSprite(trophyId);
            if (trophySprite != null)
            {
                return trophySprite;
            }
            else
            {
                Debug.LogWarning($"No sprite found for trophy ID {trophyId}.");
            }
        }
        else
        {
            Debug.LogError("TrophyDatabase instance is null.");
        }

        return null; // Return null if no sprite is found
    }

    private void SaveBio(Player player)
    {
        if (bioInputField != null && player.isLocalPlayer)
        {
            string newBio = bioInputField.text;
            if (newBio != previousBio) // Save only if the bio has changed
            {
                player.CmdUpdateBio(newBio); // Send the updated bio to the server
                Debug.Log($"Bio saved for {player.name}: {newBio}");
                previousBio = newBio; // Update the cached bio
            }
        }
    }

}
