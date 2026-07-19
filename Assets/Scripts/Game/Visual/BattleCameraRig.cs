using Game.Gameplay;
using UnityEngine;

/// <summary>
/// 场景级横向相机跟随；CameraShaker 只修改子相机局部偏移。
/// </summary>
public sealed class BattleCameraRig : MonoBehaviour
{
    private const float VerticalFramingOffset = 0.4f;

    private Transform _target;
    private Camera _camera;
    private BattleArenaBounds _bounds;

    /// <summary>当前是否允许 Rig 在 LateUpdate 中消费玩家横向位置。</summary>
    public bool IsFollowing { get; private set; }

    /// <summary>
    /// 绑定当前战局目标与边界；Camera 保持为子物体，使震屏只拥有短时局部偏移。
    /// </summary>
    public void Configure(Transform target, BattleArenaBounds bounds, Camera camera)
    {
        _target = target;
        _bounds = bounds;
        _camera = camera;
        IsFollowing = target != null && camera != null;
        SnapToTarget();
    }

    /// <summary>终局先关闭长期跟随，从而保留最后构图且不干扰 CameraShaker 的局部状态。</summary>
    public void SetFollowEnabled(bool enabled)
    {
        IsFollowing = enabled && _target != null && _camera != null;
    }

    private void LateUpdate()
    {
        if (IsFollowing)
        {
            SnapToTarget();
        }
    }

    private void SnapToTarget()
    {
        if (_target == null || _camera == null)
        {
            IsFollowing = false;
            return;
        }

        var halfWidth = _camera.orthographicSize * _camera.aspect;
        var minimum = _bounds.MinX + halfWidth;
        var maximum = _bounds.MaxX - halfWidth;
        var x = minimum > maximum
            ? _bounds.CenterX
            : Mathf.Clamp(_target.position.x, minimum, maximum);
        transform.position = new Vector3(x, VerticalFramingOffset, 0f);
    }
}
