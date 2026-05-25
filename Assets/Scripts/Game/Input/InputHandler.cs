using UnityEngine;

/// <summary>
/// 键盘输入处理器：将物理按键映射为逻辑输入信号。
/// 阶段1只支持键盘，阶段5会加入 GestureInput，
/// 两者通过 InputMediator 统一输出。
///
/// 按键布局：
/// - WASD / 方向键：移动
/// - J：轻攻击
/// - K：弹反
/// - L：冲刺
/// - I：重击（长按I触发）
/// </summary>
public class InputHandler : MonoBehaviour
{
    [Header("按键绑定")]
    [Tooltip("攻击键")]
    public KeyCode attackKey = KeyCode.J;
    [Tooltip("弹反键")]
    public KeyCode parryKey = KeyCode.K;
    [Tooltip("冲刺键")]
    public KeyCode dashKey = KeyCode.L;
    [Tooltip("重击键")]
    public KeyCode heavyAttackKey = KeyCode.I;

    [Header("重击判定")]
    [Tooltip("长按多久判定为重击（秒）")]
    public float heavyAttackHoldTime = 0.4f;

    // 当前帧的输入状态
    private float _moveInput;
    private bool _attackPressed;
    private bool _parryPressed;
    private bool _dashPressed;
    private bool _heavyAttackPressed;
    private float _heavyAttackHoldTimer;
    private bool _heavyAttackKeyHeld;

    /// <summary>水平移动输入 -1~1</summary>
    public float MoveInput => _moveInput;
    /// <summary>本帧是否按了攻击</summary>
    public bool AttackPressed => _attackPressed;
    /// <summary>本帧是否按了弹反</summary>
    public bool ParryPressed => _parryPressed;
    /// <summary>本帧是否按了冲刺</summary>
    public bool DashPressed => _dashPressed;
    /// <summary>本帧是否触发了重击</summary>
    public bool HeavyAttackPressed => _heavyAttackPressed;

    private void Update()
    {
        // 清除每帧的脉冲信号
        _attackPressed = false;
        _parryPressed = false;
        _dashPressed = false;
        _heavyAttackPressed = false;

        // 移动输入（持续量）
        _moveInput = Input.GetAxisRaw("Horizontal");

        // 攻击（单次触发）
        if (Input.GetKeyDown(attackKey))
        {
            _attackPressed = true;
            _heavyAttackKeyHeld = true;
            _heavyAttackHoldTimer = 0f;
        }

        // 重击（长按判定）
        if (_heavyAttackKeyHeld)
        {
            _heavyAttackHoldTimer += Time.deltaTime;
            if (_heavyAttackHoldTimer >= heavyAttackHoldTime)
            {
                _heavyAttackPressed = true;
                _heavyAttackKeyHeld = false;
            }
        }

        // 松开攻击键：如果没达到重击阈值，视为轻攻击
        if (Input.GetKeyUp(attackKey) && _heavyAttackKeyHeld)
        {
            _heavyAttackKeyHeld = false;
            // 已经在 GetKeyDown 时设了 _attackPressed = true
        }

        // 独立重击键
        if (Input.GetKeyDown(heavyAttackKey))
        {
            _heavyAttackPressed = true;
        }

        // 弹反（单次触发）
        if (Input.GetKeyDown(parryKey))
        {
            _parryPressed = true;
        }

        // 冲刺（单次触发）
        if (Input.GetKeyDown(dashKey))
        {
            _dashPressed = true;
        }
    }
}
