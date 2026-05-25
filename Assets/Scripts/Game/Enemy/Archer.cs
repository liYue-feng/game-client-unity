using UnityEngine;

/// <summary>
/// 弓手：远程敌人，保持距离射箭。
/// 追击到射程 → 前摇(0.6s黄闪) → 射箭 → 后退 → 再次射击。
/// 箭矢可被弹反（反弹回弓手自身）。
/// </summary>
public class Archer : EnemyBase
{
    [Header("弓手特有参数")]
    [Tooltip("箭矢预制体（运行时创建）")]
    public GameObject projectilePrefab;
    [Tooltip("射箭冷却时间")]
    public float shootCooldown = 2f;
    [Tooltip="保持的理想距离"]
    public float preferredDistance = 5f;

    private float _shootCooldownTimer;

    protected override void Awake()
    {
        hp = 20;
        maxHp = 20;
        moveSpeed = 1.5f;
        damage = 8;
        attackRange = 6f;    // 远程攻击范围
        chaseRange = 10f;
        telegraphDuration = 0.6f;
        attackDuration = 0.2f;
        isCurrentAttackParryable = true;

        base.Awake();
    }

    protected override void Update()
    {
        _shootCooldownTimer -= Time.deltaTime;
        base.Update();
    }

    protected override void UpdateChase()
    {
        if (_player == null) return;

        FacePlayer();

        float dist = _distanceToPlayer;

        // 太近 → 后退
        if (dist < preferredDistance - 1f)
        {
            float dir = _player.position.x > transform.position.x ? -1f : 1f;
            _rb.velocity = new Vector2(dir * moveSpeed, _rb.velocity.y);
        }
        // 在射程内且冷却完成 → 开始前摇
        else if (dist <= attackRange && _shootCooldownTimer <= 0)
        {
            ChangeState(EnemyState.Telegraph);
        }
        // 太远 → 追击
        else if (dist > attackRange)
        {
            float dir = _player.position.x > transform.position.x ? 1f : -1f;
            _rb.velocity = new Vector2(dir * moveSpeed, _rb.velocity.y);
        }
        else
        {
            _rb.velocity = Vector2.zero;
        }
    }

    protected override void OnAttackStart()
    {
        // 发射箭矢
        ShootArrow();
        _shootCooldownTimer = shootCooldown;
    }

    private void ShootArrow()
    {
        if (_player == null) return;

        // 创建箭矢（运行时生成，无需预制体）
        GameObject arrow = new GameObject("Arrow");
        arrow.transform.position = transform.position + new Vector3(_facingDirection * 0.3f, 0.2f, 0f);
        arrow.layer = LayerMask.NameToLayer("Default");

        // 精灵
        var sr = arrow.AddComponent<SpriteRenderer>();
        sr.sprite = PlaceholderSpriteFactory.CreateCircle(3, ShuiMoPalette.Vermillion);
        sr.sortingOrder = 5;

        // 碰撞
        var col = arrow.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.3f, 0.1f);

        // 弹丸脚本
        var projectile = arrow.AddComponent<Projectile>();
        Vector2 dir = (_player.position - transform.position).normalized;
        projectile.Launch(dir, gameObject);

        // 标记
        arrow.tag = "EnemyProjectile";
    }
}
