using UnityEngine;
using TMPro; // For TextMeshPro

public class FloatingText : MonoBehaviour
{
    public TextMeshProUGUI text; // Reference to TMP_Text component
    public float duration = 2f; // Duration before disappearing
    public float floatSpeed = 2f; // Speed of floating up

    public void SetText(string message)
    {
        text.text = message;
        Destroy(gameObject, duration); // Destroy the text after the duration
    }

    private void Update()
    {
        // Move the text upward over time
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;
    }
}
