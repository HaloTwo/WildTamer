using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AllyBrain : BrainBase
{
    int myIndex = -1;

    Transform leader;
    PlayerSquadController squad;
    PlayerMover2D playerMover;
    PlayerBrain playerBrain;

    CombatAgent currentTargetAgent;

    [Header("Move")]
    [SerializeField] float regroupDistance = 6.0f;

    [Header("Idle")]
    [SerializeField] float idleHoldRadius = 1.25f;

    [Header("Arrive (ToPoint)")]
    [SerializeField] float slowRadius = 1.2f;
    [SerializeField] float stopRadius = 0.3f;

    [Header("Avoid (Leader)")]
    [SerializeField] float leaderAvoidRadius = 0.9f;
    [SerializeField] float leaderAvoidStrength = 1.2f;
    [SerializeField] float leaderYieldStrength = 1.6f;

    [Header("Separation (Allies)")]
    [SerializeField] float allySeparationRadius = 0.6f;
    [SerializeField] float allySeparationStrength = 1.0f;

    [Header("Combat")]
    [SerializeField] float combatRepathInterval = 0.4f;
    float nextCombatRepathTime;

    Vector2 combatSlotWorld;
    Vector2 desired;
    bool desiredIsDirection;

    float regroupDistanceSqr;
    float idleHoldRadiusSqr;
    float stopRadiusSqr;
    float slowRadiusSqr;
    float leaderAvoidRadiusSqr;
    float allySeparationRadiusSqr;

    public void SetupAsAlly(int index)
    {
        var player = GameManager.Instance.playerSquad;

        // 내 편대 인덱스 저장
        myIndex = index;

        // 필요한 참조 캐싱
        leader ??= player.transform;

        squad ??= GameManager.Instance.playerSquad;
        playerMover ??= player.GetComponent<PlayerMover2D>();
        playerBrain ??= player.GetComponent<PlayerBrain>();

        // 타겟 초기화
        currentTarget = null;
        currentTargetAgent = null;

        // 팀을 아군으로 변경
        combat.SetTeam(Team.Ally);

        // 모든 아군이 같은 프레임에 스캔/리패스하지 않도록 약간 분산
        float offset = (myIndex & 3) * 0.03f;
        nextScanTime = Time.time + offset;
        nextCombatRepathTime = Time.time + offset;
    }

    void Awake()
    {
        regroupDistanceSqr = regroupDistance * regroupDistance;
        idleHoldRadiusSqr = idleHoldRadius * idleHoldRadius;
        stopRadiusSqr = stopRadius * stopRadius;
        slowRadiusSqr = slowRadius * slowRadius;
        leaderAvoidRadiusSqr = leaderAvoidRadius * leaderAvoidRadius;
        allySeparationRadiusSqr = allySeparationRadius * allySeparationRadius;
    }


    public void SetIndex(int index) => myIndex = index;

    public Vector2 GetPosition2D() => rb != null ? rb.position : (Vector2)transform.position;

    void Update()
    {
        // 죽었거나 필요한 참조가 없으면 실행 안 함
        if (combat == null || combat.IsDead) return;
        if (rb == null || leader == null || squad == null || myIndex < 0) return;

        Vector2 myPos = rb.position;
        Vector2 leaderPos = leader.position;

        // 플레이어 입력 방향 가져오기
        Vector2 inputDir = playerMover != null ? playerMover.MoveInput : Vector2.zero;
        const float inputDead = 0.15f;

        bool leaderMoving = inputDir.sqrMagnitude > (inputDead * inputDead);
        Vector2 leaderFwd = leaderMoving ? inputDir.normalized : Vector2.zero;

        desired = Vector2.zero;
        desiredIsDirection = false;

        // 1) 플레이어와 너무 멀면 전투고 뭐고 무조건 복귀
        Vector2 toLeader = leaderPos - myPos;
        if (toLeader.sqrMagnitude > regroupDistanceSqr)
        {
            currentTarget = null;
            currentTargetAgent = null;

            if (leaderMoving)
            {
                // 플레이어가 이동 중이면 같은 방향으로 따라감
                desired = leaderFwd;
                desiredIsDirection = true;

                // 플레이어 앞쪽에 너무 튀어나가 있으면 속도 줄임
                Vector2 fromLeaderToMe = myPos - leaderPos;
                float front = Vector2.Dot(fromLeaderToMe, desired);
                if (front > 0.15f)
                    desired *= 0.25f;
            }
            else
            {
                // 플레이어가 멈춰 있으면 내 idle 슬롯으로 복귀
                Vector2 idlePos = squad.GetIdleRingWorldPos(myIndex);
                desired = idlePos - myPos;
            }

            desired += GetLeaderAvoid(myPos, leaderPos, leaderFwd);
            desired += GetAllySeparation(myPos);

            ApplyAnimAndFlip(desired);
            return;
        }

        // 2) 일정 주기마다 타겟 갱신
        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            UpdateTarget(myPos);
        }

        // 3) 타겟이 있으면 전투
        if (currentTargetAgent != null && currentTarget != null)
        {
            if (!IsValidCurrentTarget())
            {
                currentTarget = null;
                currentTargetAgent = null;

                // 죽었거나 무효가 된 순간 바로 새 타겟 탐색
                UpdateTarget(myPos);
            }

            if (currentTargetAgent != null && currentTarget != null)
            {
                if (Time.time >= nextCombatRepathTime)
                {
                    nextCombatRepathTime = Time.time + combatRepathInterval;
                    combatSlotWorld = squad.GetCombatSlotWorldPos(currentTarget, myIndex, combat.AttackRange, myPos);
                }

                bool inAttack = combat.IsInRange(currentTarget);

                if (!inAttack)
                {
                    desired = combatSlotWorld - myPos;
                    desiredIsDirection = false;
                }
                else
                {
                    FaceTo(currentTarget.position);
                    combat.TryAttack(currentTarget);
                    desired = Vector2.zero;
                    desiredIsDirection = false;
                }

                desired += GetAllySeparation(myPos);
                desired += GetLeaderAvoid(myPos, leaderPos, leaderFwd);

                ApplyAnimAndFlip(desired);
                return;
            }
        }

        // 4) 타겟 없으면 기본 대형 이동
        if (leaderMoving)
        {
            desired = leaderFwd;
            desiredIsDirection = true;

            Vector2 fromLeaderToMe = myPos - leaderPos;
            float front = Vector2.Dot(fromLeaderToMe, leaderFwd);
            if (front > 0.15f)
                desired *= 0.25f;
        }
        else
        {
            // 플레이어 근처면 정지, 멀면 내 idle 슬롯으로 이동
            if ((myPos - leaderPos).sqrMagnitude <= idleHoldRadiusSqr)
            {
                desired = Vector2.zero;
                desiredIsDirection = false;
            }
            else
            {
                Vector2 idlePos = squad.GetIdleRingWorldPos(myIndex);
                desired = idlePos - myPos;
                desiredIsDirection = false;
            }
        }

        desired += GetAllySeparation(myPos);
        desired += GetLeaderAvoid(myPos, leaderPos, leaderFwd);

        ApplyAnimAndFlip(desired);
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        if (desired.sqrMagnitude < 0.0001f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (desiredIsDirection)
        {
            rb.linearVelocity = desired.normalized * moveSpeed;
            return;
        }

        float distSqr = desired.sqrMagnitude;

        if (distSqr <= stopRadiusSqr)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float dist = Mathf.Sqrt(distSqr);
        float speed = moveSpeed;

        if (distSqr < slowRadiusSqr)
            speed *= (dist / slowRadius);

        rb.linearVelocity = (desired / dist) * speed;
    }

    bool IsValidCurrentTarget()
    {
        if (currentTarget == null || currentTargetAgent == null)
            return false;

        if (!currentTarget.gameObject.activeInHierarchy)
            return false;

        if (currentTargetAgent.IsDead)
            return false;

        if (currentTargetAgent.team != Team.Enemy)
            return false;

        return true;
    }

    // 타겟 우선순위:
    // 1. 플레이어가 현재 공격 중인 적
    // 2. 없으면 내 주변 detectRadius 안의 가장 가까운 적
    void UpdateTarget(Vector2 myPos)
    {
        Transform sharedTarget = playerBrain.CurrentTarget;

        if (sharedTarget != null && sharedTarget.TryGetComponent(out CombatAgent sharedAgent))
        {
            if (!sharedAgent.IsDead && sharedAgent.team == Team.Enemy)
            {
                currentTarget = sharedTarget;
                currentTargetAgent = sharedAgent;
                return;
            }
        }

        FindClosestEnemyAroundMe(myPos);
    }

    // 내 주변 detectRadius 안에서 가장 가까운 적을 찾음
    void FindClosestEnemyAroundMe(Vector2 myPos)
    {
        float detectRadiusSqr = detectRadius * detectRadius;

        Transform bestTarget = null;
        CombatAgent bestAgent = null;
        float bestDist = float.MaxValue;

        var all = CombatAgent.All;
        for (int i = 0; i < all.Count; i++)
        {
            var a = all[i];
            if (a == null || a.IsDead) continue;
            if (a.team != Team.Enemy) continue;

            Vector2 diff = (Vector2)a.transform.position - myPos;
            float d2 = diff.sqrMagnitude;
            if (d2 > detectRadiusSqr) continue;

            if (d2 < bestDist)
            {
                bestDist = d2;
                bestTarget = a.transform;
                bestAgent = a;
            }
        }

        currentTarget = bestTarget;
        currentTargetAgent = bestAgent;
    }

    Vector2 GetLeaderAvoid(Vector2 myPos, Vector2 leaderPos, Vector2 leaderFwd)
    {
        Vector2 v = myPos - leaderPos;
        float d2 = v.sqrMagnitude;

        if (d2 >= leaderAvoidRadiusSqr || d2 < 0.000001f)
            return Vector2.zero;

        float d = Mathf.Sqrt(d2);
        float t = 1f - (d / leaderAvoidRadius);

        Vector2 push = (v / d) * (leaderAvoidStrength * t);

        if (leaderFwd.sqrMagnitude > 0.0001f)
        {
            Vector2 right = new Vector2(leaderFwd.y, -leaderFwd.x);
            float side = (myIndex & 1) == 0 ? -1f : 1f;
            Vector2 yield = right * side * (leaderYieldStrength * t);
            return push + yield;
        }

        return push;
    }

    Vector2 GetAllySeparation(Vector2 myPos)
    {
        if (squad == null) return Vector2.zero;

        Vector2 sep = Vector2.zero;
        var list = squad.allies;

        for (int i = 0; i < list.Count; i++)
        {
            var other = list[i];
            if (other == null || other == this) continue;

            Vector2 v = myPos - other.GetPosition2D();
            float d2 = v.sqrMagnitude;

            if (d2 < 0.000001f || d2 > allySeparationRadiusSqr)
                continue;

            float d = Mathf.Sqrt(d2);
            float t = 1f - (d / allySeparationRadius);

            sep += (v / d) * (allySeparationStrength * t);
        }

        return sep;
    }

    void ApplyAnimAndFlip(Vector2 moveVec)
    {
        bool moving = rb.linearVelocity.sqrMagnitude > 0.01f;
        anim.SetBool(isMovingParam, moving);

        if (!moving) return;
        FaceByMove(moveVec);
    }
}