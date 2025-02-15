using UnityEngine;

[CreateAssetMenu(menuName = "uMMORPG Item/Seed", order = 999)]
public class SeedItem : ScriptableItem
{
    public GameObject plantedTilePrefab;
    public float growthTime = 120f;  // Time to fully grow (in seconds)
}
