using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AllyBrain : BrainBase
{
    int myIndex = -1;

    Transform leader;
    PlayerSquadController squad;
    PlayerMover2D playerMover;

    [Header("Move")]
    [SerializeField] float regroupDistance = 6.0f; // leader와 이 이상 멀어지면 전투 취소하고 무조건 복귀

    [Header("Idle")]
    float idleHoldRadius = 1.25f; // leader 주변 이 안이면 "정렬 강요 없이" 정지

    [Header("Arrive (ToPoint)")]
    [SerializeField] float slowRadius = 1.2f; // 목적지에 이 안으로 들어오면 감속 시작. stopRadius보다 커야 함.
    [SerializeField] float stopRadius = 0.3f; // 목적지에 이 안으로 들어오면 멈춤.

    [Header("Avoid (Leader)")]
    [SerializeField] float leaderAvoidRadius = 0.9f; // leader 주변 이 반경 안에 들어오면 밀어내기 시작
    [SerializeField] float leaderAvoidStrength = 1.2f; // leader와 겹치지 않도록 밀어내는 힘
    [SerializeField] float leaderYieldStrength = 1.6f; // 좌우 비키기

    [Header("Separation (Allies)")]
    [SerializeField] float allySeparationRadius = 0.6f; // Separation 시작 반경 (이 안에 다른 아군이 있으면 서로 밀어내기 시작)
    [SerializeField] float allySeparationStrength = 1.0f; // 서로 겹치지 않도록 밀어내는 힘

    [Header("Combat")]
    [SerializeField] float combatRepathInterval = 0.4f; // 전투 슬롯 재선정 텀(너무 자주 바꾸면 지터)
    float nextCombatRepathTime;


    Vector2 combatSlotWorld; // 목표 슬롯

    Vector2 desired;
    bool desiredIsDirection;


    public void SetupAsAlly(PlayerSquadController squadController, int index)
    {
        var player = GamaManager.Instance.player;

        myIndex = index;

        squad ??= squadController;
        leader ??= player.transform;
        playerMover ??= player.GetComponent<PlayerMover2D>();

        currentTarget = null;

        combat.SetTeam(CombatAgent.Team.Ally);
    }

    public void SetFormation(PlayerSquadController squadController, int index)
    {
        squad = squadController;
        myIndex = index;
    }

    public void SetIndex(int index) => myIndex = index;

    public Vector2 GetPosition2D() => (rb != null) ? rb.position : (Vector2)transform.position;

    void Update()
    {
        if (combat.IsDead) return;
        if (myIndex < 0) return;

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
                    FaceTo(currentTarget.position);
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
        bool moving = rb.linearVelocity.sqrMagnitude > 0.01f;
        anim.SetBool(isMovingParam, moving);
        if (!moving) return;

        FaceByMove(moveVec);
    }
}