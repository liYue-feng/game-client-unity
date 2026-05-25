using UnityEngine;

/// <summary>
/// 流派数据库：硬编码的流派数据表。
/// 与服务端 combat/config.go 中的 GetStyleConfigs() 保持同步。
/// </summary>
public static class StyleDatabase
{
    public static readonly StyleData[] AllStyles = new StyleData[]
    {
        new StyleData
        {
            styleID = CombatStyleID.Blade,
            styleName = "刃",
            damageMult = 1.0f,
            speedMult = 1.2f,
            parryMult = 1.0f,
            dashSpeedMult = 1.0f,
            dashCostMult = 1.0f,
            specialResourceMax = 100,
            specialResourceName = "怒气",
            description = "高速连击流派，刃风暴消耗怒气"
        },
        new StyleData
        {
            styleID = CombatStyleID.Seal,
            styleName = "印",
            damageMult = 0.8f,
            speedMult = 1.0f,
            parryMult = 1.5f,
            dashSpeedMult = 1.0f,
            dashCostMult = 1.0f,
            specialResourceMax = 5,
            specialResourceName = "印记",
            description = "弹反强化流派，弹反放置印记可引爆"
        },
        new StyleData
        {
            styleID = CombatStyleID.Poison,
            styleName = "毒",
            damageMult = 0.6f,
            speedMult = 1.0f,
            parryMult = 0.8f,
            dashSpeedMult = 1.0f,
            dashCostMult = 1.0f,
            specialResourceMax = 100,
            specialResourceName = "毒液",
            description = "持续伤害流派，攻击叠毒，毒雾范围DoT"
        },
        new StyleData
        {
            styleID = CombatStyleID.Blood,
            styleName = "血",
            damageMult = 1.5f,
            speedMult = 0.8f,
            parryMult = 0.6f,
            dashSpeedMult = 1.0f,
            dashCostMult = 1.0f,
            specialResourceMax = 100,
            specialResourceName = "鲜血",
            description = "高风险高回报流派，血祭扣HP换爆发伤害"
        },
        new StyleData
        {
            styleID = CombatStyleID.Sword,
            styleName = "剑",
            damageMult = 1.2f,
            speedMult = 1.0f,
            parryMult = 1.3f,
            dashSpeedMult = 1.0f,
            dashCostMult = 1.0f,
            specialResourceMax = 100,
            specialResourceName = "专注",
            description = "均衡反击流派，完美弹反增益，剑气远程斩击"
        }
    };

    /// <summary>根据ID获取流派数据</summary>
    public static StyleData GetStyle(CombatStyleID id)
    {
        foreach (var s in AllStyles)
        {
            if (s.styleID == id) return s;
        }
        return AllStyles[0]; // 默认返回"刃"
    }
}
