﻿using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public partial class UICharacterCreation : MonoBehaviour
{
    public NetworkManagerMMO manager; // singleton is null until update
    public GameObject panel;
    public InputField nameInput;
    public Dropdown classDropdown;
    public Button createButton;
    public Button cancelButton;
    public Text hair;
    public EquipmentItem RealHairChoice;

    void Update()
    {
        // only update while visible (after character selection made it visible)
        if (panel.activeSelf)
        {
            // still in lobby?
            if (manager.state == NetworkState.Lobby)
            {
                Show();

                // copy player classes to class selection
                classDropdown.options = manager.playerClasses.Select(
                    p => new Dropdown.OptionData(p.name)
                ).ToList();

                // create
                createButton.interactable = manager.IsAllowedCharacterName(nameInput.text);
                createButton.onClick.SetListener(() => {
                    CharacterCreateMsg message = new CharacterCreateMsg{
                        name = nameInput.text,
                        classIndex = classDropdown.value,
                        hairChoice = hair.text
                    };
                    NetworkClient.Send(message);
                    Hide();
                });

                // cancel
                cancelButton.onClick.SetListener(() => {
                    nameInput.text = "";
                    Hide();
                });
            }
            else Hide();
        }
    }

    public void Hide() { panel.SetActive(false); }
    public void Show() { panel.SetActive(true); }
    public bool IsVisible() { return panel.activeSelf; }
}
