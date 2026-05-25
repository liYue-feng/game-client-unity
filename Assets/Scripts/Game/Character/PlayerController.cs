using UnityEngine;

/// <summary>
/// 玩家控制器：处理移动物理、朝向翻转、地面检测。
/// 不负责战斗逻辑（由 PlayerStateMachine 处理），
/// 只负责"角色怎么动"。
///
/// 与 PlayerStateMachine 的分工：
/// - PlayerController：移动、物理、翻转
/// - PlayerStateMachine：战斗状态转换、攻击时机
/// - 冲刺的位移由状态机触发，控制器执行速度变化
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(InputMediator))]
public class PlayerController : MonoBehaviour
{
    [Header("移动参数")]
    [Tooltip("地面检测偏移（脚底位置）")]
    public Vector2 groundCheckOffset = new Vector2(0f, -0.5f);
    [Tooltip("地面检测半径")]
    public float groundCheckRadius = 0.2f;
    [Tooltip("地面层级")]
    public LayerMask groundLayer;

    private Rigidbody2D _rb;
    private CharacterStats _stats;
    private PlayerStateMachine _stateMachine;
    private InputMediator _input;
    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider;

    /// <summary>朝向：1=右, -1=左</summary>
    public int FacingDirection { get; private set; } = 1;
    /// <summary>是否在地面</summary>
    public bool IsGrounded { get; private set; }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _stats = GetComponent<CharacterStats>();
        _stateMachine = GetComponent<PlayerStateMachine>();
        _input = GetComponent<InputMediator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        // 地面检测
        IsGrounded = Physics2D.OverlapCircle(
            (Vector2)transform.position + groundCheckOffset,
            groundCheckRadius,
            groundLayer
        );

        // 朝向翻转（非攻击/冲刺时根据移动输入翻转）
        float moveInput = _input.MoveInput;
        if (moveInput > 0.1f && FacingDirection != 1)
            Flip();
        else if (moveInput < -0.1f && FacingDirection != -1)
            Flip();
    }

    private void FixedUpdate()
    {
        // 根据当前状态决定移动行为
        switch (_stateMachine.CurrentState)
        {
            case PlayerState.Idle:
            case PlayerState.Run:
                // 正常移动
                float moveInput = _input.MoveInput;
                _rb.velocity = new Vector2(
                    moveInput * _stats.moveSpeed,
                    _rb.velocity.y
                );

                // 根据移动输入更新状态
                if (Mathf.Abs(moveInput) > 0.1f)
                    _stateMachine.RequestMove();
                else
                    _stateMachine.RequestStop();
                break;

            case PlayerState.Dash:
                // 冲刺：固定方向高速移动
                _rb.velocity = new Vector2(
                    FacingDirection * _stats.dashSpeed,
                    _rb.velocity.y
                );
                break;

            case PlayerState.Attack1:
            case PlayerState.Attack2:
            case PlayerState.Attack3:
            case PlayerState.HeavyAttack:
                // 攻击中轻微减速移动（保留微量控制感）
                float attackMoveSpeed = _stats.moveSpeed * 0.2f;
                _rb.velocity = new Vector2(
                    _input.MoveInput * attackMoveSpeed,
                    _rb.velocity.y
                );
                break;

            case PlayerState.Hurt:
                // 受击时不接受移动输入，由击退力控制
                break;

            case PlayerState.Parry:
            case PlayerState.ParrySuccess:
                // 弹反/反击时不可移动
                _rb.velocity = new Vector2(0f, _rb.velocity.y);
                break;

            case PlayerState.Die:
                _rb.velocity = Vector2.zero;
                break;
        }
    }

    /// <summary>翻转朝向</summary>
    private void Flip()
    {
        FacingDirection *= -1;
        if (_spriteRenderer != null)
        {
            _spriteRenderer.flipX = FacingDirection == -1;
        }
    }

    /// <summary>
    /// 在 Inspector 中显示地面检测范围（调试用）
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(
            (Vector2)transform.position + groundCheckOffset,
            groundCheckRadius
        );
    }
}
