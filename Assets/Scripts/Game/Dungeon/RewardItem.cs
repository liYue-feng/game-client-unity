using UnityEngine;

/// <summary>
/// 奖励类型枚举
/// </summary>
public enum RewardType
{
    /// <summary>恢复生命</summary>
    Heal,
    /// <summary>耐力上限提升</summary>
    StaminaUp,
    /// <summary>攻击力提升</summary>
    AttackUp,
    /// <summary>流派解锁</summary>
    StyleUnlock,
    /// <summary>金币</summary>
    Gold
}

/// <summary>
/// 奖励条目
/// </summary>
[System.Serializable]
public class RewardItem
{
    public RewardType type;
    public int value;
    public string display_name;
    public string description;

    /// <summary>生成随机奖励（3选1用）</summary>
    public static RewardItem[] GenerateRewards(int dungeonLevel)
    {
        RewardItem[] rewards = new RewardItem[3];
        for (int i = 0; i < 3; i++)
        {
            rewards[i] = GenerateRandomReward(dungeonLevel);
        }
        return rewards;
    }

    private static RewardItem GenerateRandomReward(int level)
    {
        float roll = Random.value;
        if (roll < 0.35f)
        {
            return new RewardItem
            {
                type = RewardType.Heal,
                value = 20 + level * 5,
                display_name = "回血药",
                description = $"恢复 {20 + level * 5} 点生命值"
            };
        }
        else if (roll < 0.55f)
        {
            return new RewardItem
            {
                type = RewardType.StaminaUp,
                value = 5 + level * 2,
                display_name = "耐力强化",
                description = $"耐力上限 +{5 + level * 2}"
            };
        }
        else if (roll < 0.75f)
        {
            return new RewardItem
            {
                type = RewardType.AttackUp,
                value = 2 + level,
                display_name = "力量增幅",
                description = $"攻击力 +{2 + level}"
            };
        }
        else
        {
            return new RewardItem
            {
                type = RewardType.Gold,
                value = 50 + level * 20,
                display_name = "金币袋",
                description = $"获得 {50 + level * 20} 金币"
            };
        }
    }
}
