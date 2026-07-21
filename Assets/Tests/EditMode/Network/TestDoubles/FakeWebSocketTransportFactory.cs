using System;
using System.Collections.Generic;
using Game.Network;

namespace Game.Tests.EditMode.Network.TestDoubles
{
    public sealed class FakeWebSocketTransportFactory : IWebSocketTransportFactory
    {
        private readonly List<FakeWebSocketTransport> _created = new List<FakeWebSocketTransport>();

        public IReadOnlyList<FakeWebSocketTransport> Created => _created;

        public FakeWebSocketTransport LastTransport => _created.Count == 0 ? null : _created[_created.Count - 1];

        public Action<FakeWebSocketTransport> CreateAction { get; set; }

        public IWebSocketTransport Create(string url)
        {
            var transport = new FakeWebSocketTransport();
            _created.Add(transport);
            CreateAction?.Invoke(transport);
            return transport;
        }
    }
}
