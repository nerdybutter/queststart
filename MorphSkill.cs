using UnityEngine;
using Mirror;
using System.Collections;

[CreateAssetMenu(menuName = "uMMORPG Skill/Morph", order = 999)]
public class MorphSkill : BuffSkill
{
    [Header("Morph Properties")]
    public Monster[] possibleMorphTargets;
    public LinearFloat baseDuration;
    public int[] levelThresholds = new int[] { 10, 30, 50, 70 };

    float CalculateMorphDuration(Player caster)
    {
        int illusionLevel = (caster.skills as PlayerSkills)?.GetSkillLevel("Illusion") ?? 0;
        float duration = baseDuration.Get(1);
        if (illusionLevel > 10) duration += 2f;
        if (illusionLevel > 20) duration += 2f;
        if (illusionLevel > 30) duration += 3f;
        if (illusionLevel > 50) duration += 4f;
        if (illusionLevel > 70) duration += 5f;
        return duration;
    }

    Monster[] GetAvailableMorphTargets(Player caster)
    {
        int illusionLevel = (caster.skills as PlayerSkills)?.GetSkillLevel("Illusion") ?? 0;
        int availableTargetsCount = 1;
        for (int i = 0; i < levelThresholds.Length; i++)
        {
            if (illusionLevel >= levelThresholds[i])
                availableTargetsCount++;
            else
                break;
        }

        return possibleMorphTargets.Length > availableTargetsCount
            ? possibleMorphTargets[..availableTargetsCount]
            : possibleMorphTargets;
    }

    public override bool CheckTarget(Entity caster)
    {
        return caster.target != null && caster.target.health.current > 0 && caster.target != caster;
    }

    public override bool CheckDistance(Entity caster, int skillLevel, out Vector2 destination)
    {
        if (caster.target != null)
        {
            destination = caster.target.collider.ClosestPointOnBounds(caster.transform.position);
            return Utils.ClosestDistance(caster.collider, caster.target.collider) <= castRange.Get(skillLevel);
        }

        destination = caster.transform.position;
        return false;
    }

    public override void Apply(Entity caster, int skillLevel, Vector2 direction)
    {
        if (caster.target != null && caster.target.health.current > 0)
        {
            if (caster is Player player)
            {
                // Get the available morph targets based on Illusion level
                Monster[] availableTargets = GetAvailableMorphTargets(player);
                Monster randomMonster = availableTargets[Random.Range(0, availableTargets.Length)];
                float duration = CalculateMorphDuration(player);

                // Apply the morph effect
                RpcApplyMorphEffect(caster.target.netIdentity, randomMonster, duration);
            }
        }
    }

    [ClientRpc]
    void RpcApplyMorphEffect(NetworkIdentity targetIdentity, Monster morphPrefab, float duration)
    {
        if (targetIdentity != null)
        {
            Entity targetEntity = targetIdentity.GetComponent<Entity>();
            if (targetEntity != null && morphPrefab != null)
            {
                targetEntity.StartCoroutine(MorphRoutine(targetEntity, morphPrefab, duration));
            }
        }
    }

    IEnumerator MorphRoutine(Entity target, Monster morphPrefab, float duration)
    {
        GameObject originalModel = target.gameObject;

        // Instantiate the morph model temporarily
        GameObject morphModel = Instantiate(morphPrefab.gameObject, target.transform.position, target.transform.rotation);
        morphModel.transform.SetParent(target.transform);

        originalModel.SetActive(false);

        yield return new WaitForSeconds(duration);

        Destroy(morphModel);
        originalModel.SetActive(true);
    }
}
