using DG.Tweening;
using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;


public class UnitStateController : MonoBehaviour
{
    [Header("Core Refs")]
    [SerializeField] CombatAgent combat;
    [SerializeField] EnemyBrain enemyBrain;
    [SerializeField] AllyBrain allyBrain;
    [SerializeField] UnitVisualFeedback visualFeedback;

    [Header("Render Refs")]
    [SerializeField] SpriteRenderer bodyRenderer;
    [SerializeField] SpriteRenderer backArmRenderer;
    [SerializeField] SpriteRenderer frontArmRenderer;
    [SerializeField] SpriteRenderer hatRenderer;

    [Header("Enemy Sprites")]
    [SerializeField] Sprite enemyBodySprite;
    [SerializeField] Sprite enemyHatSprite;

    [Header("Ally Sprites")]
    [SerializeField] Sprite allyBodySprite;
    [SerializeField] Sprite allyHatSprite;

    [Header("Corpse UI (Canvas under Unit)")]
    [SerializeField] GameObject corpseUI;
    [SerializeField] Button recruitBtn;

    UnitType unitType;
    public UnitType UnitType => unitType;

    [Header("Unit Data")]
    [SerializeField] UnitKey unitKey;
    public UnitKey UnitKey => unitKey;

    UnitState state = UnitState.Default;

    CapsuleCollider2D capsuleCollider;
    CircleCollider2D circleCollider;


    PlayerSquadController playerSquad;
    Animator anim;
    Rigidbody2D rb;

    int enemyLayer;
    int allyLayer;

    public event Action OnRequestFadeOutAndRelease;


