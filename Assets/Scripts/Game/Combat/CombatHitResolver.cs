using Game.Gameplay;
using UnityEngine;

public static class CombatHitResolver
{
    public static CombatHitOutcome ResolveAndPublish(
        Hurtbox target,
        CombatHit hit,
        GameObject source,
        CombatFeedbackSourceKind sourceKind,
        CombatFeedbackStrength strength,
        int facingDirection)
    {
        if (target == null)
        {
            return new CombatHitOutcome(CombatHitResult.Ignored, 0);
        }

        var outcome = target.ResolveHit(hit);
        var targetObject = target.gameObject;
        var targetKind = targetObject.CompareTag("Player")
            ? CombatFeedbackTargetKind.Player
            : CombatFeedbackTargetKind.Enemy;
        CombatEvents.InvokeHitResolved(new CombatFeedbackContext(
            source,
            targetObject,
            target.transform.position,
            sourceKind,
            targetKind,
            strength,
            outcome,
            facingDirection));
        return outcome;
    }
}
