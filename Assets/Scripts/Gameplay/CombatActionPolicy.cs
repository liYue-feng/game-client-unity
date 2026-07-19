namespace Game.Gameplay
{
    public enum CombatActionFailure
    {
        None,
        TransitionBlocked,
        OnCooldown,
        InsufficientStamina
    }

    public readonly struct CombatActionDecision
    {
        public CombatActionDecision(bool allowed, CombatActionFailure failure)
        {
            Allowed = allowed;
            Failure = failure;
        }

        public bool Allowed { get; }

        public CombatActionFailure Failure { get; }
    }

    public static class CombatActionPolicy
    {
        public static CombatActionDecision Evaluate(
            bool transitionAllowed,
            bool onCooldown,
            float currentStamina,
            float cost)
        {
            if (!transitionAllowed)
            {
                return Rejected(CombatActionFailure.TransitionBlocked);
            }

            if (onCooldown)
            {
                return Rejected(CombatActionFailure.OnCooldown);
            }

            var normalizedCost = float.IsNaN(cost) || cost < 0f ? 0f : cost;
            if (float.IsNaN(currentStamina) || currentStamina < normalizedCost)
            {
                return Rejected(CombatActionFailure.InsufficientStamina);
            }

            return new CombatActionDecision(true, CombatActionFailure.None);
        }

        private static CombatActionDecision Rejected(CombatActionFailure failure)
        {
            return new CombatActionDecision(false, failure);
        }
    }
}
