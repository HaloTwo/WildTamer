using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBrain : BrainBase
{
    readonly Collider2D[] scanBuffer = new Collider2D[16];
    ContactFilter2D scanFilter;

    Vector2 desiredMove;

    [Header("Enemy")]
    [SerializeField] float loseTargetRadius = 8f;

    [Header("Leader Patrol")]
    [SerializeField] float leaderArriveDistance = 0.4f;

    [Header("Follower")]
    [SerializeField] float followStopDistance = 0.15f;
    [SerializeField] float catchUpDistance = 4.5f;
    [SerializeField] float separationRadius = 0.4f;
    [SerializeField] float separationWeight = 0.2f;

    EnemyGroup group;
    int slotIndex = -1;

    public void EnemyBrainSet()
    {
        combat.SetTeam(Team.Enemy);

        scanFilter = new ContactFilter2D();
        scanFilter.SetLayerMask(targetMask);
        scanFilter.useLayerMask = true;
        scanFilter.useTriggers = true;

        desiredMove = Vector2.zero;
        currentTarget = null;
    }

    public void SetupGroup(EnemyGroup newGroup, int newSlotIndex)
    {
        group = newGroup;
        slotIndex = newSlotIndex;
        currentTarget = null;
    }

    public bool IsDead()
    {
        return combat == null || combat.IsDead || !gameObject.activeSelf;
    }

    public void NotifyAttacked(Transform attacker)
    {
        if (group != null && attacker != null)
            group.EnterCombat(attacker);
    }

    void Update()
    {
        if (combat == null || combat.IsDead || !gameObject.activeSelf)
        {
            desiredMove = Vector2.zero;
            SetMoveAnim(false);
            return;
        }

        desiredMove = Vector2.zero;

        if (group == null)
        {
            SetMoveAnim(false);
            return;
        }

        group.RefreshLeader();

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;

            Transform found = FindClosestPlayerOrAllyAroundMe();
            if (found != null)
            {
                group.EnterCombat(found);
            }
        }

        if (group.state == EnemyGroupState.Combat)
        {
            UpdateCombatStateMove();
        }
        else
        {
            UpdatePatrolStateMove();
        }
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (desiredMove.sqrMagnitude < 0.0001f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = desiredMove.normalized * moveSpeed;
    }

    void UpdatePatrolStateMove()
    {
        currentTarget = null;

        if (group.leader == this)
            UpdateLeaderPatrolMove();
        else
            UpdateFollowerMove();
    }

    void UpdateCombatStateMove()
    {
        Transform target = group.combatTarget;

        if (!IsValidGroupTarget(target))
        {
            target = FindClosestPlayerOrAllyAroundMe();

            if (target != null)
                group.EnterCombat(target);
        }

        currentTarget = target;

        if (!IsValidGroupTarget(currentTarget))
        {
            desiredMove = Vector2.zero;
            SetMoveAnim(false);
            return;
        }

        group.KeepCombatAlive();

        Vector2 toTarget = (Vector2)currentTarget.position - rb.position;
        float loseR2 = loseTargetRadius * loseTargetRadius;

        if (toTarget.sqrMagnitude > loseR2)
        {
            Transform found = FindClosestPlayerOrAllyAroundMe();
            if (found != null)
            {
                group.EnterCombat(found);
                currentTarget = found;
                toTarget = (Vector2)currentTarget.position - rb.position;
            }
        }

        if (!combat.IsInRange(currentTarget))
        {
            desiredMove = toTarget;
            FaceByMove(desiredMove);
            SetMoveAnim(true);
        }
        else
        {
            desiredMove = Vector2.zero;
            FaceTo(currentTarget.position);
            SetMoveAnim(false);
            combat.TryAttack(currentTarget);
        }
    }

    void UpdateLeaderPatrolMove()
    {
        Transform waypoint = group.GetCurrentWaypoint();

        if (waypoint == null)
        {
            desiredMove = Vector2.zero;
            SetMoveAnim(false);
            return;
        }

        Vector2 myPos = rb.position;
        Vector2 targetPos = waypoint.position;
        Vector2 toTarget = targetPos - myPos;

        if (toTarget.sqrMagnitude <= leaderArriveDistance * leaderArriveDistance)
        {
            group.AdvanceWaypoint();

            waypoint = group.GetCurrentWaypoint();
            if (waypoint == null)
            {
                desiredMove = Vector2.zero;
                SetMoveAnim(false);
                return;
            }

            targetPos = waypoint.position;
            toTarget = targetPos - myPos;
        }

        desiredMove = toTarget;
        FaceByMove(desiredMove);
        SetMoveAnim(true);
    }

    void UpdateFollowerMove()
    {
        if (group.leader == null || group.leader == this)
        {
            desiredMove = Vector2.zero;
            SetMoveAnim(false);
            return;
        }

        Vector2 myPos = rb.position;
        Vector2 leaderPos = group.leader.rb.position;
        Vector2 leaderVel = group.leader.rb.linearVelocity;

        Vector2 leaderForward;
        if (leaderVel.sqrMagnitude > 0.01f)
            leaderForward = leaderVel.normalized;
        else
            leaderForward = (leaderPos - myPos).normalized;

        if (leaderForward.sqrMagnitude < 0.01f)
            leaderForward = Vector2.right;

        Vector2 side = new Vector2(-leaderForward.y, leaderForward.x);

        float backOffset = 0.8f + (slotIndex % 2) * 0.35f;
        float sideOffset = 0f;

        switch (slotIndex % 3)
        {
            case 0: sideOffset = 0f; break;
            case 1: sideOffset = 0.45f; break;
            case 2: sideOffset = -0.45f; break;
        }

        Vector2 followPoint = leaderPos - leaderForward * backOffset + side * sideOffset;
        Vector2 toLeader = leaderPos - myPos;
        float distToLeader = toLeader.magnitude;

        Vector2 separation = GetSeparationVector() * 0.35f;

        if (distToLeader > catchUpDistance)
        {
            desiredMove = toLeader + separation;
            FaceByMove(desiredMove);
            SetMoveAnim(true);
            return;
        }

        Vector2 toFollowPoint = followPoint - myPos;

        if (toFollowPoint.sqrMagnitude <= followStopDistance * followStopDistance)
        {
            if (leaderVel.sqrMagnitude > 0.01f)
            {
                desiredMove = leaderVel.normalized * 0.35f + separation;
                FaceByMove(desiredMove);
                SetMoveAnim(true);
            }
            else
            {
                desiredMove = Vector2.zero;
                SetMoveAnim(false);
            }
            return;
        }

        desiredMove = toFollowPoint + separation;
        FaceByMove(desiredMove);
        SetMoveAnim(true);
    }

    Vector2 GetSeparationVector()
    {
        if (group == null) return Vector2.zero;

        Vector2 myPos = rb.position;
        Vector2 push = Vector2.zero;
        float radiusSqr = separationRadius * separationRadius;

        for (int i = 0; i < group.members.Count; i++)
        {
            EnemyBrain other = group.members[i];
            if (other == null || other == this || other.IsDead()) continue;

            Vector2 delta = myPos - other.rb.position;
            float d2 = delta.sqrMagnitude;

            if (d2 < 0.0001f || d2 > radiusSqr) continue;

            push += delta.normalized / Mathf.Max(0.1f, Mathf.Sqrt(d2));
        }

        return push * separationWeight;
    }

    Transform FindClosestPlayerOrAllyAroundMe()
    {
        Vector2 center = rb.position;

        int cnt = Physics2D.OverlapCircle(center, detectRadius, scanFilter, scanBuffer);

        Transform best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < cnt; i++)
        {
            Collider2D col = scanBuffer[i];
            if (col == null) continue;

            CombatAgent other = col.GetComponent<CombatAgent>();
            if (other == null || other.IsDead) continue;
            if (other.team == Team.Enemy) continue;

            float d = ((Vector2)other.transform.position - center).sqrMagnitude;

            if (d < bestDist)
            {
                bestDist = d;
                best = other.transform;
            }
        }

        return best;
    }

    bool IsValidGroupTarget(Transform target)
    {
        if (!IsValidTarget(target))
            return false;

        CombatAgent other = target.GetComponent<CombatAgent>();
        if (other == null || other.IsDead)
            return false;

        return other.team != Team.Enemy;
    }

    void SetMoveAnim(bool moving)
    {
        if (anim != null)
            anim.SetBool(isMovingParam, moving);
    }
}