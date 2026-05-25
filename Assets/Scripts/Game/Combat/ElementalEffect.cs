using UnityEngine;
using System.Collections;

/// <summary>
/// 元素效果数据：定义一种元素状态的属性。
/// 挂在受影响的敌人上，由 ElementalEffectManager 管理。
/// </summary>
public enum ElementalType
{
    Burn,       // 灼烧 — 持续火焰伤害
    Frost,      // 冰霜 — 减速
    Thunder,    // 惊雷 — 弹跳伤害
    Poison,     // 毒雾 — 持续毒伤害
    InkFlame    // 墨焰 — 百分比生命伤害
}

/// <summary>
/// 元素效果实例：敌人身上的一个活跃元素状态。
/// </summary>
public class ActiveEffect : MonoBehaviour
{
    public ElementalType type;
    public float damagePerTick;       // 每跳伤害
    public float tickInterval;        // 间隔（秒）
    public float remainingDuration;   // 剩余时间
    public float slowPercent;         // 减速百分比（仅Frost）
    public float bounceRange = 3f;    // 弹跳范围（仅Thunder）
    public int bounceCount = 2;       // 弹跳次数（仅Thunder）
    public float percentHpDamage;     // 百分比HP伤害（仅InkFlame）

    private float _tickTimer;
    private EnemyBase _enemy;

    void Awake()
    {
        _enemy = GetComponent<EnemyBase>();
    }

    /// <summary>初始化效果</summary>
    public void Init(ElementalType effectType, float dps, float duration,
        float tickFreq = 0.5f, float slow = 0, float percentDmg = 0)
    {
        type = effectType;
        damagePerTick = dps * tickFreq;
        tickInterval = tickFreq;
        remainingDuration = duration;
        slowPercent = slow;
        percentHpDamage = percentDmg;
        _tickTimer = 0f;
    }

    void Update()
    {
        if (_enemy == null || _enemy.IsDead)
        {
            Destroy(this);
            return;
        }

        remainingDuration -= Time.deltaTime;
        if (remainingDuration <= 0)
        {
            Destroy(this);
            return;
        }

        _tickTimer += Time.deltaTime;
        if (_tickTimer >= tickInterval)
        {
            _tickTimer -= tickInterval;
            ApplyTick();
        }
    }

    void ApplyTick()
    {
        int dmg = Mathf.RoundToInt(damagePerTick);
        if (percentHpDamage > 0 && _enemy != null)
        {
            var stats = _enemy.GetComponent<CharacterStats>();
            if (stats != null)
            {
                dmg += Mathf.RoundToInt(stats.maxHp * percentHpDamage * tickInterval);
            }
        }

        if (dmg > 0 && _enemy != null)
        {
            // 通过 combat 方式扣血
            var stats = _enemy.GetComponent<CharacterStats>();
            if (stats != null) stats.TakeDamage(dmg);

            DamageNumberPool.Spawn(dmg, transform.position + Vector3.up * 0.5f,
                type == ElementalType.InkFlame ? DamageType.Crit : DamageType.Normal);
        }

        // Thunder 弹跳
        if (type == ElementalType.Thunder && bounceCount > 0 && _enemy != null)
        {
            BounceThunder();
        }
    }

    void BounceThunder()
    {
        var enemies = FindObjectsOfType<EnemyBase>();
        EnemyBase closest = null;
        float closestDist = bounceRange;

        foreach (var e in enemies)
        {
            if (e == _enemy || e.IsDead) continue;
            float dist = Vector3.Distance(transform.position, e.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = e;
            }
        }

        if (closest != null)
        {
            var boltGo = new GameObject("ThunderBolt");
            boltGo.transform.position = closest.transform.position + Vector3.up * 0.5f;
            var line = boltGo.AddComponent<LineRenderer>();
            line.startWidth = 0.04f;
            line.endWidth = 0.02f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = ShuiMoPalette.Gamboge;
            line.endColor = ShuiMoPalette.InkPurple;
            line.SetPosition(0, transform.position + Vector3.up * 0.5f);
            line.SetPosition(1, closest.transform.position + Vector3.up * 0.5f);
            Destroy(boltGo, 0.15f);

            int bounceDmg = Mathf.RoundToInt(damagePerTick * 0.5f);
            var stats = closest.GetComponent<CharacterStats>();
            if (stats != null) stats.TakeDamage(bounceDmg);
            DamageNumberPool.Spawn(bounceDmg, closest.transform.position + Vector3.up,
                DamageType.Normal);

            bounceCount--;
            AudioManager.Instance.PlaySFX("special_skill");
        }
    }

    /// <summary>获取减速倍数（1=无减速，0.5=50%减速）</summary>
    public float GetSlowMultiplier()
    {
        if (type != ElementalType.Frost) return 1f;
        if (remainingDuration <= 0) return 1f;
        return 1f - slowPercent;
    }
}

