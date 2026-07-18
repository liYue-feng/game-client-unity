using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Core;

/// <summary>
/// 成就定义
/// </summary>
[Serializable]
public class Achievement
{
    public string id;                  // 唯一ID
    public string displayName;         // 显示名称
    public string description;         // 描述
    public string category;            // 分类: combat/collection/survival/mastery
    public int targetValue;            // 目标值
    public int currentValue;           // 当前进度
    public int rewardTalentPoints;     // 奖励天赋点
    public bool isCompleted;           // 是否已完成
    public bool isClaimed;             // 是否已领取奖励

    public float Progress => targetValue > 0 ? Mathf.Clamp01((float)currentValue / targetValue) : 0f;
    public string ProgressText => $"{currentValue}/{targetValue}";
}

/// <summary>
/// 成就管理器：追踪所有成就进度，发放天赋点奖励。
/// 数据持久化到 PlayerPrefs。
/// 单例模式。
/// </summary>
public class AchievementManager : MonoBehaviour, IGameService
{
    private static AchievementManager _instance;
    public static AchievementManager Instance
    {
        get
        {
            if (_instance == null)
            {
                Debug.LogError("[AchievementManager] Service is not installed by GameApplication.");
            }

            return _instance;
        }
    }

    public string ServiceName => nameof(AchievementManager);

    public List<Achievement> AllAchievements = new List<Achievement>();

