using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 通用对象池：Dictionary<string, Queue<GameObject>> 模式。
/// 参考 VampireSurvivors 的对象池设计，避免战斗中频繁 Instantiate/Destroy。
///
/// 使用方式：
///   ObjectPool.Instance.Register("grunt", () => CreateGrunt(), 10);
///   GameObject enemy = ObjectPool.Instance.Get("grunt");
///   ObjectPool.Instance.Return("grunt", enemy);
/// </summary>
public class ObjectPool : MonoBehaviour
{
    public static ObjectPool Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[ObjectPool]");
                _instance = go.AddComponent<ObjectPool>();
            }
            return _instance;
        }
    }
    private static ObjectPool _instance;

    public static ObjectPool ExistingInstance => _instance;

    /// <summary>池存储：Key -> 可用对象队列</summary>
    private Dictionary<string, Queue<GameObject>> _pools = new Dictionary<string, Queue<GameObject>>();
    /// <summary>工厂方法：Key -> 创建函数</summary>
    private Dictionary<string, System.Func<GameObject>> _factories = new Dictionary<string, System.Func<GameObject>>();
    /// <summary>池根节点</summary>
    private Dictionary<string, Transform> _poolRoots = new Dictionary<string, Transform>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }

    /// <summary>
    /// 注册对象池。
    /// </summary>
    /// <param name="key">池标识（如 "grunt", "archer", "arrow"）</param>
    /// <param name="factory">创建新对象的工厂方法</param>
    /// <param name="initialCount">预分配数量</param>
    public bool Register(string key, System.Func<GameObject> factory, int initialCount = 5)
    {
        if (_pools.ContainsKey(key)) return false;

        // 创建池根节点
        var root = new GameObject($"Pool_{key}");
        root.transform.SetParent(transform);

        _factories[key] = factory;
        _poolRoots[key] = root.transform;
        _pools[key] = new Queue<GameObject>();

        // 预分配
        for (int i = 0; i < initialCount; i++)
        {
            var obj = factory();
            obj.name = $"{key}_{i}";
            obj.transform.SetParent(root.transform);
            obj.SetActive(false);
            _pools[key].Enqueue(obj);
        }

        return true;
    }

    /// <summary>
    /// 从池中取出对象。池为空时自动创建新对象。
    /// </summary>
    public GameObject Get(string key)
    {
        if (!_pools.ContainsKey(key))
        {
            Debug.LogError($"[ObjectPool] 未注册的池: {key}");
            return null;
        }

        GameObject obj;
        if (_pools[key].Count > 0)
        {
            obj = _pools[key].Dequeue();
        }
        else
        {
            // 池耗尽，动态扩容
            obj = _factories[key]();
            obj.transform.SetParent(_poolRoots[key]);
        }

        obj.SetActive(true);
        return obj;
    }

    /// <summary>
    /// 归还对象到池。
    /// </summary>
    public void Return(string key, GameObject obj)
    {
        if (!_pools.ContainsKey(key))
        {
            Destroy(obj);
            return;
        }

        obj.SetActive(false);
        obj.transform.SetParent(_poolRoots[key]);

        // 重置刚体速度
        var rb = obj.GetComponent<Rigidbody2D>();
        if (rb != null) rb.velocity = Vector2.zero;

        _pools[key].Enqueue(obj);
    }

    /// <summary>
    /// 清空指定池。
    /// </summary>
    public void Clear(string key)
    {
        if (!_pools.ContainsKey(key)) return;

        while (_pools[key].Count > 0)
        {
            Destroy(_pools[key].Dequeue());
        }
        _pools.Remove(key);
        _factories.Remove(key);

        if (_poolRoots.TryGetValue(key, out var root))
        {
            Destroy(root.gameObject);
            _poolRoots.Remove(key);
        }
    }

    /// <summary>清空所有池</summary>
    public void ClearAll()
    {
        foreach (var key in new List<string>(_pools.Keys))
        {
            Clear(key);
        }
    }

    /// <summary>获取池中可用数量</summary>
    public int AvailableCount(string key)
    {
        return _pools.TryGetValue(key, out var pool) ? pool.Count : 0;
    }

    /// <summary>预实例化指定数量的对象到已注册的池（不重复注册）</summary>
    public void PreWarm(string key, int count)
    {
        if (!_pools.ContainsKey(key) || !_factories.ContainsKey(key))
        {
            Debug.LogError($"[ObjectPool] PreWarm失败: 池 '{key}' 未注册");
            return;
        }

        var factory = _factories[key];
        var root = _poolRoots[key];
        for (int i = 0; i < count; i++)
        {
            var obj = factory();
            obj.name = $"{key}_pw{i}";
            obj.transform.SetParent(root);
            obj.SetActive(false);
            _pools[key].Enqueue(obj);
        }
    }

    /// <summary>确保池中至少有 minCount 个可用对象，不足则补充</summary>
    public void EnsureCapacity(string key, int minCount)
    {
        if (!_pools.ContainsKey(key)) return;
        int deficit = minCount - _pools[key].Count;
        if (deficit > 0)
        {
            PreWarm(key, deficit);
        }
    }

    private void OnDestroy()
    {
        _pools.Clear();
        _factories.Clear();
        _poolRoots.Clear();
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
