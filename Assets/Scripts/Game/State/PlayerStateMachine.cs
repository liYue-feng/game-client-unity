using UnityEngine;
using System.Collections;

/// <summary>
/// 玩家状态机：控制战斗状态转换、连击缓冲、动画取消规则。
/// 这是整个战斗手感的核心——所有攻击、弹反、冲刺的时机和优先级都在这里决定。
///
/// 状态转换规则（动画取消规则）：
/// - Attack1 → Attack2(命中帧后)/Dash/Parry
/// - Attack2 → Attack3(命中帧后)/Dash/Parry
/// - Attack3 → 锁定到恢复帧/Dash(后期)
/// - Dash → Attack1/Parry(冲刺末尾)
/// - Parry → ParrySuccess(成功)/Idle(失败)
/// - ParrySuccess → Attack1/HeavyAttack(反击窗口) + 慢动作效果
/// - Hurt → 锁定
///
/// 连击缓冲：攻击输入在 comboWindow(0.3s) 内会被记住，
/// 在当前攻击动画命中帧后自动触发下一段连击。
///
/// 弹反流程：进入 Parry 状态 → 开启弹反窗口(0.3s) → 敌方攻击命中 →
/// 触发慢动作(0.5s, timeScale=0.2) → 开启反击窗口(0.8s) → 玩家反击
/// </summary>
public class PlayerStateMachine : MonoBehaviour
{
    [Header("状态机参数")]
    [Tooltip("连击输入缓冲窗口（秒）")]
    public float comboWindow = 0.3f;
    [Tooltip("弹反窗口持续时间（秒）")]
    public float parryWindowDuration = 0.3f;
    [Tooltip("弹反成功后的反击窗口（秒）")]
    public float counterWindowDuration = 0.8f;
    [Tooltip("弹反成功慢动作持续时间（真实秒）")]
    public float slowMoDuration = 0.5f;
    [Tooltip("弹反成功慢动作时间缩放")]
    public float slowMoScale = 0.2f;
    [Tooltip("受击硬直持续时间（秒）")]
    public float hurtDuration = 0.3f;
    [Tooltip("各段攻击的基础持续时间（秒），Attack1/2/3/Heavy")]
    public float[] attackDurations = { 0.4f, 0.35f, 0.5f, 0.7f };

    /// <summary>当前战斗状态</summary>
    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;

    /// <summary>状态变化时触发</summary>
    public event System.Action<PlayerState, PlayerState> OnStateChanged;

    // 组件引用
    private CharacterStats _stats;
    private PlayerController _controller;

    // 计时器
    private float _stateTimer;
    private float _comboBufferTimer;
    private bool _comboBuffered;
    private float _dashCooldownTimer;

    // 弹反相关
    private bool _isInParryWindow;
    private bool _isInCounterWindow;
    private float _parryWindowTimer;
    private float _counterWindowTimer;
    private bool _isSlowMoActive;

    // 命中标记（由动画事件/Hitbox 设置）
    private bool _hasHitThisAttack;

