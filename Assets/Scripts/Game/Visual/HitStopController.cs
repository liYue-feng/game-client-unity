using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Game.Gameplay;

/// <summary>
/// 卡帧控制器：在打击命中的瞬间短暂冻结画面，增强冲击感。
/// 与弹反慢动作的区别：
/// - 慢动作(slow-mo)：0.5s，timeScale=0.2，用于弹反成功后的"子弹时间"
/// - 卡帧(hit stop)：0.03-0.06s，timeScale≈0，用于命中瞬间的"定格感"
///
/// 每次卡帧持有独立的时间请求，与慢动作、暂停等请求按最小值组合。
/// </summary>
public class HitStopController : MonoBehaviour
{
    [Header("卡帧参数")]
    [Tooltip("轻攻击命中卡帧时长（真实秒）")]
    public float lightHitStopDuration = 0.03f;
    [Tooltip("重击命中卡帧时长（真实秒）")]
    public float heavyHitStopDuration = 0.05f;
    [Tooltip("弹反成功卡帧时长（真实秒）")]
    public float parryHitStopDuration = 0.06f;
    [Tooltip("卡帧时的时间缩放")]
    public float hitStopTimeScale = 0.05f;

    /// <summary>是否正在卡帧中</summary>
    public bool IsInHitStop => _activeHitStopTokens.Count > 0;

    private BattleTimeController _battleTimeController;
    private readonly HashSet<TimeScaleRequestToken> _activeHitStopTokens =
        new HashSet<TimeScaleRequestToken>();

    public void ConfigureBattleTimeController(BattleTimeController controller)
    {
        if (_battleTimeController == controller)
        {
            return;
        }

        StopAllCoroutines();
        ReleaseAllHitStopRequests();
        _battleTimeController = controller;
    }

    private void OnDisable()
    {
        ClearHitStops();
    }

    /// <summary>
    /// 执行卡帧。每次调用申请独立请求，并按真实时间单独释放。
    /// </summary>
    public void DoHitStop(float duration)
    {
        if (duration <= 0 || _battleTimeController == null) return;
        StartCoroutine(HitStopCoroutine(duration));
    }

    /// <summary>
    /// 外部调用的重击卡帧。由 PlayerStateMachine 在重击命中时调用。
    /// </summary>
    public void DoHeavyHitStop()
    {
        DoHitStop(heavyHitStopDuration);
    }

    public void ClearHitStops()
    {
        StopAllCoroutines();
        ReleaseAllHitStopRequests();
    }

    private IEnumerator HitStopCoroutine(float duration)
    {
        var owner = _battleTimeController;
        var token = owner.RequestTimeScale(BattleTimeController.HitStopReason, hitStopTimeScale);
        _activeHitStopTokens.Add(token);

        yield return new WaitForSecondsRealtime(duration);

        ReleaseHitStopRequest(owner, token);
    }

    /// <summary>
    /// 安全恢复：防止场景切换时卡在 freeze 状态。
    /// </summary>
    private void OnDestroy()
    {
        ReleaseAllHitStopRequests();
    }

    private void ReleaseHitStopRequest(
        BattleTimeController owner,
        TimeScaleRequestToken token)
    {
        if (_activeHitStopTokens.Remove(token) && owner != null)
        {
            owner.ReleaseTimeScale(token);
        }
    }

    private void ReleaseAllHitStopRequests()
    {
        if (_battleTimeController != null)
        {
            foreach (var token in _activeHitStopTokens)
            {
                _battleTimeController.ReleaseTimeScale(token);
            }
        }

        _activeHitStopTokens.Clear();
    }
}
