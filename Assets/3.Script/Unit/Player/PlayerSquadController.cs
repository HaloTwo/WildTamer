using System.Collections.Generic;
using UnityEngine;

public class PlayerSquadController : MonoBehaviour
{
    [SerializeField] int maxAllies = 20;
    public readonly List<AllyBrain> allies = new();

    [Header("Formation: Grid")]
    [SerializeField] int columns = 3;
    [SerializeField] float spacingX = 0.9f;
    [SerializeField] float spacingY = 0.9f;
    [SerializeField] float backOffset = 1.2f;

    [SerializeField] List<AllyBrain> testAllies;
    [SerializeField] PlayerMover2D playerMover;

    void Start()
    {
        foreach (var a in testAllies) OnTame(a);

        if (playerMover == null) playerMover = GetComponent<PlayerMover2D>();
    }

    public bool Register(AllyBrain ally)
    {
        if (ally == null) return false;
        if (allies.Count >= maxAllies) return false;
        if (allies.Contains(ally)) return false;

        allies.Add(ally);

        // 리더=플레이어, 인덱스 주입
        ally.SetupAsAlly(transform);
        ally.SetFormation(this, allies.Count - 1);
        RefreshIndices();
        return true;
    }

    public void Unregister(AllyBrain ally)
    {
        if (ally == null) return;
        allies.Remove(ally);
        RefreshIndices();
    }

    public void OnTame(AllyBrain newAlly) => Register(newAlly);

    void RefreshIndices()
    {
        for (int i = 0; i < allies.Count; i++)
            if (allies[i] != null) allies[i].SetIndex(i);
    }

    public Vector2 GetRingWorldPos(int index)
    {
        // allies.Count 기준으로 원형 배치
        int n = Mathf.Max(1, allies.Count);
        float angle = (index / (float)n) * Mathf.PI * 2f;

        float radius = 1.2f + (index / 8) * 0.4f; // 인원 많아지면 바깥 링
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

        return (Vector2)transform.position + offset;
    }
}