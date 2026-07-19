using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class InkHitEffect : MonoBehaviour
{
    public int particleCount = 7;
    public float minSpeed = 2f;
    public float maxSpeed = 5f;
    public float lifetime = 0.3f;

    private readonly List<InkParticleHandle> _activeHandles = new List<InkParticleHandle>();
    private Coroutine _splashRoutine;
    private InkParticlePool _pool;

    public void PlayAt(Vector3 position, InkParticlePool pool)
    {
        ClearAll();
        if (pool == null)
        {
            return;
        }

        _pool = pool;
        _splashRoutine = StartCoroutine(SplashCoroutine(position));
    }

    public void ClearAll()
    {
        if (_splashRoutine != null)
        {
            StopCoroutine(_splashRoutine);
            _splashRoutine = null;
        }

        if (_pool != null)
        {
            foreach (var handle in _activeHandles)
            {
                _pool.Return(handle);
            }
        }

        _activeHandles.Clear();
        _pool = null;
    }

    private IEnumerator SplashCoroutine(Vector3 position)
    {
        for (var index = 0; index < particleCount; index++)
        {
            var handle = _pool.Get();
            var particle = handle.Particle;
            particle.transform.position = position;
            var angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            var speed = Random.Range(minSpeed, maxSpeed);
            var body = particle.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = particle.AddComponent<Rigidbody2D>();
                body.gravityScale = 3f;
            }
            body.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            _activeHandles.Add(handle);
        }

        yield return new WaitForSeconds(lifetime);
        if (_pool != null)
        {
            foreach (var handle in _activeHandles)
            {
                _pool.Return(handle);
            }
        }
        _activeHandles.Clear();
        _pool = null;
        _splashRoutine = null;
    }

    private void OnDisable()
    {
        ClearAll();
    }
}
