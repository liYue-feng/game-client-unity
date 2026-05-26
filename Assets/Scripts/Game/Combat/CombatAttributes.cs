using System;
using UnityEngine;

/// <summary>
/// 战斗属性系统：一级属性 → 二级属性转换 + 伤害公式。
/// 严格按照《代号·剑》设计文档实现。
///
/// 一级属性：力量/内力/体力/精神/悟性 — 不直接参与战斗
/// 二级属性：5种攻防 + HP/耐力 — 直接用于伤害计算
/// 等级系数：每级 +0.25x，影响一级→二级转换倍率
/// </summary>

// ====== 战斗流派 ======
public enum CombatStyle
{
    Sword = 0,  // 剑 — 均衡
    Blade = 1,  // 刀 — 爆发
    Seal = 2,   // 印 — 弹反
    Poison = 3, // 毒 — 持续
    Blood = 4   // 血 — 牺牲
}

// ====== 一级属性 ======
[Serializable]
public struct PrimaryAttributes
{
    public int strength;       // 力量 → 剑ATK/DEF, 刀ATK/DEF
    public int innerForce;     // 内力 → 印ATK/DEF, 毒ATK/DEF, 血ATK/DEF
    public int vitality;       // 体力 → HP Max, Stamina Max
    public int spirit;         // 精神 → HP Regen, Stamina Regen
    public int comprehension;  // 悟性 → Crit, Crit Resist

    public static PrimaryAttributes Default => new PrimaryAttributes
    {
        strength = 5,
        innerForce = 5,
        vitality = 5,
        spirit = 3,
        comprehension = 3
    };

    public static PrimaryAttributes Zero => new PrimaryAttributes();

    public static PrimaryAttributes operator +(PrimaryAttributes a, PrimaryAttributes b)
    {
        return new PrimaryAttributes
        {
            strength = a.strength + b.strength,
            innerForce = a.innerForce + b.innerForce,
            vitality = a.vitality + b.vitality,
            spirit = a.spirit + b.spirit,
            comprehension = a.comprehension + b.comprehension
        };
    }
}

// ====== 二级属性（由一级属性+等级系数计算得出） ======
[Serializable]
public struct SecondaryAttributes
{
    public int swordAtk, bladeAtk, sealAtk, poisonAtk, bloodAtk;
    public int swordDef, bladeDef, sealDef, poisonDef, bloodDef;
    public int maxHp, maxStamina;
    public int hpRegen, staminaRegen;
    public int critValue, critResistValue;

    /// <summary>获取指定流派的ATK</summary>
    public int GetAtk(CombatStyle style)
    {
        switch (style)
        {
            case CombatStyle.Sword: return swordAtk;
            case CombatStyle.Blade: return bladeAtk;
            case CombatStyle.Seal: return sealAtk;
            case CombatStyle.Poison: return poisonAtk;
            case CombatStyle.Blood: return bloodAtk;
            default: return 0;
        }
    }

    /// <summary>获取指定流派的DEF</summary>
    public int GetDef(CombatStyle style)
    {
        switch (style)
        {
            case CombatStyle.Sword: return swordDef;
            case CombatStyle.Blade: return bladeDef;
            case CombatStyle.Seal: return sealDef;
            case CombatStyle.Poison: return poisonDef;
            case CombatStyle.Blood: return bloodDef;
            default: return 0;
        }
    }

    /// <summary>获取最高的ATK流派</summary>
    public CombatStyle HighestAtkStyle
    {
        get
        {
            int max = swordAtk;
            CombatStyle best = CombatStyle.Sword;
            if (bladeAtk > max) { max = bladeAtk; best = CombatStyle.Blade; }
            if (sealAtk > max) { max = sealAtk; best = CombatStyle.Seal; }
            if (poisonAtk > max) { max = poisonAtk; best = CombatStyle.Poison; }
            if (bloodAtk > max) { max = bloodAtk; best = CombatStyle.Blood; }
            return best;
        }
    }
}

// ====== 一级→二级属性转换器 ======
public static class PrimaryAttributeConverter
{
    // 等级系数：Lv1=1.0, Lv2=1.25, Lv3=1.5, ... 每级 +0.25
    public static float LevelMultiplier(int level)
    {
        return 1.0f + (level - 1) * 0.25f;
    }

