using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 升级管理器：生成3选1升级选项，应用加成到玩家。
/// 升级后会同步存入 Inventory 背包。
/// 包含30+种默认升级选项，覆盖6大品类。
/// </summary>
public class UpgradeManager : MonoBehaviour
{
    public List<ItemData> defaultItems = new List<ItemData>();

    public LevelUpUI levelUpUI;
    private CharacterStats _playerStats;
    private List<ItemData> _itemDatabase = new List<ItemData>();
    private bool _isInitialized;

    /// <summary>升级选项生成时触发，可在此添加自定义选项</summary>
    public System.Action<List<ItemData>> OnBeforeGenerateOptions;

    void Awake()
    {
        InitializeDefaultItems();
    }

    public void Initialize(CharacterStats playerStats)
    {
        _playerStats = playerStats;

        if (levelUpUI == null)
        {
            var uiObj = new GameObject("LevelUpUI");
            uiObj.transform.SetParent(transform, false);
            levelUpUI = uiObj.AddComponent<LevelUpUI>();
        }
        levelUpUI.Initialize(playerStats);
        _playerStats.OnLevelUp += OnPlayerLevelUp;
        _isInitialized = true;
    }

    public void SetItemDatabase(List<ItemData> items)
    {
        _itemDatabase.Clear();
        if (items != null) _itemDatabase.AddRange(items);
    }

    void OnPlayerLevelUp(int newLevel)
    {
        if (!_isInitialized || levelUpUI == null) return;
        var options = GenerateRandomOptions(3);
        OnBeforeGenerateOptions?.Invoke(options);
        levelUpUI.Show(options);
    }

    List<ItemData> GenerateRandomOptions(int count)
    {
        var result = new List<ItemData>();
        var pool = new List<ItemData>(_itemDatabase.Count > 0 ? _itemDatabase : defaultItems);

        if (pool.Count == 0)
        {
            Debug.LogWarning("[UpgradeManager] 无可用升级选项");
            return result;
        }

        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
        }

        var usedCategories = new HashSet<ItemData.ItemType>();
        foreach (var item in pool)
        {
            if (result.Count >= count) break;
            if (!usedCategories.Contains(item.type) || result.Count >= count - 1)
            {
                result.Add(item);
                usedCategories.Add(item.type);
            }
        }

