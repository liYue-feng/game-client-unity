using UnityEngine;

/// <summary>
/// 耐力控制器：管理耐力消耗常量和回复逻辑。
/// 实际消耗通过 CharacterStats.TryUseStamina 执行，
/// 这里定义各动作的耐力消耗数值，供 PlayerStateMachine 参考。
/// </summary>
public class StaminaController : MonoBehaviour
{
    [Header("耐力消耗")]
    [Tooltip("冲刺消耗")]
    public int dashCost = 25;
    [Tooltip("重击消耗")]
    public int heavyAttackCost = 30;
    [Tooltip("弹反消耗")]
    public int parryCost = 15;

    private CharacterStats _stats;

    private void Awake()
    {
        _stats = GetComponent<CharacterStats>();
    }

    /// <summary>检查并消耗冲刺所需耐力</summary>
    public bool TryDash()
    {
        return _stats.TryUseStamina(dashCost);
    }

    /// <summary>检查并消耗重击所需耐力</summary>
    public bool TryHeavyAttack()
    {
        return _stats.TryUseStamina(heavyAttackCost);
    }

    /// <summary>检查并消耗弹反所需耐力</summary>
    public bool TryParry()
    {
        return _stats.TryUseStamina(parryCost);
    }
}
