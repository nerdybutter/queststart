using UnityEngine;

public class StunHandler : MonoBehaviour
{
    public GameObject stunnedOverlay; // Assign the "StunnedOverlay" in Inspector
    private Animator stunAnimator;

    void Start()
    {
        if (stunnedOverlay != null)
            stunAnimator = stunnedOverlay.GetComponent<Animator>();

        stunnedOverlay.SetActive(false); // Ensure it's hidden by default
    }

    public void SetStunned(bool isStunned)
    {
        stunnedOverlay.SetActive(isStunned); // Show/hide the effect
        if (stunAnimator != null)
            stunAnimator.SetBool("STUNNED", isStunned); // Play animation if needed
    }
}
