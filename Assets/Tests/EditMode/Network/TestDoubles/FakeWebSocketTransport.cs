using System;
using System.Collections.Generic;
using Game.Network;

namespace Game.Tests.EditMode.Network.TestDoubles
{
    public sealed class FakeWebSocketTransport : IWebSocketTransport
    {
        public event Action Opened;
        public event Action<byte[]> MessageReceived;
        public event Action<NetworkCloseInfo> Closed;
        public event Action<string> Error;

        public List<byte[]> SentPayloads { get; } = new List<byte[]>();

        public List<NetworkCloseInfo> CloseCalls { get; } = new List<NetworkCloseInfo>();

        public int ConnectAsyncCalls { get; private set; }

        public int DisposeCalls { get; private set; }

        public bool IsAlive { get; set; }

        public void ConnectAsync()
        {
            ConnectAsyncCalls++;
        }

        public void Send(byte[] payload)
        {
            SentPayloads.Add(payload);
        }

        public void Close(ushort code, string reason)
        {
            CloseCalls.Add(new NetworkCloseInfo(code, reason));
        }

        public void Dispose()
        {
            DisposeCalls++;
        }

        public void RaiseOpened()
        {
            Opened?.Invoke();
        }

        public void RaiseMessage(byte[] payload)
        {
            MessageReceived?.Invoke(payload);
        }

        public void RaiseClosed(ushort code = 1006, string reason = "closed")
        {
            Closed?.Invoke(new NetworkCloseInfo(code, reason));
        }

        public void RaiseError(string message)
        {
            Error?.Invoke(message);
        }
    }
}