    void Awake()
    {
        enemyLayer = LayerMask.NameToLayer("Enemy");
        allyLayer = LayerMask.NameToLayer("Ally");

        if (combat == null) combat = GetComponent<CombatAgent>();
        if (enemyBrain == null) enemyBrain = GetComponent<EnemyBrain>();
        if (allyBrain == null) allyBrain = GetComponent<AllyBrain>();
        if (visualFeedback == null) visualFeedback = GetComponent<UnitVisualFeedback>();

        TryGetComponent(out capsuleCollider);
        TryGetComponent(out circleCollider);
        TryGetComponent(out rb);

        anim = GetComponentInChildren<Animator>(true);

        if (rb != null)
        {
            rb.mass = 10f;
            rb.gravityScale = 0f;
            rb.linearDamping = 10f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        // BrainBase 공통 초기 바인딩
        enemyBrain.FirstBrainSet(combat, anim, rb);
        allyBrain.FirstBrainSet(combat, anim, rb);

        // CombatAgent는 "죽었다" 이벤트만 발행.
        // 상태 전환 책임은 UnitStateController가 가진다.
        combat.OnDead += OnDead;
    }

    void OnDestroy()
    {
        combat.OnDead -= OnDead;
    }

    /// <summary>
    /// 유닛을 기본 상태로 되돌린다.
    /// 주의:
    /// 여기서 바로 ObjectPool.Release를 하면 안 된다.
    /// 페이드 연출이 끝난 뒤 ReleaseToPoolExternally()에서 반납해야 한다.
    /// </summary>
    void InitializeUnit()
    {
        state = UnitState.Default;

        corpseUI.SetActive(false);

        combat.enabled = true;
        combat.ResetRuntime(fullHeal: true);


        enemyBrain.enabled = false;
        allyBrain.enabled = false;

        capsuleCollider.enabled = true;
        circleCollider.enabled = false;

        // 혹시 플래시/페이드 중간 상태에서 재사용될 수 있으므로
        // 알파, 위치, 스케일, 색을 원복한다.
        visualFeedback?.CancelAllEffectsAndRestore();
    }

    /// <summary>
    /// UnitVisualFeedback가 페이드 완료 후 호출하는 외부 반납 함수.
    /// 상태 초기화 후 풀에 되돌린다.
    /// </summary>
    public void ReleaseToPoolExternally()
    {
        InitializeUnit();
        ObjectPool.Instance.Release(gameObject);
    }

    /// <summary>
    /// 팀에 따라 스프라이트/팔 색상 세팅.
    /// Ally는 팔을 검정색으로 유지한다.
    /// Enemy는 팔을 흰색으로 유지한다.
    /// </summary>
    void SetRender(Team team)
    {
        if (team == Team.Ally)
        {
            if (bodyRenderer != null) bodyRenderer.sprite = allyBodySprite;
            if (hatRenderer != null) hatRenderer.sprite = allyHatSprite;

            if (backArmRenderer != null) backArmRenderer.color = Color.black;
            if (frontArmRenderer != null) frontArmRenderer.color = Color.black;
            return;
        }

        if (bodyRenderer != null) bodyRenderer.sprite = enemyBodySprite;
        if (hatRenderer != null) hatRenderer.sprite = enemyHatSprite;

        if (backArmRenderer != null) backArmRenderer.color = Color.white;
        if (frontArmRenderer != null) frontArmRenderer.color = Color.white;
    }

    /// <summary>
    /// 적 상태로 스폰/재초기화.
    /// 이 시점에 visualFeedback의 기준 색상도 다시 저장해야
    /// 피격 플래시 후 팔 색이 꼬이지 않는다.
    /// </summary>
    public void SpawnAsEnemy()
    {
        state = UnitState.EnemyAlive;

        corpseUI.SetActive(false);

        combat.enabled = true;
        combat.SetTeam(Team.Enemy);

        SetRender(Team.Enemy);

        gameObject.layer = enemyLayer;
        allyBrain.enabled = false;
        enemyBrain.enabled = true;
        enemyBrain.EnemyBrainSet();

        combat.DataSetup(unitKey);
        combat.ResetRuntime(fullHeal: true);

        anim.ResetControllerState(true);

        capsuleCollider.enabled = true;
        circleCollider.enabled = false;

        // 이전 플래시/페이드 잔재 제거
        visualFeedback?.CancelAllEffectsAndRestore();

        // 현재 렌더 상태(적 팔 흰색 포함)를 새로운 기준값으로 저장
        visualFeedback?.RefreshBaseColors();
    }

    /// <summary>
    /// 아군 상태로 전환.
    /// Ally 팔 검정색이 피격 후에도 유지되도록
    /// 마지막에 RefreshBaseColors()를 반드시 호출한다.
    /// </summary>
    public void SpawnAsAlly(bool startSpawn = false)
    {
        state = UnitState.AllyAlive;
        gameObject.layer = allyLayer;

        corpseUI.SetActive(false);

        combat.enabled = true;
        combat.SetTeam(Team.Ally);

        SetRender(Team.Ally);

        enemyBrain.enabled = false;
        allyBrain.enabled = true;

        combat.DataSetup(unitKey);
        combat.ResetRuntime(fullHeal: true);
        anim.ResetControllerState(true);

        capsuleCollider.enabled = true;
        circleCollider.enabled = false;

        anim.SetTrigger("IsTaming");

        playerSquad ??= GameManager.Instance.playerSquad;
        playerSquad.Register(allyBrain);

        if (startSpawn) return;

        GameManager.Instance.UnlockUnit(unitKey);

        // 이전 효과 취소 후, 현재 아군 시각 상태를 기준값으로 재저장
        visualFeedback?.CancelAllEffectsAndRestore();
        visualFeedback?.RefreshBaseColors();

        SoundManager.Instance.PlaySFX(SFXType.Recruit);
    }

    /// <summary>
    /// CombatAgent 사망 이벤트 수신.
    /// 현재 상태에 따라 죽음 처리 분기.
    /// </summary>
    void OnDead(CombatAgent dead)
    {
        switch (state)
        {
            case UnitState.EnemyAlive:
                HandleEnemyDeath();
                break;

            case UnitState.AllyAlive:
                HandleAllyDeath();
                break;
        }
    }

    /// <summary>
    /// 적 사망 처리.
    /// - 테이밍 실패: 페이드 후 풀 반납
    /// - 테이밍 성공: Corpse 상태 유지, UI 상호작용 대기
    /// </summary>
    void HandleEnemyDeath()
    {
        bool tameSuccess = UnityEngine.Random.value <= combat.TameChance;

        enemyBrain.enabled = false;
        allyBrain.enabled = false;
        combat.enabled = false;

        capsuleCollider.enabled = false;

        // 죽는 모션 방향 고정
        Vector3 s = transform.localScale;
        s.x = 0.7f;
        transform.localScale = s;

        // 공격 트리거 제거 후 죽는 애니
        anim.ResetTrigger("IsAttack");
        anim.ResetTrigger("IsTaming");
        anim.SetTrigger("IsDead");

        if (!tameSuccess)
        {
            corpseUI.SetActive(false);
            DOVirtual.DelayedCall(0.5f, () =>
            {
                OnRequestFadeOutAndRelease?.Invoke();
            });
            return;
        }



        state = UnitState.Corpse;
        circleCollider.enabled = true;
    }

    /// <summary>
    /// 아군 사망 처리.
    /// 스쿼드에서 제거 후 페이드 아웃 요청.
    /// </summary>
    void HandleAllyDeath()
    {
        playerSquad.Unregister(allyBrain);
        corpseUI.SetActive(false);

        OnRequestFadeOutAndRelease?.Invoke();

        // 죽는 모션 방향 고정
        Vector3 s = transform.localScale;
        s.x = 0.7f;
        transform.localScale = s;

        // 공격 트리거 제거 후 죽는 애니
        anim.ResetTrigger("IsAttack");
        anim.ResetTrigger("IsTaming");
        anim.SetTrigger("IsDead");
    }

    /// <summary>
    /// Corpse UI에서 "테이밍" 버튼 눌렀을 때 호출.
    /// 버튼이 남지 않도록 먼저 UI를 끄고 아군으로 전환한다.
    /// </summary>
    public void OnClickTame()
    {
        if (!UpdateRecruitButton())
            return;

        corpseUI.SetActive(false);
        SpawnAsAlly();
    }

    /// <summary>
    /// Corpse UI에서 "회수/살베지" 버튼 눌렀을 때 호출.
    /// 버튼만 남는 문제 방지를 위해 먼저 UI를 끈 뒤 페이드 요청.
    /// </summary>
    public void OnClickSalvage()
    {
        corpseUI.SetActive(false);

        OnRequestFadeOutAndRelease?.Invoke();

        GameManager.Instance.AddCoin(1);
        SoundManager.Instance.PlaySFX(SFXType.GetCoin);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (state != UnitState.Corpse) return;
        if (!collision.CompareTag("Player")) return;

        UpdateRecruitButton();
        corpseUI.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (state != UnitState.Corpse) return;
        if (!collision.CompareTag("Player")) return;

        UpdateRecruitButton();
        corpseUI.SetActive(false);
    }

    bool UpdateRecruitButton()
    {
        recruitBtn.interactable = !GameManager.Instance.playerSquad.IsFull;
        return recruitBtn.interactable;
    }
}