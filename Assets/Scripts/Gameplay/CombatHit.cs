namespace Game.Gameplay
{
    public interface IParryResponder
    {
        void OnParried();
    }

    public enum CombatHitResult
    {
        Ignored,
        Damaged,
        Parried
    }

    public readonly struct CombatHit
    {
        public CombatHit(
            int damage,
            float knockbackDirectionX,
            float knockbackForce,
            bool isParryable,
            IParryResponder source = null)
        {
            Damage = damage;
            KnockbackDirectionX = knockbackDirectionX;
            KnockbackForce = knockbackForce;
            IsParryable = isParryable;
            Source = source;
        }

        public int Damage { get; }

        public float KnockbackDirectionX { get; }

        public float KnockbackForce { get; }

        public bool IsParryable { get; }

        public IParryResponder Source { get; }
    }
}
