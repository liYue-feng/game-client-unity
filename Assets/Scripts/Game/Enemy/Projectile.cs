using System;
using UnityEngine;
using Game.Gameplay;

/// <summary>
/// 弹丸：弓手射出的箭矢。
/// 直线飞行，命中后造成伤害。可被弹反（反弹回敌人）。
/// 超时自动销毁。
/// </summary>
public class Projectile : MonoBehaviour, IParryResponder
{
    private const float DefaultKnockbackForce = 3f;

    [Tooltip("飞行速度")]
    public float speed = 8f;
    [Tooltip("伤害值")]
    public int damage = 8;
    [Tooltip("生存时间（秒）")]
    public float lifetime = 3f;
    [Tooltip("是否可被弹反")]
    public bool isParryable = true;
    [Tooltip("发射者（避免自伤）")]
    public GameObject owner;

    private Vector2 _direction;
    private Rigidbody2D _rb;
    private bool _isDeflected;
    private int _launchDamage;
    private bool _launchIsParryable;
    private float _launchKnockbackForce = DefaultKnockbackForce;

    public event Action<Projectile> Destroyed;

    /// <summary>初始化弹丸方向</summary>
    public void Launch(Vector2 direction, GameObject source)
    {
        Launch(direction, source, damage, isParryable, DefaultKnockbackForce);
    }

    /// <summary>Atomically applies the frozen attack snapshot for this launch.</summary>
    public void Launch(
        Vector2 direction,
        GameObject source,
        int launchDamage,
        bool launchIsParryable)
    {
        Launch(
            direction,
            source,
            launchDamage,
            launchIsParryable,
            DefaultKnockbackForce);
    }

    /// <summary>Atomically freezes every projectile field consumed on contact.</summary>
    public void Launch(
        Vector2 direction,
        GameObject source,
        int launchDamage,
        bool launchIsParryable,
        float launchKnockbackForce)
    {
        _direction = direction.normalized;
        owner = source;
        _launchDamage = Mathf.Max(0, launchDamage);
        _launchIsParryable = launchIsParryable;
        _launchKnockbackForce = Mathf.Max(0f, launchKnockbackForce);
        _isDeflected = false;

        _rb = GetComponent<Rigidbody2D>();
        if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.velocity = _direction * speed;

        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 不命中发射者
        if (other.gameObject == owner) return;

        // 命中玩家
        if (other.CompareTag("Player"))
        {
            var hurtbox = other.GetComponent<Hurtbox>();
            if (hurtbox != null)
            {
                float dir = _direction.x > 0 ? 1f : -1f;
                var result = hurtbox.ReceiveHit(new CombatHit(
                    _launchDamage,
                    dir,
                    _launchKnockbackForce,
                    _launchIsParryable,
                    this));
                if (result == CombatHitResult.Parried)
                {
                    return;
                }
            }
            Destroy(gameObject);
            return;
        }

        if (_isDeflected)
        {
            var hurtbox = other.GetComponentInParent<Hurtbox>();
            var enemy = hurtbox != null ? hurtbox.GetComponent<EnemyBase>() : null;
            if (enemy != null)
            {
                float dir = _direction.x > 0 ? 1f : -1f;
                var result = hurtbox.ReceiveHit(new CombatHit(
                    _launchDamage,
                    dir,
                    _launchKnockbackForce,
                    false,
                    this));
                if (result == CombatHitResult.Damaged)
                {
                    Destroy(gameObject);
                }
                return;
            }
        }

        // 命中其他物体（墙壁等）
        if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>弹反反弹：反转方向，改为命中敌人</summary>
    public void Deflect()
    {
        _direction = -_direction;
        _rb.velocity = _direction * speed * 1.5f; // 反弹加速
        owner = null; // 反弹后不再区分友军

        // 改变 tag 以命中敌人
        gameObject.tag = "PlayerProjectile";

        // 视觉：变色表示反弹
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(0.3f, 0.6f, 1f); // 蓝色=反弹
    }

    public void OnParried()
    {
        if (_isDeflected)
        {
            return;
        }

        _isDeflected = true;
        Deflect();
    }

    private void OnDestroy()
    {
        var destroyed = Destroyed;
        Destroyed = null;
        destroyed?.Invoke(this);
        // 如果有对象池，归还而非销毁
    }
}
