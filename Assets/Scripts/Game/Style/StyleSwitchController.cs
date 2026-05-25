using UnityEngine;

/// <summary>
/// 流派切换控制器：处理键盘1-5的流派切换输入。
/// 切换规则：
/// - 可在战斗中切换
/// - Hurt/Die/Dash期间不可切换
/// - 切换消耗0.3s（可中断当前连击）
/// </summary>
public class StyleSwitchController : MonoBehaviour
{
    [Tooltip="切换按键：1-5对应5种流派")]
    public KeyCode[] switchKeys = new KeyCode[]
    {
        KeyCode.Alpha1, // Blade
        KeyCode.Alpha2, // Seal
        KeyCode.Alpha3, // Poison
        KeyCode.Alpha4, // Blood
        KeyCode.Alpha5  // Sword
    };

    private PlayerStateMachine _stateMachine;

    private void Awake()
    {
        _stateMachine = GetComponent<PlayerStateMachine>();
    }

    private void Update()
    {
        if (_stateMachine == null) return;

        // Hurt/Die/Dash期间不可切换
        var state = _stateMachine.CurrentState;
        if (state == PlayerState.Hurt || state == PlayerState.Die || state == PlayerState.Dash)
            return;

        for (int i = 0; i < switchKeys.Length; i++)
        {
            if (Input.GetKeyDown(switchKeys[i]))
            {
                CombatStyleID targetStyle = (CombatStyleID)(i + 1);
                StyleManager.Instance.SwitchStyle(targetStyle);
                break;
            }
        }
    }
}
