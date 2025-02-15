using UnityEngine;

public class BlacksmithingAnimationHandler : MonoBehaviour
{
    public AudioSource blacksmithSound;    // Assign the blacksmith sound here
    public GameObject effectPrefab;        // Assign the prefab for the effect
    public Transform effectSpawnPoint;     // The position near the anvil for the effect

    // Call this method in the animation event or in Update()
    public void PlayBlacksmithingEffect()
    {
        // Play the sound if not already playing
        if (!blacksmithSound.isPlaying)
        {
            blacksmithSound.Play();
        }

        // Instantiate the effect prefab at the spawn point
        Instantiate(effectPrefab, effectSpawnPoint.position, Quaternion.identity);
    }
}
