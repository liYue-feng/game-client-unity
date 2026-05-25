using UnityEngine;

/// <summary>
/// 升级物品数据：定义可选择的升级选项。
/// 水墨风格：用颜色区分不同类型的升级。
/// 升级后自动存入 Inventory 背包。
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Game/Item Data")]
public class ItemData : ScriptableObject
{
    public enum ItemType
    {
        Attack,     // 朱砂红 — 攻击/暴击/穿透
        Defense,    // 花青 — 生命/护甲/格挡
        Speed,      // 藤黄 — 移速/冲刺/冷却
        Utility,    // 靛蓝 — 经验/拾取/掉率
        Elemental,  // 墨紫 — 元素/灼烧/冰冻/雷电
        Summon      // 翡翠绿 — 召唤/宠物/光环
    }

    [Header("基础信息")]
    public string itemName;
    [TextArea]
    public string description;
    public ItemType type;

    [Header("数值加成（基础值，可升级）")]
    public int attackBonus = 0;
    public int maxHpBonus = 0;
    public float moveSpeedBonus = 0f;
    public float dashSpeedBonus = 0f;

    [Header("高级属性加成")]
    public float critChanceBonus = 0f;
    public float critDamageBonus = 0f;
    public float lifestealBonus = 0f;
    public float cooldownReduction = 0f;
    public float expGainBonus = 0f;
    public float pickupRangeBonus = 0f;

    [Header("特殊效果")]
    public string specialEffect = "";        // 特殊效果ID
    public float specialEffectValue = 0f;     // 特殊效果数值
    public int maxLevel = 5;                  // 最大等级（可重复选择升到5级）

    [Header("被动道具ID（存入背包用）")]
    public string itemId = "";

    /// <summary>物品唯一ID，用于去重和升级</summary>
    public string UniqueId => string.IsNullOrEmpty(itemId) ? itemName : itemId;

    /// <summary>
    /// 获取水墨风格颜色
    /// </summary>
    public Color GetColor()
    {
        switch (type)
        {
            case ItemType.Attack:
                return ShuiMoPalette.CinnabarRed;
            case ItemType.Defense:
                return ShuiMoPalette.Indigo;
            case ItemType.Speed:
                return ShuiMoPalette.Gamboge;
            case ItemType.Utility:
                return ShuiMoPalette.FlowerBlue;
            case ItemType.Elemental:
                return ShuiMoPalette.InkPurple;
            case ItemType.Summon:
                return ShuiMoPalette.JadeGreen;
            default:
                return ShuiMoPalette.InkBlack;
        }
    }

    /// <summary>获取一级时总攻击力加成</summary>
    public int GetTotalAttack(int level = 1) => attackBonus * level;
    public int GetTotalMaxHp(int level = 1) => maxHpBonus * level;
    public float GetTotalMoveSpeed(int level = 1) => moveSpeedBonus * level;
    public float GetTotalCritChance(int level = 1) => critChanceBonus * level;
    public float GetTotalCooldown(int level = 1) => cooldownReduction * level;

    /// <summary>品类分类中文</summary>
    public string CategoryZh
    {
        get
        {
            switch (type)
            {
                case ItemType.Attack: return "攻";
                case ItemType.Defense: return "御";
                case ItemType.Speed: return "速";
                case ItemType.Utility: return "辅";
                case ItemType.Elemental: return "元";
                case ItemType.Summon: return "召";
                default: return "?";
            }
        }
    }
}