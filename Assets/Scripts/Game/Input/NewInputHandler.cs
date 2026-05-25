using UnityEngine;

/// <summary>
/// 新InputSystem输入处理器 - 暂时禁用，使用旧InputHandler instead
/// </summary>
public class NewInputHandler : MonoBehaviour
{
    [Header("重击判定")]
    [Tooltip("长按多久判定为重击（秒）")]
    public float heavyAttackHoldTime = 0.4f;

    // 当前帧的输入状态
    private float _moveInput;
    private bool _attackPressed;
    private bool _parryPressed;
    private bool _dashPressed;
    private bool _heavyAttackPressed;
    private bool _pausePressed;
    private bool _inventoryPressed;

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
}
