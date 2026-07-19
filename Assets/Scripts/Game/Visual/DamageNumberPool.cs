using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 伤害数字对象池：管理浮动伤害数字的创建和回收。
/// 单例模式，静态方法 Spawn() 方便各处调用。
/// </summary>
public class DamageNumberPool : MonoBehaviour
{
    private static DamageNumberPool _instance;
    public static DamageNumberPool Current => _instance;

    public static DamageNumberPool Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[DamageNumberPool]");
                _instance = go.AddComponent<DamageNumberPool>();
            }
            return _instance;
        }
    }

    [Tooltip("初始池大小")]
    public int preloadCount = 15;

    private readonly Queue<DamageNumber> _pool = new Queue<DamageNumber>();
    private GameObject _poolRoot;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        _poolRoot = new GameObject("DamageNumbers");
        _poolRoot.transform.SetParent(transform);

        for (int i = 0; i < preloadCount; i++)
        {
            CreateNew();
        }
    }

    private DamageNumber CreateNew()
    {
        var go = new GameObject("DamageNum");
        go.transform.SetParent(_poolRoot.transform);
        go.SetActive(false);
        var dn = go.AddComponent<DamageNumber>();
        _pool.Enqueue(dn);
        return dn;
    }

    public DamageNumber Get()
    {
        if (_pool.Count == 0) CreateNew();
        return _pool.Dequeue();
    }

    public void Return(DamageNumber dn)
    {
        dn.gameObject.SetActive(false);
        _pool.Enqueue(dn);
    }

    public void ClearAll()
    {
        _pool.Clear();
        if (_poolRoot == null)
        {
            return;
        }

        foreach (var damageNumber in _poolRoot.GetComponentsInChildren<DamageNumber>(true))
        {
            damageNumber.StopAllCoroutines();
            damageNumber.gameObject.SetActive(false);
            _pool.Enqueue(damageNumber);
        }
    }

    /// <summary>快捷生成伤害数字</summary>
    public static void Spawn(int damage, Vector2 worldPos, DamageType type = DamageType.Normal)
    {
        var dn = Instance.Get();
        string text = type switch
        {
            DamageType.Crit => $"{damage}!",
            DamageType.Heal => $"+{damage}",
            DamageType.Parry => $"破!{damage}",
            _ => damage.ToString()
        };
        dn.Init(text, worldPos, type);
    }

    /// <summary>快捷生成文字</summary>
    public static void SpawnText(string text, Vector2 worldPos, DamageType type = DamageType.Normal)
    {
        var dn = Instance.Get();
        dn.Init(text, worldPos, type);
    }

    private void OnDestroy()
    {
        _pool.Clear();
        _poolRoot = null;
        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
        }
    }
}
