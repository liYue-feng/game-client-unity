using System;
using System.Collections.Generic;
using Game.Gameplay;
using UnityEngine;

/// <summary>
/// Scene owner for every presentation side effect produced by resolved combat hits.
/// </summary>
public sealed class CombatFeedbackController : MonoBehaviour, IDisposable
{
    private readonly HashSet<HitEffectPlayer> _activeHitEffects = new HashSet<HitEffectPlayer>();
    private GameObject _player;
    private InkParticlePool _inkParticlePool;
    private InkHitEffect _inkHitEffect;
    private InkSlashEffect _inkSlashEffect;
    private CameraShaker _cameraShaker;
    private HitStopController _hitStop;
    private bool _configured;
    private bool _disposed;
    private bool _acceptingFeedback;

    public void Configure(
        GameObject player,
        InkParticlePool inkParticlePool,
        InkHitEffect inkHitEffect,
        InkSlashEffect inkSlashEffect,
        CameraShaker cameraShaker,
        HitStopController hitStop)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CombatFeedbackController));
        }

        if (_configured)
        {
            throw new InvalidOperationException("CombatFeedbackController can only be configured once.");
        }

        _player = player != null ? player : throw new ArgumentNullException(nameof(player));
        _inkParticlePool = inkParticlePool != null
            ? inkParticlePool
            : throw new ArgumentNullException(nameof(inkParticlePool));
        _inkHitEffect = inkHitEffect != null ? inkHitEffect : throw new ArgumentNullException(nameof(inkHitEffect));
        _inkSlashEffect = inkSlashEffect != null
            ? inkSlashEffect
            : throw new ArgumentNullException(nameof(inkSlashEffect));
        _cameraShaker = cameraShaker != null
            ? cameraShaker
            : throw new ArgumentNullException(nameof(cameraShaker));
        _hitStop = hitStop != null ? hitStop : throw new ArgumentNullException(nameof(hitStop));
        _acceptingFeedback = true;
        CombatEvents.OnHitResolved += Handle;
        _configured = true;
    }

    public void Handle(CombatFeedbackContext context)
    {
        if (_disposed || !_acceptingFeedback)
        {
            return;
        }

        if (context.Result == CombatHitResult.Parried)
        {
            DamageNumberPool.SpawnText("\u5f39\u53cd", context.Position, DamageType.Parry);
            _hitStop.DoHitStop(_hitStop.parryHitStopDuration);
            _cameraShaker.CustomShake(
                _cameraShaker.parryShakeIntensity,
                _cameraShaker.parryShakeDuration);
            AudioManager.Instance?.PlaySFX("parry");
            return;
        }

        if (context.Result != CombatHitResult.Damaged)
        {
            return;
        }

        var hitEffect = context.Target != null
            ? context.Target.GetComponent<HitEffectPlayer>()
            : null;
        if (hitEffect != null)
        {
            hitEffect.PlayHitEffect();
            _activeHitEffects.Add(hitEffect);
        }

        DamageNumberPool.Spawn(context.AppliedDamage, context.Position, DamageType.Normal);
        if (context.TargetKind == CombatFeedbackTargetKind.Enemy)
        {
            _inkHitEffect.PlayAt(context.Position, _inkParticlePool);
        }

        if (context.SourceKind == CombatFeedbackSourceKind.PlayerMelee)
        {
            _inkSlashEffect.Play(_player.transform.position, context.FacingDirection);
        }

        ApplyImpact(context);
        AudioManager.Instance?.PlaySFX("hit");
    }

    public void ClearTransient()
    {
        _acceptingFeedback = false;
        if (_inkHitEffect != null)
        {
            _inkHitEffect.ClearAll();
        }
        if (_inkParticlePool != null)
        {
            _inkParticlePool.ClearAll();
        }
        if (_inkSlashEffect != null)
        {
            _inkSlashEffect.Hide();
        }
        foreach (var hitEffect in _activeHitEffects)
        {
            if (hitEffect != null)
            {
                hitEffect.Clear();
            }
        }
        _activeHitEffects.Clear();
        if (_hitStop != null)
        {
            _hitStop.ClearHitStops();
        }
        if (_cameraShaker != null)
        {
            _cameraShaker.ClearShake();
        }
        if (DamageNumberPool.Current != null)
        {
            DamageNumberPool.Current.ClearAll();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_configured)
        {
            CombatEvents.OnHitResolved -= Handle;
        }
        ClearTransient();
    }

    private void ApplyImpact(CombatFeedbackContext context)
    {
        if (context.TargetKind == CombatFeedbackTargetKind.Player)
        {
            _hitStop.DoHitStop(_hitStop.lightHitStopDuration * 0.5f);
            _cameraShaker.CustomShake(
                _cameraShaker.hurtShakeIntensity,
                _cameraShaker.hurtShakeDuration);
            return;
        }

        var hitStopDuration = context.Strength == CombatFeedbackStrength.Heavy
            ? _hitStop.heavyHitStopDuration
            : _hitStop.lightHitStopDuration;
        var shakeMultiplier = context.Strength == CombatFeedbackStrength.Heavy ? 1.5f : 1f;
        _hitStop.DoHitStop(hitStopDuration);
        _cameraShaker.CustomShake(
            _cameraShaker.hitShakeIntensity * shakeMultiplier,
            _cameraShaker.hitShakeDuration);
    }

    private void OnDestroy()
    {
        Dispose();
    }
}
