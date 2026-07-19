using System;
using System.Collections.Generic;

namespace Game.Gameplay
{
    public readonly struct TimeScaleRequestToken : IEquatable<TimeScaleRequestToken>
    {
        private readonly Guid _id;

        internal TimeScaleRequestToken(Guid id)
        {
            _id = id;
        }

        public bool IsValid => _id != Guid.Empty;

        public bool Equals(TimeScaleRequestToken other)
        {
            return _id.Equals(other._id);
        }

        public override bool Equals(object obj)
        {
            return obj is TimeScaleRequestToken other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }

        public static bool operator ==(TimeScaleRequestToken left, TimeScaleRequestToken right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TimeScaleRequestToken left, TimeScaleRequestToken right)
        {
            return !left.Equals(right);
        }

        internal Guid Id => _id;
    }

    public sealed class TimeScaleRequestSet
    {
        private readonly Dictionary<Guid, float> _requests = new Dictionary<Guid, float>();

        public int Count => _requests.Count;

        public float EffectiveScale
        {
            get
            {
                var effectiveScale = 1f;
                foreach (var scale in _requests.Values)
                {
                    if (scale < effectiveScale)
                    {
                        effectiveScale = scale;
                    }
                }

                return effectiveScale;
            }
        }

        public TimeScaleRequestToken Add(string reason, float scale)
        {
            var token = new TimeScaleRequestToken(Guid.NewGuid());
            _requests.Add(token.Id, ClampScale(scale));
            return token;
        }

        public bool Update(TimeScaleRequestToken token, float scale)
        {
            if (!token.IsValid || !_requests.ContainsKey(token.Id))
            {
                return false;
            }

            _requests[token.Id] = ClampScale(scale);
            return true;
        }

        public bool Release(TimeScaleRequestToken token)
        {
            return token.IsValid && _requests.Remove(token.Id);
        }

        public void Clear()
        {
            _requests.Clear();
        }

        private static float ClampScale(float scale)
        {
            if (float.IsNaN(scale) || scale >= 1f)
            {
                return 1f;
            }

            return scale <= 0f ? 0f : scale;
        }
    }
}
