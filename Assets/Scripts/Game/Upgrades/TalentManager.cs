using System;
using System.Collections.Generic;

/// <summary>
/// 天赋节点定义
/// </summary>
[Serializable]
public class TalentNode
{
    public string id;                // 唯一ID
    public string displayName;       // 显示名称
    public string description;       // 描述
    public string branch;            // 分支: sword/shield/speed/wisdom
    public int maxLevel = 5;         // 最大等级
    public int currentLevel;         // 当前等级
    public int costPerLevel = 1;     // 每级消耗天赋点
    public List<string> prerequisites = new List<string>(); // 前置天赋ID

    // 每级加成
    public float attackPerLevel;
    public float maxHpPerLevel;
    public float moveSpeedPerLevel;
    public float expGainPerLevel;
    public float critChancePerLevel;
    public float lifestealPerLevel;
    public float cooldownPerLevel;
    public float startingLevelPerLevel;  // 开局等级加成

    public bool IsMaxLevel => currentLevel >= maxLevel;
    public bool IsUnlocked => currentLevel > 0;

    public float TotalAttack => attackPerLevel * currentLevel;
    public float TotalMaxHp => maxHpPerLevel * currentLevel;
    public float TotalMoveSpeed => moveSpeedPerLevel * currentLevel;
    public float TotalExpGain => expGainPerLevel * currentLevel;
    public float TotalCritChance => critChancePerLevel * currentLevel;
    public float TotalLifesteal => lifestealPerLevel * currentLevel;
    public float TotalCooldown => cooldownPerLevel * currentLevel;
    public int TotalStartingLevel => Mathf.RoundToInt(startingLevelPerLevel * currentLevel);
}

