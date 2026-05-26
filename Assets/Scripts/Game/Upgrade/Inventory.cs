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
    public float luckPerLevel;
    public float expPerLevel;
    // 一级属性每级加成
    public int strPerLevel;
    public int innerPerLevel;
    public int vitPerLevel;
    public int spiPerLevel;
    public int compPerLevel;

    /// <summary>是否已满级</summary>
    public bool IsMaxLevel => currentLevel >= maxLevel;

    /// <summary>总攻击力加成</summary>
    public float TotalAttack => attackPerLevel * currentLevel;
    public float TotalMaxHp => maxHpPerLevel * currentLevel;
    public float TotalSpeed => speedPerLevel * currentLevel;
    public float TotalCooldown => cooldownPerLevel * currentLevel;
    public float TotalCrit => critPerLevel * currentLevel;
    public float TotalLifesteal => lifestealPerLevel * currentLevel;
    public float TotalLuck => luckPerLevel * currentLevel;
    public float TotalExp => expPerLevel * currentLevel;

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

    /// <summary>武器库存：weaponBehaviourId -> 当前等级</summary>
    private System.Collections.Generic.Dictionary<string, int> _weaponInventory =
        new System.Collections.Generic.Dictionary<string, int>();

    /// <summary>武器最大槽位</summary>
    public const int MaxWeapons = 6;

    /// <summary>背包变化事件</summary>
    public event Action<int, PassiveItem> OnItemChanged; // slotIndex, item

    /// <summary>武器变化事件 (weaponId, level)</summary>
    public event Action<string, int> OnWeaponChanged;

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
        float cooldownPer = 0, float critPer = 0, float lifestealPer = 0,
        float luckPer = 0, float expPer = 0,
        int strPer = 0, int innerPer = 0, int vitPer = 0, int spiPer = 0, int compPer = 0)
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
                // 同步更新一级属性（新参数可能 > 0）
                if (strPer > 0) Items[i].strPerLevel = strPer;
                if (innerPer > 0) Items[i].innerPerLevel = innerPer;
                if (vitPer > 0) Items[i].vitPerLevel = vitPer;
                if (spiPer > 0) Items[i].spiPerLevel = spiPer;
                if (compPer > 0) Items[i].compPerLevel = compPer;
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
            lifestealPerLevel = lifestealPer,
            luckPerLevel = luckPer,
            expPerLevel = expPer,
            strPerLevel = strPer,
            innerPerLevel = innerPer,
            vitPerLevel = vitPer,
            spiPerLevel = spiPer,
            compPerLevel = compPer
        };

        Items[Count] = item;
        OnItemChanged?.Invoke(Count, item);
        Count++;
        Debug.Log($"[Inventory] 获得新道具: {displayName}");
        return true;
    }

    /// <summary>添加或升级自动武器，返回当前等级</summary>
    public int AddWeapon(string weaponId, string displayName, int maxLevel = 5)
    {
        if (_weaponInventory.TryGetValue(weaponId, out int currentLevel))
        {
            if (currentLevel >= maxLevel) return currentLevel;
            currentLevel++;
        }
        else
        {
            if (_weaponInventory.Count >= MaxWeapons)
            {
                Debug.Log("[Inventory] 武器槽已满");
                return 0;
            }
            currentLevel = 1;
        }
        _weaponInventory[weaponId] = currentLevel;
        OnWeaponChanged?.Invoke(weaponId, currentLevel);
        Debug.Log($"[Inventory] 武器 {displayName}({weaponId}) Lv.{currentLevel}");
        return currentLevel;
    }

    /// <summary>是否拥有某武器</summary>
    public bool HasWeapon(string weaponId) => _weaponInventory.ContainsKey(weaponId);

    /// <summary>获取武器等级</summary>
    public int GetWeaponLevel(string weaponId) =>
        _weaponInventory.TryGetValue(weaponId, out int lv) ? lv : 0;

    /// <summary>重置背包（重新开始游戏时调用）</summary>
    public void Reset()
    {
        for (int i = 0; i < MaxSlots; i++)
            Items[i] = null;
        Count = 0;
        _weaponInventory.Clear();
    }

    /// <summary>总运气加成（影响第4槽位出现概率）</summary>
    public float TotalLuckBonus
    {
        get
        {
            float sum = 0;
            for (int i = 0; i < Count; i++)
                if (Items[i] != null) sum += Items[i].TotalLuck;
            return sum;
        }
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

    /// <summary>总经验加成（百分比：15 表示 +15%）</summary>
    public float TotalExpBonus
    {
        get
        {
            float sum = 0;
            for (int i = 0; i < Count; i++)
                if (Items[i] != null) sum += Items[i].TotalExp;
            return sum;
        }
    }

    /// <summary>总一级属性加成（来自所有被动道具）</summary>
    public PrimaryAttributes TotalPrimaryBonus
    {
        get
        {
            PrimaryAttributes sum = PrimaryAttributes.Zero;
            for (int i = 0; i < Count; i++)
            {
                if (Items[i] != null)
                {
                    sum.strength += Items[i].strPerLevel * Items[i].currentLevel;
                    sum.innerForce += Items[i].innerPerLevel * Items[i].currentLevel;
                    sum.vitality += Items[i].vitPerLevel * Items[i].currentLevel;
                    sum.spirit += Items[i].spiPerLevel * Items[i].currentLevel;
                    sum.comprehension += Items[i].compPerLevel * Items[i].currentLevel;
                }
            }
            return sum;
        }
    }
}