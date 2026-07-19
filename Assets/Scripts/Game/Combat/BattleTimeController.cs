using Game.Gameplay;
using UnityEngine;

public sealed class BattleTimeController : MonoBehaviour
{
    public const string PauseReason = "Pause";
    public const string LevelUpReason = "LevelUp";
    public const string HitStopReason = "HitStop";
    public const string ParrySlowMotionReason = "ParrySlowMotion";
    public const string BattleResultReason = "BattleResult";

    private static BattleTimeController _authoritativeController;
    private readonly TimeScaleRequestSet _requests = new TimeScaleRequestSet();

    public int ActiveRequestCount => _requests.Count;
    public float EffectiveScale => _requests.EffectiveScale;

    private void Awake()
    {
        _authoritativeController = this;
        ApplyEffectiveScale();
    }

    public TimeScaleRequestToken RequestTimeScale(string reason, float scale)
    {
        var token = _requests.Add(reason, scale);
        ApplyEffectiveScale();
        return token;
    }

    public bool UpdateTimeScale(TimeScaleRequestToken token, float scale)
    {
        var updated = _requests.Update(token, scale);
        if (updated)
        {
            ApplyEffectiveScale();
        }

        return updated;
    }

    public bool ReleaseTimeScale(TimeScaleRequestToken token)
    {
        var released = _requests.Release(token);
        if (released)
        {
            ApplyEffectiveScale();
        }

        return released;
    }

    private void ApplyEffectiveScale()
    {
        if (_authoritativeController == this)
        {
            Time.timeScale = _requests.EffectiveScale;
        }
    }

    private void OnDestroy()
    {
        _requests.Clear();
        if (_authoritativeController == this)
        {
            _authoritativeController = null;
            Time.timeScale = 1f;
        }
    }
}
