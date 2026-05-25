using UnityEngine;

/// <summary>
/// 弹反判定框：玩家前方的特殊 hitbox，仅在弹反窗口内激活。
/// 当敌方 Hitbox 进入此区域时，判定弹反成功。
///
/// 为什么不用 Hurtbox 直接判定弹反：
/// - ParryHitbox 是独立的 Trigger 区域，可以比 Hurtbox 小/偏前
/// - 弹反的判定范围 ≠ 受击范围，需要独立配置
/// </summary>
public class ParryHitbox : MonoBehaviour
{
    [Tooltip("弹反判定范围偏移（相对角色位置）")]
    public Vector2 offset = new Vector2(0.8f, 0f);
    [Tooltip("弹反判定范围大小")]
    public Vector2 size = new Vector2(1.0f, 1.2f);

    private PlayerStateMachine _stateMachine;
    private BoxCollider2D _collider;

    /// <summary>标记为弹反区域，供 Hitbox 识别</summary>
    public bool IsParryZone => true;

    private void Awake()
    {
        _stateMachine = GetComponentInParent<PlayerStateMachine>();
        _collider = gameObject.AddComponent<BoxCollider2D>();
        _collider.isTrigger = true;
        _collider.offset = offset;
        _collider.size = size;
        _collider.enabled = false; // 默认关闭

        // 设置 tag 便于识别
        gameObject.tag = "ParryZone";
    }

    private void Update()
    {
        // 只在弹反窗口内启用
        _collider.enabled = _stateMachine != null && _stateMachine.IsInParryWindow;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 只响应敌方 Hitbox
        Hitbox hitbox = other.GetComponent<Hitbox>();
        if (hitbox == null) return;
        if (hitbox.isParryable && _stateMachine != null && _stateMachine.IsInParryWindow)
        {
            _stateMachine.OnParrySuccess();
            // 禁用敌方 hitbox，防止穿透
            hitbox.DisableHitbox();
        }
    }
}
