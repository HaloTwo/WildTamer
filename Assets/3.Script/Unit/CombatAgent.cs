using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatAgent : MonoBehaviour
{
    public enum Team { Player, Ally, Enemy }

    [Header("Team")]
    public Team team = Team.Enemy;

    [Header("Stats")]
    [SerializeField] float maxHP = 20f;
    [SerializeField] float damage = 5f;
    [SerializeField] float attackRange = 1.0f;    
    [SerializeField] float attackCooldown = 0.8f;
    public float TameChance = 0.6f;

    float nextAttackTime;
    Transform pendingTarget;

    [Header("Animation")]
    [SerializeField] Animator anim;
    readonly string attackTriggerName = "IsAttack"; // Trigger

    [Header("Projectile (optional)")]
    [SerializeField] GameObject projectilePrefab;
    [SerializeField] float projectileFlightTime = 0.25f;
    [SerializeField] float projectileArcHeight = 0.4f;

    public float HP { get; private set; }
    public bool IsDead => HP <= 0f;
    public float AttackRange => attackRange;

    public event Action<CombatAgent> OnDead;

    static readonly List<CombatAgent> s_all = new();
    public static IReadOnlyList<CombatAgent> All => s_all;


    void OnEnable()
    {
        if (!s_all.Contains(this)) s_all.Add(this);
    }

    void OnDisable()
    {
        s_all.Remove(this);
    }

    void OnDestroy()
    {
        s_all.Remove(this);
    }
    // ---------------------------------------------------------------------------------

    void Awake()
    {
        HP = maxHP;
        if (anim == null) anim = GetComponentInChildren<Animator>(true);
    }

    public void SetTeam(Team newTeam) => team = newTeam;

    public void ResetRuntime(bool fullHeal = true)
    {
        pendingTarget = null;
        nextAttackTime = 0f;
        if (fullHeal) HP = maxHP;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        HP -= amount;
        if (HP <= 0f)
        {
            HP = 0f;
            pendingTarget = null;
            OnDead?.Invoke(this);
        }
    }

    public bool IsInRange(Transform target)
    {
        if (!target) return false;
        float r = attackRange;
        return ((Vector2)target.position - (Vector2)transform.position).sqrMagnitude <= r * r;
    }

    public bool TryAttack(Transform target)
    {
        if (IsDead) return false;
        if (!target) { pendingTarget = null; return false; }

        var other = target.GetComponentInParent<CombatAgent>();
        if (other == null || other.IsDead) return false;
        if (other.team == team) return false;
        if (!IsInRange(target)) { pendingTarget = null; return false; }

        if (Time.time < nextAttackTime) return false;
        nextAttackTime = Time.time + attackCooldown;

        pendingTarget = target;

        anim.SetTrigger(attackTriggerName);

        if (projectilePrefab != null)
        {
            StartCoroutine(ArcProjectileRoutine(transform.position, target));
        }

        return true;
    }

    // 근접공격이면 attack 애니 중간에 AnimationEvent로 이거 호출
    public void ApplyDamageEvent()
    {
        if (IsDead) return;
        if (!pendingTarget) { pendingTarget = null; return; }
        if (!IsInRange(pendingTarget)) { pendingTarget = null; return; }

        var other = pendingTarget.GetComponentInParent<CombatAgent>();
        if (other == null || other.IsDead || other.team == team) { pendingTarget = null; return; }

        other.TakeDamage(damage);
        pendingTarget = null;
    }

    IEnumerator ArcProjectileRoutine(Vector3 start, Transform target)
    {
        if (!target) yield break;

        GameObject proj = Instantiate(projectilePrefab, start, Quaternion.identity);

        float t = 0f;
        float dur = Mathf.Max(0.01f, projectileFlightTime);

        while (t < 1f)
        {
            if (!target) break;

            t += Time.deltaTime / dur;

            Vector3 end = target.position;
            Vector3 pos = Vector3.Lerp(start, end, t);
            float arc = 4f * projectileArcHeight * t * (1f - t);
            pos.y += arc;

            proj.transform.position = pos;
            yield return null;
        }

        if (target)
        {
            var other = target.GetComponentInParent<CombatAgent>();
            if (other != null && !other.IsDead && other.team != team)
                other.TakeDamage(damage);
        }

        Destroy(proj);
    }
}