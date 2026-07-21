using System;
using System.Collections.Generic;
using Game.Network;

namespace Game.Tests.EditMode.Network.TestDoubles
{
    internal sealed class FakeNetworkDispatcher : INetworkDispatcher
    {
        private readonly Queue<Action> _queue = new Queue<Action>();

        public int PendingCount => _queue.Count;

        public bool Enqueue(Action action)
        {
            _queue.Enqueue(action);
            return true;
        }

        public void PumpOne()
        {
            _queue.Dequeue().Invoke();
        }

        public void PumpLast()
        {
            var pending = _queue.ToArray();
            _queue.Clear();
            for (var index = 0; index < pending.Length - 1; index++)
            {
                _queue.Enqueue(pending[index]);
            }

            pending[pending.Length - 1].Invoke();
        }

        public void PumpAll()
        {
            while (_queue.Count > 0)
            {
                PumpOne();
            }
        }
    }
}
