using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

public class SlotMachineUI : MonoBehaviour
{
    public static SlotMachineUI instance;
    public GameObject slotMachinePanel;
    public Image[] slotImages;
    public Sprite[] slotIcons;
    public Button[] betButtons;
    public Button leverButton;
    private int selectedBet = 10;
    private bool isRolling = false; // Prevents multiple rolls at once

    public AudioSource audioSource;  // 🎵 For playing sounds
    public AudioClip slotStartSound; // Assign this in the Inspector

    private Color defaultColor = Color.white;
    private Color highlightColor = Color.yellow; // Color for selected bet button

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        foreach (Button btn in betButtons)
        {
            btn.onClick.AddListener(() => SetBet(int.Parse(btn.name), btn)); // Ensure buttons are named "10", "50", "100"
        }

        leverButton.onClick.AddListener(() => PullLever());

        // ✅ Highlight the default bet button when the UI opens
        HighlightDefaultBet();
    }

    public void ToggleUI(bool state)
    {
        slotMachinePanel.SetActive(state);
        if (state) HighlightDefaultBet(); // ✅ Ensure the default bet is highlighted every time the UI is opened
    }

    private void HighlightDefaultBet()
    {
        foreach (Button btn in betButtons)
        {
            if (btn.name == "10") // ✅ Ensure this matches the actual button name in Unity (case-sensitive)
            {
                SetBet(10, btn);
                return;
            }
        }
    }

    public void SetBet(int bet, Button selectedButton)
    {
        selectedBet = bet;
        UpdateButtonHighlights(selectedButton);
    }

    private void UpdateButtonHighlights(Button selectedButton)
    {
        foreach (Button btn in betButtons)
        {
            ColorBlock cb = btn.colors;
            cb.normalColor = (btn == selectedButton) ? highlightColor : defaultColor;
            cb.selectedColor = cb.normalColor;
            cb.pressedColor = cb.normalColor;
            btn.colors = cb;
        }
    }

    public void PullLever()
    {
        if (isRolling) return; // Prevent multiple rolls at the same time
        isRolling = true;

        Player player = Player.localPlayer;
        if (player != null)
        {
            StartCoroutine(SpinSlots());
            player.CmdStartSlotMachine(selectedBet);

            // 🎵 Play slot start sound if assigned
            if (audioSource != null && slotStartSound != null)
            {
                audioSource.PlayOneShot(slotStartSound);
            }
        }
    }

    public void StopRollingAnimation()
    {
        StopAllCoroutines(); // Stop any ongoing animation
        isRolling = false; // Allow rolling again
    }

    private IEnumerator SpinSlots()
    {
        float spinDuration = 2.5f; // Total spin time
        float spinSpeed = 0.1f; // Speed of image cycling
        float elapsedTime = 0f;

        while (elapsedTime < spinDuration)
        {
            for (int i = 0; i < slotImages.Length; i++)
            {
                slotImages[i].sprite = slotIcons[Random.Range(0, slotIcons.Length)];
            }
            elapsedTime += spinSpeed;
            yield return new WaitForSeconds(spinSpeed);
        }
    }

    public void UpdateSlotGraphics(int s1, int s2, int s3)
    {
        StopAllCoroutines(); // Ensure that the slot stops immediately when results arrive
        slotImages[0].sprite = slotIcons[s1];
        slotImages[1].sprite = slotIcons[s2];
        slotImages[2].sprite = slotIcons[s3];

        isRolling = false; // Allow rolling again
    }
}
