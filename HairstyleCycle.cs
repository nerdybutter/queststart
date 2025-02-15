using UnityEngine;
using UnityEngine.UI;

public class HairstyleCycle : MonoBehaviour
{
    public Sprite[] hairstyleSprites; // Array of hairstyle sprites (HeadA, HeadB, HeadC, etc.)
    public string[] hairstyleNames; // Array of corresponding hairstyle names (e.g., "HeadA", "HeadB", ...)
    public Image hairImage; // Reference to the Image component that displays the hair in UI
    public Text hair; // Reference to the Text component that displays the current hairstyle name

    private int currentHairstyleIndex = 0;

    private void Start()
    {
        // Validate input arrays
        if (hairstyleSprites.Length != hairstyleNames.Length)
        {
            Debug.LogError("The length of hairstyleSprites and hairstyleNames must be the same!");
            return;
        }

        // Initialize the hairstyle and text on start
        SetHairstyle(currentHairstyleIndex);
    }

    public void NextHairstyle()
    {
        if (hairstyleSprites.Length == 0) return;

        currentHairstyleIndex = (currentHairstyleIndex + 1) % hairstyleSprites.Length;
        SetHairstyle(currentHairstyleIndex);
    }

    private void SetHairstyle(int index)
    {
        if (index < 0 || index >= hairstyleSprites.Length) return;

        hairImage.sprite = hairstyleSprites[index];
        hair.text = hairstyleNames[index];
    }
}
