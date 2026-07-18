using System;

namespace Game.Core
{
    public enum GameApplicationState
    {
        Created,
        Initializing,
        Ready,
        Failed,
        ShuttingDown,
        Stopped
    }

    public sealed class GameApplicationLifecycle
    {
        public GameApplicationState State { get; private set; } = GameApplicationState.Created;

        public void BeginInitialization()
        {
            Transition(GameApplicationState.Created, GameApplicationState.Initializing);
        }

        public void MarkReady()
        {
            Transition(GameApplicationState.Initializing, GameApplicationState.Ready);
        }

        public void MarkFailed()
        {
            Transition(GameApplicationState.Initializing, GameApplicationState.Failed);
        }

        public void BeginShutdown()
        {
            if (State != GameApplicationState.Ready && State != GameApplicationState.Failed)
            {
                throw new InvalidOperationException($"Cannot shut down from {State}.");
            }

            State = GameApplicationState.ShuttingDown;
        }

        public void MarkStopped()
        {
            Transition(GameApplicationState.ShuttingDown, GameApplicationState.Stopped);
        }

        private void Transition(GameApplicationState expected, GameApplicationState next)
        {
            if (State != expected)
            {
                throw new InvalidOperationException($"Expected {expected}, actual {State}.");
            }

            State = next;
        }
    }
}
