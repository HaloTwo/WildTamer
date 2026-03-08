using Unity.VisualScripting;
using UnityEngine;

public class PlayerBrain : MonoBehaviour
{
    CombatAgent combat;

    [Header("Auto Target Scan")]
    [SerializeField] LayerMask enemyMask;
    [SerializeField] float scanRadius = 3.0f;
    [SerializeField] float scanInterval = 0.2f;

    Transform currentTarget;
    public Transform CurrentTarget => currentTarget;

    readonly Collider2D[] buffer = new Collider2D[20];
    ContactFilter2D filter;

    float nextScanTime;

    void Awake()
    {
        TryGetComponent(out combat);
        combat.team = Team.Player;

        filter = new ContactFilter2D();
        filter.SetLayerMask(enemyMask);
        filter.useLayerMask = true;
        filter.useTriggers = true;
    }


    void Update()
    {
        if (Time.time >= nextScanTime || currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            nextScanTime = Time.time + scanInterval;
            currentTarget = FindClosestEnemy();
        }

        if (currentTarget == null)
        {
            combat.TryAttack(null);
            return;
        }

        combat.TryAttack(currentTarget);
    }

    Transform FindClosestEnemy()
    {
        Vector2 center = transform.position;

        int cnt = Physics2D.OverlapCircle(center, scanRadius, filter, buffer);

        Transform best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < cnt; i++)
        {
            var col = buffer[i];
            if (col == null) continue;

            var enemy = col.GetComponentInParent<CombatAgent>();
            if (enemy == null || enemy.IsDead) continue;
            if (enemy.team == combat.team) continue;

            float d = ((Vector2)enemy.transform.position - center).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = enemy.transform;
            }
        }

        return best;
    }
}