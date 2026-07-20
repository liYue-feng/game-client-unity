using System;
using Game.Core;

namespace Game.Online
{
    public sealed class EditorLoginCodeProvider : ILoginCodeProvider
    {
        private readonly string _identity;

        public EditorLoginCodeProvider(GameRuntimeSettings settings)
            : this(settings == null ? null : settings.EditorLoginIdentity)
        {
        }

        public EditorLoginCodeProvider(string identity)
        {
            _identity = identity;
        }

        public void RequestCode(Action<string> succeeded, Action<string> failed)
        {
            var identity = _identity;
            while (identity != null && identity.StartsWith("dev:", StringComparison.Ordinal))
            {
                identity = identity.Substring("dev:".Length);
            }

            if (string.IsNullOrWhiteSpace(identity))
            {
                failed?.Invoke("Editor login identity is required.");
                return;
            }

            succeeded?.Invoke($"dev:{identity}");
        }
    }
}
