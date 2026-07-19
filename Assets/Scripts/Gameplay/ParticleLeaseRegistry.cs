using System;
using System.Collections.Generic;

namespace Game.Gameplay
{
    public readonly struct ParticleLeaseToken : IEquatable<ParticleLeaseToken>
    {
        public ParticleLeaseToken(int slot, uint generation)
        {
            Slot = slot;
            Generation = generation;
        }

        public int Slot { get; }
        public uint Generation { get; }

        public bool Equals(ParticleLeaseToken other)
        {
            return Slot == other.Slot && Generation == other.Generation;
        }

        public override bool Equals(object obj)
        {
            return obj is ParticleLeaseToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Slot * 397) ^ (int)Generation;
            }
        }

        public static bool operator ==(ParticleLeaseToken left, ParticleLeaseToken right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ParticleLeaseToken left, ParticleLeaseToken right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// Tracks one active generation per particle slot so stale delayed returns are harmless.
    /// </summary>
    public sealed class ParticleLeaseRegistry
    {
        private readonly Dictionary<int, uint> _generations = new Dictionary<int, uint>();
        private readonly Dictionary<int, uint> _active = new Dictionary<int, uint>();

        public ParticleLeaseToken Acquire(int slot)
        {
            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Particle slot cannot be negative.");
            }

            var generation = NextGeneration(slot);
            _active[slot] = generation;
            return new ParticleLeaseToken(slot, generation);
        }

        public bool TryRelease(ParticleLeaseToken token)
        {
            if (!_active.TryGetValue(token.Slot, out var activeGeneration) ||
                activeGeneration != token.Generation)
            {
                return false;
            }

            _active.Remove(token.Slot);
            return true;
        }

        public bool IsActive(ParticleLeaseToken token)
        {
            return _active.TryGetValue(token.Slot, out var activeGeneration) &&
                activeGeneration == token.Generation;
        }

        public void InvalidateAll()
        {
            var knownSlots = new List<int>(_generations.Keys);
            foreach (var slot in knownSlots)
            {
                NextGeneration(slot);
            }

            _active.Clear();
        }

        private uint NextGeneration(int slot)
        {
            _generations.TryGetValue(slot, out var previous);
            var next = unchecked(previous + 1u);
            if (next == 0u)
            {
                next = 1u;
            }

            _generations[slot] = next;
            return next;
        }
    }
}
