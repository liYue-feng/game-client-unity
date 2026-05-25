using UnityEngine;

/// <summary>
/// 流派ID枚举
/// </summary>
public enum CombatStyleID
{
    /// <summary>刃——高速连击</summary>
    Blade = 1,
    /// <summary>印——弹反强化</summary>
    Seal = 2,
    /// <summary>毒——持续伤害</summary>
    Poison = 3,
    /// <summary>血——高风险高回报</summary>
    Blood = 4,
    /// <summary>剑——均衡反击</summary>
    Sword = 5
}

/// <summary>
/// 流派数据：定义一个流派的所有属性倍率和特殊资源。
/// 服务端也有对应的 StyleConfigItem，两边保持同步。
/// </summary>
[System.Serializable]
public class StyleData
{
    public CombatStyleID styleID;
    public string styleName;
    public float damageMult = 1f;
    public float speedMult = 1f;
    public float parryMult = 1f;
    public float dashSpeedMult = 1f;
    public float dashCostMult = 1f;
    public int specialResourceMax = 100;
    public string specialResourceName;
    public string description;
}
