using UnityEngine;
using UnityEngine.UIElements.Experimental;

public class UnitStateController : MonoBehaviour, ObjectPool.IPoolable
{
    public enum UnitState { EnemyAlive, Corpse, AllyAlive }

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


        if (TryGetComponent(out Rigidbody2D rb))
        {
            rb.gravityScale = 0f;
            //rb.mass = 1f;
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
        if (state == UnitState.Corpse) return;

        bool tameSuccess = Random.value <= combat.TameChance;

        if (!tameSuccess)
        {
            // 돈 지급
            // Economy.Instance.AddGold(...);

            ObjectPool.Instance.Release(gameObject);
            return;
        }

        // 테이밍 성공 => 시체(기절) 상태로 남김 + UI 표시
        state = UnitState.Corpse;

        if (enemyBrain) enemyBrain.enabled = false;
        if (allyBrain) allyBrain.enabled = false;

        //anim.SetBool("IsDead", true);
        anim.SetTrigger("IsDead");

        Vector3 s = transform.localScale;
        s.x = 0.7f;
        transform.localScale = s;
        combat.enabled = false;

        capsuleCollider.enabled = false;
        circleCollider.enabled = true;

        SetLayerRecursively(gameObject, corpseLayer);

        // UI는 Update 거리 체크로 켜짐(지금 방식 유지)
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
        ObjectPool.Instance.Release(gameObject);
    }

    static void SetLayerRecursively(GameObject go, int layer)
    {
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform c in go.transform)
            SetLayerRecursively(c.gameObject, layer);
    }

    // ===== 풀 콜백 =====
    public void OnSpawned()
    {
        // 스폰러가 SpawnAsEnemy/SpawnAsAlly 중 하나를 호출하는 게 정석.
        // 실수 방지로 기본은 Enemy로 둬도 됨:
        // SpawnAsEnemy();
    }

    public void OnDespawned()
    {
        if (corpseUI != null) corpseUI.gameObject.SetActive(false);
        combat.enabled = true; // 다음 재사용 대비
        if (enemyBrain) enemyBrain.enabled = false;
        if (allyBrain) allyBrain.enabled = false;
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