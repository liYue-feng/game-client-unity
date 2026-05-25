using UnityEngine;

/// <summary>
/// 输入中介器：统一键盘和手势输入到同一接口。
/// PlayerStateMachine 和 PlayerController 只从这里读取输入，
/// 不关心输入来自键盘还是触摸屏。
///
/// 阶段1只接入 InputHandler（键盘），阶段5加入 GestureInput（触摸）。
/// </summary>
public class InputMediator : MonoBehaviour
{
    private InputHandler _keyboard;
    // 阶段5: private GestureInput _gesture;

    /// <summary>水平移动输入 -1~1</summary>
    public float MoveInput { get; private set; }
    /// <summary>本帧是否按了攻击</summary>
    public bool AttackPressed { get; private set; }
    /// <summary>本帧是否按了弹反</summary>
    public bool ParryPressed { get; private set; }
    /// <summary>本帧是否按了冲刺</summary>
    public bool DashPressed { get; private set; }
    /// <summary>本帧是否按了重击</summary>
    public bool HeavyAttackPressed { get; private set; }

    private void Awake()
    {
        _keyboard = GetComponent<InputHandler>();
        // 阶段5: _gesture = GetComponent<GestureInput>();
    }

    private void Update()
    {
        // 优先级：手势 > 键盘（移动端手势覆盖键盘）
        // 阶段1：只读键盘
        MoveInput = _keyboard.MoveInput;
        AttackPressed = _keyboard.AttackPressed;
        ParryPressed = _keyboard.ParryPressed;
        DashPressed = _keyboard.DashPressed;
        HeavyAttackPressed = _keyboard.HeavyAttackPressed;

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
