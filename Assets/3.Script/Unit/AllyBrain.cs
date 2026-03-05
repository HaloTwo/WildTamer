using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AllyBrain : MonoBehaviour
{
    PlayerMover2D playerMover;

    CombatAgent combat;
    Rigidbody2D rb;
    Animator anim;

    Transform leader;
    PlayerSquadController squad;
    int myIndex = -1;

    Transform currentTarget;

    [Header("Move")]
    [SerializeField] float moveSpeed = 3.25f;
    [SerializeField] float regroupDistance = 6.0f;

    [Header("Idle")]
    float idleHoldRadius = 1.25f; // leader 주변 이 안이면 "정렬 강요 없이" 정지

    [Header("Scan")]
    [SerializeField] float scanInterval = 0.2f;
    float detectRadius = 3.5f;
    float nextScanTime;

    [Header("Arrive (ToPoint)")]
    [SerializeField] float slowRadius = 1.2f;
    [SerializeField] float stopRadius = 0.25f;

    [Header("Avoid (Leader)")]
    [SerializeField] float leaderAvoidRadius = 0.9f;
    [SerializeField] float leaderAvoidStrength = 1.2f;
    [SerializeField] float leaderYieldStrength = 1.6f; // 좌우 비키기

    [Header("Separation (Allies)")]
    [SerializeField] float allySeparationRadius = 0.6f;
    [SerializeField] float allySeparationStrength = 1.0f;

    [Header("Combat")]
    [SerializeField] float combatRepathInterval = 0.12f; // 전투 슬롯 재선정 텀(너무 자주 바꾸면 지터)
    float nextCombatRepathTime;
    Vector2 combatSlotWorld; // 목표 슬롯


    Vector2 desired;
    bool desiredIsDirection;

    float facing = 1f;
    const string isMovingParam = "IsMoving";

    public void FirstBrainSet(CombatAgent combatAgent, Animator animator, Rigidbody2D rigidbody2D)
    {
        combat = combatAgent;
        anim = animator;
        rb = rigidbody2D;
    }

    public void SetupAsAlly(Transform leaderTr)
    {
        leader = leaderTr;
        enabled = true;
        currentTarget = null;

        if (combat != null) combat.SetTeam(CombatAgent.Team.Ally);

        if (playerMover == null && leader != null) playerMover = leader.GetComponent<PlayerMover2D>();
    }

    public void SetFormation(PlayerSquadController squadController, int index)
    {
        squad = squadController;
        myIndex = index;
    }

    public void SetIndex(int index) => myIndex = index;

    // PlayerSquadController 점유체크에서 사용
    public Vector2 GetPosition2D() => (rb != null) ? rb.position : (Vector2)transform.position;

    void Update()
    {
        if (combat == null || combat.IsDead) return;
        if (rb == null) return;
        if (leader == null) return;
        if (squad == null || myIndex < 0) return;

        Vector2 myPos = rb.position;
        Vector2 leaderPos = leader.position;

        // 리더 입력 (deadZone 동일하게)
        Vector2 inputDir = (playerMover != null) ? playerMover.MoveInput : Vector2.zero;
        const float inputDead = 0.15f;
        bool leaderMoving = inputDir.sqrMagnitude > (inputDead * inputDead);
        Vector2 leaderFwd = leaderMoving ? inputDir.normalized : Vector2.zero;

        desired = Vector2.zero;
        desiredIsDirection = false;

        // 1) 너무 멀면 "복귀 우선" (전투 취소)
        float leaderDist = Vector2.Distance(myPos, leaderPos);
        if (leaderDist > regroupDistance)
        {
            currentTarget = null;

            if (leaderMoving)
            {
                // 그룹 방향으로 같이 이동 (앞쪽이면 속도 줄여서 역돌진 방지)
                desired = leaderFwd;
                desiredIsDirection = true;

                Vector2 toMe = myPos - leaderPos;
                float front = Vector2.Dot(toMe, desired);
                if (front > 0.15f)
                    desired *= 0.25f;
            }
            else
            {
                Vector2 idlePos = squad.GetIdleRingWorldPos(myIndex);
                desired = idlePos - myPos;
                desiredIsDirection = false;
            }

            desired += GetLeaderAvoid(myPos, leaderPos, leaderFwd);
            desired += GetAllySeparation(myPos);
            ApplyAnimAndFlip(desired);
            return;
        }

        // 2) 타겟 갱신 (항상 돌아감)
        if (Time.time >= nextScanTime && (!currentTarget || !currentTarget.gameObject.activeInHierarchy))
        {
            nextScanTime = Time.time + scanInterval;
            currentTarget = FindClosestEnemyAroundLeader_NoOverlap(leaderPos);
        }

        // 3) 전투: 타겟 있으면 "둘러싸기 슬롯"으로 이동 후 사정거리에서 공격
        if (currentTarget && currentTarget.gameObject.activeInHierarchy)
        {
            var enemy = currentTarget.GetComponentInParent<CombatAgent>();
            if (enemy == null || enemy.IsDead || enemy.team != CombatAgent.Team.Enemy)
            {
                currentTarget = null;
            }
            else
            {
                // 슬롯 목표점 재계산(너무 자주 바꾸면 덜덜 떠서 interval)
                if (Time.time >= nextCombatRepathTime)
                {
                    nextCombatRepathTime = Time.time + combatRepathInterval;
                    combatSlotWorld = squad.GetCombatSlotWorldPos(currentTarget, myIndex, combat.AttackRange, myPos);
                }

                bool inAttack = combat.IsInRange(currentTarget);

                if (!inAttack)
                {
                    desired = combatSlotWorld - myPos;   // 타겟 "본체"가 아니라 슬롯으로 접근
                    desiredIsDirection = false;
                }
                else
                {
                    // 공격 중엔 밀기 줄이려고 이동은 멈추는 편이 훨씬 안정적
                    combat.TryAttack(currentTarget);
                    desired = Vector2.zero;
                    desiredIsDirection = false;
                }

                desired += GetAllySeparation(myPos);         // 서로 겹침/밀기 최소화
                desired += GetLeaderAvoid(myPos, leaderPos, leaderFwd); // 플레이어 길막도 최소화
                ApplyAnimAndFlip(desired);
                return;
            }
        }

        // 4) 타겟 없으면: leaderMoving이면 그룹 이동 / 아니면 hold 안이면 정지, 밖이면 idle 링으로
        if (leaderMoving)
        {
            desired = leaderFwd;
            desiredIsDirection = true;

            Vector2 toMe = myPos - leaderPos;
            float front = Vector2.Dot(toMe, leaderFwd);
            if (front > 0.15f)
                desired *= 0.25f;
        }
        else
        {
            float hold2 = idleHoldRadius * idleHoldRadius;

            if ((myPos - leaderPos).sqrMagnitude <= hold2)
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
            Vector2 dir = desired.normalized;
            rb.linearVelocity = dir * moveSpeed;
            return;
        }

        // ToPoint Arrive
        float dist = desired.magnitude;
        if (dist <= stopRadius)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float speed = moveSpeed;
        if (dist < slowRadius)
            speed *= (dist / slowRadius);

        Vector2 d = desired / dist;
        rb.linearVelocity = d * speed;
    }

    // ---------- Scan (No Overlap) ----------
    Transform FindClosestEnemyAroundLeader_NoOverlap(Vector2 leaderPos)
    {
        float r2 = detectRadius * detectRadius;

        Transform best = null;
        float bestDist = float.MaxValue;

        var all = CombatAgent.All; // :contentReference[oaicite:4]{index=4}
        for (int i = 0; i < all.Count; i++)
        {
            var a = all[i];
            if (a == null || a.IsDead) continue;
            if (a.team != CombatAgent.Team.Enemy) continue;

            Vector2 p = a.transform.position;

            // leader 주변 detectRadius 안
            float d2Leader = (p - leaderPos).sqrMagnitude;
            if (d2Leader > r2) continue;

            // 나 기준 가까운 적
            float d2Me = (p - rb.position).sqrMagnitude;
            if (d2Me < bestDist)
            {
                bestDist = d2Me;
                best = a.transform;
            }
        }

        return best;
    }

    // ---------- Avoid Leader (push + yield) ----------
    Vector2 GetLeaderAvoid(Vector2 myPos, Vector2 leaderPos, Vector2 leaderFwd)
    {
        Vector2 v = myPos - leaderPos;
        float d2 = v.sqrMagnitude;
        float r2 = leaderAvoidRadius * leaderAvoidRadius;

        if (d2 >= r2 || d2 < 0.000001f) return Vector2.zero;

        float d = Mathf.Sqrt(d2);
        float t = 1f - (d / leaderAvoidRadius);

        Vector2 push = (v / d) * (leaderAvoidStrength * t);

        // leader가 움직일 때는 "옆으로 비키기" 추가
        if (leaderFwd.sqrMagnitude > 0.0001f)
        {
            Vector2 right = new Vector2(leaderFwd.y, -leaderFwd.x);

            // 같은 프레임에 모두 같은 쪽으로 몰리는 거 방지: 인덱스로 side 고정
            float side = (myIndex % 2 == 0) ? -1f : +1f;

            Vector2 yield = right * side * (leaderYieldStrength * t);
            return push + yield;
        }

        return push;
    }

    // ---------- Separation (Allies) ----------
    Vector2 GetAllySeparation(Vector2 myPos)
    {
        if (squad == null) return Vector2.zero;

        float r = allySeparationRadius;
        float r2 = r * r;

        Vector2 sep = Vector2.zero;

        var list = squad.allies; // public readonly list :contentReference[oaicite:5]{index=5}
        for (int i = 0; i < list.Count; i++)
        {
            var other = list[i];
            if (other == null || other == this) continue;

            Vector2 op = other.GetPosition2D();
            Vector2 v = myPos - op;
            float d2 = v.sqrMagnitude;

            if (d2 < 0.000001f || d2 > r2) continue;

            float d = Mathf.Sqrt(d2);
            float t = 1f - (d / r);

            sep += (v / d) * (allySeparationStrength * t);
        }

        return sep;
    }

    void ApplyAnimAndFlip(Vector2 moveVec)
    {
        if (anim == null) anim = GetComponentInChildren<Animator>(true);
        if (anim == null) return;

        bool moving = rb.linearVelocity.sqrMagnitude > 0.01f;
        anim.SetBool(isMovingParam, moving);
        if (!moving) return;

        if (moveVec.x > 0.01f) facing = -0.7f;
        else if (moveVec.x < -0.01f) facing = 0.7f;

        Vector3 s = transform.localScale;
        s.x = facing;
        transform.localScale = s;
    }
}