using UnityEngine;
using System.Collections;

public class MonsterUIHandler : MonoBehaviour
{
    public GameObject nameOverlayPosition;
    public GameObject healthBarParent;

    private float healthBarDisplayTime = 3f; // Time in seconds to show the health bar after attack
    private Coroutine healthBarCoroutine;

    private void Start()
    {
        if (healthBarParent != null)
            healthBarParent.SetActive(false); // Hide by default
    }

    private void OnMouseEnter()
    {
        // Show the name overlay and health bar on mouse-over
        nameOverlayPosition.SetActive(true);
        healthBarParent.SetActive(true);
    }

    public void SetHealthBarVisibility(bool isVisible)
    {
        if (healthBarParent != null)
            healthBarParent.SetActive(isVisible);
    }

    private void OnMouseExit()
    {
        // Hide the name overlay when the mouse exits
        nameOverlayPosition.SetActive(false);

        // Only hide the health bar if it's not being displayed due to an attack
        if (healthBarCoroutine == null)
            healthBarParent.SetActive(false);
    }

    public void OnAttacked()
    {
        // Show the health bar when attacked
        healthBarParent.SetActive(true);

        // Restart the coroutine to hide the health bar after a delay
        if (healthBarCoroutine != null)
            StopCoroutine(healthBarCoroutine);
        healthBarCoroutine = StartCoroutine(HideHealthBarAfterDelay());
    }

    private IEnumerator HideHealthBarAfterDelay()
    {
        yield return new WaitForSeconds(healthBarDisplayTime);
        healthBarParent.SetActive(false);
        healthBarCoroutine = null;
    }
}
