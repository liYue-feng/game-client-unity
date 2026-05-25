using UnityEngine;
using System.Collections;

/// <summary>
/// 墨迹飞溅特效：受击时溅出黑色墨点粒子。
/// 水墨画风格的核心视觉反馈——每次命中都像毛笔在宣纸上溅墨。
/// 使用 InkParticlePool 对象池，性能友好。
/// </summary>
public class InkHitEffect : MonoBehaviour
{
    [Tooltip("溅出的墨点数量")]
    public int particleCount = 7;
    [Tooltip="粒子速度范围")]
    public float minSpeed = 2f;
    public float maxSpeed = 5f;
    [Tooltip="粒子存活时间（秒）")]
    public float lifetime = 0.3f;

    /// <summary>
    /// 在指定位置播放墨迹飞溅。
    /// 由 CombatEvents.OnHitLanded 触发。
    /// </summary>
    public void PlayAt(Vector3 position)
    {
        StartCoroutine(SplashCoroutine(position));
    }

    private IEnumerator SplashCoroutine(Vector3 position)
    {
        GameObject[] particles = new GameObject[particleCount];

        for (int i = 0; i < particleCount; i++)
        {
            GameObject p = InkParticlePool.Instance.Get();
            p.transform.position = position;

            // 随机方向和速度
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float speed = Random.Range(minSpeed, maxSpeed);
            Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;

            var rb = p.GetComponent<Rigidbody2D>();
            if (rb == null)
            {
                rb = p.AddComponent<Rigidbody2D>();
                rb.gravityScale = 3f; // 墨滴有重力感
            }
            rb.velocity = velocity;
            particles[i] = p;
        }

        // 等待粒子寿命结束
        yield return new WaitForSeconds(lifetime);

        // 归还粒子
        foreach (var p in particles)
        {
            if (p != null && p.activeSelf)
            {
                InkParticlePool.Instance.Return(p);
            }
        }
    }
}
