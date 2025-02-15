using UnityEngine;

public class DisarmingKit : MonoBehaviour
{
    public void UseDisarmingKit(SpikeTrap trap)
    {
        if (trap != null && trap.isActive)
        {
            trap.DisarmTrap();  // Call without arguments
            Debug.Log("Trap disarmed for " + trap.defaultDisarmedDuration + " seconds.");
        }
    }
}
