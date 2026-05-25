using UnityEngine;
using System.Collections.Generic;

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
    private HashSet<Collider2D> _hitTargets = new HashSet<Collider2D>();
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
        _hitTargets.Clear();
        _collider.enabled = true;
        _disableTimer = autoDisableTime;
    }

    /// <summary>禁用 hitbox</summary>
    public void DisableHitbox()
    {
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
        // 防止重复命中
        if (_hitTargets.Contains(other)) return;

        // 层级过滤
        if (targetLayer != -1 && ((1 << other.gameObject.layer) & targetLayer) == 0) return;

        // 不命中自己
        if (other.gameObject == owner) return;

        // 查找 Hurtbox
        Hurtbox hurtbox = other.GetComponent<Hurtbox>();
        if (hurtbox == null) return;

        _hitTargets.Add(other);

        // 计算击退方向
        Vector2 knockbackDir = (other.transform.position - transform.position).normalized;
        if (knockbackDir == Vector2.zero) knockbackDir = Vector2.right;

        // 通知 Hurtbox
        hurtbox.ReceiveHit(damage, knockbackDir.x, knockbackForce, this);

        // 触发全局事件
        CombatEvents.InvokeHitLanded(other.transform.position, damage);

        // 应用玩家背包中的元素效果
        ApplyElementalEffects(other.gameObject);
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
