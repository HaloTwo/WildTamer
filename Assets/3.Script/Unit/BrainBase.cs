using UnityEngine;

public abstract class BrainBase : MonoBehaviour
{
    protected CombatAgent combat;
    protected Rigidbody2D rb;
    protected Animator anim;

    protected Transform currentTarget;
    protected float facing = 1f;

    [Header("Move")]
    [SerializeField] protected float moveSpeed = 3.25f;

    [Header("Scan")]
    [SerializeField] protected LayerMask targetMask;       // Player + Ally ·¹ÀÌ¾î
    [SerializeField] protected float scanInterval = 0.2f;
    [SerializeField] protected float detectRadius = 3.5f;

    protected float nextScanTime;
    protected readonly string isMovingParam = "IsMoving";

    public void FirstBrainSet(CombatAgent combatAgent, Animator animator, Rigidbody2D rigidbody2D)
    {
        combat = combatAgent;
        anim = animator;
        rb = rigidbody2D;
    }

    protected bool IsValidTarget(Transform target)
    {
        if (target == null) return false;

        CombatAgent other = target.GetComponentInParent<CombatAgent>();
        if (other == null) return false;
        if (other.IsDead) return false;
        if (other.team == combat.team) return false;

        return true;
    }

    protected void Move(Vector2 dir, float speed)
    {
        if (dir.sqrMagnitude < 0.0001f)
        {
            rb.linearVelocity = Vector2.zero;
            if (anim != null) anim.SetBool(isMovingParam, false);
            return;
        }

        rb.linearVelocity = dir.normalized * speed;

        anim.SetBool(isMovingParam, true);
        FaceByMove(dir);
    }

    protected void StopMove()
    {
        rb.linearVelocity = Vector2.zero;
        if (anim != null) anim.SetBool(isMovingParam, false);
    }

    protected void FaceByMove(Vector2 moveVec)
    {
        if (moveVec.x > 0.01f) facing = -0.7f;
        else if (moveVec.x < -0.01f) facing = 0.7f;
        else return;

        Vector3 s = transform.localScale;
        s.x = facing;
        transform.localScale = s;
    }

    protected void FaceTo(Vector3 targetPos)
    {
        float dx = targetPos.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.01f) return;

        Vector3 s = transform.localScale;
        s.x = dx > 0f ? -0.7f : 0.7f;
        transform.localScale = s;
        facing = s.x;
    }
}