    /// <summary>一级属性 → 二级属性转换</summary>
    public static SecondaryAttributes Convert(PrimaryAttributes primary, int level)
    {
        float lvMult = LevelMultiplier(level);
        return new SecondaryAttributes
        {
            // 力量 → 剑/刀 ATK+DEF
            swordAtk = Mathf.RoundToInt(primary.strength * 4f * lvMult),
            bladeAtk = Mathf.RoundToInt(primary.strength * 2f * lvMult),
            swordDef = Mathf.RoundToInt(primary.strength * 2f * lvMult),
            bladeDef = Mathf.RoundToInt(primary.strength * 1f * lvMult),

            // 内力 → 印/毒/血 ATK+DEF
            sealAtk = Mathf.RoundToInt(primary.innerForce * 4f * lvMult),
            poisonAtk = Mathf.RoundToInt(primary.innerForce * 2f * lvMult),
            bloodAtk = Mathf.RoundToInt(primary.innerForce * 2f * lvMult),
            sealDef = Mathf.RoundToInt(primary.innerForce * 2f * lvMult),
            poisonDef = Mathf.RoundToInt(primary.innerForce * 1f * lvMult),
            bloodDef = Mathf.RoundToInt(primary.innerForce * 1f * lvMult),

            // 体力 → HP/耐力
            maxHp = Mathf.RoundToInt(20f + primary.vitality * 20f * lvMult),
            maxStamina = Mathf.RoundToInt(20f + primary.vitality * 10f * lvMult),

            // 精神 → 回复
            hpRegen = Mathf.RoundToInt(primary.spirit * 2f * lvMult),
            staminaRegen = Mathf.RoundToInt(primary.spirit * 2f * lvMult),

            // 悟性 → 暴击
            critValue = Mathf.RoundToInt(primary.comprehension * 10f * lvMult),
            critResistValue = Mathf.RoundToInt(primary.comprehension * 5f * lvMult)
        };
    }
}

// ====== 伤害计算器 ======
public static class DamageCalculator
{
    /// <summary>暴击等级系数（暴击率 = 暴击值 / 系数）</summary>
    public static float CritLevelCoefficient(int level)
    {
        // Lv10=5000, Lv11=10000 — 等比缩放适配当前等级
        return 500f * Mathf.Pow(1.5f, level - 1);
    }

    /// <summary>基础暴击伤害倍率</summary>
    public const float BaseCritMultiplier = 2.0f;

    /// <summary>
    /// 计算HP伤害。
    /// 公式：Base = ATK * (1 - min(0.9, DEF * 0.01 * DEF_coef))
    ///       Final = (Base * skill% + absolute) * (1 - dmg_reduction%)
    /// </summary>
    public static int CalculateHpDamage(
        int attrAtk, int attrDef,
        float skillPercentage = 1f, int absoluteDamage = 0,
        float damageReduction = 0f,
        int critValue = 0, int critResistValue = 0,
        float critDamageBonus = 0f, int attackerLevel = 1)
    {
        // 防御减免（上限90%）
        float defCoef = 0.01f; // DEF转换系数
        float reduction = Mathf.Min(0.9f, attrDef * defCoef * defCoef * 10f);
        float baseDamage = attrAtk * (1f - reduction);

        // 技能倍率
        float damage = baseDamage * skillPercentage + absoluteDamage;

        // 伤害减免
        damage *= (1f - Mathf.Clamp01(damageReduction));

        // 暴击判定
        float critRate = 0f;
        if (critValue > 0)
        {
            float coeff = CritLevelCoefficient(attackerLevel);
            float effectiveCrit = Mathf.Max(0, critValue - critResistValue);
            critRate = effectiveCrit / coeff;
        }

        if (UnityEngine.Random.value < critRate)
        {
            damage *= BaseCritMultiplier + critDamageBonus;
        }

        return Mathf.Max(1, Mathf.RoundToInt(damage));
    }

    /// <summary>
    /// 计算耐力伤害。
    /// 公式：Base = 最高属性ATK * 0.375
    ///       Final = Base * skill% + absolute
    /// </summary>
    public static int CalculateStaminaDamage(
        SecondaryAttributes attr,
        float skillPercentage = 1f, int absoluteDamage = 0)
    {
        int highestAtk = Mathf.Max(
            attr.swordAtk, attr.bladeAtk, attr.sealAtk,
            attr.poisonAtk, attr.bloodAtk);
        float baseDamage = highestAtk * 0.375f;
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * skillPercentage + absoluteDamage));
    }
}