using UnityEngine;

public enum UnitState
{
    Default,
    EnemyAlive,
    Corpse,
    AllyAlive
}

public enum UnitType
{
    Default,
    Elite,
    Boss
}
public enum UnitKey
{
    None = 0,

    Merchant,
    Peasant,
    Thief,
    Priest,
    Knight,
}

public class UnitStateController : MonoBehaviour
{

    [Header("Refs")]
    [SerializeField] CombatAgent combat;
    [SerializeField] EnemyBrain enemyBrain;
    [SerializeField] AllyBrain allyBrain;

    [Header("랜더링 Refs")]
    [SerializeField] SpriteRenderer bodyRenderer;
    [SerializeField] SpriteRenderer backArmRenderer;
    [SerializeField] SpriteRenderer frontArmRenderer;
    [SerializeField] SpriteRenderer hatRenderer;

    [Header("적 스프라이트")]
    [SerializeField] Sprite enemybodyRenderer;
    [SerializeField] Sprite enemyHatRenderer;

    [Header("아군 스프라이트")]
    [SerializeField] Sprite allybodyRenderer;
    [SerializeField] Sprite allyHatRenderer;

    [Header("Corpse UI (Unit 안에 있는 Canvas)")]
    [SerializeField] GameObject corpseUI;

    CapsuleCollider2D capsuleCollider;
    CircleCollider2D circleCollider;

    UnitState state;
    [SerializeField]UnitType unitType;
    [SerializeField]UnitKey unitKey;


    PlayerSquadController playerSquad;
    Animator anim;
    Rigidbody2D rb;

    int enemyLayer;
    int allyLayer;

    void Awake()
    {
        enemyLayer = LayerMask.NameToLayer("Enemy");
        allyLayer = LayerMask.NameToLayer("Ally");

        TryGetComponent(out capsuleCollider);
        TryGetComponent(out circleCollider);
        TryGetComponent(out rb);
        anim = GetComponentInChildren<Animator>(true);

        rb.mass = 10f;
        rb.gravityScale = 0f;
        rb.linearDamping = 10f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        enemyBrain.FirstBrainSet(combat, anim, rb);
        allyBrain.FirstBrainSet(combat, anim, rb);

        // 죽음 이벤트는 “시체 상태로 전환”만 담당
        combat.OnDead += OnDead;
    }

    private void OnEnable()
    {
        SpawnAsEnemy();
    }

    private void OnDisable()
    {
        InitializeUnit();
    }

    //상태 초기화
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

        ObjectPool.Instance.Release(gameObject);
    }


    void SetRender(CombatAgent.Team team)
    {
        if (team == CombatAgent.Team.Ally)
        {
            bodyRenderer.sprite = allybodyRenderer;
            hatRenderer.sprite = allyHatRenderer;

            backArmRenderer.color = Color.black;
            frontArmRenderer.color = Color.black;
            return;
        }

        bodyRenderer.sprite = enemybodyRenderer;
        hatRenderer.sprite = enemyHatRenderer;

        backArmRenderer.color = Color.white;
        frontArmRenderer.color = Color.white;
    }


    public void SpawnAsEnemy()
    {
        state = UnitState.EnemyAlive;

        corpseUI.SetActive(false);

        // 팀/레이어
        combat.enabled = true;
        combat.SetTeam(CombatAgent.Team.Enemy);
        SetRender(CombatAgent.Team.Enemy);

        gameObject.layer = enemyLayer;

        // 브레인 전환
        allyBrain.enabled = false;
        enemyBrain.enabled = true;
        enemyBrain.EnemyBrainSet();

        combat.ResetRuntime(fullHeal: true);
        anim.ResetControllerState(true);

        capsuleCollider.enabled = true;
        circleCollider.enabled = false;
    }

    public void SpawnAsAlly()
    {
        state = UnitState.AllyAlive;
        gameObject.layer = allyLayer;

        corpseUI.SetActive(false);

        combat.enabled = true;
        combat.SetTeam(CombatAgent.Team.Ally);
        SetRender(CombatAgent.Team.Ally);

        enemyBrain.enabled = false;
        allyBrain.enabled = true;

        combat.ResetRuntime(fullHeal: true);
        anim.ResetControllerState(true);

        capsuleCollider.enabled = true;
        circleCollider.enabled = false;

        anim.SetTrigger("IsTaming");

        playerSquad ??= GamaManager.Instance.player.GetComponent<PlayerSquadController>();
        playerSquad.Register(allyBrain);
    }

    // ===== 죽었을 때: 시체 상태로 전환 =====
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

            default:
                break;
        }
    }

    //Enemy일때 사망하면
    void HandleEnemyDeath()
    {
        //확률적으로 아군으로 전환 시도, 실패하면 풀 반환
        bool tameSuccess = Random.value <= combat.TameChance;

        //실패하면 즉시 풀 반환
        if (!tameSuccess)
        {
            InitializeUnit();
            return;
        }

        //corpse 상태로 전환 (즉시 풀 반환하지 않고, 플레이어가 상호작용할 때까지 대기)
        state = UnitState.Corpse;

        enemyBrain.enabled = false;
        allyBrain.enabled = false;
        combat.enabled = false;

        capsuleCollider.enabled = false;
        circleCollider.enabled = true;

        //죽을땐 무조건 정방향으로 죽도록
        Vector3 s = transform.localScale;
        s.x = 0.7f;
        transform.localScale = s;

        //애니메이션은 무조건 죽는 애니메이션이 나오도록 (공격 애니메이션이 나와있을 수도 있으므로 초기화)
        anim.ResetTrigger("IsAttack");
        anim.SetTrigger("IsDead");
    }


    //Ally일때 사망하면 
    void HandleAllyDeath()
    {
        //스쿼드에서 등록 해제
        playerSquad.Unregister(allyBrain);

        //초기화   
        InitializeUnit();
    }



    // ===== 버튼에서 호출 =====
    public void OnClickTame()
    {
        // 아군으로 전환 + 스쿼드 등록
        SpawnAsAlly();
    }

    public void OnClickSalvage()
    {
        // TODO: 골드 지급
        // Economy.Instance.AddGold(...);

        // 초기화
        InitializeUnit();
    }










    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (state != UnitState.Corpse) return;

        // 플레이어만 반응
        if (!collision.CompareTag("Player")) return;

        corpseUI.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (state != UnitState.Corpse) return;

        // 플레이어만 반응
        if (!collision.CompareTag("Player")) return;

        corpseUI.SetActive(false);
    }
}