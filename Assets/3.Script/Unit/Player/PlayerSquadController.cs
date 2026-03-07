using System.Collections.Generic;
using UnityEngine;

public class PlayerSquadController : MonoBehaviour
{
    [SerializeField] int maxAllies = 20;

    public readonly List<AllyBrain> allies = new();
    public CombatAgent SharedTarget { get; private set; }

    [Header("Idle Ring (around player)")]
    [SerializeField] float idleBaseRadius = 1.2f;
    [SerializeField] float idleRingStep = 0.4f;
    [SerializeField] int idlePerRing = 8;

    [Header("Combat Ring (around target)")]
    [SerializeField] float combatRadiusMul = 0.85f;   // AttackRange * mul 위치에 서서 때리기
    [SerializeField] float combatSlotStepRad = 0.55f; // 슬롯 각도 간격(라디안). 작을수록 더 촘촘
    [SerializeField] int combatTrySlots = 12;         // 자리 없으면 회전하며 최대 시도 횟수
    [SerializeField] float combatOccupyRadius = 0.35f;// 이 반경 안에 다른 Ally 있으면 점유로 판단

    public bool Register(AllyBrain ally)
    {
        if (allies.Count >= maxAllies) return false;
        if (allies.Contains(ally)) return false;

        allies.Add(ally);

        ally.SetupAsAlly(allies.Count - 1);
        RefreshIndices();

        return true;
    }

    public void Unregister(AllyBrain ally)
    {
        if (ally == null) return;

        allies.Remove(ally);

        RefreshIndices();
    }


    void RefreshIndices()
    {
        for (int i = 0; i < allies.Count; i++)
        {
            if (allies[i] != null) allies[i].SetIndex(i);
        }

        UIManager.Instance.UnitCountTxtUpdate(allies.Count, maxAllies);
    }

    // -------------------- Idle Ring --------------------
    public Vector2 GetIdleRingWorldPos(int index)
    {
        int ring = index / Mathf.Max(1, idlePerRing);
        int inRing = index % Mathf.Max(1, idlePerRing);
        int perRing = Mathf.Max(1, idlePerRing);

        float angle = (inRing / (float)perRing) * Mathf.PI * 2f;
        float radius = idleBaseRadius + ring * idleRingStep;

        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        return (Vector2)transform.position + offset;
    }

    // -------------------- Combat Ring Slot --------------------
    // "빈 자리" 찾아서 반환. (OverlapCircle 금지 → squad.allies + 거리로 점유 판단)
    public Vector2 GetCombatSlotWorldPos(Transform target, int myIndex, float attackRange, Vector2 myPos)
    {
        if (target == null) return myPos;

        Vector2 center = target.position;

        float radius = Mathf.Max(0.05f, attackRange * combatRadiusMul);

        // golden angle 기반으로 퍼뜨리기 (같은 인덱스라도 겹침 덜함)
        float baseAngle = (myIndex * 2.39996323f); // golden angle (rad)
        float step = Mathf.Max(0.2f, combatSlotStepRad);

        for (int k = 0; k < Mathf.Max(1, combatTrySlots); k++)
        {
            float a = baseAngle + (k * step);

            Vector2 candidate = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;

            if (!IsOccupied(candidate, myPos))
                return candidate;
        }

        // 다 막혀 있으면 그냥 내가 가까운 쪽(그래도 덜 비비게)
        Vector2 fallback = center + (myPos - center).normalized * radius;
        return fallback;
    }

    bool IsOccupied(Vector2 candidate, Vector2 myPos)
    {
        float occ2 = combatOccupyRadius * combatOccupyRadius;

        for (int i = 0; i < allies.Count; i++)
        {
            var a = allies[i];
            if (a == null) continue;

            // 나 자신 제외: myPos랑 거의 같으면 스킵
            Vector2 ap = a.GetPosition2D();
            if ((ap - myPos).sqrMagnitude < 0.0001f) continue;

            if ((ap - candidate).sqrMagnitude <= occ2)
                return true;
        }

        return false;
    }

    public void SetSharedTarget(CombatAgent target)
    {
        if (target == null || target.IsDead || target.team != Team.Enemy)
        {
            SharedTarget = null;
            return;
        }

        SharedTarget = target;
    }

    /// <summary>
    /// 저장된 currentAllies를 읽어서 아군 복구
    /// </summary>
    public void RestoreSavedAllies(List<UnitKey> savedKeys)
    {
        if (savedKeys == null || savedKeys.Count == 0)
        {
            UIManager.Instance.UnitCountTxtUpdate(allies.Count, maxAllies);
            return;
        }


        int restoreCount = Mathf.Min(savedKeys.Count, maxAllies);

        for (int i = 0; i < restoreCount; i++)
        {
            SpawnSavedAlly(savedKeys[i], i);
        }

        RefreshIndices();
    }

    void SpawnSavedAlly(UnitKey unitKey, int orderIndex)
    {
        if (unitKey == UnitKey.None)
            return;

        string poolKey = unitKey.ToString();

        // 네 ObjectPool API에 맞춰 수정
        GameObject go = ObjectPool.Instance.Get(poolKey, transform.position, Quaternion.identity);
        if (go == null)
        {
            Debug.LogWarning($"[PlayerSquadController] 풀에서 못 찾음: {poolKey}");
            return;
        }

        UnitStateController usc = go.GetComponent<UnitStateController>();
        if (usc == null)
        {
            Debug.LogWarning($"[PlayerSquadController] UnitStateController 없음: {poolKey}");
            ObjectPool.Instance.Release(go);
            return;
        }

        usc.SpawnAsAlly();
    }

}