/// <summary>
/// 天赋管理器：局外长线成长系统。
/// 天赋点通过游戏结算获取（等级越高得越多）。
/// 天赋加成在进入战斗时应用到 CharacterStats。
/// 数据持久化到 PlayerPrefs（后续可迁移到存档系统）。
///
/// 四个分支:
///   sword(剑) — 攻击/暴击
///   shield(盾) — 生命/吸血
///   speed(风) — 速度/冷却
///   wisdom(智) — 经验/开局等级
/// </summary>
public class TalentManager : MonoBehaviour
{
    private static TalentManager _instance;
    public static TalentManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[TalentManager]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<TalentManager>();
            }
            return _instance;
        }
    }

    public List<TalentNode> AllTalents = new List<TalentNode>();

    /// <summary>可用天赋点</summary>
    public int AvailablePoints { get; private set; }

    /// <summary>已花费天赋点总数</summary>
    public int TotalSpentPoints { get; private set; }

    public event Action OnTalentChanged;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeTalents();
        LoadFromPrefs();
    }

    void InitializeTalents()
    {
        // === 剑分支 (攻击) ===
        AllTalents.Add(new TalentNode
        {
            id = "sword_edge", displayName = "剑锋", description = "攻击力 +3",
            branch = "sword", maxLevel = 5, attackPerLevel = 3
        });
        AllTalents.Add(new TalentNode
        {
            id = "sword_momentum", displayName = "剑势", description = "攻击力 +5，暴击率 +1%",
            branch = "sword", maxLevel = 5, attackPerLevel = 5, critChancePerLevel = 1,
            prerequisites = new List<string> { "sword_edge" }
        });
        AllTalents.Add(new TalentNode
        {
            id = "sword_mastery", displayName = "剑道", description = "攻击力 +8，暴击率 +3%",
            branch = "sword", maxLevel = 3, attackPerLevel = 8, critChancePerLevel = 3,
            prerequisites = new List<string> { "sword_momentum" }
        });
        AllTalents.Add(new TalentNode
        {
            id = "sword_blood", displayName = "饮血", description = "攻击力 +4，吸血 +2%",
            branch = "sword", maxLevel = 5, attackPerLevel = 4, lifestealPerLevel = 2,
            prerequisites = new List<string> { "sword_edge" }
        });

        // === 盾分支 (防御) ===
        AllTalents.Add(new TalentNode
        {
            id = "shield_body", displayName = "锻体", description = "最大生命 +15",
            branch = "shield", maxLevel = 5, maxHpPerLevel = 15
        });
        AllTalents.Add(new TalentNode
        {
            id = "shield_fortress", displayName = "铁壁", description = "最大生命 +25",
            branch = "shield", maxLevel = 5, maxHpPerLevel = 25,
            prerequisites = new List<string> { "shield_body" }
        });
        AllTalents.Add(new TalentNode
        {
            id = "shield_undying", displayName = "不死", description = "生命 +30，吸血 +2%",
            branch = "shield", maxLevel = 3, maxHpPerLevel = 30, lifestealPerLevel = 2,
            prerequisites = new List<string> { "shield_fortress" }
        });
        AllTalents.Add(new TalentNode
        {
            id = "shield_regen", displayName = "回生", description = "生命 +10，每级额外 +0.5% 吸血",
            branch = "shield", maxLevel = 5, maxHpPerLevel = 10, lifestealPerLevel = 0.5f,
            prerequisites = new List<string> { "shield_body" }
        });

        // === 风分支 (速度) ===
        AllTalents.Add(new TalentNode
        {
            id = "speed_footwork", displayName = "步法", description = "移动速度 +0.3",
            branch = "speed", maxLevel = 5, moveSpeedPerLevel = 0.3f
        });
        AllTalents.Add(new TalentNode
        {
            id = "speed_gust", displayName = "阵风", description = "移速 +0.5，冷却缩减 +3%",
            branch = "speed", maxLevel = 5, moveSpeedPerLevel = 0.5f, cooldownPerLevel = 3,
            prerequisites = new List<string> { "speed_footwork" }
        });
        AllTalents.Add(new TalentNode
        {
            id = "speed_flash", displayName = "一闪", description = "移速 +0.8，冷却缩减 +5%",
            branch = "speed", maxLevel = 3, moveSpeedPerLevel = 0.8f, cooldownPerLevel = 5,
            prerequisites = new List<string> { "speed_gust" }
        });

        // === 智分支 (经验/功能) ===
        AllTalents.Add(new TalentNode
        {
            id = "wisdom_learn", displayName = "好学", description = "经验获取 +10%",
            branch = "wisdom", maxLevel = 5, expGainPerLevel = 10
        });
        AllTalents.Add(new TalentNode
        {
            id = "wisdom_insight", displayName = "顿悟", description = "经验获取 +15%",
            branch = "wisdom", maxLevel = 5, expGainPerLevel = 15,
            prerequisites = new List<string> { "wisdom_learn" }
        });
        AllTalents.Add(new TalentNode
        {
            id = "wisdom_prep", displayName = "备战", description = "开局等级 +1",
            branch = "wisdom", maxLevel = 3, startingLevelPerLevel = 1,
            prerequisites = new List<string> { "wisdom_insight" }
        });
        AllTalents.Add(new TalentNode
        {
            id = "wisdom_master", displayName = "宗师", description = "全属性 +5%",
            branch = "wisdom", maxLevel = 1,
            attackPerLevel = 3, maxHpPerLevel = 10, moveSpeedPerLevel = 0.2f,
            expGainPerLevel = 5, critChancePerLevel = 2,
            prerequisites = new List<string> { "wisdom_prep", "sword_mastery" }
        });

        Debug.Log($"[TalentManager] 初始化 {AllTalents.Count} 个天赋节点");
    }

    /// <summary>尝试解锁/升级天赋</summary>
    public bool TryUpgradeTalent(string talentId)
    {
        var node = AllTalents.Find(t => t.id == talentId);
        if (node == null) return false;
        if (node.IsMaxLevel) return false;
        if (AvailablePoints < node.costPerLevel) return false;

        // 检查前置
        foreach (var preId in node.prerequisites)
        {
            var pre = AllTalents.Find(t => t.id == preId);
            if (pre == null || !pre.IsUnlocked) return false;
        }

        AvailablePoints -= node.costPerLevel;
        TotalSpentPoints += node.costPerLevel;
        node.currentLevel++;

        SaveToPrefs();
        OnTalentChanged?.Invoke();
        Debug.Log($"[TalentManager] 天赋升级: {node.displayName} Lv.{node.currentLevel}");
        return true;
    }

    /// <summary>重置所有天赋（退还点数）</summary>
    public void ResetAll()
    {
        foreach (var node in AllTalents)
            node.currentLevel = 0;

        AvailablePoints += TotalSpentPoints;
        TotalSpentPoints = 0;
        SaveToPrefs();
        OnTalentChanged?.Invoke();
    }

    /// <summary>游戏结算时添加天赋点（等级越高越多）</summary>
    public void AddTalentPoints(int playerLevel)
    {
        int points = Mathf.Max(1, playerLevel / 5); // 每5级1点
        AvailablePoints += points;
        Debug.Log($"[TalentManager] 获得 {points} 天赋点 (等级{playerLevel})");
        SaveToPrefs();
    }

    /// <summary>获取总分加成，用于进入战斗时应用</summary>
    public void ApplyToPlayer(CharacterStats stats)
    {
        foreach (var node in AllTalents)
        {
            if (!node.IsUnlocked) continue;
            stats.attack += Mathf.RoundToInt(node.TotalAttack);
            stats.maxHp += Mathf.RoundToInt(node.TotalMaxHp);
            stats.moveSpeed += node.TotalMoveSpeed;
            stats.level += node.TotalStartingLevel;
        }
        stats.UpdateMoveSpeed();
        Debug.Log($"[TalentManager] 天赋加成已应用");
    }

    /// <summary>检查某天赋的前置是否满足</summary>
    public bool CanUnlock(string talentId)
    {
        var node = AllTalents.Find(t => t.id == talentId);
        if (node == null || node.IsMaxLevel) return false;
        foreach (var preId in node.prerequisites)
        {
            var pre = AllTalents.Find(t => t.id == preId);
            if (pre == null || !pre.IsUnlocked) return false;
        }
        return true;
    }

    // ===== 持久化 =====

    void SaveToPrefs()
    {
        PlayerPrefs.SetInt("talent_points", AvailablePoints);
        PlayerPrefs.SetInt("talent_spent", TotalSpentPoints);
        foreach (var node in AllTalents)
        {
            PlayerPrefs.SetInt($"talent_{node.id}", node.currentLevel);
        }
        PlayerPrefs.Save();
    }

    void LoadFromPrefs()
    {
        AvailablePoints = PlayerPrefs.GetInt("talent_points", 5); // 初始送5点
        TotalSpentPoints = PlayerPrefs.GetInt("talent_spent", 0);
        foreach (var node in AllTalents)
        {
            node.currentLevel = PlayerPrefs.GetInt($"talent_{node.id}", 0);
        }
        Debug.Log($"[TalentManager] 加载天赋: 可用{AvailablePoints}点, 已花{TotalSpentPoints}点");
    }
}