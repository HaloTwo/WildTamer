using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class AllyBrain : MonoBehaviour
{
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
    float idleHoldRadius = 2.5f; 

    [Header("Scan")]
    [SerializeField] float scanInterval = 0.2f;
    [SerializeField] float detectRadius = 5.0f; 
    float nextScanTime;

    [Header("Smooth")]
    [SerializeField] float slowRadius = 1.2f;
    [SerializeField] float stopRadius = 0.25f; // ★ 0.15 너무 빡셈. 제자리걸음/밀림 줄이려고 올림

    Vector2 desiredMove;
    float facing = 1f;
    const string isMovingParam = "IsMoving";

    [SerializeField] PlayerMover2D playerMover;

    public void FirstBrainSet(CombatAgent combatAgent, Animator animator, Rigidbody2D rigidbody2D)
    {
        combat = combatAgent;
        anim = animator;
        rb = rigidbody2D;
    }

    // 테이밍 전환에서 호출
    public void SetupAsAlly(Transform leaderTr)
    {
        leader = leaderTr;
        enabled = true;
        currentTarget = null;

        combat.SetTeam(CombatAgent.Team.Ally);

        if (playerMover == null && leader != null)
            playerMover = leader.GetComponent<PlayerMover2D>();
    }

    // 스쿼드 등록 시 주입
    public void SetFormation(PlayerSquadController squadController, int index)
    {
        squad = squadController;
        myIndex = index;
    }

    public void SetIndex(int index) => myIndex = index;

    void Update()
    {
        if (combat == null || combat.IsDead) return;
        if (rb == null) return;
        if (leader == null) return;
        if (squad == null || myIndex < 0) return;

        // --- 튜닝값 ---
        float leaderAvoidRadius = 0.9f;
        float leaderAvoidStrength = 1.2f;

        float engageRadius = combat.AttackRange * 2.5f;
        float engageRadius2 = engageRadius * engageRadius;

        float rallyForward = 0.9f;
        // -------------

        desiredMove = Vector2.zero;

        Vector2 myPos = rb.position;
        Vector2 leaderPos = leader.position;

        // 플레이어 입력 기반 그룹 이동
        Vector2 inputDir = (playerMover != null) ? playerMover.MoveInput : Vector2.zero;

        const float inputDead = 0.15f; // PlayerMover2D deadZone과 동일하게
        bool leaderMoving = inputDir.sqrMagnitude > (inputDead * inputDead);
        Vector2 leaderFwd = leaderMoving ? inputDir.normalized : Vector2.zero;

        float leaderDist = Vector2.Distance(transform.position, leader.position);

        // 1) 너무 멀면 무조건 복귀 (이게 최우선)
        if (leaderDist > regroupDistance)
        {
            currentTarget = null;

            if (leaderMoving)
            {
                desiredMove = leaderFwd;

                Vector2 toMe = myPos - leaderPos;
                float front = Vector2.Dot(toMe, desiredMove);
                if (front > 0.15f)
                    desiredMove *= 0.2f;
            }
            else
            {
                // 멀고, 플레이어도 안 움직이면 → 링/슬롯으로 천천히 복귀
                Vector2 slotPos = squad.GetRingWorldPos(myIndex);
                desiredMove = slotPos - myPos;
            }

            desiredMove += GetLeaderAvoid(myPos, leaderPos, leaderFwd, leaderAvoidRadius, leaderAvoidStrength);
            ApplyAnimAndFlip(desiredMove);
            return;
        }

        // 2) 타겟 갱신 (leader가 멈춰도 항상 돌아야 함)
        if (Time.time >= nextScanTime && (!currentTarget || !currentTarget.gameObject.activeInHierarchy))
        {
            nextScanTime = Time.time + scanInterval;
            currentTarget = FindClosestEnemyAroundLeader_NoOverlap(); // 내부에서 detectRadius 사용하게 바꿀거임
        }

        // 3) 전투: 타겟 있으면 무조건 추적/공격 (leaderMoving 여부랑 무관)
        if (currentTarget && currentTarget.gameObject.activeInHierarchy)
        {
            var enemy = currentTarget.GetComponentInParent<CombatAgent>();
            if (enemy == null || enemy.IsDead || enemy.team != CombatAgent.Team.Enemy)
            {
                currentTarget = null;
            }
            else
            {
                // 사정거리까지 다다닥
                if (!combat.IsInRange(currentTarget))
                    desiredMove = (Vector2)currentTarget.position - myPos;
                else
                    combat.TryAttack(currentTarget);

                desiredMove += GetLeaderAvoid(myPos, leaderPos, leaderFwd, leaderAvoidRadius, leaderAvoidStrength);
                ApplyAnimAndFlip(desiredMove);
                return;
            }
        }

        // 플레이어가 멈췄고, 내 위치가 leader 주변 hold 안이면 완전 정지
        if (!leaderMoving)
        {
            float hold2 = idleHoldRadius * idleHoldRadius;
            if ((myPos - leaderPos).sqrMagnitude <= hold2)
            {
                desiredMove = Vector2.zero;
                rb.linearVelocity = Vector2.zero; // ★ 즉시 정지
                ApplyAnimAndFlip(Vector2.zero);
                return;
            }

            // hold 밖이면 자리 잡으러만 이동 (링/슬롯)
            Vector2 targetPos = squad.GetRingWorldPos(myIndex);
            desiredMove = targetPos - myPos;

            desiredMove += GetLeaderAvoid(myPos, leaderPos, leaderFwd, leaderAvoidRadius, leaderAvoidStrength);
            ApplyAnimAndFlip(desiredMove);
            return;
        }

        // 5) 타겟 없으면: 그룹 이동 or 링 정렬
        if (leaderMoving)
        {
            desiredMove = leaderFwd;

            // 내가 플레이어 앞에 있으면 속도 줄이기(역돌진 방지)
            Vector2 toMe = myPos - leaderPos;
            float front = Vector2.Dot(toMe, leaderFwd);
            if (front > 0.15f)
                desiredMove *= 0.2f;
        }
        else
        {
            // 멈췄을 때만 링 정렬 (hold 로직이 위에서 걸러져야 함)
            Vector2 ringPos = squad.GetRingWorldPos(myIndex);
            desiredMove = ringPos - myPos;
        }

        // 6) 플레이어 근접 비키기 (좌우 yield 포함)
        desiredMove += GetLeaderAvoid(myPos, leaderPos, leaderFwd, leaderAvoidRadius, leaderAvoidStrength);
        ApplyAnimAndFlip(desiredMove);
    }

    // 플레이어 근접 회피: 방사형(push) + 측면(yield)
    Vector2 GetLeaderAvoid(Vector2 myPos, Vector2 leaderPos, Vector2 leaderFwd, float radius, float strength)
    {
        Vector2 v = myPos - leaderPos;
        float d2 = v.sqrMagnitude;
        float r2 = radius * radius;

        if (d2 >= r2 || d2 < 0.000001f) return Vector2.zero;

        float d = Mathf.Sqrt(d2);
        float t = 1f - (d / radius); // 가까울수록 1

        Vector2 push = v.normalized * (strength * t);

        if (leaderFwd.sqrMagnitude > 0.0001f)
        {
            leaderFwd.Normalize();
            Vector2 right = new Vector2(leaderFwd.y, -leaderFwd.x);

            // ★ 둘이 같은 쪽으로 몰리면 여기 side를 인덱스로 고정해라
            // float side = (myIndex % 2 == 0) ? -1f : +1f;

            float side = Mathf.Sign(Vector2.Dot(v, right));
            if (side == 0f) side = 1f;

            Vector2 yield = right * side * (strength * 1.6f * t);
            return push + yield;
        }

        return push;
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        Vector2 inputDir = (playerMover != null) ? playerMover.MoveInput : Vector2.zero;
        bool leaderMoving = inputDir.sqrMagnitude > 0.0001f;

        if (leaderMoving)
        {
            if (desiredMove.sqrMagnitude < 0.0001f)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 dir = desiredMove.normalized;
            rb.linearVelocity = dir * moveSpeed;
            return;
        }

        // leader stop: desiredMove는 위치차로 취급
        if (desiredMove.sqrMagnitude < 0.0001f)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float dist = desiredMove.magnitude;
        if (dist <= stopRadius)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 d = desiredMove / dist;

        float speed = moveSpeed;
        if (dist < slowRadius)
            speed *= (dist / slowRadius);

        rb.linearVelocity = d * speed;
    }

    Transform FindClosestEnemyAroundLeader_NoOverlap()
    {
        Vector2 center = leader.position;

        float r = detectRadius;   
        float r2 = r * r;

        Transform best = null;
        float bestDist = float.MaxValue;

        var all = CombatAgent.All;
        for (int i = 0; i < all.Count; i++)
        {
            var a = all[i];
            if (a == null || a.IsDead) continue;
            if (a.team != CombatAgent.Team.Enemy) continue;

            // leader 주변 detectRadius 안에 있는 적만 후보
            float d2Leader = ((Vector2)a.transform.position - center).sqrMagnitude;
            if (d2Leader > r2) continue;

            // 내 기준 가장 가까운 적 선택
            float d2Me = ((Vector2)a.transform.position - rb.position).sqrMagnitude;
            if (d2Me < bestDist)
            {
                bestDist = d2Me;
                best = a.transform;
            }
        }

        return best;
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