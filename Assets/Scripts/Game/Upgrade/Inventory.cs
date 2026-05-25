using System;
using UnityEngine;

/// <summary>
/// 被动道具/技能定义。玩家通过升级3选1获得，存入背包。
/// </summary>
[Serializable]
public class PassiveItem
{
    public string id;              // 唯一ID
    public string displayName;     // 显示名称
    public string description;     // 描述
    public string category;        // 分类: attack/defense/speed/utility/elemental
    public int maxLevel = 5;       // 最大等级
    public int currentLevel;       // 当前等级
    public string flavorText;      // 水墨风格题词

    // 每级加成
    public float attackPerLevel;
    public float maxHpPerLevel;
    public float speedPerLevel;
    public float cooldownPerLevel;
    public float critPerLevel;
    public float lifestealPerLevel;

    /// <summary>是否已满级</summary>
    public bool IsMaxLevel => currentLevel >= maxLevel;

    /// <summary>总攻击力加成</summary>
    public float TotalAttack => attackPerLevel * currentLevel;
    public float TotalMaxHp => maxHpPerLevel * currentLevel;
    public float TotalSpeed => speedPerLevel * currentLevel;
    public float TotalCooldown => cooldownPerLevel * currentLevel;
    public float TotalCrit => critPerLevel * currentLevel;
    public float TotalLifesteal => lifestealPerLevel * currentLevel;

    public PassiveItem Clone()
    {
        return (PassiveItem)MemberwiseClone();
    }
}

/// <summary>
/// 玩家背包：管理已获得的被动道具和统计总加成。
/// 单例模式。
/// </summary>
public class Inventory : MonoBehaviour
{
    private static Inventory _instance;
    public static Inventory Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[Inventory]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<Inventory>();
            }
            return _instance;
        }
    }

    /// <summary>背包最大容量</summary>
    public const int MaxSlots = 12;

    /// <summary>已拥有的道具列表（索引即槽位）</summary>
    public PassiveItem[] Items = new PassiveItem[MaxSlots];

    /// <summary>当前道具数量</summary>
    public int Count { get; private set; }

    /// <summary>背包变化事件</summary>
    public event Action<int, PassiveItem> OnItemChanged; // slotIndex, item

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>添加或升级道具</summary>
    public bool AddOrUpgrade(string id, string displayName, string description,
        string category, int maxLevel = 5, string flavorText = "",
        float attackPer = 0, float hpPer = 0, float speedPer = 0,
        float cooldownPer = 0, float critPer = 0, float lifestealPer = 0)
    {
        // 已有此道具 → 升级
        for (int i = 0; i < Count; i++)
        {
            if (Items[i] != null && Items[i].id == id)
            {
                if (Items[i].IsMaxLevel)
                {
                    Debug.Log($"[Inventory] {displayName} 已满级，无法继续升级");
                    return false;
                }
                Items[i].currentLevel++;
                OnItemChanged?.Invoke(i, Items[i]);
                Debug.Log($"[Inventory] {displayName} 升级到 Lv.{Items[i].currentLevel}/{maxLevel}");
                return true;
            }
        }

        // 新道具
        if (Count >= MaxSlots)
        {
            Debug.Log("[Inventory] 背包已满");
            return false;
        }

        var item = new PassiveItem
        {
            id = id,
            displayName = displayName,
            description = description,
            category = category,
            maxLevel = maxLevel,
            currentLevel = 1,
            flavorText = flavorText,
            attackPerLevel = attackPer,
            maxHpPerLevel = hpPer,
            speedPerLevel = speedPer,
            cooldownPerLevel = cooldownPer,
            critPerLevel = critPer,
            lifestealPerLevel = lifestealPer
        };

        Items[Count] = item;
        OnItemChanged?.Invoke(Count, item);
        Count++;
        Debug.Log($"[Inventory] 获得新道具: {displayName}");
        return true;
    }

    /// <summary>重置背包（重新开始游戏时调用）</summary>
    public void Reset()
    {
        for (int i = 0; i < MaxSlots; i++)
            Items[i] = null;
        Count = 0;
    }

    // ===== 总加成计算 =====

    public float TotalAttackBonus
    {
        get
        {
            float sum = 0;
            for (int i = 0; i < Count; i++)
                if (Items[i] != null) sum += Items[i].TotalAttack;
            return sum;
        }
    }

    public float TotalMaxHpBonus
    {
        get
        {
            float sum = 0;
            for (int i = 0; i < Count; i++)
                if (Items[i] != null) sum += Items[i].TotalMaxHp;
            return sum;
        }
    }

    public float TotalSpeedBonus
    {
        get
        {
            float sum = 0;
            for (int i = 0; i < Count; i++)
                if (Items[i] != null) sum += Items[i].TotalSpeed;
            return sum;
        }
    }

    public float TotalCooldownBonus
    {
        get
        {
            float sum = 0;
            for (int i = 0; i < Count; i++)
                if (Items[i] != null) sum += Items[i].TotalCooldown;
            return sum;
        }
    }

    public float TotalCritBonus
    {
        get
        {
            float sum = 0;
            for (int i = 0; i < Count; i++)
                if (Items[i] != null) sum += Items[i].TotalCrit;
            return sum;
        }
    }

    public float TotalLifestealBonus
    {
        get
        {
            float sum = 0;
            for (int i = 0; i < Count; i++)
                if (Items[i] != null) sum += Items[i].TotalLifesteal;
            return sum;
        }
    }
}