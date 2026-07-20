namespace Game.Online
{
    public enum OnlineStartupResult
    {
        Waiting,
        Ready,
        Failed,
        TimedOut
    }

    public sealed class OnlineStartupDecision
    {
        public OnlineStartupResult Evaluate(
            OnlineSessionState state,
            float elapsedSeconds,
            float timeoutSeconds)
        {
            if (state == OnlineSessionState.Ready)
            {
                return OnlineStartupResult.Ready;
            }

            if (state == OnlineSessionState.Failed || state == OnlineSessionState.Stopped)
            {
                return OnlineStartupResult.Failed;
            }

            if (elapsedSeconds >= timeoutSeconds)
            {
                return OnlineStartupResult.TimedOut;
            }

            return OnlineStartupResult.Waiting;
        }
    }
}
