using System.Collections.Generic;
using Game.Gameplay;
using UnityEngine;

public readonly struct InkParticleHandle
{
    public InkParticleHandle(GameObject particle, ParticleLeaseToken token)
    {
        Particle = particle;
        Token = token;
    }

    public GameObject Particle { get; }
    public ParticleLeaseToken Token { get; }
}

/// <summary>
/// Scene-owned fixed particle pool with generation-safe delayed returns.
/// </summary>
public sealed class InkParticlePool : MonoBehaviour
{
    private static InkParticlePool _instance;

    public static InkParticlePool Instance => _instance;

    public int poolSize = 50;
    public float particleLifetime = 0.3f;

    private readonly Queue<int> _available = new Queue<int>();
    private readonly List<GameObject> _allParticles = new List<GameObject>();
    private readonly ParticleLeaseRegistry _leases = new ParticleLeaseRegistry();
    private Sprite _particleSprite;
    private int _nextReuseSlot;
    private bool _initialized;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        Initialize();
    }

    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        poolSize = Mathf.Max(1, poolSize);
        _particleSprite = PlaceholderSpriteFactory.InkParticleSprite();
        for (var slot = 0; slot < poolSize; slot++)
        {
            var particle = CreateParticle();
            particle.SetActive(false);
            _allParticles.Add(particle);
            _available.Enqueue(slot);
        }
    }

    public InkParticleHandle Get()
    {
        if (!_initialized || _allParticles.Count == 0)
        {
            throw new System.InvalidOperationException("InkParticlePool is not initialized.");
        }

        var slot = _available.Count > 0
            ? _available.Dequeue()
            : NextReuseSlot();
        var particle = _allParticles[slot];
        ResetParticle(particle);
        var token = _leases.Acquire(slot);
        particle.SetActive(true);
        return new InkParticleHandle(particle, token);
    }

    public bool Return(InkParticleHandle handle)
    {
        var slot = handle.Token.Slot;
        if (slot < 0 || slot >= _allParticles.Count ||
            handle.Particle != _allParticles[slot] ||
            !_leases.TryRelease(handle.Token))
        {
            return false;
        }

        var particle = _allParticles[slot];
        ResetParticle(particle);
        particle.SetActive(false);
        _available.Enqueue(slot);
        return true;
    }

    public bool IsActive(InkParticleHandle handle)
    {
        return _leases.IsActive(handle.Token) &&
            handle.Particle != null &&
            handle.Particle.activeSelf;
    }

    public void ClearAll()
    {
        _leases.InvalidateAll();
        _available.Clear();
        for (var slot = 0; slot < _allParticles.Count; slot++)
        {
            var particle = _allParticles[slot];
            if (particle != null)
            {
                ResetParticle(particle);
                particle.SetActive(false);
            }
            _available.Enqueue(slot);
        }
        _nextReuseSlot = 0;
    }

    private int NextReuseSlot()
    {
        var slot = _nextReuseSlot % _allParticles.Count;
        _nextReuseSlot = (_nextReuseSlot + 1) % _allParticles.Count;
        return slot;
    }

    private GameObject CreateParticle()
    {
        var particle = new GameObject("InkParticle");
        particle.transform.SetParent(transform, false);
        var renderer = particle.AddComponent<SpriteRenderer>();
        renderer.sprite = _particleSprite;
        renderer.sortingOrder = 10;
        return particle;
    }

    private static void ResetParticle(GameObject particle)
    {
        particle.transform.localRotation = Quaternion.identity;
        particle.transform.localScale = Vector3.one;
        var body = particle.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private void OnDestroy()
    {
        _leases.InvalidateAll();
        _available.Clear();
        _allParticles.Clear();
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
