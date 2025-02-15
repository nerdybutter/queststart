using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIHighScores : MonoBehaviour
{
    public static UIHighScores singleton; // Singleton instance
    public GameObject panel; // The high score panel
    public Transform content; // The content of the scroll view
    public Text highScorePrefab; // Prefab for displaying each high score

    private void Awake()
    {
        if (singleton == null)
        {
            singleton = this; // Set the singleton instance
        }
        else
        {
            Destroy(gameObject); // Ensure there's only one instance
        }
    }

    public void ShowHighScores(List<KeyValuePair<string, int>> highScores)
    {
        panel.SetActive(true);

        // Clear previous entries
        foreach (Transform child in content)
            Destroy(child.gameObject);

        // Populate the list with high scores
        foreach (var score in highScores)
        {
            Text entry = Instantiate(highScorePrefab, content);
            entry.text = $"{score.Key}: {score.Value}";
        }
    }

    public void HideHighScores()
    {
        panel.SetActive(false);
    }
}
