using UnityEngine;
using System.Collections;
using Game.Gameplay;

/// <summary>
/// Boss敌人：双阶段战斗，50%血量狂暴。
/// 阶段1：连斩 + 冲锋攻击 + 跳劈
/// 阶段2：更快 + 范围攻击（不可弹反，红色前摇）
///
/// Boss战的核心体验：读前摇颜色决策——黄闪弹反，红闪闪避。
/// 这就是"见招拆招"的终极体现。
/// </summary>
public class Boss : EnemyBase
{
    [Header("Boss阶段参数")]
    [Tooltip("狂暴血量阈值（百分比）")]
    public float enrageThreshold = 0.5f;
    [Tooltip("狂暴后速度倍率")]
    public float enrageSpeedMult = 1.5f;
    [Tooltip("狂暴后伤害倍率")]
    public float enrageDamageMult = 1.3f;

    [Header("Boss攻击模式")]
    [Tooltip("冲锋攻击距离")]
    public float chargeRange = 5f;
    [Tooltip("冲锋速度")]
    public float chargeSpeed = 10f;
    [Tooltip("跳劈伤害")]
    public int slamDamage = 30;
    [Tooltip("范围攻击伤害")]
    public int aoeDamage = 20;

    private bool _isEnraged;
    private int _attackPattern; // 0=连斩, 1=冲锋, 2=跳劈, 3=范围(狂暴)

    protected override void Awake()
    {
        hp = 300;
        maxHp = 300;
        moveSpeed = 2.5f;
        damage = 25;
        attackRange = 2.0f;
        chaseRange = 12f;
        telegraphDuration = 0.8f;
        attackDuration = 0.5f;

        base.Awake();
    }

    protected override void Update()
    {
        // 检测阶段切换
        if (!_isEnraged && hp <= maxHp * enrageThreshold)
        {
            EnterEnrage();
        }

        base.Update();
    }

    /// <summary>进入狂暴阶段</summary>
    private void EnterEnrage()
    {
        _isEnraged = true;
        moveSpeed *= enrageSpeedMult;
        damage = Mathf.RoundToInt(damage * enrageDamageMult);
        telegraphDuration *= 0.7f; // 狂暴后前摇更短

        // 视觉：红色调
        if (_sprite != null)
        {
            _sprite.color = new Color(1f, 0.4f, 0.3f);
        }
    }

    protected override void UpdateChase()
    {
        if (_player == null) return;
        FacePlayer();

        // Boss追击更积极
        float dir = _player.position.x > transform.position.x ? 1f : -1f;
        _rb.velocity = new Vector2(dir * moveSpeed, _rb.velocity.y);

        if (_distanceToPlayer <= attackRange)
        {
            // 选择攻击模式
            ChooseAttackPattern();
            ChangeState(EnemyState.Telegraph);
        }
    }

    /// <summary>选择攻击模式</summary>
    private void ChooseAttackPattern()
    {
        if (_isEnraged)
        {
            // 狂暴模式：随机选择，包含不可弹反的范围攻击
            float roll = Random.value;
            if (roll < 0.3f) _attackPattern = 0;       // 连斩
            else if (roll < 0.55f) _attackPattern = 1;  // 冲锋
            else if (roll < 0.75f) _attackPattern = 2;  // 跳劈
            else _attackPattern = 3;                     // 范围攻击(不可弹反)
        }
        else
        {
            // 正常模式：全部可弹反
            float roll = Random.value;
            if (roll < 0.4f) _attackPattern = 0;
            else if (roll < 0.7f) _attackPattern = 1;
            else _attackPattern = 2;
        }

        // 根据攻击模式设置前摇参数
        switch (_attackPattern)
        {
            case 0: // 连斩
                telegraphDuration = 0.5f;
                isCurrentAttackParryable = true;
                break;
            case 1: // 冲锋
                telegraphDuration = 0.8f;
                isCurrentAttackParryable = true;
                break;
            case 2: // 跳劈
                telegraphDuration = 1.0f;
                isCurrentAttackParryable = true;
                break;
            case 3: // 范围攻击（不可弹反！）
                telegraphDuration = 0.6f;
                isCurrentAttackParryable = false;
                break;
        }
    }

    protected override void OnAttackStart()
    {
        switch (_attackPattern)
        {
            case 0:
                StartCoroutine(SlashComboCoroutine());
                break;
            case 1:
                StartCoroutine(ChargeCoroutine());
                break;
            case 2:
                StartCoroutine(SlamCoroutine());
                break;
            case 3:
                StartCoroutine(AoEAttackCoroutine());
                break;
        }
    }

    /// <summary>连斩攻击</summary>
    private IEnumerator SlashComboCoroutine()
    {
        for (int i = 0; i < 3; i++)
        {
            PerformMeleeAttack(damage, 8f);
            yield return new WaitForSeconds(0.3f);
        }
    }

    /// <summary>冲锋攻击</summary>
    private IEnumerator ChargeCoroutine()
    {
        _rb.velocity = new Vector2(_facingDirection * chargeSpeed, _rb.velocity.y);
        yield return new WaitForSeconds(0.3f);
        _rb.velocity = Vector2.zero;
        PerformMeleeAttack(damage, 12f);
    }

    /// <summary>跳劈攻击</summary>
    private IEnumerator SlamCoroutine()
    {
        // 跳起
        _rb.velocity = new Vector2(0f, 10f);
        yield return new WaitForSeconds(0.3f);
        // 落地判定
        PerformMeleeAttack(slamDamage, 15f);
    }

    /// <summary>范围攻击（不可弹反）</summary>
    private IEnumerator AoEAttackCoroutine()
    {
        // 短暂延迟后全屏范围伤害
        yield return new WaitForSeconds(0.2f);

        // 检测范围内的玩家
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 4f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                var hurtbox = hit.GetComponent<Hurtbox>();
                if (hurtbox != null)
                {
                    hurtbox.ReceiveHit(new CombatHit(
                        aoeDamage,
                        0f,
                        8f,
                        false,
                        this));
                }
            }
        }
    }

    /// <summary>通用近战攻击判定</summary>
    private void PerformMeleeAttack(int dmg, float knockback)
    {
        Vector2 hitPos = (Vector2)transform.position + new Vector2(_facingDirection * 1.0f, 0f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(hitPos, new Vector2(1.5f, 1.2f), 0f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                var hurtbox = hit.GetComponent<Hurtbox>();
                if (hurtbox != null)
                {
                    hurtbox.ReceiveHit(new CombatHit(
                        dmg,
                        _facingDirection,
                        knockback,
                        isCurrentAttackParryable,
                        this));
                }
            }
        }
    }

    protected override void UpdateAttack()
    {
        _rb.velocity = Vector2.zero;
        if (_stateTimer <= 0)
        {
            ChangeState(EnemyState.Chase);
        }
    }

    // Boss 不逃跑
    protected override void UpdateIdle()
    {
        if (_player != null)
        {
            ChangeState(EnemyState.Chase);
        }
    }

    protected override void UpdatePatrol()
    {
        if (_player != null)
        {
            ChangeState(EnemyState.Chase);
        }
    }
}
