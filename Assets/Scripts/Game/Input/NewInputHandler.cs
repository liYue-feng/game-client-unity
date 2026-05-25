using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 新InputSystem输入处理器：用新的Input System处理物理输入。
/// 保持和老InputHandler相同的接口，PlayerInputBridge可以无缝切换。
///
/// 按键布局：
/// - WASD / 方向键 / 左摇杆：移动
/// - J / 鼠标左键 / 手柄A：轻攻击
/// - K / 空格 / 手柄B：弹反
/// - L / 鼠标右键 / 手柄LT：冲刺
/// - ESC / 手柄Start：暂停
/// - Tab：背包
/// </summary>
public class NewInputHandler : MonoBehaviour, GameInput.IGameplayActions, GameInput.IUIActions
{
    [Header("重击判定")]
    [Tooltip("长按多久判定为重击（秒）")]
    public float heavyAttackHoldTime = 0.4f;

    // 输入资产
    private GameInput _gameInput;

    // 当前帧的输入状态
    private float _moveInput;
    private bool _attackPressed;
    private bool _parryPressed;
    private bool _dashPressed;
    private bool _heavyAttackPressed;
    private bool _pausePressed;
    private bool _inventoryPressed;
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
    /// <summary>本帧是否按了暂停</summary>
    public bool PausePressed => _pausePressed;
    /// <summary>本帧是否按了背包</summary>
    public bool InventoryPressed => _inventoryPressed;
    /// <summary>本帧是否触发了重击</summary>
    public bool HeavyAttackPressed => _heavyAttackPressed;

    private void Awake()
    {
        _gameInput = new GameInput();
        _gameInput.Gameplay.SetCallbacks(this);
        _gameInput.UI.SetCallbacks(this);
    }

    private void OnEnable()
    {
        _gameInput.Enable();
    }

    private void OnDisable()
    {
        _gameInput.Disable();
    }

    private void Update()
    {
        // 清除每帧的脉冲信号
        _attackPressed = false;
        _parryPressed = false;
        _dashPressed = false;
        _heavyAttackPressed = false;
        _pausePressed = false;
        _inventoryPressed = false;

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
    }

    // === Gameplay 动作回调 ===

    public void OnMove(InputAction.CallbackContext context)
    {
        _moveInput = context.ReadValue<Vector2>().x;
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _attackPressed = true;
            _heavyAttackKeyHeld = true;
            _heavyAttackHoldTimer = 0f;
        }
        else if (context.canceled && _heavyAttackKeyHeld)
        {
            _heavyAttackKeyHeld = false;
        }
    }

    public void OnDash(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _dashPressed = true;
        }
    }

    public void OnParry(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _parryPressed = true;
        }
    }

    public void OnPause(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _pausePressed = true;
        }
    }

    public void OnInventory(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _inventoryPressed = true;
        }
    }

    // === UI 动作回调（空实现但接口要求）===

    public void OnNavigate(InputAction.CallbackContext context) { }
    public void OnSubmit(InputAction.CallbackContext context) { }
    public void OnCancel(InputAction.CallbackContext context) { }
}
