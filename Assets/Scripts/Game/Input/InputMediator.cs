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
    private InputHandler _inputHandler;
    // 阶段5: private GestureInput _gesture;

    /// <summary>水平移动输入 -1~1</summary>
    public float MoveInput { get; private set; }
    /// <summary>本帧是否按了攻击</summary>
    public bool AttackPressed { get; private set; }
    /// <summary>本帧是否按了弹反</summary>
    public bool ParryPressed { get; private set; }
    /// <summary>本帧是否按了冲刺</summary>
    public bool DashPressed { get; private set; }
    /// <summary>本帧是否按了暂停</summary>
    public bool PausePressed { get; private set; }
    /// <summary>本帧是否按了背包</summary>
    public bool InventoryPressed { get; private set; }
    /// <summary>本帧是否按了重击</summary>
    public bool HeavyAttackPressed { get; private set; }

    private void Awake()
    {
        _inputHandler = GetComponent<InputHandler>();
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

        // 使用老输入系统
        if (_inputHandler != null)
        {
            MoveInput = _inputHandler.MoveInput;
            AttackPressed = _inputHandler.AttackPressed;
            ParryPressed = _inputHandler.ParryPressed;
            DashPressed = _inputHandler.DashPressed;
            HeavyAttackPressed = _inputHandler.HeavyAttackPressed;
        }

        // 临时添加ESC和Tab检测 since old InputHandler doesn't have them
        if (Input.GetKeyDown(KeyCode.Escape))
            PausePressed = true;
        if (Input.GetKeyDown(KeyCode.Tab))
            InventoryPressed = true;

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