        return result;
    }

    /// <summary>应用升级加成到玩家并存入背包</summary>
    public void ApplyUpgrade(ItemData item, int level = 1)
    {
        if (_playerStats == null) return;

        _playerStats.attack += item.attackBonus;
        _playerStats.maxHp += item.maxHpBonus;
        _playerStats.moveSpeed += item.moveSpeedBonus;
        _playerStats.UpdateMoveSpeed();
        _playerStats.baseDashSpeed += item.dashSpeedBonus;

        Inventory.Instance.AddOrUpgrade(
            id: item.UniqueId,
            displayName: item.itemName,
            description: item.description,
            category: item.type.ToString().ToLower(),
            maxLevel: item.maxLevel,
            flavorText: GetFlavorText(item.type),
            attackPer: item.attackBonus,
            hpPer: item.maxHpBonus,
            speedPer: item.moveSpeedBonus,
            cooldownPer: item.cooldownReduction,
            critPer: item.critChanceBonus,
            lifestealPer: item.lifestealBonus
        );

        Debug.Log($"[UpgradeManager] 升级: {item.itemName} atk+{item.attackBonus} hp+{item.maxHpBonus} spd+{item.moveSpeedBonus}");
    }

    string GetFlavorText(ItemData.ItemType type)
    {
        switch (type)
        {
            case ItemData.ItemType.Attack: return "剑出如龙";
            case ItemData.ItemType.Defense: return "不动如山";
            case ItemData.ItemType.Speed: return "疾风迅雷";
            case ItemData.ItemType.Utility: return "大道至简";
            case ItemData.ItemType.Elemental: return "五行使者";
            case ItemData.ItemType.Summon: return "墨魂唤醒";
            default: return "";
        }
    }

    // ===== 30+种默认升级选项 =====

    void InitializeDefaultItems()
    {
        // 攻击类 7种
        AddItem("破甲", "攻击力 +5", ItemData.ItemType.Attack, attack: 5, maxLvl: 5, id: "atk_break_armor");
        AddItem("利刃", "攻击力 +10", ItemData.ItemType.Attack, attack: 10, maxLvl: 5, id: "atk_sharp_blade");
        AddItem("重剑", "攻击力 +15", ItemData.ItemType.Attack, attack: 15, maxLvl: 3, id: "atk_heavy_sword");
        AddItem("剑气", "攻击力 +8，暴击率 +3%", ItemData.ItemType.Attack, attack: 8, crit: 3, maxLvl: 5, id: "atk_sword_qi");
        AddItem("破军", "攻击力 +12，暴击伤害 +10%", ItemData.ItemType.Attack, attack: 12, critDmg: 10, maxLvl: 3, id: "atk_breaker");
        AddItem("噬血", "攻击力 +6，吸血 +2%", ItemData.ItemType.Attack, attack: 6, lifesteal: 2, maxLvl: 5, id: "atk_bloodlust");
        AddItem("暴雨", "攻击力 +4，冷却缩减 +5%", ItemData.ItemType.Attack, attack: 4, cooldown: 5, maxLvl: 5, id: "atk_rainstorm");

        // 防御类 6种
        AddItem("铁布衫", "最大生命 +20", ItemData.ItemType.Defense, hp: 20, maxLvl: 5, id: "def_iron_skin");
        AddItem("药石", "最大生命 +30", ItemData.ItemType.Defense, hp: 30, maxLvl: 5, id: "def_medicine");
        AddItem("罡气", "最大生命 +50", ItemData.ItemType.Defense, hp: 50, maxLvl: 3, id: "def_gang_qi");
        AddItem("金钟罩", "生命 +25，受伤 -3%", ItemData.ItemType.Defense, hp: 25, maxLvl: 5, id: "def_golden_bell");
        AddItem("不坏", "生命 +15，吸血 +1%", ItemData.ItemType.Defense, hp: 15, lifesteal: 1, maxLvl: 5, id: "def_indestructible");
        AddItem("回春", "生命 +10，每秒回复 +0.5", ItemData.ItemType.Defense, hp: 10, maxLvl: 5, id: "def_rejuvenation");

        // 速度类 5种
        AddItem("轻功", "移动速度 +0.5", ItemData.ItemType.Speed, moveSpd: 0.5f, maxLvl: 5, id: "spd_lightfoot");
        AddItem("疾风", "移动速度 +1.0", ItemData.ItemType.Speed, moveSpd: 1.0f, maxLvl: 5, id: "spd_gale");
        AddItem("神行", "冲刺速度 +3", ItemData.ItemType.Speed, dashSpd: 3f, maxLvl: 3, id: "spd_divine_dash");
        AddItem("踏雪", "移速 +0.7，冲刺冷却 -8%", ItemData.ItemType.Speed, moveSpd: 0.7f, cooldown: 8, maxLvl: 5, id: "spd_snow_step");
        AddItem("幻影", "移速 +0.5，暴击率 +2%", ItemData.ItemType.Speed, moveSpd: 0.5f, crit: 2, maxLvl: 5, id: "spd_phantom");

        // 功能类 6种
        AddItem("养息", "生命 +15，攻击 +3", ItemData.ItemType.Utility, hp: 15, attack: 3, maxLvl: 5, id: "util_rest");
        AddItem("精进", "速度 +0.3，攻击 +5", ItemData.ItemType.Utility, moveSpd: 0.3f, attack: 5, maxLvl: 5, id: "util_diligence");
        AddItem("洞悉", "经验获取 +15%", ItemData.ItemType.Utility, exp: 15, maxLvl: 5, id: "util_insight");
        AddItem("拾遗", "拾取范围 +20%", ItemData.ItemType.Utility, pickup: 20, maxLvl: 5, id: "util_scavenger");
        AddItem("聚宝", "经验 +10%，拾取范围 +10%", ItemData.ItemType.Utility, exp: 10, pickup: 10, maxLvl: 5, id: "util_treasure");
        AddItem("顿悟", "经验 +25%", ItemData.ItemType.Utility, exp: 25, maxLvl: 3, id: "util_epiphany");

        // 元素类 5种
        AddItem("灼烧", "攻击附带燃烧，每秒3点伤", ItemData.ItemType.Elemental, attack: 3, maxLvl: 5, id: "elem_burn");
        AddItem("冰霜", "攻击附带减速，降敌20%速度", ItemData.ItemType.Elemental, attack: 2, maxLvl: 5, id: "elem_frost");
        AddItem("惊雷", "攻击附带雷电，10%额外伤害弹跳", ItemData.ItemType.Elemental, attack: 5, maxLvl: 5, id: "elem_thunder");
        AddItem("毒雾", "攻击附带中毒，持续4秒", ItemData.ItemType.Elemental, attack: 4, maxLvl: 5, id: "elem_poison");
        AddItem("墨焰", "所有元素伤害 +15%", ItemData.ItemType.Elemental, attack: 6, maxLvl: 3, id: "elem_ink_flame");

        // 召唤类 4种
        AddItem("墨魂", "召唤墨影分身助战", ItemData.ItemType.Summon, maxLvl: 5, id: "summon_ink_spirit");
        AddItem("剑灵", "召唤悬浮飞剑自动攻击", ItemData.ItemType.Summon, attack: 5, maxLvl: 5, id: "summon_sword_spirit");
        AddItem("墨雨", "墨点从天而降，范围伤害", ItemData.ItemType.Summon, attack: 8, maxLvl: 3, id: "summon_ink_rain");
        AddItem("策令", "召唤物伤害+20%", ItemData.ItemType.Summon, attack: 3, maxLvl: 5, id: "summon_command");

        // 稀有类 2种（低概率出现的强力道具）
        AddItem("天书", "全属性 +10%", ItemData.ItemType.Utility, attack: 5, hp: 10, moveSpd: 0.3f, crit: 5, maxLvl: 1, id: "rare_heavenly_book");
        AddItem("墨宝", "攻击 +20，暴击伤害 +30%", ItemData.ItemType.Attack, attack: 20, critDmg: 30, maxLvl: 1, id: "rare_ink_treasure");

        Debug.Log($"[UpgradeManager] 默认升级库: {defaultItems.Count} 种 (攻/御/速/辅/元/召)");
    }

    void AddItem(string name, string desc, ItemData.ItemType type,
        int attack = 0, int hp = 0, float moveSpd = 0, float dashSpd = 0,
        float crit = 0, float critDmg = 0, float lifesteal = 0,
        float cooldown = 0, float exp = 0, float pickup = 0,
        string special = "", float specialVal = 0, int maxLvl = 5, string id = "")
    {
        var item = ScriptableObject.CreateInstance<ItemData>();
        item.name = name;
        item.itemName = name;
        item.description = desc;
        item.type = type;
        item.attackBonus = attack;
        item.maxHpBonus = hp;
        item.moveSpeedBonus = moveSpd;
        item.dashSpeedBonus = dashSpd;
        item.critChanceBonus = crit;
        item.critDamageBonus = critDmg;
        item.lifestealBonus = lifesteal;
        item.cooldownReduction = cooldown;
        item.expGainBonus = exp;
        item.pickupRangeBonus = pickup;
        item.specialEffect = special;
        item.specialEffectValue = specialVal;
        item.maxLevel = maxLvl;
        item.itemId = id;
        defaultItems.Add(item);
    }

    void OnDestroy()
    {
        if (_playerStats != null)
            _playerStats.OnLevelUp -= OnPlayerLevelUp;
    }
}