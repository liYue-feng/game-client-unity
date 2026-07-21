using System;
using System.Collections.Generic;
using Game.Network;
using Google.Protobuf;

namespace Game.Online
{
    internal sealed class PendingRequestOwner : IDisposable
    {
        private readonly NetworkClient _client;
        private readonly Dictionary<uint, RequestState> _active =
            new Dictionary<uint, RequestState>();
        private bool _disposed;

        public PendingRequestOwner(NetworkClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public bool Request<TRequest, TResponse>(
            ushort requestId,
            ushort responseId,
            TRequest payload,
            Action<TResponse> onSuccess,
            Action<string> onFailure,
            out uint seq)
            where TRequest : class, IMessage<TRequest>
            where TResponse : class, IMessage<TResponse>
        {
            if (_disposed)
            {
                seq = 0;
                return false;
            }

            var state = new RequestState();
            uint requestSeq = 0;
            var sent = _client.Request<TRequest, TResponse>(
                requestId,
                responseId,
                payload,
                response => Complete(state, requestSeq, () => onSuccess?.Invoke(response)),
                reason => Complete(state, requestSeq, () => onFailure?.Invoke(reason)),
                out requestSeq);
            seq = requestSeq;
            state.Seq = requestSeq;
            if (!sent || !state.Active)
            {
                return sent;
            }

            if (_disposed)
            {
                state.Active = false;
                _client.CancelRequest(seq);
            }
            else
            {
                _active.Add(seq, state);
            }

            return sent;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            var active = new List<RequestState>(_active.Values);
            _active.Clear();
            foreach (var state in active)
            {
                state.Active = false;
            }

            foreach (var state in active)
            {
                _client.CancelRequest(state.Seq);
            }
        }

        private void Complete(RequestState state, uint seq, Action callback)
        {
            if (!state.Active)
            {
                return;
            }

            state.Active = false;
            if (seq != 0)
            {
                _active.Remove(seq);
            }

            if (!_disposed)
            {
                callback?.Invoke();
            }
        }

        private sealed class RequestState
        {
            public uint Seq { get; set; }
            public bool Active { get; set; } = true;
        }
    }
}
