using UnityEngine;

/// <summary>
/// 输入中介器：统一键盘/新手柄/手势输入到同一接口。
/// PlayerStateMachine 和 PlayerController 只从这里读取输入，
/// 不关心输入来自哪里。
///
/// 自动检测：优先用 NewInputHandler（新InputSystem），
/// 如果没有则回退到老 InputHandler。
/// </summary>
public class InputMediator : MonoBehaviour
{
    private InputHandler _oldHandler;
    private NewInputHandler _newHandler;
    // 阶段5: private GestureInput _gesture;

    /// <summary>水平移动输入 -1~1</summary>
    public float MoveInput { get; private set; }
    /// <summary>本帧是否按了攻击</summary>
    public bool AttackPressed { get; private set; }
    /// <summary>本帧是否按了弹反</summary>
    public bool ParryPressed { get; private set; }
    /// <summary>本帧是否按了冲刺</summary>
    public bool DashPressed { get; private set; }
    /// <summary>本帧是否按了暂停（仅新输入系统）</summary>
    public bool PausePressed { get; private set; }
    /// <summary>本帧是否按了背包（仅新输入系统）</summary>
    public bool InventoryPressed { get; private set; }
    /// <summary>本帧是否按了重击</summary>
    public bool HeavyAttackPressed { get; private set; }

    private void Awake()
    {
        // 优先找新输入系统
        _newHandler = GetComponent<NewInputHandler>();
        if (_newHandler == null)
        {
            // 没有的话回退到老系统
            _oldHandler = GetComponent<InputHandler>();
        }
        // 阶段5: _gesture = GetComponent<GestureInput>();
    }

    private void Update()
    {
        // 重置所有脉冲信号
        MoveInput = 0f;
        AttackPressed = false;
        ParryPressed = false;
        DashPressed = false;
        PausePressed = false;
        InventoryPressed = false;
        HeavyAttackPressed = false;

        // 优先用新输入系统
        if (_newHandler != null)
        {
            MoveInput = _newHandler.MoveInput;
            AttackPressed = _newHandler.AttackPressed;
            ParryPressed = _newHandler.ParryPressed;
            DashPressed = _newHandler.DashPressed;
            PausePressed = _newHandler.PausePressed;
            InventoryPressed = _newHandler.InventoryPressed;
            HeavyAttackPressed = _newHandler.HeavyAttackPressed;
        }
        // 回退到老系统
        else if (_oldHandler != null)
        {
            MoveInput = _oldHandler.MoveInput;
            AttackPressed = _oldHandler.AttackPressed;
            ParryPressed = _oldHandler.ParryPressed;
            DashPressed = _oldHandler.DashPressed;
            HeavyAttackPressed = _oldHandler.HeavyAttackPressed;
        }

        // 阶段5 加入手势时取消注释：
        // if (_gesture != null)
        // {
        //     if (_gesture.AttackPressed) AttackPressed = true;
        //     if (_gesture.ParryPressed) ParryPressed = true;
        //     if (_gesture.DashPressed) DashPressed = true;
        //     if (_gesture.HeavyAttackPressed) HeavyAttackPressed = true;
        // }
    }
}
