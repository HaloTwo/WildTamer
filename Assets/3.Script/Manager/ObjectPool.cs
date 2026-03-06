using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : Singleton<ObjectPool>
{
    [System.Serializable]
    public class PoolEntry
    {
        public GameObject prefab;
        public int prewarmCount = 10;
    }

    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    private class Pool
    {
        public string key;
        public GameObject prefab;
        public Transform rootParent;
        public readonly Queue<GameObject> queue = new Queue<GameObject>();
    }

    [Header("Initial Pools")]
    [SerializeField] private List<PoolEntry> entries = new();

    private readonly Dictionary<string, Pool> poolsByKey = new Dictionary<string, Pool>(128);
    private readonly Dictionary<GameObject, Pool> poolsByPrefab = new Dictionary<GameObject, Pool>(128);

    protected override void Awake()
    {
        base.Awake();
        InitializeEntries();
    }

    private void InitializeEntries()
    {
        if (entries == null || entries.Count == 0) return;

        for (int i = 0; i < entries.Count; i++)
        {
            PoolEntry entry = entries[i];
            if (entry == null || entry.prefab == null) continue;

            Register(entry.prefab, entry.prewarmCount);
        }
    }

    // -------------------------
    // Register
    // -------------------------

    public void Register(GameObject prefab, int prewarmCount = 0)
    {
        if (prefab == null) return;

        Pool pool = EnsurePool(prefab);
        Prewarm(pool, Mathf.Max(0, prewarmCount));
    }

    // -------------------------
    // Get by prefab
    // -------------------------

    public GameObject Get(GameObject prefab, Vector3? position = null, Quaternion? rotation = null, Transform parent = null)
    {
        if (prefab == null) return null;

        Pool pool = EnsurePool(prefab);
        return SpawnFromPool(pool, position, rotation, parent);
    }

    public T Get<T>(GameObject prefab, Vector3? position = null, Quaternion? rotation = null, Transform parent = null) where T : Component
    {
        GameObject go = Get(prefab, position, rotation, parent);
        return go != null ? go.GetComponent<T>() : null;
    }

    // -------------------------
    // Get by name (prefab.name)
    // -------------------------

    public GameObject Get(string prefabName, Vector3? position = null, Quaternion? rotation = null, Transform parent = null)
    {
        if (string.IsNullOrWhiteSpace(prefabName)) return null;

        if (!poolsByKey.TryGetValue(prefabName, out Pool pool))
        {
            Debug.LogError($"[ObjectPool] '{prefabName}' 이름의 풀을 찾을 수 없습니다. 먼저 entries에 등록하거나 prefab으로 Get 하셈.");
            return null;
        }

        return SpawnFromPool(pool, position, rotation, parent);
    }

    public T Get<T>(string prefabName, Vector3? position = null, Quaternion? rotation = null, Transform parent = null) where T : Component
    {
        GameObject go = Get(prefabName, position, rotation, parent);
        return go != null ? go.GetComponent<T>() : null;
    }

    // -------------------------
    // Release
    // -------------------------

    public void Release(GameObject go)
    {
        if (go == null) return;

        PooledObjectTag tag = go.GetComponent<PooledObjectTag>();
        if (tag == null || tag.poolPrefab == null)
        {
            Destroy(go);
            return;
        }

        if (tag.isInPool) return;

        if (!poolsByPrefab.TryGetValue(tag.poolPrefab, out Pool pool))
        {
            pool = EnsurePool(tag.poolPrefab);
        }

        IPoolable poolable = go.GetComponent<IPoolable>();
        poolable?.OnDespawned();

        Transform tr = go.transform;
        tr.SetParent(pool.rootParent, false);
        tr.localPosition = Vector3.zero;
        tr.localRotation = Quaternion.identity;
        tr.localScale = tag.initialLocalScale;

        go.SetActive(false);
        tag.isInPool = true;
        pool.queue.Enqueue(go);
    }

    public void Release(PooledObjectTag tag)
    {
        if (tag == null) return;
        Release(tag.gameObject);
    }

    // -------------------------
    // Internal
    // -------------------------

    private Pool EnsurePool(GameObject prefab)
    {
        if (prefab == null) return null;

        if (poolsByPrefab.TryGetValue(prefab, out Pool existing))
        {
            return existing;
        }

        string key = prefab.name;

        if (poolsByKey.TryGetValue(key, out Pool keyPool))
        {
            poolsByPrefab[prefab] = keyPool;
            return keyPool;
        }

        Pool newPool = new Pool
        {
            key = key,
            prefab = prefab,
            rootParent = CreatePoolRoot(key)
        };

        poolsByKey[key] = newPool;
        poolsByPrefab[prefab] = newPool;

        return newPool;
    }

    private Transform CreatePoolRoot(string key)
    {
        GameObject root = new GameObject($"[{key}]");
        root.transform.SetParent(transform, false);
        return root.transform;
    }

    private void Prewarm(Pool pool, int count)
    {
        if (pool == null || pool.prefab == null || count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            GameObject go = CreateNew(pool);
            go.SetActive(false);
            go.GetComponent<PooledObjectTag>().isInPool = true;
            pool.queue.Enqueue(go);
        }
    }

    private GameObject SpawnFromPool(Pool pool, Vector3? position, Quaternion? rotation, Transform parent)
    {
        if (pool == null || pool.prefab == null)
        {
            Debug.LogError("[ObjectPool] Pool 또는 Prefab이 유효하지 않습니다.");
            return null;
        }

        GameObject go = pool.queue.Count > 0 ? pool.queue.Dequeue() : CreateNew(pool);

        PooledObjectTag tag = go.GetComponent<PooledObjectTag>();
        tag.isInPool = false;

        Transform tr = go.transform;

        if (parent != null)
        {
            tr.SetParent(parent, false);
        }
        else
        {
            tr.SetParent(pool.rootParent, false);
        }

        tr.position = position ?? Vector3.zero;
        tr.rotation = rotation ?? Quaternion.identity;

        go.SetActive(true);

        IPoolable poolable = go.GetComponent<IPoolable>();
        poolable?.OnSpawned();

        return go;
    }

    private GameObject CreateNew(Pool pool)
    {
        GameObject go = Instantiate(pool.prefab, pool.rootParent);
        go.name = pool.prefab.name;

        PooledObjectTag tag = go.GetComponent<PooledObjectTag>();
        if (tag == null) tag = go.AddComponent<PooledObjectTag>();

        tag.poolPrefab = pool.prefab;
        tag.poolName = pool.key;
        tag.initialLocalScale = go.transform.localScale;
        tag.isInPool = false;

        return go;
    }
}

public class PooledObjectTag : MonoBehaviour
{
    [HideInInspector] public string poolName;
    [HideInInspector] public GameObject poolPrefab;
    [HideInInspector] public Vector3 initialLocalScale = Vector3.one;
    [HideInInspector] public bool isInPool;

    public void ReleaseToPool()
    {
        if (ObjectPool.Instance != null)
        {
            ObjectPool.Instance.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}