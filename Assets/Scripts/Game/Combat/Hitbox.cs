using UnityEngine;
using System.Collections.Generic;
using Game.Gameplay;

/// <summary>
/// 攻击判定框：挂在角色子 GameObject 上，配合 Trigger Collider2D 使用。
/// 由动画事件（Animation Event）控制 Enable/Disable，只在攻击的命中帧激活。
/// 使用 HashSet 防止同一 hitbox 对同一目标多段命中。
///
/// 用法：
/// 1. 创建子 GameObject，添加 BoxCollider2D (isTrigger=true) + Hitbox
/// 2. 在攻击动画的命中帧调用 EnableHitbox()，恢复帧调用 DisableHitbox()
/// 3. 或者直接在 Inspector 中配置 autoDisableTime 自动关闭
/// </summary>
public class Hitbox : MonoBehaviour
{
    [Header("伤害参数")]
    [Tooltip("基础伤害值")]
    public int damage = 10;
    [Tooltip("击退力度")]
    public float knockbackForce = 5f;
    [Tooltip("命中后自动关闭时间（秒），0=不自动关闭")]
    public float autoDisableTime = 0.2f;

    [Header("目标层")]
    [Tooltip("可以命中的目标层级")]
    public LayerMask targetLayer = -1; // 默认全部

    /// <summary>此 hitbox 是否可被弹反</summary>
    public bool isParryable = true;

    /// <summary>此 hitbox 的拥有者（用于区分友军伤害）</summary>
    public GameObject owner;

    // 已命中的目标，防止多段命中
    private HashSet<Hurtbox> _hitTargets = new HashSet<Hurtbox>();
    private float _disableTimer;
    private Collider2D _collider;

    /// <summary>hitbox 是否处于激活状态</summary>
    public bool IsActive => _collider != null && _collider.enabled;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _collider.isTrigger = true;
        _collider.enabled = false; // 默认关闭
    }

    /// <summary>启用 hitbox，开始检测命中</summary>
    public void EnableHitbox()
    {
        if (IsActive) return;

        _hitTargets.Clear();
        _collider.enabled = true;
        _disableTimer = autoDisableTime;
    }

    /// <summary>禁用 hitbox</summary>
    public void DisableHitbox()
    {
        if (!IsActive) return;

        _collider.enabled = false;
        _hitTargets.Clear();
    }

    private void Update()
    {
        // 自动关闭计时
        if (_collider.enabled && autoDisableTime > 0)
        {
            _disableTimer -= Time.deltaTime;
            if (_disableTimer <= 0)
            {
                DisableHitbox();
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (targetLayer != -1 && ((1 << other.gameObject.layer) & targetLayer) == 0) return;

        Hurtbox hurtbox = other.GetComponentInParent<Hurtbox>();
        if (hurtbox == null) return;
        if (hurtbox.gameObject == owner) return;
        if (!_hitTargets.Add(hurtbox)) return;

        Vector2 knockbackDir = (hurtbox.transform.position - transform.position).normalized;
        if (knockbackDir == Vector2.zero) knockbackDir = Vector2.right;

        int finalDamage = CalculateDamage(hurtbox.gameObject);
        var source = owner != null ? owner.GetComponent<IParryResponder>() : null;
        var result = hurtbox.ReceiveHit(new CombatHit(
            finalDamage,
            knockbackDir.x,
            knockbackForce,
            isParryable,
            source));
        if (result == CombatHitResult.Damaged)
        {
            owner?.GetComponent<PlayerStateMachine>()?.MarkHit();
            CombatEvents.InvokeHitLanded(hurtbox.transform.position, finalDamage);
            ApplyElementalEffects(hurtbox.gameObject);
        }
    }

    int CalculateDamage(GameObject target)
    {
        CharacterStats attackerStats = owner != null ? owner.GetComponent<CharacterStats>() : null;
        if (attackerStats == null) return damage;

        SecondaryAttributes sec = attackerStats.Secondary;
        int attrAtk = sec.GetAtk(attackerStats.combatStyle);

        int attrDef = 0;
        EnemyBase enemy = target.GetComponent<EnemyBase>();
        if (enemy != null)
            attrDef = enemy.GetDefense(attackerStats.combatStyle);
        else
        {
            CharacterStats defStats = target.GetComponent<CharacterStats>();
            if (defStats != null)
                attrDef = defStats.Secondary.GetDef(attackerStats.combatStyle);
        }

        return DamageCalculator.CalculateHpDamage(
            attrAtk, attrDef,
            damageReduction: attackerStats.damageReduction,
            critValue: sec.critValue + attackerStats.extraCritValue,
            critResistValue: sec.critResistValue,
            critDamageBonus: attackerStats.critDamageBonus,
            attackerLevel: attackerStats.level
        );
    }

    /// <summary>根据攻击者背包中的元素升级施加效果</summary>
    void ApplyElementalEffects(GameObject target)
    {
        if (ElementalEffectManager.Instance == null) return;

        var elementalIds = new[] { "elem_burn", "elem_frost", "elem_thunder", "elem_poison", "elem_ink_flame" };
        foreach (var id in elementalIds)
        {
            if (ElementalEffectManager.Instance.HasElementalEffect(id))
            {
                ElementalEffectManager.Instance.ApplyEffect(target, id);
            }
        }
    }

    /// <summary>动画事件：启用 hitbox（无参数版本）</summary>
    public void OnAnimationHitStart()
    {
        EnableHitbox();
    }

    /// <summary>动画事件：禁用 hitbox</summary>
    public void OnAnimationHitEnd()
    {
        DisableHitbox();
    }
}
