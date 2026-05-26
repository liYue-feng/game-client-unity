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

    [Header("升级选项配置")]
    [Tooltip("武器选项出现概率 (0-1)")]
    public float weaponRatio = 0.4f;
    [Tooltip("运气基础值（第4槽位概率 = luck / 100）")]
    public float baseLuck = 0f;

    /// <summary>总运气值 = 基础运气 + 背包运气加成</summary>
    public float TotalLuck => baseLuck + (Inventory.Instance != null ? Inventory.Instance.TotalLuckBonus : 0f);

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

        // 基础 3 槽位，运气检查第 4 槽位
        int slotCount = 3;
        if (Random.Range(0, 100) < TotalLuck)
        {
            slotCount = 4;
        }

        var options = GenerateRandomOptions(slotCount);
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

        // 分为武器池和道具池
        var weaponPool = new List<ItemData>();
        var itemPool = new List<ItemData>();
        foreach (var it in pool)
        {
            if (it.isWeapon) weaponPool.Add(it);
            else itemPool.Add(it);
        }

        // 计算武器槽位数量（至少 1 个如果武器池不为空）
        int weaponCount = weaponPool.Count > 0 ? Mathf.Max(1, Mathf.RoundToInt(count * weaponRatio)) : 0;
        int itemCount = count - weaponCount;
        if (weaponPool.Count < weaponCount) { itemCount += weaponCount - weaponPool.Count; weaponCount = weaponPool.Count; }
        if (itemPool.Count < itemCount) { weaponCount += itemCount - itemPool.Count; itemCount = itemPool.Count; }

        // 从各池选（去重品类）
        Shuffle(weaponPool);
        Shuffle(itemPool);
        var usedCategories = new HashSet<ItemData.ItemType>();

        foreach (var w in weaponPool)
        {
            if (result.Count >= count) break;
            if (!usedCategories.Contains(w.type) || result.Count >= count - 1)
            {
                result.Add(w);
                usedCategories.Add(w.type);
            }
        }
        foreach (var it in itemPool)
        {
            if (result.Count >= count) break;
            if (!usedCategories.Contains(it.type) || result.Count >= count - 1)
            {
                result.Add(it);
                usedCategories.Add(it.type);
            }
        }

        // 按稀有度排序展示（Epic 排前面）
        result.Sort((a, b) => b.rarity.CompareTo(a.rarity));

        return result;
    }

    void Shuffle(List<ItemData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    /// <summary>武器激活/升级事件 (weaponId, level)</summary>
    public event System.Action<string, int> OnWeaponActivated;

    /// <summary>应用升级加成到玩家并存入背包</summary>
    public void ApplyUpgrade(ItemData item, int level = 1)
    {
        if (_playerStats == null) return;

        // 武器类型：调用武器系统
        if (item.isWeapon && !string.IsNullOrEmpty(item.weaponBehaviourId))
        {
            var inv = Inventory.Instance;
            int currentLevel = inv.AddWeapon(item.weaponBehaviourId, item.itemName, item.maxLevel);
            OnWeaponActivated?.Invoke(item.weaponBehaviourId, currentLevel);
            Debug.Log($"[UpgradeManager] 武器激活: {item.itemName} ({item.weaponBehaviourId}) Lv.{currentLevel}");
            return;
        }

        // 一级属性加成（设计文档规范，优先使用）
        bool hasPrimaryBonus = item.strengthBonus != 0 || item.innerForceBonus != 0
            || item.vitalityBonus != 0 || item.spiritBonus != 0 || item.comprehensionBonus != 0;

        if (hasPrimaryBonus)
        {
            _playerStats.AddPrimaryBonus(
                item.strengthBonus, item.innerForceBonus,
                item.vitalityBonus, item.spiritBonus, item.comprehensionBonus);
        }
        else
        {
            // 传统扁平属性加成（向后兼容）
            _playerStats.attack += item.attackBonus;
            _playerStats.maxHp += item.maxHpBonus;
            _playerStats.moveSpeed += item.moveSpeedBonus;
            _playerStats.UpdateMoveSpeed();
            _playerStats.baseDashSpeed += item.dashSpeedBonus;
        }

        _playerStats.critDamageBonus += item.critDamageBonus / 100f;
        _playerStats.extraCritValue += Mathf.RoundToInt(item.critChanceBonus);

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
            lifestealPer: item.lifestealBonus,
            luckPer: item.luckBonus,
            expPer: item.expGainBonus,
            strPer: item.strengthBonus,
            innerPer: item.innerForceBonus,
            vitPer: item.vitalityBonus,
            spiPer: item.spiritBonus,
            compPer: item.comprehensionBonus
        );

        Debug.Log($"[UpgradeManager] 升级: {item.itemName} "
            + (hasPrimaryBonus
                ? $"力+{item.strengthBonus} 内+{item.innerForceBonus} 体+{item.vitalityBonus} 精+{item.spiritBonus} 悟+{item.comprehensionBonus}"
                : $"atk+{item.attackBonus} hp+{item.maxHpBonus} spd+{item.moveSpeedBonus}"));
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
        // 攻击类 — 力量加成（影响剑/刀ATK+DEF）
        AddItem("破甲", "力量 +1，剑系攻防提升", ItemData.ItemType.Attack, str: 1, maxLvl: 5, id: "atk_break_armor");
        AddItem("利刃", "力量 +2，剑系攻防提升", ItemData.ItemType.Attack, str: 2, maxLvl: 5, id: "atk_sharp_blade");
        AddItem("重剑", "力量 +3", ItemData.ItemType.Attack, str: 3, maxLvl: 3, id: "atk_heavy_sword");
        AddItem("剑气", "力量 +1，悟性 +1", ItemData.ItemType.Attack, str: 1, comp: 1, maxLvl: 5, id: "atk_sword_qi");
        AddItem("破军", "力量 +2，暴击伤害 +10%", ItemData.ItemType.Attack, str: 2, critDmg: 10, maxLvl: 3, id: "atk_breaker");
        AddItem("噬血", "力量 +1，吸血 +2%", ItemData.ItemType.Attack, str: 1, lifesteal: 2, maxLvl: 5, id: "atk_bloodlust");
        AddItem("暴雨", "力量 +1，精神 +1（冷却缩减）", ItemData.ItemType.Attack, str: 1, spi: 1, maxLvl: 5, id: "atk_rainstorm");

        // 防御类 — 体力加成（影响HP/耐力上限）
        AddItem("铁布衫", "体力 +1，增加最大生命", ItemData.ItemType.Defense, vit: 1, maxLvl: 5, id: "def_iron_skin");
        AddItem("药石", "体力 +2", ItemData.ItemType.Defense, vit: 2, maxLvl: 5, id: "def_medicine");
        AddItem("罡气", "体力 +3", ItemData.ItemType.Defense, vit: 3, maxLvl: 3, id: "def_gang_qi");
        AddItem("金钟罩", "体力 +1，精神 +1", ItemData.ItemType.Defense, vit: 1, spi: 1, maxLvl: 5, id: "def_golden_bell");
        AddItem("不坏", "体力 +1，吸血 +1%", ItemData.ItemType.Defense, vit: 1, lifesteal: 1, maxLvl: 5, id: "def_indestructible");
        AddItem("回春", "精神 +2（HP回复提升）", ItemData.ItemType.Defense, spi: 2, maxLvl: 5, id: "def_rejuvenation");

        // 速度类 — 精神加成（影响回复）+ 移动速度
        AddItem("轻功", "精神 +1，移动速度 +0.3", ItemData.ItemType.Speed, spi: 1, moveSpd: 0.3f, maxLvl: 5, id: "spd_lightfoot");
        AddItem("疾风", "精神 +2", ItemData.ItemType.Speed, spi: 2, maxLvl: 5, id: "spd_gale");
        AddItem("神行", "精神 +1，冲刺速度 +2", ItemData.ItemType.Speed, spi: 1, dashSpd: 2f, maxLvl: 3, id: "spd_divine_dash");
        AddItem("踏雪", "精神 +1，移动速度 +0.5", ItemData.ItemType.Speed, spi: 1, moveSpd: 0.5f, maxLvl: 5, id: "spd_snow_step");
        AddItem("幻影", "精神 +1，悟性 +1", ItemData.ItemType.Speed, spi: 1, comp: 1, maxLvl: 5, id: "spd_phantom");

        // 功能类 — 混合加成
        AddItem("养息", "体力 +1，力量 +1", ItemData.ItemType.Utility, vit: 1, str: 1, maxLvl: 5, id: "util_rest");
        AddItem("精进", "力量 +1，精神 +1", ItemData.ItemType.Utility, str: 1, spi: 1, maxLvl: 5, id: "util_diligence");
        AddItem("洞悉", "经验获取 +15%", ItemData.ItemType.Utility, exp: 15, maxLvl: 5, id: "util_insight");
        AddItem("拾遗", "拾取范围 +20%", ItemData.ItemType.Utility, pickup: 20, maxLvl: 5, id: "util_scavenger");
        AddItem("聚宝", "经验 +10%，幸运 +5", ItemData.ItemType.Utility, exp: 10, luck: 5, maxLvl: 5, id: "util_treasure");
        AddItem("顿悟", "悟性 +2，经验 +10%", ItemData.ItemType.Utility, comp: 2, exp: 10, maxLvl: 3, id: "util_epiphany");

        // 元素类 — 内力加成（影响印/毒/血ATK+DEF）
        AddItem("灼烧", "内力 +1，攻击附带燃烧", ItemData.ItemType.Elemental, inner: 1, maxLvl: 5, id: "elem_burn");
        AddItem("冰霜", "内力 +1，攻击附带减速", ItemData.ItemType.Elemental, inner: 1, maxLvl: 5, id: "elem_frost");
        AddItem("惊雷", "内力 +2，攻击附带雷电", ItemData.ItemType.Elemental, inner: 2, maxLvl: 5, id: "elem_thunder");
        AddItem("毒雾", "内力 +1，攻击附带中毒", ItemData.ItemType.Elemental, inner: 1, maxLvl: 5, id: "elem_poison");
        AddItem("墨焰", "内力 +2，元素伤害 +15%", ItemData.ItemType.Elemental, inner: 2, maxLvl: 3, id: "elem_ink_flame");

        // 召唤类 4种
        AddItem("墨魂", "召唤墨影分身助战", ItemData.ItemType.Summon, maxLvl: 5, id: "summon_ink_spirit");
        AddItem("剑灵", "召唤悬浮飞剑自动攻击", ItemData.ItemType.Summon, attack: 5, maxLvl: 5, id: "summon_sword_spirit");
        AddItem("墨雨", "墨点从天而降，范围伤害", ItemData.ItemType.Summon, attack: 8, maxLvl: 3, id: "summon_ink_rain");
        AddItem("策令", "召唤物伤害+20%", ItemData.ItemType.Summon, attack: 3, maxLvl: 5, id: "summon_command");

        // 自动武器类 4种（40%概率出现）
        AddItem("墨弹", "向前扇形发射墨滴", ItemData.ItemType.Attack, attack: 6, maxLvl: 5, id: "wpn_ink_bolt",
            isWeapon: true, weaponId: "ink_bolt", rare: ItemData.Rarity.Common);
        AddItem("墨旋", "墨滴环绕周身旋转守护", ItemData.ItemType.Defense, attack: 5, maxLvl: 5, id: "wpn_ink_swirl",
            isWeapon: true, weaponId: "ink_swirl", rare: ItemData.Rarity.Common);
        AddItem("墨击", "锁定敌人降下墨柱", ItemData.ItemType.Elemental, attack: 12, maxLvl: 5, id: "wpn_ink_strike",
            isWeapon: true, weaponId: "ink_strike", rare: ItemData.Rarity.Rare);
        AddItem("墨斩", "沿朝向发出横向墨刃", ItemData.ItemType.Attack, attack: 7, maxLvl: 5, id: "wpn_ink_slash",
            isWeapon: true, weaponId: "ink_slash", rare: ItemData.Rarity.Rare);

        // 稀有类 2种（低概率出现的强力道具）
        AddItem("天书", "全属性 +10%", ItemData.ItemType.Utility, attack: 5, hp: 10, moveSpd: 0.3f, crit: 5, maxLvl: 1, id: "rare_heavenly_book");
        AddItem("墨宝", "攻击 +20，暴击伤害 +30%", ItemData.ItemType.Attack, attack: 20, critDmg: 30, maxLvl: 1, id: "rare_ink_treasure");

        Debug.Log($"[UpgradeManager] 默认升级库: {defaultItems.Count} 种 (攻/御/速/辅/元/召)");
    }

    void AddItem(string name, string desc, ItemData.ItemType type,
        int attack = 0, int hp = 0, float moveSpd = 0, float dashSpd = 0,
        float crit = 0, float critDmg = 0, float lifesteal = 0,
        float cooldown = 0, float exp = 0, float pickup = 0,
        string special = "", float specialVal = 0, int maxLvl = 5, string id = "",
        bool isWeapon = false, string weaponId = "", ItemData.Rarity rare = ItemData.Rarity.Common,
        float luck = 0,
        int str = 0, int inner = 0, int vit = 0, int spi = 0, int comp = 0)
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
        item.isWeapon = isWeapon;
        item.weaponBehaviourId = weaponId;
        item.rarity = rare;
        item.luckBonus = luck;
        item.strengthBonus = str;
        item.innerForceBonus = inner;
        item.vitalityBonus = vit;
        item.spiritBonus = spi;
        item.comprehensionBonus = comp;
        defaultItems.Add(item);
    }

    void OnDestroy()
    {
        if (_playerStats != null)
            _playerStats.OnLevelUp -= OnPlayerLevelUp;
    }
}