namespace Game.Gameplay
{
    /// <summary>
    /// Authoritative hit result paired with the HP delta actually applied to the target.
    /// </summary>
    public readonly struct CombatHitOutcome
    {
        public CombatHitOutcome(CombatHitResult result, int appliedDamage)
        {
            Result = result;
            AppliedDamage = result == CombatHitResult.Damaged
                ? System.Math.Max(0, appliedDamage)
                : 0;
        }

        public CombatHitResult Result { get; }
        public int AppliedDamage { get; }
    }
}
