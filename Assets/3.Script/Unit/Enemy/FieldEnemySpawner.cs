using System.Collections.Generic;
using UnityEngine;

public enum SpawnMode
{
    FixedPoint,
    RandomArea
}

public class FieldEnemySpawner : MonoBehaviour
{
    [System.Serializable]
    public class SpawnGroupPreset
    {
        public GameObject enemyPrefab;
        public int minCount = 3;
        public int maxCount = 6;
        public float spawnScatterRadius = 1.5f;
    }

    [Header("Spawn")]
    [SerializeField] SpawnMode spawnMode = SpawnMode.RandomArea;
    [SerializeField] SpawnGroupPreset preset;

    [Header("기즈모 컬러")]
    [SerializeField] Color gizmoColor = Color.green;

    [Header("Fixed")]
    [SerializeField] Transform fixedSpawnPoint;

    [Header("Random Area")]
    [SerializeField] Vector2 randomAreaSize = new Vector2(10f, 10f);

    [Header("Routes - 순서대로 라운드로빈 배정")]
    [SerializeField] List<EnemyPathRoute> routes = new();

    [Header("Control")]
    [SerializeField] int maxAliveGroups = 3;
    [SerializeField] float spawnInterval = 5f;
    [SerializeField] float combatExitDelay = 4f;
    [SerializeField] bool spawnOnStart = true;

    readonly List<EnemyGroup> aliveGroups = new();

    float nextSpawnTime;
    int nextGroupId = 1;
    int nextRouteIndex = 0;

    void Start()
    {
        if (spawnOnStart)
        {
            while (aliveGroups.Count < maxAliveGroups)
            {
                SpawnGroup();
            }
        }

        nextSpawnTime = Time.time + spawnInterval;
    }

    void Update()
    {
        CleanupDeadGroups();

        if (aliveGroups.Count >= maxAliveGroups)
            return;

        if (Time.time < nextSpawnTime)
            return;

        SpawnGroup();
        nextSpawnTime = Time.time + spawnInterval;
    }

    void SpawnGroup()
    {
        if (preset == null || preset.enemyPrefab == null)
            return;

        EnemyPathRoute selectedRoute = GetNextRoute();
        Vector2 spawnCenter = GetSpawnCenter(selectedRoute);

        EnemyGroup group = new EnemyGroup();
        group.groupId = nextGroupId++;
        group.combatExitDelay = combatExitDelay;

        if (selectedRoute != null)
        {
            group.loopPath = selectedRoute.loop;

            for (int i = 0; i < selectedRoute.points.Count; i++)
            {
                Transform p = selectedRoute.points[i];
                if (p != null)
                    group.waypoints.Add(p);
            }
        }

        int spawnCount = Random.Range(preset.minCount, preset.maxCount + 1);

        for (int i = 0; i < spawnCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * preset.spawnScatterRadius;
            Vector2 spawnPos = spawnCenter + offset;

            GameObject go = ObjectPool.Instance.Get(preset.enemyPrefab);
            if (go == null) continue;

            go.transform.position = spawnPos;
            go.transform.rotation = Quaternion.identity;
            go.SetActive(true);

            UnitStateController state = go.GetComponent<UnitStateController>();
            if (state != null)
                state.SpawnAsEnemy();

            EnemyBrain brain = go.GetComponent<EnemyBrain>();
            if (brain != null)
            {
                brain.EnemyBrainSet();
                group.members.Add(brain);
            }
        }

        if (group.members.Count == 0)
            return;

        group.leader = group.members[0];

        for (int i = 0; i < group.members.Count; i++)
        {
            group.members[i].SetupGroup(group, i);
        }

        aliveGroups.Add(group);
    }

    EnemyPathRoute GetNextRoute()
    {
        if (routes == null || routes.Count == 0)
            return null;

        int validCount = 0;
        for (int i = 0; i < routes.Count; i++)
        {
            if (routes[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return null;

        for (int i = 0; i < routes.Count; i++)
        {
            int idx = (nextRouteIndex + i) % routes.Count;
            EnemyPathRoute route = routes[idx];

            if (route == null)
                continue;

            nextRouteIndex = (idx + 1) % routes.Count;
            return route;
        }

        return null;
    }

    Vector2 GetSpawnCenter(EnemyPathRoute route)
    {
        if (route != null && route.points != null && route.points.Count > 0 && route.points[0] != null)
            return route.points[0].position;

        if (spawnMode == SpawnMode.FixedPoint)
        {
            if (fixedSpawnPoint != null)
                return fixedSpawnPoint.position;

            return transform.position;
        }

        Vector2 half = randomAreaSize * 0.5f;

        Vector2 rand = new Vector2(
            Random.Range(-half.x, half.x),
            Random.Range(-half.y, half.y)
        );

        return (Vector2)transform.position + rand;
    }

    void CleanupDeadGroups()
    {
        for (int i = aliveGroups.Count - 1; i >= 0; i--)
        {
            EnemyGroup g = aliveGroups[i];

            if (g == null)
            {
                aliveGroups.RemoveAt(i);
                continue;
            }

            g.CleanupNullOrDeadMembers();

            if (g.IsEmpty())
            {
                aliveGroups.RemoveAt(i);
                continue;
            }

            g.RefreshLeader();
            g.UpdateCombatState();
        }
    }

    //void OnDrawGizmosSelected()
    //{
    //    if (spawnMode == SpawnMode.RandomArea)
    //    {
    //        Gizmos.color = gizmoColor;
    //        Gizmos.DrawWireCube(transform.position, randomAreaSize);
    //    }


    //}

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (routes == null || routes.Count == 0)
            return;

        Gizmos.color = gizmoColor;

        for (int r = 0; r < routes.Count; r++)
        {
            var route = routes[r];
            if (route == null)
                continue;

            DrawRoute(route);
        }
    }

    void DrawRoute(EnemyPathRoute route)
    {
        if (route.points == null || route.points.Count == 0)
            return;

        var points = route.points;

        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] == null)
                continue;

            // 웨이포인트 표시
            Gizmos.DrawSphere(points[i].position, 0.2f);

            // 다음 웨이포인트 라인
            if (i < points.Count - 1 && points[i + 1] != null)
            {
                Gizmos.DrawLine(points[i].position, points[i + 1].position);
            }
        }

        // loop 처리
        if (route.loop && points.Count > 1 && points[0] != null && points[points.Count - 1] != null)
        {
            Gizmos.DrawLine(points[points.Count - 1].position, points[0].position);
        }
    }
#endif

}