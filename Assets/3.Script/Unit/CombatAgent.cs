using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class CombatAgent : MonoBehaviour
{
    [HideInInspector] public Team team = Team.Enemy;

    [Header("플레이어 제외 Bar")]
    [SerializeField] Image hpBarGauge;
    [SerializeField] GameObject hpBar;
    Tween hideTween;

    float maxHP = 20f;
    float damage = 5f;
    float attackRange = 1.0f;
    float attackCooldown = 0.8f;
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
    [SerializeField] Transform startPoint;


    public float HP { get; private set; }
    public bool IsDead => HP <= 0f;
    public float AttackRange => attackRange;

    public event Action<CombatAgent, float> OnDamaged;
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


    public void DataSetup(UnitKey unitKey)
    {
        UnitDataSO data = GameManager.Instance.GetUnitData(unitKey);

        maxHP = data.maxHP;
        damage = data.attackDamage;
        attackCooldown = data.attackCooldown;
        attackRange = data.attackRange;
        TameChance = data.tameChance;
    }

    public void PlayerDataSet()
    {
        maxHP = 200;
        damage = 10;
        attackCooldown = 1;
        attackRange = 3;
        TameChance = 1;

        ResetRuntime(true);
    }


    public void SetTeam(Team newTeam) => team = newTeam;

    public void ResetRuntime(bool fullHeal = true)
    {
        pendingTarget = null;
        nextAttackTime = 0f;

        if (fullHeal) HP = maxHP;

        if (team.Equals(Team.Player))
        {
            UIManager.Instance.SetHpBar(HP, maxHP);
            return;
        }

        switch (team)
        {
            case Team.Player:
                UIManager.Instance.SetHpBar(HP, maxHP);
                return;

            case Team.Ally:
                hpBarGauge.color = Color.cyan;
                break;

            case Team.Enemy:
                hpBarGauge.color = Color.red;
                break;
        }

        hpBar.SetActive(false);
    }

    public void ShowHPBar()
    {
        if (team.Equals(Team.Player))
        {
            UIManager.Instance.SetHpBar(HP, maxHP);
            return;
        }

        hideTween?.Kill();
        hpBar.SetActive(true);
        hpBarGauge.fillAmount = HP / maxHP;

        hideTween = DOVirtual.DelayedCall(0.5f, () =>
        {
            hpBar.SetActive(false);
        });
    }



    public void TakeDamage(float amount)
    {
        if (IsDead) return;

        HP -= amount;

        ShowHPBar();

        OnDamaged?.Invoke(this, amount);

        if (HP <= 0f)
        {
            HP = 0f;
            pendingTarget = null;

            hideTween?.Kill();

            OnDead?.Invoke(this);

            if (team.Equals(Team.Player))
            {
                UIManager.Instance.SetHpBar(HP, maxHP);
                UIManager.Instance.ShowGameOverUI();
                anim.SetTrigger("IsDead");
                return;
            }

            hpBar.SetActive(false);
            return;
        }

        if (team.Equals(Team.Player)) UIManager.Instance.SetHpBar(HP, maxHP);
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

        if (!target.TryGetComponent(out CombatAgent other) || other.IsDead)
            return false;
        if (other.team == team)
            return false;
        if (!IsInRange(target)) { pendingTarget = null; return false; }

        if (Time.time < nextAttackTime) return false;
        nextAttackTime = Time.time + attackCooldown;

        pendingTarget = target;

        anim.SetTrigger(attackTriggerName);

        return true;
    }

    // 근접공격이면 attack 애니 중간에 AnimationEvent로 이거 호출
    public void OnAttackHitEvent()
    {
        if (IsDead) return;
        if (!pendingTarget) { pendingTarget = null; return; }

        if (!pendingTarget.TryGetComponent(out CombatAgent other) || other.IsDead)
        {
            pendingTarget = null;
            return;
        }

        if (other.team == team)
        {
            pendingTarget = null;
            return;
        }

        // 원거리면 투사체 발사
        if (projectilePrefab != null)
        {
            if (startPoint == null)
            {
                Debug.LogWarning($"{name} : projectile startPoint is null");
                pendingTarget = null;
                return;
            }

            StartCoroutine(ArcProjectileRoutine(startPoint.position, pendingTarget));
            return;
        }

        // 근접이면 이벤트 시점에 데미지 적용
        if (!IsInRange(pendingTarget))
        {
            anim.ResetTrigger(attackTriggerName); 
            pendingTarget = null;
            return;
        }

        other.TakeDamage(damage);
        pendingTarget = null;
    }

    IEnumerator ArcProjectileRoutine(Vector3 start, Transform target)
    {
        if (!target) yield break;

        GameObject proj = ObjectPool.Instance.Get(projectilePrefab, start, Quaternion.identity);
        float t = 0f;
        float dur = Mathf.Max(0.01f, projectileFlightTime);

        Vector3 prevPos = start;

        while (t < 1f)
        {
            if (!target) break;

            t += Time.deltaTime / dur;

            Vector3 end = target.position + Vector3.up * 0.75f;
            Vector3 pos = Vector3.Lerp(start, end, t);
            float arc = 4f * projectileArcHeight * t * (1f - t);
            pos.y += arc;

            Vector3 dir = pos - prevPos;

            if (dir.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                proj.transform.rotation = Quaternion.Euler(0f, 0f, angle + 270f);
            }

            proj.transform.position = pos;
            prevPos = pos;

            yield return null;
        }

        if (target)
        {
            var other = target.GetComponentInParent<CombatAgent>();
            if (other != null && !other.IsDead && other.team != team)
                other.TakeDamage(damage);
        }

        ObjectPool.Instance.Release(proj);
    }
}