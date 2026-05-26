using UnityEngine;
using System;

/// <summary>
/// 角色属性组件：HP、耐力、经验值、战斗数值。
/// 按《代号·剑》设计文档：一级属性(力/内/体/精/悟) → 二级属性转换。
/// 挂在角色 GameObject 上，由 PlayerStateMachine 和 EnemyBase 读取。
/// 所有数值变化通过事件通知 UI 层，实现数据与显示解耦。
/// </summary>
public class CharacterStats : MonoBehaviour
{
    [Header("一级属性（基础，影响所有二级属性）")]
    public PrimaryAttributes primary = PrimaryAttributes.Default;

    [Header("当前战斗流派")]
    public CombatStyle combatStyle = CombatStyle.Sword;

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
    [Tooltip("基础攻击力（由一级属性计算得出，直接修改仅用于测试）")]
    public int attack = 10;
    [Tooltip("基础移动速度")]
    public float moveSpeed = 5f;
    [Tooltip("基础冲刺速度")]
    public float baseDashSpeed = 15f;
    public float dashSpeed => baseDashSpeed;
    [Tooltip("冲刺持续时间")]
    public float dashDuration = 0.2f;
    [Tooltip("冲刺冷却时间")]
    public float dashCooldown = 0.5f;

    [Header("暴击属性")]
    [Tooltip("暴击伤害加成（百分比，0.5=+50%暴击伤害）")]
    public float critDamageBonus = 0f;
    [Tooltip("道具提供的暴击值加成")]
    public int extraCritValue = 0;

    [Header("伤害减免")]
    [Tooltip("全局伤害减免（0-1）")]
    public float damageReduction = 0f;

    /// <summary>二级属性缓存（由一级属性+等级系数计算）</summary>
    public SecondaryAttributes Secondary { get; private set; }

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
        RecalculateSecondaryStats();
    }

    /// <summary>
    /// 根据一级属性和等级重新计算所有二级属性。
    /// 在一级属性变化或升级后调用。
    /// </summary>
    public void RecalculateSecondaryStats()
    {
        // 从背包获取一级属性加成
        PrimaryAttributes bonus = PrimaryAttributes.Zero;
        if (Inventory.Instance != null)
        {
            bonus = Inventory.Instance.TotalPrimaryBonus;
        }

        PrimaryAttributes total = primary + bonus;
        Secondary = PrimaryAttributeConverter.Convert(total, level);

        // 更新传统字段（向后兼容）
        maxHp = Secondary.maxHp;
        maxStamina = Secondary.maxStamina;
        staminaRegenRate = Secondary.staminaRegen;
        attack = Secondary.GetAtk(combatStyle);

        // HP 和耐力不低于基础值
        currentHp = Mathf.Min(currentHp, maxHp);
        currentStamina = Mathf.Min(currentStamina, maxStamina);

        OnHpChanged?.Invoke(currentHp, maxHp);
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);
    }

    /// <summary>
    /// 增加一级属性（来自装备/道具加成）。
    /// </summary>
    public void AddPrimaryBonus(int str, int inner, int vit, int spi, int comp)
    {
        primary.strength += str;
        primary.innerForce += inner;
        primary.vitality += vit;
        primary.spirit += spi;
        primary.comprehension += comp;
        RecalculateSecondaryStats();
    }

    /// <summary>
    /// 受到 HP 伤害。扣减 HP 并触发事件。
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

    public void RaiseDeathEvent()
    {
        OnDeath?.Invoke();
    }

    /// <summary>
    /// 受到耐力伤害。
    /// </summary>
    public void TakeStaminaDamage(int amount)
    {
        if (IsDead) return;
        currentStamina = Mathf.Max(0, currentStamina - amount);
        _lastStaminaUseTime = Time.time;
        OnStaminaChanged?.Invoke(currentStamina, maxStamina);

        // 耐力归零 → 破防状态（虚弱）
        if (currentStamina <= 0)
        {
            CombatEvents.InvokeStaminaBreak(transform.position);
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
    /// 增加经验值，检查是否升级。升级时重新计算二级属性。
    /// </summary>
    public void AddExp(int amount)
    {
        if (amount <= 0) return;

        currentExp += amount;

        while (currentExp >= ExpToNextLevel)
        {
            currentExp -= ExpToNextLevel;
            level++;
            RecalculateSecondaryStats();
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
