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

    // Position and rotation are applied before SetActive so OnEnable sees correct values.
    public GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        var instance = GetOrCreatePool(prefab).Get();
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
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

    private ObjectPool<GameObject> GetOrCreatePool(GameObject prefab) =>
        _pools.TryGetValue(prefab, out var pool) ? pool : (_pools[prefab] = new ObjectPool<GameObject>(
            createFunc:      () => Instantiate(prefab),
            actionOnGet:     _ => { },                      // SetActive done after position is set
            actionOnRelease: obj => obj.SetActive(false),
            actionOnDestroy: obj => Destroy(obj),
            defaultCapacity: 4,
            maxSize:         20
        ));
}
