using UnityEngine;
using System.Collections;

/// <summary>
/// 血流派：高风险高回报，鲜血资源，血祭特殊技能。
/// 被动：受伤时+10鲜血，低HP时伤害额外提升
/// 特殊：血祭——消耗20%当前HP，3秒内伤害x3
/// </summary>
public class BloodStyle : IStyleBehaviour
{
    private StyleData _data;

    public BloodStyle()
    {
        _data = StyleDatabase.GetStyle(CombatStyleID.Blood);
    }

    public void OnAttackHit(EnemyBase enemy)
    {
        // 血流派攻击不增加鲜血（通过受伤增加）
    }

    public void OnParrySuccess()
    {
        // 弹反也加鲜血
        StyleManager.Instance.AddSpecialResource(8);
    }

    public void ActivateSpecial(GameObject player)
    {
        // 血祭：扣20%HP，3秒3x伤害
        var stats = player.GetComponent<CharacterStats>();
        if (stats == null) return;

        int hpCost = Mathf.RoundToInt(stats.currentHp * 0.2f);
        stats.TakeDamage(hpCost);

        // 3秒3x伤害buff
        var buff = player.AddComponent<BloodPriceBuff>();
        buff.duration = 3f;
        buff.damageMultiplier = 3f;

        // 受伤时加鲜血
        StyleManager.Instance.AddSpecialResource(hpCost);
    }

    public void PassiveUpdate()
    {
        // 低HP时伤害提升（在 PlayerStateMachine 中读取 bloodDamageBonus）
    }

    /// <summary>根据当前HP计算额外伤害倍率</summary>
    public static float GetDamageBonus(CharacterStats stats)
    {
        if (stats == null) return 1f;
        float hpPercent = (float)stats.currentHp / stats.maxHp;
        if (hpPercent < 0.3f) return 1.5f;
        if (hpPercent < 0.5f) return 1.2f;
        return 1f;
    }

    public StyleData GetData() => _data;
}

/// <summary>血祭buff：3秒内伤害x3</summary>
public class BloodPriceBuff : MonoBehaviour
{
    public float duration = 3f;
    public float damageMultiplier = 3f;

    private void Update()
    {
        duration -= Time.deltaTime;
        if (duration <= 0)
        {
            Destroy(this);
        }
    }
}
