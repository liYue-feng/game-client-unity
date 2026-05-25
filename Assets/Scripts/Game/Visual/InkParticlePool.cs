using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 墨迹粒子对象池：预分配粒子 GameObject，避免战斗中频繁实例化/销毁。
/// 池大小固定，用完时循环复用最早的粒子。
/// </summary>
public class InkParticlePool : MonoBehaviour
{
    public static InkParticlePool Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[InkParticlePool]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<InkParticlePool>();
                _instance.Initialize();
            }
            return _instance;
        }
    }
    private static InkParticlePool _instance;

    [Tooltip("池大小")]
    public int poolSize = 50;

    [Tooltip("粒子存活时间（秒）")]
    public float particleLifetime = 0.3f;

    private Queue<GameObject> _available = new Queue<GameObject>();
    private List<GameObject> _allParticles = new List<GameObject>();
    private Sprite _particleSprite;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        Initialize();
    }

    private void Initialize()
    {
        _particleSprite = PlaceholderSpriteFactory.InkParticleSprite();

        for (int i = 0; i < poolSize; i++)
        {
            GameObject particle = CreateParticle();
            particle.SetActive(false);
            _available.Enqueue(particle);
            _allParticles.Add(particle);
        }
    }

    /// <summary>从池中获取一个粒子</summary>
    public GameObject Get()
    {
        GameObject particle;
        if (_available.Count > 0)
        {
            particle = _available.Dequeue();
        }
        else
        {
            // 池耗尽，复用最早的
            particle = _allParticles[0];
            _allParticles.RemoveAt(0);
            _allParticles.Add(particle);
        }
        particle.SetActive(true);
        return particle;
    }

    /// <summary>归还粒子到池</summary>
    public void Return(GameObject particle)
    {
        particle.SetActive(false);
        _available.Enqueue(particle);
    }

    private GameObject CreateParticle()
    {
        GameObject go = new GameObject("InkParticle");
        go.transform.SetParent(transform);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _particleSprite;
        sr.sortingOrder = 10; // 在角色上层

        return go;
    }
}