/// <summary>
/// 元素效果管理器：统一管理所有敌人的活跃元素状态。
/// 单例模式。
/// </summary>
public class ElementalEffectManager : MonoBehaviour
{
    private static ElementalEffectManager _instance;
    public static ElementalEffectManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[ElementalEffectManager]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ElementalEffectManager>();
            }
            return _instance;
        }
    }

    /// <summary>检查玩家是否拥有某个元素升级（通过背包）</summary>
    public bool HasElementalEffect(string elementId)
    {
        if (Inventory.Instance == null) return false;
        for (int i = 0; i < Inventory.Instance.Count; i++)
        {
            if (Inventory.Instance.Items[i]?.id == elementId)
                return true;
        }
        return false;
    }

    /// <summary>获取元素升级的等级</summary>
    public int GetEffectLevel(string elementId)
    {
        if (Inventory.Instance == null) return 0;
        for (int i = 0; i < Inventory.Instance.Count; i++)
        {
            if (Inventory.Instance.Items[i]?.id == elementId)
                return Inventory.Instance.Items[i].currentLevel;
        }
        return 0;
    }

    /// <summary>对敌人施加元素效果</summary>
    public void ApplyEffect(GameObject target, string elementId)
    {
        var existing = target.GetComponent<ActiveEffect>();
        var stats = target.GetComponent<CharacterStats>();
        if (stats == null) return;

        float duration = 4f;
        float dps = 3f;
        int level = GetEffectLevel(elementId);

        switch (elementId)
        {
            case "elem_burn":
                // 灼烧: 每秒3点 * 等级
                if (existing != null && existing.type == ElementalType.Burn)
                {
                    existing.remainingDuration = Mathf.Max(existing.remainingDuration, duration);
                    return;
                }
                if (existing != null) Destroy(existing);
                var burn = target.AddComponent<ActiveEffect>();
                burn.Init(ElementalType.Burn, dps * level, duration);
                SpawnVisual(target, ShuiMoPalette.Vermillion, "🔥");
                break;

            case "elem_frost":
                // 冰霜: 减速20% * 等级，持续4秒
                if (existing != null && existing.type == ElementalType.Frost)
                {
                    existing.remainingDuration = Mathf.Max(existing.remainingDuration, duration);
                    existing.slowPercent = Mathf.Max(existing.slowPercent, 0.2f * level);
                    return;
                }
                if (existing != null) Destroy(existing);
                var frost = target.AddComponent<ActiveEffect>();
                frost.Init(ElementalType.Frost, 2f * level, duration, slow: 0.2f * level);
                SpawnVisual(target, ShuiMoPalette.FlowerBlue, "❄");
                break;

            case "elem_thunder":
                // 惊雷: 弹跳2次
                dps = 5f * level;
                if (existing != null && existing.type == ElementalType.Thunder)
                {
                    existing.damagePerTick = dps * 0.5f;
                    existing.remainingDuration = Mathf.Max(existing.remainingDuration, duration);
                    return;
                }
                if (existing != null) Destroy(existing);
                var thunder = target.AddComponent<ActiveEffect>();
                thunder.Init(ElementalType.Thunder, dps, duration, tickFreq: 0.8f);
                thunder.bounceCount = 2 + level;
                SpawnVisual(target, ShuiMoPalette.Gamboge, "⚡");
                break;

            case "elem_poison":
                // 毒雾: 持续4秒毒伤害
                dps = 4f * level;
                if (existing != null && existing.type == ElementalType.Poison)
                {
                    existing.damagePerTick = dps * 0.5f;
                    existing.remainingDuration = Mathf.Max(existing.remainingDuration, duration);
                    return;
                }
                if (existing != null) Destroy(existing);
                var poison = target.AddComponent<ActiveEffect>();
                poison.Init(ElementalType.Poison, dps, duration);
                SpawnVisual(target, ShuiMoPalette.JadeGreen, "☠");
                break;

            case "elem_ink_flame":
                // 墨焰: 百分比生命伤害 + 固定伤害
                float percentDmg = 0.02f * level;
                if (existing != null && existing.type == ElementalType.InkFlame)
                {
                    existing.percentHpDamage = percentDmg;
                    existing.remainingDuration = Mathf.Max(existing.remainingDuration, duration);
                    return;
                }
                if (existing != null) Destroy(existing);
                var flame = target.AddComponent<ActiveEffect>();
                flame.Init(ElementalType.InkFlame, 6f * level, duration, percentDmg: percentDmg);
                SpawnVisual(target, ShuiMoPalette.InkPurple, "墨焰");
                break;
        }
    }

    /// <summary>简易视觉反馈</summary>
    void SpawnVisual(GameObject target, Color color, string label)
    {
        DamageNumberPool.SpawnText(label, target.transform.position + Vector3.up * 0.8f,
            DamageType.Normal);

        // 颜色闪烁
        var sr = target.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            StartCoroutine(FlashColor(sr, color, 0.3f));
        }
    }

    IEnumerator FlashColor(SpriteRenderer sr, Color color, float duration)
    {
        var original = sr.color;
        sr.color = color;
        yield return new WaitForSeconds(duration);
        sr.color = original;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}