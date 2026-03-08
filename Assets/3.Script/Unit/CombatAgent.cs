using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum SkillType
{
    None,
    MagicRain,
    Shockwave
}

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
    readonly string attackTriggerName = "IsAttack";

    [Header("Projectile (optional)")]
    [SerializeField] GameObject projectilePrefab;
    [SerializeField] float projectileFlightTime = 0.25f;
    [SerializeField] float projectileArcHeight = 0.4f;
    [SerializeField] Transform startPoint;

    [Header("Skill")]
    [SerializeField] SkillType skillType = SkillType.None;
    [SerializeField] float skillCooldown = 10f;
    [SerializeField] float skillCastRange = 3f;
    readonly string skillTriggerName = "IsSkill";

    float nextSkillTime;
    Transform pendingSkillTarget;
    public bool IsSkillCasting { get; private set; }

    [Header("Magic Rain")]
    [SerializeField] float magicRainRadius = 2.5f;
    [SerializeField] int magicRainMaxTargets = 5;
    [SerializeField] float magicRainDelay = 0.6f;
    [SerializeField] float magicRainDamage = 8f;

    [Header("Magic Rain Visual")]
    [SerializeField] GameObject magicRainProjectilePrefab;
    [SerializeField] float magicRainFallHeight = 4f;
    [SerializeField] float magicRainFlightTime = 0.35f;


    [Header("Shockwave")]
    [SerializeField] float shockwaveRadius = 2.2f;
    [SerializeField] float shockwaveDelay = 0.35f;
    [SerializeField] float shockwaveDamage = 15f;
    [SerializeField] GameObject shockwaveFxPrefab;

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
        maxHP = 400;
        damage = 20;
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

        pendingSkillTarget = null;
        nextSkillTime = 0f;

        IsSkillCasting = false;

        anim.SetBool(skillTriggerName, false);

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
            pendingSkillTarget = null;

            IsSkillCasting = false;

            anim.SetBool(skillTriggerName, false);

            hideTween?.Kill();

            OnDead?.Invoke(this);
            SoundManager.Instance.PlaySFX(SFXType.Dead);

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

        if (team.Equals(Team.Player))
        {
            UIManager.Instance.SetHpBar(HP, maxHP);
            SoundManager.Instance.PlaySFX(SFXType.PlayerHit);
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
        if (IsSkillCasting) return false;
        if (!target) { pendingTarget = null; return false; }

        if (!target.TryGetComponent(out CombatAgent other) || other.IsDead)
            return false;
        if (IsFriendly(other.team))
            return false;
        if (!IsInRange(target)) { pendingTarget = null; return false; }

        if (Time.time < nextAttackTime) return false;
        nextAttackTime = Time.time + attackCooldown;

        pendingTarget = target;

        anim.SetTrigger(attackTriggerName);

        return true;
    }

  

    public void OnAttackHitEvent()
    {
        if (IsDead) return;
        if (!pendingTarget) { pendingTarget = null; return; }

        if (!pendingTarget.TryGetComponent(out CombatAgent other) || other.IsDead)
        {
            pendingTarget = null;
            return;
        }

        if (IsFriendly(other.team))
        {
            pendingTarget = null;
            return;
        }

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

        if (!IsInRange(pendingTarget))
        {
            anim.ResetTrigger(attackTriggerName);
            pendingTarget = null;
            return;
        }

        other.TakeDamage(damage);
        pendingTarget = null;

        if (team.Equals(Team.Player))
            SoundManager.Instance.PlaySFX(SFXType.PlayerAttack);
    }

    #region Skills

    public bool TryUseSkill(Transform target)
    {
        if (skillType == SkillType.None) return false;
        if (IsDead) return false;
        if (IsSkillCasting) return false;
        if (!target) { pendingSkillTarget = null; return false; }

        if (!target.TryGetComponent(out CombatAgent other) || other.IsDead)
            return false;

        if (IsFriendly(other.team))
            return false;

        if (!IsSkillInRange(target))
        {
            pendingSkillTarget = null;
            return false;
        }

        if (Time.time < nextSkillTime)
            return false;

        nextSkillTime = Time.time + skillCooldown;
        pendingSkillTarget = target;

        BeginSkillCast();

        switch (skillType)
        {
            case SkillType.MagicRain:
                StartCoroutine(CoMagicRain(target));
                return true;

            case SkillType.Shockwave:
                StartCoroutine(CoShockwave());
                return true;
        }

        EndSkillCast();
        return false;
    }
    void BeginSkillCast()
    {
        IsSkillCasting = true;

        if (anim != null)
            anim.SetBool(skillTriggerName, true);
    }

    void EndSkillCast()
    {
        IsSkillCasting = false;
        pendingSkillTarget = null;

        if (anim != null)
            anim.SetBool(skillTriggerName, false);
    }
    public bool IsSkillInRange(Transform target)
    {
        if (!target) return false;
        float r = skillCastRange;
        return ((Vector2)target.position - (Vector2)transform.position).sqrMagnitude <= r * r;
    }
    IEnumerator CoMagicRain(Transform target)
    {
        if (target == null)
        {
            EndSkillCast();
            yield break;
        }

        Vector2 center = target.position;
        float radiusSqr = magicRainRadius * magicRainRadius;

        List<CombatAgent> hitTargets = new List<CombatAgent>(magicRainMaxTargets);
        var all = All;

        for (int i = 0; i < all.Count; i++)
        {
            CombatAgent other = all[i];
            if (other == null || other == this || other.IsDead) continue;
            if (other.team == team) continue;

            Vector2 diff = (Vector2)other.transform.position - center;
            if (diff.sqrMagnitude > radiusSqr) continue;

            hitTargets.Add(other);
        }

        hitTargets.Sort((a, b) =>
        {
            float da = ((Vector2)a.transform.position - center).sqrMagnitude;
            float db = ((Vector2)b.transform.position - center).sqrMagnitude;
            return da.CompareTo(db);
        });

        int count = Mathf.Min(magicRainMaxTargets, hitTargets.Count);

        yield return new WaitForSeconds(magicRainDelay);

        if (IsDead)
        {
            EndSkillCast();
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            CombatAgent victim = hitTargets[i];
            if (victim == null || victim.IsDead) continue;
            if (IsFriendly(victim.team)) continue;

            StartCoroutine(CoMagicRainHit(victim));
        }

        EndSkillCast();
    }

    IEnumerator CoMagicRainHit(CombatAgent victim)
    {
        if (victim == null || victim.IsDead) yield break;

        Vector3 hitPos = victim.transform.position;
        Vector3 startPos = hitPos + Vector3.up * magicRainFallHeight;

        GameObject proj = null;

        if (magicRainProjectilePrefab != null)
        {
            proj = ObjectPool.Instance.Get(magicRainProjectilePrefab, startPos, Quaternion.identity);
        }

        float t = 0f;
        float dur = Mathf.Max(0.01f, magicRainFlightTime);

        while (t < 1f)
        {
            if (victim == null || victim.IsDead) break;

            t += Time.deltaTime / dur;

            Vector3 endPos = victim.transform.position;
            Vector3 pos = Vector3.Lerp(startPos, endPos, t);

            if (proj != null)
                proj.transform.position = pos;

            yield return null;
        }

        if (victim != null && !victim.IsDead && victim.team != team)
        {
            victim.TakeDamage(magicRainDamage);
        }

        if (proj != null)
            ObjectPool.Instance.Release(proj);
    }

    IEnumerator CoShockwave()
    {
        yield return new WaitForSeconds(shockwaveDelay);

        if (IsDead)
        {
            EndSkillCast();
            yield break;
        }

        if (shockwaveFxPrefab != null)
        {
            GameObject fx = ObjectPool.Instance.Get(shockwaveFxPrefab, transform.position, Quaternion.identity);
            StartCoroutine(CoReleaseFx(fx, 1f));
        }

        Vector2 center = transform.position;
        float radiusSqr = shockwaveRadius * shockwaveRadius;

        var all = All;
        for (int i = 0; i < all.Count; i++)
        {
            CombatAgent other = all[i];
            if (other == null || other == this || other.IsDead) continue;
            if (IsFriendly(other.team)) continue;

            Vector2 diff = (Vector2)other.transform.position - center;
            if (diff.sqrMagnitude > radiusSqr) continue;

            other.TakeDamage(shockwaveDamage);
        }

        EndSkillCast();
    }

    IEnumerator CoReleaseFx(GameObject fx, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (fx != null)
            ObjectPool.Instance.Release(fx);
    }

#endregion

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


    bool IsFriendly(Team otherTeam)
    {
        if (team == Team.Enemy)
            return otherTeam == Team.Enemy;

        // Player, Ally는 같은 편 취급
        return otherTeam == Team.Player || otherTeam == Team.Ally;
    }



    public void Heal(float amount)
    {
        if (IsDead) return;
        if (amount <= 0f) return;

        HP = Mathf.Min(HP + amount, maxHP);

        if (team == Team.Player)
        {
            UIManager.Instance?.SetHpBar(HP, maxHP);
        }
        else
        {
            ShowHPBar();
        }
    }

    public void FullHeal()
    {
        if (IsDead) return;

        HP = maxHP;

        if (team == Team.Player)
        {
            UIManager.Instance?.SetHpBar(HP, maxHP);
        }
        else
        {
            ShowHPBar();
        }
    }
}