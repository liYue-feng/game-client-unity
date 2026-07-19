using Game.Gameplay;
using UnityEngine;

public enum CombatFeedbackSourceKind
{
    PlayerMelee,
    PlayerRanged,
    Summon,
    Elemental,
    Style,
    EnemyMelee,
    EnemyProjectile
}

public enum CombatFeedbackTargetKind
{
    Player,
    Enemy
}

public enum CombatFeedbackStrength
{
    Light,
    Heavy
}

public readonly struct CombatFeedbackContext
{
    public CombatFeedbackContext(
        GameObject source,
        GameObject target,
        Vector3 position,
        CombatFeedbackSourceKind sourceKind,
        CombatFeedbackTargetKind targetKind,
        CombatFeedbackStrength strength,
        CombatHitOutcome outcome,
        int facingDirection)
    {
        Source = source;
        Target = target;
        Position = position;
        SourceKind = sourceKind;
        TargetKind = targetKind;
        Strength = strength;
        Result = outcome.Result;
        AppliedDamage = outcome.AppliedDamage;
        FacingDirection = facingDirection < 0 ? -1 : 1;
    }

    public GameObject Source { get; }
    public GameObject Target { get; }
    public Vector3 Position { get; }
    public CombatFeedbackSourceKind SourceKind { get; }
    public CombatFeedbackTargetKind TargetKind { get; }
    public CombatFeedbackStrength Strength { get; }
    public CombatHitResult Result { get; }
    public int AppliedDamage { get; }
    public int FacingDirection { get; }
}
