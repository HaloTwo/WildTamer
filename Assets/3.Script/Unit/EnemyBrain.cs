using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBrain : BrainBase
{
    readonly Collider2D[] scanBuffer = new Collider2D[16];
    ContactFilter2D scanFilter;

    Vector2 desiredMove;

    [Header("Enemy")]
    [SerializeField] float loseTargetRadius = 6f;


    public void EnemyBrainSet()
    {
        combat.SetTeam(CombatAgent.Team.Enemy);

        scanFilter = new ContactFilter2D();
        scanFilter.SetLayerMask(targetMask);
        scanFilter.useLayerMask = true;
        scanFilter.useTriggers = true;
    }

    void Update()
    {
        if (combat == null || combat.IsDead || !gameObject.activeSelf)
        {
            desiredMove = Vector2.zero;
            anim.SetBool(isMovingParam, false);
            return;
        }

        desiredMove = Vector2.zero;

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;

            if (!IsValidTarget(currentTarget))
            {
                currentTarget = FindClosestPlayerOrAllyAroundMe();
            }
            else
            {
                float loseR2 = loseTargetRadius * loseTargetRadius;
                float d2 = ((Vector2)currentTarget.position - rb.position).sqrMagnitude;
                if (d2 > loseR2)
                {
                    currentTarget = FindClosestPlayerOrAllyAroundMe();
                }
            }
        }

        if (IsValidTarget(currentTarget))
        {
            Vector2 toTarget = (Vector2)currentTarget.position - rb.position;

            if (!combat.IsInRange(currentTarget))
            {
                desiredMove = toTarget;
                FaceByMove(desiredMove);
                anim.SetBool(isMovingParam, true);
            }
            else
            {
                desiredMove = Vector2.zero;
                FaceTo(currentTarget.position);
                anim.SetBool(isMovingParam, false);
                combat.TryAttack(currentTarget);
            }

            return;
        }

        currentTarget = null;
        desiredMove = Vector2.zero;
        anim.SetBool(isMovingParam, false);
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (desiredMove.sqrMagnitude < 0.0001f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 dir = desiredMove.normalized;
        rb.linearVelocity = dir * moveSpeed;
    }

    Transform FindClosestPlayerOrAllyAroundMe()
    {
        Vector2 center = rb.position;
        float r = detectRadius;

        int cnt = Physics2D.OverlapCircle(center, r, scanFilter, scanBuffer);

        Transform best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < cnt; i++)
        {
            var col = scanBuffer[i];
            if (col == null) continue;

            var other = col.GetComponentInParent<CombatAgent>();
            if (other == null || other.IsDead) continue;
            if (other.team == CombatAgent.Team.Enemy) continue;

            float d = ((Vector2)other.transform.position - center).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = other.transform;
            }
        }

        return best;
    }
}