namespace Game.Gameplay
{
    public enum AttackPhase
    {
        Windup,
        Active,
        Recovery,
        Complete
    }

    public readonly struct AttackTimeline
    {
        private readonly float _windupEnd;
        private readonly float _activeEnd;

        public AttackTimeline(float totalDuration, float windupFraction, float activeFraction)
        {
            TotalDuration = NormalizeDuration(totalDuration);

            var normalizedWindup = NormalizeFraction(windupFraction);
            var normalizedActive = NormalizeFraction(activeFraction);
            if (normalizedActive > 1f - normalizedWindup)
            {
                normalizedActive = 1f - normalizedWindup;
            }

            _windupEnd = TotalDuration * normalizedWindup;
            _activeEnd = _windupEnd + TotalDuration * normalizedActive;
        }

        public float TotalDuration { get; }

        public AttackPhase Evaluate(float elapsed)
        {
            if (TotalDuration <= 0f || elapsed >= TotalDuration || float.IsPositiveInfinity(elapsed))
            {
                return AttackPhase.Complete;
            }

            if (float.IsNaN(elapsed) || elapsed < 0f)
            {
                elapsed = 0f;
            }

            if (elapsed < _windupEnd)
            {
                return AttackPhase.Windup;
            }

            if (elapsed < _activeEnd)
            {
                return AttackPhase.Active;
            }

            return AttackPhase.Recovery;
        }

        private static float NormalizeDuration(float duration)
        {
            return duration > 0f && !float.IsInfinity(duration) ? duration : 0f;
        }

        private static float NormalizeFraction(float fraction)
        {
            if (float.IsNaN(fraction) || fraction <= 0f)
            {
                return 0f;
            }

            return fraction >= 1f ? 1f : fraction;
        }
    }
}
