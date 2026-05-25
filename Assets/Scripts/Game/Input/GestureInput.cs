using UnityEngine;

/// <summary>
/// 手势输入：将触摸屏操作转换为战斗输入信号。
/// 微信小游戏移动端适配：
/// - 点击 = 攻击
/// - 滑动 = 冲刺
/// - 长按 = 重击
/// - 弹反窗口内点击 = 弹反
///
/// 为什么独立于 InputHandler：手势识别需要状态跟踪（按下时间、移动距离），
/// 与键盘输入的逻辑完全不同。通过 InputMediator 统一输出。
/// </summary>
public class GestureInput : MonoBehaviour
{
    [Header("手势阈值")]
    [Tooltip("滑动判定距离（像素）")]
    public float swipeThreshold = 50f;
    [Tooltip("长按判定时间（秒）")]
    public float longPressThreshold = 0.4f;
    [Tooltip("点击最大持续时间（秒）")]
    public float tapWindow = 0.2f;

    // 手势状态
    private bool _isTouching;
    private Vector2 _touchStartPos;
    private float _touchStartTime;

    // 本帧输出
    private bool _attackPressed;
    private bool _parryPressed;
    private bool _dashPressed;
    private bool _heavyAttackPressed;
    private float _moveInput;

    /// <summary>水平移动输入</summary>
    public float MoveInput => _moveInput;
    /// <summary>攻击</summary>
    public bool AttackPressed => _attackPressed;
    /// <summary>弹反</summary>
    public bool ParryPressed => _parryPressed;
    /// <summary>冲刺</summary>
    public bool DashPressed => _dashPressed;
    /// <summary>重击</summary>
    public bool HeavyAttackPressed => _heavyAttackPressed;

    private void Update()
    {
        // 清除每帧脉冲
        _attackPressed = false;
        _parryPressed = false;
        _dashPressed = false;
        _heavyAttackPressed = false;

        // 移动：虚拟摇杆区域（屏幕左半部分）
        _moveInput = 0f;

#if UNITY_EDITOR || UNITY_STANDALONE
        // 编辑器下用鼠标模拟
        HandleMouseInput();
#else
        HandleTouchInput();
#endif
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                _isTouching = true;
                _touchStartPos = touch.position;
                _touchStartTime = Time.time;

                // 判断是左侧（移动）还是右侧（操作）
                if (touch.position.x < Screen.width * 0.4f)
                {
                    // 左侧：移动区域
                    _moveInput = 0f;
                }
                break;

            case TouchPhase.Moved:
                // 左侧移动
                if (_touchStartPos.x < Screen.width * 0.4f)
                {
                    float dx = touch.position.x - _touchStartPos.x;
                    _moveInput = Mathf.Clamp(dx / 50f, -1f, 1f);
                }
                break;

            case TouchPhase.Ended:
                if (!_isTouching) break;
                _isTouching = false;

                float elapsed = Time.time - _touchStartTime;
                float distance = Vector2.Distance(touch.position, _touchStartPos);

                // 右侧：操作区域
                if (_touchStartPos.x >= Screen.width * 0.4f)
                {
                    if (distance > swipeThreshold)
                    {
                        // 滑动 = 冲刺
                        _dashPressed = true;
                    }
                    else if (elapsed >= longPressThreshold)
                    {
                        // 长按 = 重击
                        _heavyAttackPressed = true;
                    }
                    else if (elapsed <= tapWindow)
                    {
                        // 点击 = 攻击 或 弹反
                        // 如果在弹反窗口内，视为弹反
                        var parryCtrl = FindObjectOfType<ParryController>();
                        if (parryCtrl != null && parryCtrl.IsInCounterWindow)
                        {
                            _parryPressed = true;
                        }
                        else
                        {
                            _attackPressed = true;
                        }
                    }
                }
                break;
        }
    }

    private void HandleMouseInput()
    {
        // 编辑器下用鼠标右键模拟攻击，中键模拟弹反
        if (Input.GetMouseButtonDown(0))
        {
            _attackPressed = true;
        }
    }
}
