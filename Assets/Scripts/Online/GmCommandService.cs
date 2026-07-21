using System;
using Game.Network;
using Game.Protocol;
using Google.Protobuf;
using UnityEngine;

namespace Game.Online
{
    public sealed class GmCommandService : IDisposable
    {
        private readonly NetworkClient _client;
        private readonly PendingRequestOwner _requests;
        private readonly IDisposable _broadcastSubscription;
        private bool _disposed;

        public GmCommandService(NetworkClient client = null)
        {
            _client = client ?? NetworkClient.Instance;
            _requests = new PendingRequestOwner(_client);
            _broadcastSubscription = _client.On<GMCommandResp>(
                MsgID.GMCommandResp,
                PublishBroadcast);
        }

        public event Action<GMCommandResp> BroadcastReceived;

        public bool Execute(
            string command,
            byte[] argsJson,
            Action<GMCommandResp> onSuccess,
            Action<string> onFailure,
            out uint seq)
        {
            if (_disposed)
            {
                seq = 0;
                return false;
            }

            return _requests.Request<GMCommandReq, GMCommandResp>(
                MsgID.GMCommandReq,
                MsgID.GMCommandResp,
                new GMCommandReq
                {
                    Cmd = command ?? string.Empty,
                    ArgsJson = ByteString.CopyFrom(argsJson ?? Array.Empty<byte>())
                },
                onSuccess,
                onFailure,
                out seq);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _broadcastSubscription.Dispose();
            BroadcastReceived = null;
            _requests.Dispose();
        }

        private void PublishBroadcast(GMCommandResp response)
        {
            var observers = BroadcastReceived;
            if (observers == null)
            {
                return;
            }

            foreach (Action<GMCommandResp> observer in observers.GetInvocationList())
            {
                try
                {
                    observer(response);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
