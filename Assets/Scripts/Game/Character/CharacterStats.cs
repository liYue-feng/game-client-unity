using UnityEngine;
using System;

/// <summary>
/// 角色属性组件：HP、耐力、经验值、基础战斗数值。
/// 挂在角色 GameObject 上，由 PlayerStateMachine 和 EnemyBase 读取。
/// 所有数值变化通过事件通知 UI 层，实现数据与显示解耦。
/// </summary>
public class CharacterStats : MonoBehaviour
{
    [Header("生命值")]
    [Tooltip("最大生命值")]
    public int maxHp = 100;
    [Tooltip("当前生命值，运行时由代码修改")]
    public int currentHp;

    [Header("耐力")]
    [Tooltip("最大耐力值")]
    public int maxStamina = 100;
    [Tooltip("当前耐力值")]
    public int currentStamina;
    [Tooltip("耐力回复速率（点/秒）")]
    public float staminaRegenRate = 20f;
    [Tooltip("耐力使用后延迟回复时间（秒）")]
    public float staminaRegenDelay = 1.0f;

    [Header("经验值与等级")]
    [Tooltip("当前等级")]
    public int level = 1;
    [Tooltip("当前经验值")]
    public int currentExp;
    [Tooltip("升级所需基础经验")]
    public int baseExpToLevel = 10;
    [Tooltip("每级经验增量系数")]
    public float expGrowthRate = 1.5f;

    [Header("基础战斗数值")]
    [Tooltip("基础移动速度")]
    public float moveSpeed = 5f;
    [Tooltip("基础攻击力")]
    public int attack = 10;
    [Tooltip("基础冲刺速度")]
    public float baseDashSpeed = 15f;
    public float dashSpeed => baseDashSpeed; // 兼容旧代码
    [Tooltip("冲刺持续时间")]
    public float dashDuration = 0.2f;
    [Tooltip("冲刺冷却时间")]
    public float dashCooldown = 0.5f;

    /// <summary>HP 变化时触发，参数为 (currentHp, maxHp)</summary>
    public event Action<int, int> OnHpChanged;
    /// <summary>耐力变化时触发，参数为 (currentStamina, maxStamina)</summary>
    public event Action<int, int> OnStaminaChanged;
    /// <summary>角色死亡时触发</summary>
    public event Action OnDeath;
    /// <summary>经验值变化时触发，参数为 (currentExp, expToNextLevel)</summary>
    public event Action<int, int> OnExpChanged;
    /// <summary>升级时触发，参数为 (newLevel)</summary>
    public event Action<int> OnLevelUp;

    /// <summary>耐力最后使用时间戳，用于延迟回复计算</summary>
    private float _lastStaminaUseTime;

    /// <summary>是否已死亡</summary>
    public bool IsDead => currentHp <= 0;

    /// <summary>获取当前等级升到下一级所需经验</summary>
    public int ExpToNextLevel => Mathf.RoundToInt(baseExpToLevel * Mathf.Pow(expGrowthRate, level - 1));

    private void Awake()
    {
        currentHp = maxHp;
        currentStamina = maxStamina;
        currentExp = 0;
    }

    /// <summary>
    /// 受到伤害。扣减 HP 并触发事件。
    /// 不会让 HP 降到 0 以下。
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        currentHp = Mathf.Max(0, currentHp - amount);
        OnHpChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0)
        {
            OnDeath?.Invoke();
        }
    }

    /// <summary>
    /// 恢复生命值。不会超过 maxHp。
    /// </summary>
    public void Heal(int amount)
    {
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    /// <summary>
    /// 尝试消耗耐力。如果不足返回 false，不扣减。
    /// 成功时扣减并重置回复延迟计时器。
    /// </summary>
    public bool TryUseStamina(int cost)
    {
        if (currentStamina < cost) return false;
        currentStamina -= cost;
        _lastStaminaUseTime = Time.time;
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        return true;
    }

    /// <summary>
    /// 强制消耗耐力，即使不足也扣到 0。
    /// 用于必须执行的消耗（如受击时）。
    /// </summary>
    public void ForceUseStamina(int cost)
    {
        currentStamina = Mathf.Max(0, currentStamina - cost);
        _lastStaminaUseTime = Time.time;
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    /// <summary>
    /// 每帧回复耐力。仅在延迟时间过后才回复。
    /// </summary>
    public void RegenStamina()
    {
        if (IsDead) return;
        if (currentStamina >= maxStamina) return;

        // 还在延迟期内，不回复
        if (Time.time - _lastStaminaUseTime < staminaRegenDelay) return;

        int regenAmount = Mathf.RoundToInt(staminaRegenRate * Time.deltaTime);
        if (regenAmount <= 0) return;

        int oldStamina = currentStamina;
        currentStamina = Mathf.Min(maxStamina, currentStamina + regenAmount);
        if (currentStamina != oldStamina)
        {
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }
    }

    /// <summary>
    /// 增加经验值，检查是否升级。
    /// </summary>
    public void AddExp(int amount)
    {
        if (amount <= 0) return;

        currentExp += amount;

        // 检查是否升级（支持连升多级）
        while (currentExp >= ExpToNextLevel)
        {
            currentExp -= ExpToNextLevel;
            level++;
            AudioManager.Instance.PlaySFX("levelup");
            OnLevelUp?.Invoke(level);
        }

        OnExpChanged?.Invoke(currentExp, ExpToNextLevel);
    }

    /// <summary>更新移动速度（调用Inventory总加成）</summary>
    public void UpdateMoveSpeed()
    {
        float bonus = Inventory.Instance != null ? Inventory.Instance.TotalSpeedBonus : 0f;
        moveSpeed = 5f + bonus;
    }
}