    /// <summary>是否处于弹反窗口（弹反姿态激活期间）</summary>
    public bool IsInParryWindow => _isInParryWindow;
    /// <summary>是否处于反击窗口（弹反成功后）</summary>
    public bool IsInCounterWindow => _isInCounterWindow;
    /// <summary>冲刺是否在冷却中</summary>
    public bool IsDashOnCooldown => _dashCooldownTimer > 0;
    /// <summary>当前攻击是否已命中（用于连击判定）</summary>
    public bool HasHitThisAttack => _hasHitThisAttack;

    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
        _controller = GetComponent<PlayerController>();
    }

    private void Update()
    {
        _stateTimer -= Time.deltaTime;

        // 通用计时器衰减
        if (_comboBufferTimer > 0)
        {
            _comboBufferTimer -= Time.deltaTime;
            if (_comboBufferTimer <= 0) _comboBuffered = false;
        }
        if (_dashCooldownTimer > 0) _dashCooldownTimer -= Time.deltaTime;

        // 弹反窗口计时
        if (_isInParryWindow)
        {
            _parryWindowTimer -= Time.deltaTime;
            if (_parryWindowTimer <= 0)
            {
                _isInParryWindow = false;
                // 弹反窗口结束，未命中 → 回到 Idle
                if (CurrentState == PlayerState.Parry)
                {
                    ChangeState(PlayerState.Idle);
                }
            }
        }

        // 反击窗口计时
        if (_isInCounterWindow)
        {
            _counterWindowTimer -= Time.deltaTime;
            if (_counterWindowTimer <= 0)
            {
                _isInCounterWindow = false;
            }
        }

        // 各状态的持续逻辑
        switch (CurrentState)
        {
            case PlayerState.Attack1:
            case PlayerState.Attack2:
            case PlayerState.Attack3:
            case PlayerState.HeavyAttack:
                UpdateAttackState();
                break;
            case PlayerState.Parry:
                // 弹反窗口逻辑已在上面的计时器处理
                break;
            case PlayerState.ParrySuccess:
                // 反击窗口内不操作就回 Idle
                if (!_isInCounterWindow)
                {
                    ChangeState(PlayerState.Idle);
                }
                break;
            case PlayerState.Dash:
                UpdateDashState();
                break;
            case PlayerState.Hurt:
                if (_stateTimer <= 0) ChangeState(PlayerState.Idle);
                break;
            case PlayerState.Die:
                // 死亡状态不可转换
                break;
        }
    }

    /// <summary>
    /// 耐力回复。在非消耗耐力的状态下每帧调用。
    /// </summary>
    private void LateUpdate()
    {
        // 冲刺和重击消耗耐力时暂停回复，其余状态都回复
        if (CurrentState != PlayerState.Dash && CurrentState != PlayerState.Die)
        {
            _stats.RegenStamina();
        }
    }

    // ==================== 状态转换请求 ====================

    /// <summary>请求移动。只在可取消状态下生效。</summary>
    public void RequestMove()
    {
        if (CanCancelTo(PlayerState.Run))
        {
            ChangeState(PlayerState.Run);
        }
    }

    /// <summary>请求停止移动。</summary>
    public void RequestStop()
    {
        if (CurrentState == PlayerState.Run)
        {
            ChangeState(PlayerState.Idle);
        }
    }

    /// <summary>请求轻攻击。可能触发连击缓冲。</summary>
    public void RequestAttack()
    {
        // 反击窗口中，轻攻击优先
        if (_isInCounterWindow)
        {
            ChangeState(PlayerState.Attack1);
            return;
        }

        // 连击缓冲：在攻击中按攻击，记住输入
        if (IsInAttackState())
        {
            _comboBuffered = true;
            _comboBufferTimer = comboWindow;
            return;
        }

        // 可取消状态下直接攻击
        if (CanCancelTo(PlayerState.Attack1))
        {
            ChangeState(PlayerState.Attack1);
        }
    }

    /// <summary>请求重击。消耗耐力。</summary>
    public void RequestHeavyAttack()
    {
        if (!_stats.TryUseStamina(30)) return;

        // 反击窗口中，重击优先
        if (_isInCounterWindow)
        {
            ChangeState(PlayerState.HeavyAttack);
            return;
        }

        if (CanCancelTo(PlayerState.HeavyAttack))
        {
            ChangeState(PlayerState.HeavyAttack);
        }
    }

    /// <summary>请求冲刺。消耗耐力，有冷却。</summary>
    public void RequestDash()
    {
        if (IsDashOnCooldown) return;
        if (!_stats.TryUseStamina(25)) return;

        if (CanCancelTo(PlayerState.Dash))
        {
            AudioManager.Instance.PlaySFX("dash");
            ChangeState(PlayerState.Dash);
        }
    }

    /// <summary>请求弹反。消耗少量耐力。</summary>
    public void RequestParry()
    {
        if (!_stats.TryUseStamina(15)) return;

        if (CanCancelTo(PlayerState.Parry))
        {
            ChangeState(PlayerState.Parry);
        }
    }

    /// <summary>受到伤害，强制进入受击状态。</summary>
    public void ForceHurt()
    {
        if (CurrentState == PlayerState.Die) return;
        ChangeState(PlayerState.Hurt);
    }

    /// <summary>强制死亡。</summary>
    public void ForceDie()
    {
        ChangeState(PlayerState.Die);
    }

    /// <summary>
    /// 弹反成功时调用。由 Hurtbox 在弹反窗口内检测到可弹反攻击时触发。
    /// 进入慢动作 → 反击窗口。
    /// </summary>
    public void OnParrySuccess()
    {
        _isInParryWindow = false;
        ChangeState(PlayerState.ParrySuccess);
        CombatEvents.InvokeParrySuccess(transform.position);

        // 启动慢动作协程（结束时自动开启反击窗口）
        if (!_isSlowMoActive)
        {
            StartCoroutine(SlowMoCoroutine());
        }
    }

    /// <summary>
    /// 慢动作协程：降低 timeScale，使用真实时间计时后恢复。
    /// 恢复后自动开启反击窗口。
    /// </summary>
    private IEnumerator SlowMoCoroutine()
    {
        _isSlowMoActive = true;
        Time.timeScale = slowMoScale;

        yield return new WaitForSecondsRealtime(slowMoDuration);

        Time.timeScale = 1f;
        _isSlowMoActive = false;

        // 慢动作结束，开启反击窗口
        _isInCounterWindow = true;
        _counterWindowTimer = counterWindowDuration;
    }

    /// <summary>
    /// 安全恢复 timeScale：防止场景切换或角色死亡时卡在慢动作。
    /// </summary>
    private void OnDisable()
    {
        if (_isSlowMoActive)
        {
            Time.timeScale = 1f;
            _isSlowMoActive = false;
        }
    }

    /// <summary>
    /// 标记当前攻击已命中。由 Hitbox 在命中敌人时调用。
    /// </summary>
    public void MarkHit()
    {
        _hasHitThisAttack = true;
    }

    // ==================== 内部逻辑 ====================

    private void ChangeState(PlayerState newState)
    {
        PlayerState oldState = CurrentState;
        if (oldState == newState) return;

        // 退出旧状态
        OnExitState(oldState);

        CurrentState = newState;
        _stateTimer = GetStateDuration(newState);
        _hasHitThisAttack = false;

        // 进入新状态
        OnEnterState(newState);

        OnStateChanged?.Invoke(oldState, newState);
    }

    private void OnEnterState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Parry:
                _isInParryWindow = true;
                _parryWindowTimer = parryWindowDuration;
                break;
            case PlayerState.Dash:
                _dashCooldownTimer = _stats.dashCooldown;
                break;
            case PlayerState.Die:
                _stats.RaiseDeathEvent();
                break;
        }
    }

    private void OnExitState(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.Parry:
                _isInParryWindow = false;
                break;
        }
    }

    /// <summary>
    /// 判断从当前状态是否可以转换到目标状态。
    /// 这是动画取消规则表的核心。
    /// </summary>
    private bool CanCancelTo(PlayerState target)
    {
        // 死亡和受击不可取消
        if (CurrentState == PlayerState.Die || CurrentState == PlayerState.Hurt)
            return false;

        // 任何状态都可以进入 Die 和 Hurt（强制）
        if (target == PlayerState.Die || target == PlayerState.Hurt)
            return true;

        switch (CurrentState)
        {
            case PlayerState.Idle:
            case PlayerState.Run:
                // 空闲/移动可转入任何动作
                return true;

            case PlayerState.Attack1:
            case PlayerState.Attack2:
                // 攻击1/2：命中后可连击，随时可弹反/冲刺
                if (target == PlayerState.Attack1) return _hasHitThisAttack;
                if (target == PlayerState.Parry) return true;
                if (target == PlayerState.Dash) return true;
                // 连击需要命中后（通过 RequestAttack 的缓冲机制处理）
                return false;

            case PlayerState.Attack3:
                // 攻击3终结技：锁定到恢复帧，只允许冲刺（后期）
                if (target == PlayerState.Dash && _stateTimer <= 0.1f) return true;
                return false;

            case PlayerState.HeavyAttack:
                // 重击锁定到恢复帧
                return target == PlayerState.Dash && _stateTimer <= 0.1f;

            case PlayerState.Parry:
                // 弹反中可转入弹反成功
                return target == PlayerState.ParrySuccess;

            case PlayerState.ParrySuccess:
                // 反击窗口可攻击
                return target == PlayerState.Attack1 || target == PlayerState.HeavyAttack;

            case PlayerState.Dash:
                // 冲刺末尾可攻击或弹反
                if (_stateTimer <= 0.05f)
                    return target == PlayerState.Attack1 || target == PlayerState.Parry;
                return false;

            default:
                return false;
        }
    }

    private void UpdateAttackState()
    {
        if (_stateTimer > 0) return;

        // 攻击动画结束
        if (_comboBuffered)
        {
            _comboBuffered = false;
            // 尝试连击
            PlayerState nextAttack = GetNextComboAttack();
            if (nextAttack != CurrentState)
            {
                ChangeState(nextAttack);
                return;
            }
        }

        ChangeState(PlayerState.Idle);
    }

    private void UpdateDashState()
    {
        if (_stateTimer <= 0)
        {
            ChangeState(PlayerState.Idle);
        }
    }

    /// <summary>获取当前连击的下一阶段攻击</summary>
    private PlayerState GetNextComboAttack()
    {
        return CurrentState switch
        {
            PlayerState.Attack1 => PlayerState.Attack2,
            PlayerState.Attack2 => PlayerState.Attack3,
            _ => PlayerState.Attack1
        };
    }

    /// <summary>判断是否处于攻击状态</summary>
    private bool IsInAttackState()
    {
        return CurrentState == PlayerState.Attack1
            || CurrentState == PlayerState.Attack2
            || CurrentState == PlayerState.Attack3
            || CurrentState == PlayerState.HeavyAttack;
    }

    /// <summary>获取各状态的持续时间</summary>
    private float GetStateDuration(PlayerState state)
    {
        return state switch
        {
            PlayerState.Attack1 => attackDurations[0],
            PlayerState.Attack2 => attackDurations[1],
            PlayerState.Attack3 => attackDurations[2],
            PlayerState.HeavyAttack => attackDurations[3],
            PlayerState.Dash => _stats.dashDuration,
            PlayerState.Hurt => hurtDuration,
            _ => 0f
        };
    }
}