    /// <summary>成就进度更新事件</summary>
    public event Action<Achievement> OnProgressUpdated;
    /// <summary>成就完成事件</summary>
    public event Action<Achievement> OnCompleted;
    private bool _initialized;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    internal static AchievementManager Install(Transform parent)
    {
        if (_instance != null)
        {
            return _instance;
        }

        var serviceObject = new GameObject("[AchievementManager]");
        serviceObject.transform.SetParent(parent, false);
        return serviceObject.AddComponent<AchievementManager>();
    }

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        AllAchievements.Clear();
        InitializeAchievements();
        LoadFromPrefs();
        _initialized = true;
    }

    public void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }

        SaveToPrefs();
        OnProgressUpdated = null;
        OnCompleted = null;
        _initialized = false;
    }

    internal static void ResetStaticState()
    {
        _instance = null;
    }

    private void OnDestroy()
    {
        Shutdown();
        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
        }
    }

    void InitializeAchievements()
    {
        // === 杀敌 (Combat) ===
        AddAchievement("kill_100", "初出茅庐", "累计击杀100个敌人", "combat", 100, 3);
        AddAchievement("kill_500", "久经沙场", "累计击杀500个敌人", "combat", 500, 5);
        AddAchievement("kill_1000", "百战不殆", "累计击杀1000个敌人", "combat", 1000, 8);
        AddAchievement("kill_5000", "万人敌", "累计击杀5000个敌人", "combat", 5000, 15);

        // === 等级 (Level) ===
        AddAchievement("level_10", "小有所成", "单局达到等级10", "combat", 10, 3);
        AddAchievement("level_20", "炉火纯青", "单局达到等级20", "combat", 20, 5);
        AddAchievement("level_30", "登峰造极", "单局达到等级30", "combat", 30, 10);

        // === 收集 (Collection) ===
        AddAchievement("collect_10", "博采众长", "解锁过10种不同升级", "collection", 10, 3);
        AddAchievement("collect_20", "博览群书", "解锁过20种不同升级", "collection", 20, 5);
        AddAchievement("collect_30", "墨海无涯", "解锁过30种不同升级", "collection", 30, 10);

        // === 连击 (Combo) ===
        AddAchievement("combo_50", "连珠", "单局达成50连击", "combat", 50, 3);
        AddAchievement("combo_100", "墨舞", "单局达成100连击", "combat", 100, 5);
        AddAchievement("combo_200", "神来之笔", "单局达成200连击", "combat", 200, 10);

        // === 存活 (Survival) ===
        AddAchievement("survive_5m", "初涉江湖", "单局存活5分钟", "survival", 300, 3);
        AddAchievement("survive_10m", "江湖老手", "单局存活10分钟", "survival", 600, 5);
        AddAchievement("survive_20m", "不死传说", "单局存活20分钟", "survival", 1200, 10);

        // === Boss (Boss) ===
        AddAchievement("boss_1", "斩将", "击败1个Boss", "combat", 1, 5);
        AddAchievement("boss_5", "夺旗", "击败5个Boss", "combat", 5, 8);
        AddAchievement("boss_10", "擒王", "击败10个Boss", "combat", 10, 15);

        // === 元素大师 (Elemental) ===
        AddAchievement("elem_3", "五行初探", "同一局拥有3种元素升级", "mastery", 3, 5);
        AddAchievement("elem_5", "五行大师", "同一局拥有5种元素升级", "mastery", 5, 10);

        // === 召唤大师 (Summon) ===
        AddAchievement("summon_2", "墨魂初醒", "同一局拥有2种召唤升级", "mastery", 2, 5);
        AddAchievement("summon_4", "墨魂主宰", "同一局拥有4种召唤升级", "mastery", 4, 10);

        // === 天赋 (Talent) ===
        AddAchievement("talent_10", "初窥门径", "累计花费10个天赋点", "mastery", 10, 3);
        AddAchievement("talent_30", "融会贯通", "累计花费30个天赋点", "mastery", 30, 5);
        AddAchievement("talent_50", "一代宗师", "累计花费50个天赋点", "mastery", 50, 10);

        // === 风格大师 (Style) ===
        AddAchievement("style_3", "兼收并蓄", "同一局使用过3种战斗风格", "mastery", 3, 3);
        AddAchievement("style_5", "万法归一", "同一局使用过5种战斗风格", "mastery", 5, 8);

        Debug.Log($"[AchievementManager] 初始化 {AllAchievements.Count} 个成就");
    }

    void AddAchievement(string id, string name, string desc, string category, int target, int reward)
    {
        AllAchievements.Add(new Achievement
        {
            id = id,
            displayName = name,
            description = desc,
            category = category,
            targetValue = target,
            rewardTalentPoints = reward
        });
    }

    /// <summary>增加成就进度（累计型）</summary>
    public void AddProgress(string achievementId, int amount = 1)
    {
        var ach = AllAchievements.Find(a => a.id == achievementId);
        if (ach == null || ach.isCompleted) return;

        ach.currentValue += amount;
        if (ach.currentValue >= ach.targetValue)
        {
            ach.currentValue = ach.targetValue;
            ach.isCompleted = true;
            OnCompleted?.Invoke(ach);
            Debug.Log($"[AchievementManager] 成就达成: {ach.displayName}!");
        }
        OnProgressUpdated?.Invoke(ach);

        // 自动保存
        SaveToPrefs();
    }

    /// <summary>设置成就进度（覆盖型，用于"单局最高"类成就）</summary>
    public void SetProgress(string achievementId, int value)
    {
        var ach = AllAchievements.Find(a => a.id == achievementId);
        if (ach == null || ach.isCompleted) return;

        if (value > ach.currentValue)
        {
            ach.currentValue = value;
            if (ach.currentValue >= ach.targetValue)
            {
                ach.currentValue = ach.targetValue;
                ach.isCompleted = true;
                OnCompleted?.Invoke(ach);
                Debug.Log($"[AchievementManager] 成就达成: {ach.displayName}!");
            }
            OnProgressUpdated?.Invoke(ach);
            SaveToPrefs();
        }
    }

    /// <summary>领取成就奖励</summary>
    public bool ClaimReward(string achievementId)
    {
        var ach = AllAchievements.Find(a => a.id == achievementId);
        if (ach == null || !ach.isCompleted || ach.isClaimed) return false;

        ach.isClaimed = true;
        TalentManager.Instance.AddTalentPoints(ach.rewardTalentPoints);
        Debug.Log($"[AchievementManager] 领取奖励: {ach.displayName} → +{ach.rewardTalentPoints}天赋点");
        SaveToPrefs();
        return true;
    }

    /// <summary>战斗结算时汇报各项数据</summary>
    public void ReportBattleResult(CombatResultData data)
    {
        // 累计击杀
        AddProgress("kill_100", data.killCount);
        AddProgress("kill_500", data.killCount);
        AddProgress("kill_1000", data.killCount);
        AddProgress("kill_5000", data.killCount);

        // Boss击杀
        AddProgress("boss_1", data.bossKills);
        AddProgress("boss_5", data.bossKills);
        AddProgress("boss_10", data.bossKills);

        // 单局最佳
        SetProgress("level_10", data.playerLevel);
        SetProgress("level_20", data.playerLevel);
        SetProgress("level_30", data.playerLevel);
        SetProgress("combo_50", data.maxCombo);
        SetProgress("combo_100", data.maxCombo);
        SetProgress("combo_200", data.maxCombo);
        SetProgress("survive_5m", data.survivalTime);
        SetProgress("survive_10m", data.survivalTime);
        SetProgress("survive_20m", data.survivalTime);

        // 收集：统计不同升级种类
        if (Inventory.Instance != null)
        {
            int uniqueItems = 0;
            for (int i = 0; i < Inventory.Instance.Count; i++)
                if (Inventory.Instance.Items[i] != null) uniqueItems++;
            AddProgress("collect_10", uniqueItems);
            AddProgress("collect_20", uniqueItems);
            AddProgress("collect_30", uniqueItems);

            // 元素大师
            int elemCount = data.elementalUpgradeCount;
            SetProgress("elem_3", elemCount);
            SetProgress("elem_5", elemCount);

            // 召唤大师
            int summonCount = data.summonUpgradeCount;
            SetProgress("summon_2", summonCount);
            SetProgress("summon_4", summonCount);

            // 风格大师
            SetProgress("style_3", data.styleSwitchCount);
            SetProgress("style_5", data.styleSwitchCount);
        }

        // 天赋花费（来自 TalentManager）
        AddProgress("talent_10", TalentManager.Instance.TotalSpentPoints);
        AddProgress("talent_30", TalentManager.Instance.TotalSpentPoints);
        AddProgress("talent_50", TalentManager.Instance.TotalSpentPoints);
    }

    /// <summary>获取分类的成就列表</summary>
    public List<Achievement> GetByCategory(string category)
    {
        return AllAchievements.FindAll(a => a.category == category);
    }

    /// <summary>获取已完成数量</summary>
    public int CompletedCount => AllAchievements.FindAll(a => a.isCompleted).Count;

    /// <summary>重置所有成就（调试用）</summary>
    public void ResetAll()
    {
        foreach (var ach in AllAchievements)
        {
            ach.currentValue = 0;
            ach.isCompleted = false;
            ach.isClaimed = false;
        }
        PlayerPrefs.DeleteKey("ach_data");
        PlayerPrefs.Save();
        Debug.Log("[AchievementManager] 所有成就已重置");
    }

    // ===== 持久化 =====

    void SaveToPrefs()
    {
        var data = new System.Text.StringBuilder();
        foreach (var ach in AllAchievements)
        {
            data.Append($"{ach.id}:{ach.currentValue}:{ach.isCompleted}:{ach.isClaimed};");
        }
        PlayerPrefs.SetString("ach_data", data.ToString());
        PlayerPrefs.Save();
    }

    void LoadFromPrefs()
    {
        string data = PlayerPrefs.GetString("ach_data", "");
        if (string.IsNullOrEmpty(data)) return;

        var entries = data.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in entries)
        {
            var parts = entry.Split(':');
            if (parts.Length < 4) continue;

            var ach = AllAchievements.Find(a => a.id == parts[0]);
            if (ach == null) continue;

            int.TryParse(parts[1], out ach.currentValue);
            bool.TryParse(parts[2], out ach.isCompleted);
            bool.TryParse(parts[3], out ach.isClaimed);
        }
        Debug.Log($"[AchievementManager] 加载成就: {CompletedCount}/{AllAchievements.Count} 已完成");
    }
}
