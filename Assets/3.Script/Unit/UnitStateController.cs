using UnityEngine;
using UnityEngine.UIElements;

public class UnitStateController : MonoBehaviour
{
    public enum UnitState { Default, EnemyAlive, Corpse, AllyAlive }

    [Header("Refs")]
    [SerializeField] CombatAgent combat;
    [SerializeField] EnemyBrain enemyBrain;
    [SerializeField] AllyBrain allyBrain;

    [Header("Corpse UI (Unit 안에 있는 Canvas)")]
    [SerializeField] GameObject corpseUI;

    CapsuleCollider2D capsuleCollider;
    CircleCollider2D circleCollider;

    UnitState state;
    Transform player;
    Animator anim;
    Rigidbody2D rb;

    int enemyLayer;
    int allyLayer;
    int corpseLayer = 0;

    void Awake()
    {
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider2D>();
        if (circleCollider == null) circleCollider = GetComponent<CircleCollider2D>();
        if (anim == null) anim = GetComponentInChildren<Animator>(true);

        corpseUI.SetActive(false);
        circleCollider.enabled = false;

        player = GameObject.FindWithTag("Player")?.transform;

        enemyLayer = LayerMask.NameToLayer("Enemy");
        allyLayer = LayerMask.NameToLayer("Ally");


        if (TryGetComponent(out rb))
        {
            rb.gravityScale = 0f;
            rb.linearDamping = 5f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        enemyBrain.FirstBrainSet(combat, anim, rb);
        allyBrain.FirstBrainSet(combat, anim, rb);


        // 죽음 이벤트는 “시체 상태로 전환”만 담당
        combat.OnDead += OnDead;
    }

    private void OnEnable()
    {
        SpawnAsEnemy();
    }



    // ===== 외부에서 스폰할 때 호출 (풀 재사용 대비) =====

    public void SpawnAsEnemy()
    {
        state = UnitState.EnemyAlive;

        corpseUI.gameObject.SetActive(false);

        // 팀/레이어
        combat.enabled = true;
        combat.SetTeam(CombatAgent.Team.Enemy);
        SetLayerRecursively(gameObject, enemyLayer);

        // 브레인 전환
        if (allyBrain) allyBrain.enabled = false;
        if (enemyBrain)
        {
            enemyBrain.enabled = true;
            enemyBrain.EnemyBrainSet();
        }

        combat.ResetRuntime(fullHeal: true);

        anim.ResetControllerState(true);

        capsuleCollider.enabled = true;
        circleCollider.enabled = false;
    }

    public void SpawnAsAlly(Transform leader)
    {
        state = UnitState.AllyAlive;

        if (corpseUI != null) corpseUI.gameObject.SetActive(false);

        combat.enabled = true;
        combat.SetTeam(CombatAgent.Team.Ally);
        SetLayerRecursively(gameObject, allyLayer);

        if (enemyBrain) enemyBrain.enabled = false;
        if (allyBrain) allyBrain.enabled = true;

        combat.ResetRuntime(fullHeal: true);

        anim.ResetControllerState(true);

        capsuleCollider.enabled = true;
        circleCollider.enabled = false;
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
            InitializeForReuse();
            ObjectPool.Instance.Release(gameObject);
            return;
        }


        //corpse 상태로 전환 (즉시 풀 반환하지 않고, 플레이어가 상호작용할 때까지 대기)
        state = UnitState.Corpse;

        enemyBrain.enabled = false;
        allyBrain.enabled = false;
        combat.enabled = false;

        capsuleCollider.enabled = false;
        circleCollider.enabled = true;

        corpseUI.SetActive(true);
    }


    //Ally일때 사망하면 
    void HandleAllyDeath()
    {
        if (player.TryGetComponent<PlayerSquadController>(out var s))
        {
            s.Unregister(allyBrain);
        }

        InitializeForReuse();
        ObjectPool.Instance.Release(gameObject);
    }





    // ===== 버튼에서 호출 =====
    public void OnClickTame()
    {
        var squad = FindAnyObjectByType<PlayerSquadController>();
        Transform leader = squad != null ? squad.transform : player;

        // 아군으로 전환 + 스쿼드 등록
        SpawnAsAlly(leader);

        if (squad != null)
        {
            var ab = GetComponent<AllyBrain>();
            if (ab != null) squad.OnTame(ab);
        }


    }

    public void OnClickSalvage()
    {
        // TODO: 골드 지급
        // Economy.Instance.AddGold(...);

        // 풀 반환(혹은 비활성)
        InitializeForReuse();
        ObjectPool.Instance.Release(gameObject);
    }





    void SetLayerRecursively(GameObject go, int layer)
    {
        if (layer < 0) return
                ;
        go.layer = layer;

        foreach (Transform c in go.transform)
        {
            SetLayerRecursively(c.gameObject, layer);
        }
    }


    void InitializeForReuse()
    {
        state = UnitState.Default;

        corpseUI.SetActive(false);

        combat.enabled = true;
        combat.ResetRuntime(fullHeal: true);

        enemyBrain.enabled = false;
        allyBrain.enabled = false;

        capsuleCollider.enabled = true;
        circleCollider.enabled = false;
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

        if (!collision.CompareTag("Player")) return;

        corpseUI.SetActive(false);
    }
}