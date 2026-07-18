using System;

namespace Game.Network
{
    public interface IWebSocketTransport : IDisposable
    {
        event Action Opened;
        event Action<byte[]> MessageReceived;
        event Action<NetworkCloseInfo> Closed;
        event Action<string> Error;

        bool IsAlive { get; }

        void ConnectAsync();

        void Send(byte[] payload);

        void Close(ushort code, string reason);
    }
}
