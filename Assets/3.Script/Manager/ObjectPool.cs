using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : Singleton<ObjectPool>
{
    [System.Serializable]
    public class PrefabEntry
    {
        public GameObject prefab;
        public int prewarm = 10;
        public Transform parent; 
    }

    [Header("Prewarm Prefabs")]
    [SerializeField] private List<PrefabEntry> entries = new();

    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    private class Pool
    {
        public GameObject prefab;
        public Transform parent;
        public readonly Queue<GameObject> q = new Queue<GameObject>();
    }

    private readonly Dictionary<GameObject, Pool> pools = new Dictionary<GameObject, Pool>(128);

    protected override void Awake()
    {
        base.Awake();

        PrewarmFromEntries();
    }

    private void PrewarmFromEntries()
    {
        if (entries == null || entries.Count == 0) return;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.prefab == null) continue;
            Init(e.prefab, e.prewarm, e.parent);
        }
    }

    public void Init(GameObject prefab, int prewarm, Transform parent = null)
    {
        if (prefab == null) return;

        var pool = EnsurePool(prefab, parent);
        Prewarm(pool, Mathf.Max(0, prewarm));
    }

    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null) return null;

        var pool = EnsurePool(prefab, parent);
        GameObject go = pool.q.Count > 0 ? pool.q.Dequeue() : CreateNew(pool);

        Transform targetParent = parent != null ? parent : pool.parent;
        if (go.transform.parent != targetParent)
            go.transform.SetParent(targetParent, false);

        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);

        var poolable = go.GetComponent<IPoolable>();
        poolable?.OnSpawned();

        return go;
    }

    public T Get<T>(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
    {
        var go = Get(prefab, position, rotation, parent);
        return go ? go.GetComponent<T>() : null;
    }

    public void Release(GameObject go)
    {
        if (go == null) return;

        var tag = go.GetComponent<PooledObjectTag>();
        if (tag == null || tag.prefabKey == null)
        {
            Destroy(go);
            return;
        }

        if (!pools.TryGetValue(tag.prefabKey, out var pool))
        {
            pool = EnsurePool(tag.prefabKey, null);
        }

        var poolable = go.GetComponent<IPoolable>();
        poolable?.OnDespawned();

        go.SetActive(false);

        if (go.transform.parent != pool.parent)
            go.transform.SetParent(pool.parent, false);

        pool.q.Enqueue(go);
    }

    public void Release(PooledObjectTag tag)
    {
        if (tag == null) return;
        Release(tag.gameObject);
    }

    // -------------------- Internal --------------------

    private Pool EnsurePool(GameObject prefab, Transform parent)
    {
        if (pools.TryGetValue(prefab, out var existing))
        {
            if (existing.parent == null)
                existing.parent = parent != null ? parent : CreateDefaultParent(prefab.name);
            return existing;
        }

        var pool = new Pool
        {
            prefab = prefab,
            parent = parent != null ? parent : CreateDefaultParent(prefab.name),
        };

        pools[prefab] = pool;
        return pool;
    }

    private Transform CreateDefaultParent(string prefabName)
    {
        var folder = new GameObject($"{prefabName}_Pool");
        folder.transform.SetParent(transform, false);
        return folder.transform;
    }

    private void Prewarm(Pool pool, int count)
    {
        if (pool == null || pool.prefab == null || count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            var go = CreateNew(pool);
            go.SetActive(false);
            pool.q.Enqueue(go);
        }
    }

    private GameObject CreateNew(Pool pool)
    {
        var go = Instantiate(pool.prefab, pool.parent);
        go.name = pool.prefab.name;

        var tag = go.GetComponent<PooledObjectTag>();
        if (tag == null) tag = go.AddComponent<PooledObjectTag>();
        tag.prefabKey = pool.prefab;

        return go;
    }
}

public class PooledObjectTag : MonoBehaviour
{
    public GameObject prefabKey;

    public void ReleaseToPool()
    {
        if (ObjectPool.Instance != null)
            ObjectPool.Instance.Release(this);
        else
            Destroy(gameObject);
    }
}