using UnityEngine;

/// <summary>
/// 玩家输入桥梁：将 InputMediator 的输入信号转发给 PlayerStateMachine。
/// 为什么不直接在 StateMachine 里读输入：单一职责。
/// StateMachine 只管状态逻辑，不关心输入来源。
/// 这个桥梁只管"翻译"：输入 → 状态请求。
/// </summary>
[RequireComponent(typeof(InputMediator))]
[RequireComponent(typeof(PlayerStateMachine))]
public class PlayerInputBridge : MonoBehaviour
{
    private InputMediator _input;
    private PlayerStateMachine _stateMachine;

    private void Awake()
    {
        _input = GetComponent<InputMediator>();
        _stateMachine = GetComponent<PlayerStateMachine>();
    }

    private void Update()
    {
        if (_stateMachine.CurrentState == PlayerState.Die) return;

        // 攻击（优先级：重击 > 轻攻击）
        if (_input.HeavyAttackPressed)
        {
            _stateMachine.RequestHeavyAttack();
        }
        else if (_input.AttackPressed)
        {
            _stateMachine.RequestAttack();
        }

        // 弹反
        if (_input.ParryPressed)
        {
            _stateMachine.RequestParry();
        }

        // 冲刺
        if (_input.DashPressed)
        {
            _stateMachine.RequestDash();
        }
    }
}
