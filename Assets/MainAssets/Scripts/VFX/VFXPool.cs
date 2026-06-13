using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class VFXPool : MonoBehaviour
{
    private static VFXPool _instance;
    public static VFXPool Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<VFXPool>();
                if (_instance == null)
                    _instance = new GameObject("[VFXPool]").AddComponent<VFXPool>();
            }
            return _instance;
        }
    }

    private readonly Dictionary<GameObject, ObjectPool<GameObject>> _pools = new();

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    private void OnDestroy()
    {
        foreach (var pool in _pools.Values) pool.Dispose();
        _pools.Clear();
        if (_instance == this) _instance = null;
    }

    // Returns the instance INACTIVE with position applied.
    // Caller must call SetActive(true) after any additional per-instance setup (e.g. SetTarget on VFXShooter).
    // This prevents OnEnable from firing before the instance is fully configured.
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        var instance = GetOrCreatePool(prefab).Get();
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    public void Return(GameObject prefab, GameObject instance)
    {
        if (instance == null) return;
        if (_pools.TryGetValue(prefab, out var pool))
            pool.Release(instance);
        else
            Destroy(instance);
    }

    private ObjectPool<GameObject> GetOrCreatePool(GameObject prefab)
    {
        if (_pools.TryGetValue(prefab, out var pool)) return pool;

        pool = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                // Ensure the prefab is inactive before cloning so the clone is also born
                // inactive and OnEnable never fires until we call SetActive(true) after
                // SetTarget.  We leave the prefab inactive for the session — it is a
                // template and should never run its own components at runtime.
                if (prefab.activeSelf) prefab.SetActive(false);
                return Instantiate(prefab);
            },
            actionOnGet:     _ => { },
            actionOnRelease: obj => obj.SetActive(false),
            actionOnDestroy: obj => Destroy(obj),
            defaultCapacity: 4,
            maxSize:         20
        );
        _pools[prefab] = pool;
        return pool;
    }
}
