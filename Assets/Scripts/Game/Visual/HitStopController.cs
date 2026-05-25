using UnityEngine;
using System.Collections;

/// <summary>
/// 卡帧控制器：在打击命中的瞬间短暂冻结画面，增强冲击感。
/// 与弹反慢动作的区别：
/// - 慢动作(slow-mo)：0.5s，timeScale=0.2，用于弹反成功后的"子弹时间"
/// - 卡帧(hit stop)：0.03-0.06s，timeScale≈0，用于命中瞬间的"定格感"
///
/// 为什么需要保存/恢复 timeScale：卡帧可能与慢动作叠加触发，
/// 恢复时必须回到之前的值而非硬编码的 1.0。
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
    public bool IsInHitStop { get; private set; }

    private void OnEnable()
    {
        CombatEvents.OnHitLanded += OnHitLanded;
        CombatEvents.OnParrySuccess += OnParrySuccess;
        CombatEvents.OnDamageTaken += OnDamageTaken;
    }

    private void OnDisable()
    {
        CombatEvents.OnHitLanded -= OnHitLanded;
        CombatEvents.OnParrySuccess -= OnParrySuccess;
        CombatEvents.OnDamageTaken -= OnDamageTaken;
    }

    private void OnHitLanded(Vector3 pos, int dmg)
    {
        // 命中敌人：轻击卡帧
        DoHitStop(lightHitStopDuration);
    }

    private void OnParrySuccess(Vector3 pos)
    {
        // 弹反成功：较长卡帧（叠加后续慢动作，形成"停顿→慢放"的节奏）
        DoHitStop(parryHitStopDuration);
    }

    private void OnDamageTaken(Vector3 pos, int dmg)
    {
        // 受伤：轻微卡帧
        DoHitStop(lightHitStopDuration * 0.5f);
    }

    /// <summary>
    /// 执行卡帧。保存当前 timeScale，冻结，然后恢复。
    /// 使用真实时间计时，不受自身 timeScale 影响。
    /// </summary>
    public void DoHitStop(float duration)
    {
        if (duration <= 0) return;
        StartCoroutine(HitStopCoroutine(duration));
    }

    /// <summary>
    /// 外部调用的重击卡帧。由 PlayerStateMachine 在重击命中时调用。
    /// </summary>
    public void DoHeavyHitStop()
    {
        DoHitStop(heavyHitStopDuration);
    }

    private IEnumerator HitStopCoroutine(float duration)
    {
        // 保存当前 timeScale（可能是 1.0，也可能是慢动作的 0.2）
        float previousTimeScale = Time.timeScale;

        IsInHitStop = true;
        Time.timeScale = hitStopTimeScale;

        yield return new WaitForSecondsRealtime(duration);

        // 恢复到卡帧前的值
        Time.timeScale = previousTimeScale;
        IsInHitStop = false;
    }

    /// <summary>
    /// 安全恢复：防止场景切换时卡在 freeze 状态。
    /// </summary>
    private void OnDestroy()
    {
        if (IsInHitStop)
        {
            Time.timeScale = 1f;
        }
    }
}