using System;

namespace Game.Network
{
    public sealed class MainThreadNetworkDispatcher : INetworkDispatcher
    {
        public bool Enqueue(Action action)
        {
            return MainThreadDispatcher.Enqueue(action);
        }
    }
}
