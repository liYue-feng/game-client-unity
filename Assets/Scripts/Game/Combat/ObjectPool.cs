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
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ObjectPool>();
            }
            return _instance;
        }
    }
    private static ObjectPool _instance;

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
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 注册对象池。
    /// </summary>
    /// <param name="key">池标识（如 "grunt", "archer", "arrow"）</param>
    /// <param name="factory">创建新对象的工厂方法</param>
    /// <param name="initialCount">预分配数量</param>
    public void Register(string key, System.Func<GameObject> factory, int initialCount = 5)
    {
        if (_pools.ContainsKey(key)) return;

        // 创建池根节点
        var root = new GameObject($"Pool_{key}");
        root.transform.SetParent(transform);

        _factories[key] = factory;
        _poolRoots[key] = root;
        _pools[key] = new Queue<GameObject>();

        // 预分配
        for (int i = 0; i < initialCount; i++)
        {
            var obj = factory();
            obj.name = $"{key}_{i}";
            obj.transform.SetParent(root);
            obj.SetActive(false);
            _pools[key].Enqueue(obj);
        }
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
}