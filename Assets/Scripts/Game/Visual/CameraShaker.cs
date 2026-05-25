using UnityEngine;
using System.Collections;

/// <summary>
/// 屏幕震动器：监听战斗事件，根据事件类型触发不同强度的屏幕震动。
/// 为什么用代码驱动而非 Animator：震动参数需要根据伤害值、弹反时机等动态调整，
/// 代码驱动更灵活。
///
/// 工作原理：
/// - 记录相机的原始位置
/// - 每帧生成随机偏移（递减衰减）
/// - 震动结束后恢复到原始位置
///
/// 使用方式：挂在 Main Camera 上，自动监听 CombatEvents。
/// </summary>
public class CameraShaker : MonoBehaviour
{
    [Header("命中震动")]
    [Tooltip("玩家命中敌人时的震动强度")]
    public float hitShakeIntensity = 0.05f;
    [Tooltip("命中震动持续时间（秒）")]
    public float hitShakeDuration = 0.08f;

    [Header("受伤震动")]
    [Tooltip("玩家受伤时的震动强度")]
    public float hurtShakeIntensity = 0.12f;
    [Tooltip("受伤震动持续时间（秒）")]
    public float hurtShakeDuration = 0.15f;

    [Header("弹反震动")]
    [Tooltip("弹反成功时的震动强度")]
    public float parryShakeIntensity = 0.2f;
    [Tooltip("弹反震动持续时间（秒）")]
    public float parryShakeDuration = 0.25f;

    [Header("死亡震动")]
    [Tooltip("玩家死亡时的震动强度")]
    public float deathShakeIntensity = 0.3f;
    [Tooltip("死亡震动持续时间（秒）")]
    public float deathShakeDuration = 0.4f;

    private Vector3 _originalPosition;
    private Coroutine _currentShake;

    private void Awake()
    {
        _originalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        CombatEvents.OnHitLanded += OnHitLanded;
        CombatEvents.OnDamageTaken += OnDamageTaken;
        CombatEvents.OnParrySuccess += OnParrySuccess;
        CombatEvents.OnPlayerDeath += OnPlayerDeath;
    }

    private void OnDisable()
    {
        CombatEvents.OnHitLanded -= OnHitLanded;
        CombatEvents.OnDamageTaken -= OnDamageTaken;
        CombatEvents.OnParrySuccess -= OnParrySuccess;
        CombatEvents.OnPlayerDeath -= OnPlayerDeath;
    }

    private void OnHitLanded(Vector3 pos, int dmg)
    {
        Shake(hitShakeIntensity, hitShakeDuration);
    }

    private void OnDamageTaken(Vector3 pos, int dmg)
    {
        Shake(hurtShakeIntensity, hurtShakeDuration);
    }

    private void OnParrySuccess(Vector3 pos)
    {
        Shake(parryShakeIntensity, parryShakeDuration);
    }

    private void OnPlayerDeath()
    {
        Shake(deathShakeIntensity, deathShakeDuration);
    }

    /// <summary>
    /// 触发屏幕震动。新的震动会打断当前震动。
    /// </summary>
    public void Shake(float intensity, float duration)
    {
        if (_currentShake != null)
        {
            StopCoroutine(_currentShake);
        }
        _currentShake = StartCoroutine(ShakeCoroutine(intensity, duration));
    }

    /// <summary>
    /// 震动协程：每帧随机偏移，强度随时间线性衰减。
    /// </summary>
    private IEnumerator ShakeCoroutine(float intensity, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float currentIntensity = intensity * (1f - elapsed / duration);
            float x = Random.Range(-1f, 1f) * currentIntensity;
            float y = Random.Range(-1f, 1f) * currentIntensity;
            transform.localPosition = _originalPosition + new Vector3(x, y, 0f);
            yield return null;
        }
        transform.localPosition = _originalPosition;
    }

    /// <summary>
    /// 外部可调用的自定义震动（用于特殊事件如 Boss 登场等）。
    /// </summary>
    public void CustomShake(float intensity, float duration)
    {
        Shake(intensity, duration);
    }
}