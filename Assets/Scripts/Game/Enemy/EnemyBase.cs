using UnityEngine;
using System.Collections;

/// <summary>
/// 敌人基类：所有敌人共享的核心逻辑。
/// 子类（Grunt/Archer/Elite/Boss）通过重写虚方法定制行为。
///
/// 核心循环：Idle → Patrol → Chase → Telegraph → Attack → 回到Chase
/// 前摇(Telegraph)是关键——必须给玩家可见的攻击预警，
/// 黄色=可弹反，红色=不可弹反（必须闪避）。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class EnemyBase : MonoBehaviour
{
    [Header("基础属性")]
    public int hp = 30;
    public int maxHp = 30;
    public float moveSpeed = 2f;
    public int damage = 10;
    public float attackRange = 1.5f;
    public float chaseRange = 8f;
    public float attackDuration = 0.3f;
    [Tooltip("死亡时掉落的经验值")]
    public int expValue = 1;

    [Header("前摇参数")]
    [Tooltip("出招前摇时间（秒），这是给玩家的反应时间")]
    public float telegraphDuration = 0.6f;
    [Tooltip("本次攻击是否可被弹反")]
    public bool isCurrentAttackParryable = true;
    [Tooltip("前摇颜色（黄=可弹反，红=不可弹反）")]
    public Color parryableColor = new Color(1f, 0.9f, 0f);   // 黄色
    public Color unparryableColor = new Color(1f, 0f, 0f);   // 红色

    [Header("AI决策")]
    [Tooltip("AI决策间隔（秒）")]
    public float decisionInterval = 0.5f;
    [Tooltip("追击中随机停顿概率")]
    public float idleChance = 0.1f;

    /// <summary>当前状态</summary>
    public EnemyState CurrentState { get; protected set; } = EnemyState.Idle;
    /// <summary>是否已死亡</summary>
    public bool IsDead { get; protected set; }
    /// <summary>死亡事件</summary>
    public event System.Action<EnemyBase> OnDeath;

    // 组件引用
    protected Rigidbody2D _rb;
    protected SpriteRenderer _sprite;
    protected Transform _player;
    protected float _distanceToPlayer;
    protected float _stateTimer;
    protected float _decisionTimer;
    protected int _facingDirection = 1;

    private HitEffectPlayer _hitEffect;
    private Hurtbox _hurtbox;

    protected virtual void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _sprite = GetComponent<SpriteRenderer>();
        _hitEffect = GetComponent<HitEffectPlayer>();

        // 确保 Hurtbox 存在
        _hurtbox = GetComponent<Hurtbox>();
        if (_hurtbox == null)
        {
            _hurtbox = gameObject.AddComponent<Hurtbox>();
        }

        // 初始化碰撞体
        var col = GetComponent<Collider2D>();
        if (col == null)
        {
            var boxCol = gameObject.AddComponent<BoxCollider2D>();
            boxCol.size = new Vector2(0.32f, 0.48f);
        }
    }

    protected virtual void Start()
    {
        // 找到玩家
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) _player = playerObj.transform;
    }

    protected virtual void Update()
    {
        if (IsDead) return;

        // 更新与玩家的距离
        if (_player != null)
        {
            _distanceToPlayer = Vector2.Distance(transform.position, _player.position);
        }

        // AI决策计时器
        _decisionTimer -= Time.deltaTime;
        _stateTimer -= Time.deltaTime;

        // 状态机
        switch (CurrentState)
        {
            case EnemyState.Idle:
                UpdateIdle();
                break;
            case EnemyState.Patrol:
                UpdatePatrol();
                break;
            case EnemyState.Chase:
                UpdateChase();
                break;
            case EnemyState.Telegraph:
                UpdateTelegraph();
                break;
            case EnemyState.Attack:
                UpdateAttack();
                break;
            case EnemyState.Hurt:
                UpdateHurt();
                break;
            case EnemyState.Stunned:
                UpdateStunned();
                break;
        }
    }

    // ========== 状态更新（子类可重写） ==========

    protected virtual void UpdateIdle()
    {
        if (_decisionTimer > 0) return;
        _decisionTimer = decisionInterval;

        if (_distanceToPlayer <= chaseRange)
        {
            ChangeState(EnemyState.Chase);
        }
        else
        {
            ChangeState(EnemyState.Patrol);
        }
    }

    protected virtual void UpdatePatrol()
    {
        if (_decisionTimer > 0) return;
        _decisionTimer = decisionInterval;

        if (_distanceToPlayer <= chaseRange)
        {
            ChangeState(EnemyState.Chase);
            return;
        }

        // 随机移动
        _rb.velocity = new Vector2(_facingDirection * moveSpeed * 0.5f, _rb.velocity.y);
    }

    protected virtual void UpdateChase()
    {
        if (_player == null) return;

        // 朝向玩家
        FacePlayer();

        if (_distanceToPlayer <= attackRange)
        {
            // 进入攻击范围，开始前摇
            ChangeState(EnemyState.Telegraph);
            return;
        }

        if (_distanceToPlayer > chaseRange)
        {
            ChangeState(EnemyState.Idle);
            return;
        }

        // 追击移动
        float dir = _player.position.x > transform.position.x ? 1f : -1f;
        _rb.velocity = new Vector2(dir * moveSpeed, _rb.velocity.y);
    }

    protected virtual void UpdateTelegraph()
    {
        // 前摇期间不移动，播放警示闪烁
        _rb.velocity = Vector2.zero;

        if (_stateTimer <= 0)
        {
            ChangeState(EnemyState.Attack);
        }
    }

    protected virtual void UpdateAttack()
    {
        _rb.velocity = Vector2.zero;

        if (_stateTimer <= 0)
        {
            // 攻击结束，回到追击
            ChangeState(EnemyState.Chase);
        }
    }

    protected virtual void UpdateHurt()
    {
        if (_stateTimer <= 0)
        {
            ChangeState(EnemyState.Chase);
        }
    }

    protected virtual void UpdateStunned()
    {
        if (_stateTimer <= 0)
        {
            ChangeState(EnemyState.Chase);
        }
    }

    // ========== 核心方法 ==========

    /// <summary>切换状态</summary>
    protected virtual void ChangeState(EnemyState newState)
    {
        CurrentState = newState;

        switch (newState)
        {
            case EnemyState.Telegraph:
                _stateTimer = telegraphDuration;
                ShowTelegraph();
                break;
            case EnemyState.Attack:
                _stateTimer = attackDuration;
                OnAttackStart();
                break;
            case EnemyState.Hurt:
                _stateTimer = 0.3f;
                break;
            case EnemyState.Stunned:
                _stateTimer = 1.0f; // 弹反成功后眩晕1秒
                break;
            default:
                _stateTimer = 0f;
                break;
        }
    }

    /// <summary>受到伤害</summary>
    public virtual void TakeDamage(int amount, float knockbackDirX = 0f, float knockbackForce = 5f)
    {
        if (IsDead) return;

        hp = Mathf.Max(0, hp - amount);

        // 受击闪烁
        if (_hitEffect != null) _hitEffect.PlayHitEffect();

        // 击退
        _rb.velocity = Vector2.zero;
        _rb.AddForce(new Vector2(knockbackDirX * knockbackForce, 2f), ForceMode2D.Impulse);

        if (hp <= 0)
        {
            Die();
        }
        else
        {
            ChangeState(EnemyState.Hurt);
        }
    }

    /// <summary>眩晕（被弹反成功后触发）</summary>
    public virtual void Stun(float duration = 1f)
    {
        ChangeState(EnemyState.Stunned);
        _stateTimer = duration;
    }

    /// <summary>死亡</summary>
    protected virtual void Die()
    {
        IsDead = true;
        CurrentState = EnemyState.Die;
        CombatEvents.InvokeEnemyDeath(gameObject);
        OnDeath?.Invoke(this);

        // 掉落经验水晶
        DropExpCrystal();

        // 简单消失
        StartCoroutine(DieFadeCoroutine());
    }

    /// <summary>掉落经验水晶</summary>
    protected virtual void DropExpCrystal()
    {
        if (expValue <= 0) return;

        var crystalObj = new GameObject("ExpCrystal");
        crystalObj.transform.position = transform.position + Vector3.up * 0.5f;

        var crystal = crystalObj.AddComponent<ExpCrystal>();
        crystal.expValue = expValue;
    }

    /// <summary>
    /// 重置状态以复用对象池。
    /// 在 ObjectPool.Get() 返回对象后调用。
    /// </summary>
    public virtual void ResetForPool()
    {
        IsDead = false;
        CurrentState = EnemyState.Idle;
        hp = maxHp;
        _stateTimer = 0f;
        _decisionTimer = 0f;

        // 重置精灵颜色（池中可能保留了淡出后的透明色）
        if (_sprite != null)
        {
            Color c = _sprite.color;
            c.a = 1f;
            _sprite.color = c;
        }

        // 停止所有协程（包括淡出等残留效果）
        StopAllCoroutines();
    }

    /// <summary>面向玩家</summary>
    protected void FacePlayer()
    {
        if (_player == null) return;
        _facingDirection = _player.position.x > transform.position.x ? 1 : -1;
        if (_sprite != null) _sprite.flipX = _facingDirection == -1;
    }

    /// <summary>显示前摇警示</summary>
    protected virtual void ShowTelegraph()
    {
        if (_sprite == null) return;

        // 根据是否可弹反选择闪烁颜色
        Color flashColor = isCurrentAttackParryable ? parryableColor : unparryableColor;
        StartCoroutine(TelegraphFlashCoroutine(flashColor, telegraphDuration));
    }

    /// <summary>
    /// 前摇闪烁协程：3次快闪 + 持续高亮。
    /// 黄色=可弹反，红色=不可弹反——这是"见招拆招"的核心视觉语言。
    /// </summary>
    protected IEnumerator TelegraphFlashCoroutine(Color color, float duration)
    {
        Color original = _sprite.color;
        float flashInterval = duration / 6f; // 3次闪+3次暗

        for (int i = 0; i < 3; i++)
        {
            _sprite.color = color;
            yield return new WaitForSeconds(flashInterval);
            _sprite.color = original;
            yield return new WaitForSeconds(flashInterval);
        }

        // 最后持续高亮直到攻击
        _sprite.color = color;
    }

    /// <summary>攻击开始（子类重写以启用 hitbox）</summary>
    protected virtual void OnAttackStart() { }

    /// <summary>死亡淡出</summary>
    private IEnumerator DieFadeCoroutine()
    {
        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (_sprite != null)
            {
                Color c = _sprite.color;
                c.a = 1f - (elapsed / duration);
                _sprite.color = c;
            }
            yield return null;
        }

        gameObject.SetActive(false);
    }
}
