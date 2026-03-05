using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyBrain : MonoBehaviour
{
    CombatAgent combat;
    Rigidbody2D rb;
    Animator anim;

    Transform currentTarget;

    [Header("Move")]
    [SerializeField] float moveSpeed = 3.5f;     // 플레이어와 동일
    [SerializeField] float idleWanderStop = 0f;  // 필요 없으면 0 유지 (타겟 없을 때 정지)

    [Header("Scan")]
    [SerializeField] LayerMask targetMask;       // Player + Ally 레이어
    [SerializeField] float scanInterval = 0.2f;

    float nextScanTime;

    readonly Collider2D[] scanBuffer = new Collider2D[16];
    ContactFilter2D scanFilter;

    Vector2 desiredMove;
    float facing = 1f;
    const string isMovingParam = "IsMoving";



    public void FirstBrainSet(CombatAgent combatAgent, Animator animator, Rigidbody2D rigidbody2D)
    {
        combat = combatAgent;
        anim = animator;
        rb = rigidbody2D;
    }

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
        if (combat == null || combat.IsDead || !gameObject.activeSelf) return;

        desiredMove = Vector2.zero;

        // 타겟 없거나 죽었으면 주기적으로 재탐색 (요구사항: 스캔=공격범위)
        if (Time.time >= nextScanTime && (currentTarget == null || !currentTarget.gameObject.activeInHierarchy))
        {
            nextScanTime = Time.time + scanInterval;
            currentTarget = FindClosestPlayerOrAllyAroundMe();
        }

        // 타겟 있으면 접근/공격
        if (currentTarget != null && currentTarget.gameObject.activeInHierarchy || combat.IsDead)
        {
            var other = currentTarget.GetComponentInParent<CombatAgent>();
            if (other == null || other.IsDead) { currentTarget = null; return; }
            if (other.team == CombatAgent.Team.Enemy) { currentTarget = null; return; } // Player/Ally만

            if (!combat.IsInRange(currentTarget))
                desiredMove = (currentTarget.position - transform.position);
            else
                combat.TryAttack(currentTarget);

            ApplyAnimAndFlip(desiredMove);
            return;
        }

        // 타겟 없으면 정지(또는 네가 원하면 여기서 배회 로직 넣기)
        if (idleWanderStop > 0f)
        {
            // 옵션: 아주 약하게 랜덤 배회 같은 거 넣고 싶으면 여기서 처리
        }

        ApplyAnimAndFlip(desiredMove);
    }

    void FixedUpdate()
    {
        if (desiredMove.sqrMagnitude < 0.0001f) return;

        Vector2 dir = desiredMove.normalized;
        rb.MovePosition(rb.position + dir * moveSpeed * Time.fixedDeltaTime);
    }

    Transform FindClosestPlayerOrAllyAroundMe()
    {
        Vector2 center = rb.position;
        float r = combat.AttackRange; // 요구사항: 스캔 = 공격범위

        int cnt = Physics2D.OverlapCircle(center, r, scanFilter, scanBuffer);

        Transform best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < cnt; i++)
        {
            var col = scanBuffer[i];
            if (col == null) continue;

            var other = col.GetComponentInParent<CombatAgent>();
            if (other == null || other.IsDead) continue;

            // Enemy는 Player/Ally 아무나 공격
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

    void ApplyAnimAndFlip(Vector2 moveVec)
    {
        bool moving = moveVec.sqrMagnitude > 0.0001f;
        if (anim != null) anim.SetBool(isMovingParam, moving);

        if (!moving) return;

        // PlayerMover2D랑 같은 플립 방식
        if (moveVec.x > 0.01f) facing = -0.7f;
        else if (moveVec.x < -0.01f) facing = 0.7f;

        Vector3 s = transform.localScale;
        s.x = facing;
        transform.localScale = s;

    }
}