using System;

namespace Game.Network
{
    public interface INetworkDispatcher
    {
        bool Enqueue(Action action);
    }
}
