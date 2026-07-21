using System;
using System.Collections.Generic;
using Game.Protocol;
using Google.Protobuf;
using UnityEngine;

namespace Game.Network
{
    public sealed class NetworkClient : IDisposable
    {
        private const string TransportDisconnectedError = "Transport disconnected.";
        private static NetworkClient _facade = new NetworkClient();
        private static NetworkClient _registeredInstance;

        private readonly Dictionary<ushort, List<Subscription>> _handlers =
            new Dictionary<ushort, List<Subscription>>();
        private readonly object _pendingGate = new object();
        private readonly Dictionary<uint, PendingRequest> _pending =
            new Dictionary<uint, PendingRequest>();

        private INetworkConnectionGateway _connectionGateway = NoOpNetworkConnectionGateway.Instance;
        private IWebSocketTransport _transport;
        private uint _nextSeq = 1;
        private long _uid;
        private string _token;
        private bool _disposed;

        public static NetworkClient Instance => _registeredInstance ?? _facade;

        public static void RegisterInstance(NetworkClient client)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (ReferenceEquals(_registeredInstance, client))
            {
                return;
            }

            _facade.MoveActiveSubscriptionsTo(client);
            _registeredInstance = client;
        }

        public static void UnregisterInstance(NetworkClient client)
        {
            if (ReferenceEquals(_registeredInstance, client))
            {
                _registeredInstance = null;
            }
        }

        public static void ResetStaticState()
        {
            var registeredInstance = _registeredInstance;
            _registeredInstance = null;
            registeredInstance?.Dispose();

            _facade.Dispose();
            _facade = new NetworkClient();
        }

        public NetworkConnectionState ConnectionState => _connectionGateway.State;

        public bool IsConnected => _connectionGateway.IsConnected;

        public bool IsLoggedIn => _uid > 0;

        public long UID => _uid;

        public string Token => _token;

        public string serverUrl { get; set; } = "ws://localhost:8080/ws";

        public bool IsTransportTerminationFailure(string reason)
        {
            return (_transport == null || !_transport.IsAlive) &&
                   string.Equals(reason, TransportDisconnectedError, StringComparison.Ordinal);
        }

        public event Action OnConnected;

        public event Action OnDisconnected;

        public event Action<string> OnError;

        public void BindConnectionGateway(INetworkConnectionGateway gateway)
        {
            if (gateway == null)
            {
                throw new ArgumentNullException(nameof(gateway));
            }

            if (!ReferenceEquals(_connectionGateway, NoOpNetworkConnectionGateway.Instance) &&
                !ReferenceEquals(_connectionGateway, gateway))
            {
                throw new InvalidOperationException("A different network connection gateway is already bound.");
            }

            _connectionGateway = gateway;
        }

        public void UnbindConnectionGateway(INetworkConnectionGateway gateway)
        {
            if (ReferenceEquals(_connectionGateway, gateway))
            {
                _connectionGateway = NoOpNetworkConnectionGateway.Instance;
            }
        }

        public IDisposable On<T>(ushort msgId, Action<T> handler) where T : class, IMessage<T>
        {
            if (!ProtocolMessageRegistry.IsRegistered<T>(msgId))
            {
                throw new ArgumentException(
                    $"Message ID {msgId} is not registered for {typeof(T).Name}.",
                    nameof(msgId));
            }

            Action<byte[]> wrapper = body =>
            {
                if (!ProtocolMessageRegistry.TryParse(msgId, body, out T payload))
                {
                    Debug.LogWarning($"[NetworkClient] Dropped malformed protobuf message. msgId={msgId}");
                    return;
                }

                handler?.Invoke(payload);
            };

            return AddSubscription(msgId, wrapper);
        }

        public bool Send<T>(ushort msgId, T payload) where T : class, IMessage<T>
        {
            if (!ProtocolMessageRegistry.IsRegistered<T>(msgId))
            {
                Debug.LogWarning($"[NetworkClient] Send dropped because message type does not match msgId={msgId}");
                return false;
            }

            byte[] body;
            try
            {
                body = payload.ToByteArray();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[NetworkClient] Send serialization failed. msgId={msgId}: {exception.Message}");
                return false;
            }

            var transport = _transport;
            if (transport == null || !transport.IsAlive)
            {
                LogDisconnectedSend(msgId);
                return false;
            }

            uint seq;
            lock (_pendingGate)
            {
                if (_disposed || !ReferenceEquals(_transport, transport) || !transport.IsAlive)
                {
                    LogDisconnectedSend(msgId);
                    return false;
                }

                seq = AllocateSequenceLocked();
            }

            try
            {
                transport.Send(Codec.Encode(msgId, seq, body));
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[NetworkClient] Send failed. msgId={msgId} seq={seq}: {exception.Message}");
                return false;
            }
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
            seq = 0;
            byte[] body;
            try
            {
                body = payload.ToByteArray();
            }
            catch (Exception exception)
            {
                InvokeFailure(onFailure, $"Request serialization failed: {exception.Message}");
                return false;
            }

            if (!ProtocolMessageRegistry.IsRegistered<TRequest>(requestId))
            {
                InvokeFailure(onFailure,
                    $"Request message type {typeof(TRequest).Name} does not match msgId={requestId}.");
                return false;
            }

            if (!ProtocolMessageRegistry.IsRegistered<TResponse>(responseId))
            {
                InvokeFailure(onFailure,
                    $"Response message type {typeof(TResponse).Name} does not match msgId={responseId}.");
                return false;
            }

            var transport = _transport;
            if (transport == null || !transport.IsAlive)
            {
                LogDisconnectedSend(requestId);
                InvokeFailure(onFailure, "Transport is disconnected.");
                return false;
            }

            PendingRequest pending = null;
            string unavailableReason = null;
            lock (_pendingGate)
            {
                if (_disposed)
                {
                    unavailableReason = "Network client is disposed.";
                }
                else if (!ReferenceEquals(_transport, transport) || !transport.IsAlive)
                {
                    unavailableReason = "Transport is disconnected.";
                }
                else
                {
                    seq = AllocateSequenceLocked();
                    pending = new PendingRequest(
                        transport,
                        responseId,
                        responseBody =>
                        {
                            if (!ProtocolMessageRegistry.TryParse(responseId, responseBody, out TResponse response))
                            {
                                return false;
                            }

                            onSuccess?.Invoke(response);
                            return true;
                        },
                        onFailure);
                    _pending.Add(seq, pending);
                }
            }

            if (unavailableReason != null)
            {
                InvokeFailure(onFailure, unavailableReason);
                return false;
            }

            try
            {
                var frame = Codec.Encode(requestId, seq, body);
                transport.Send(frame);
                return true;
            }
            catch (Exception exception)
            {
                if (TryTakePending(seq, pending))
                {
                    CompleteFailure(pending, $"Request send failed: {exception.Message}");
                }

                return false;
            }
        }

        public bool CancelRequest(uint seq)
        {
            if (!TryTakePending(seq, out var pending))
            {
                return false;
            }

            CompleteFailure(pending, "Request cancelled.");
            return true;
        }

        public void Connect(string url = null)
        {
            if (!string.IsNullOrEmpty(url))
            {
                serverUrl = url;
            }

            _connectionGateway.Connect(serverUrl);
        }

        public void Disconnect()
        {
            FailAllPending(TransportDisconnectedError);
            _connectionGateway.Disconnect();
        }

        public void ReceiveFrame(byte[] frame)
        {
            if (!Codec.TryDecode(frame, out var msgId, out var seq, out var body))
            {
                Debug.LogWarning("[NetworkClient] Dropped malformed frame.");
                return;
            }

            if (seq != 0)
            {
                ReceiveResponse(msgId, seq, body);
                return;
            }

            DispatchPush(msgId, body);
        }

        private void DispatchPush(ushort msgId, byte[] body)
        {
            if (!_handlers.TryGetValue(msgId, out var handlers))
            {
                return;
            }

            var snapshot = handlers.ToArray();
            foreach (var subscription in snapshot)
            {
                if (!subscription.IsActive)
                {
                    continue;
                }

                try
                {
                    subscription.Handler?.Invoke(body);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[NetworkClient] Message handler failed for {msgId}: {exception.Message}");
                }
            }
        }

        public void SetTransport(IWebSocketTransport transport)
        {
            _transport = transport;
        }

        public void SetLoginInfo(long uid, string token)
        {
            _uid = uid;
            _token = token;
        }

        public void ClearLoginInfo()
        {
            _uid = 0;
            _token = null;
        }

        public void Dispose()
        {
            List<PendingRequest> pending;
            lock (_pendingGate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                pending = new List<PendingRequest>(_pending.Values);
                _pending.Clear();
            }

            CompleteFailures(pending, "Network client disposed.");
            foreach (var handlers in _handlers.Values)
            {
                foreach (var subscription in handlers)
                {
                    subscription.Invalidate(this);
                }
            }

            _handlers.Clear();
            ClearLoginInfo();
            _transport = null;
            _connectionGateway = NoOpNetworkConnectionGateway.Instance;
            OnConnected = null;
            OnDisconnected = null;
            OnError = null;
        }

        internal void NotifyConnected()
        {
            OnConnected?.Invoke();
        }

        internal void NotifyDisconnected()
        {
            FailAllPending(TransportDisconnectedError);
            OnDisconnected?.Invoke();
        }

        internal void NotifyTransportTerminated(
            IWebSocketTransport transport,
            bool notifyDisconnected)
        {
            FailPendingForTransport(transport, TransportDisconnectedError);
            if (notifyDisconnected)
            {
                OnDisconnected?.Invoke();
            }
        }

        internal void NotifyError(string message)
        {
            OnError?.Invoke(message);
        }

        private IDisposable AddSubscription(ushort msgId, Action<byte[]> handler)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NetworkClient));
            }

            if (!_handlers.TryGetValue(msgId, out var handlers))
            {
                handlers = new List<Subscription>();
                _handlers.Add(msgId, handlers);
            }

            var subscription = new Subscription(this, msgId, handler);
            handlers.Add(subscription);
            return subscription;
        }

        private void Remove(Subscription subscription)
        {
            if (!_handlers.TryGetValue(subscription.MsgId, out var handlers))
            {
                return;
            }

            handlers.Remove(subscription);
            if (handlers.Count == 0)
            {
                _handlers.Remove(subscription.MsgId);
            }
        }

        private void MoveActiveSubscriptionsTo(NetworkClient destination)
        {
            if (ReferenceEquals(this, destination))
            {
                return;
            }

            foreach (var pair in _handlers)
            {
                foreach (var subscription in pair.Value)
                {
                    if (!subscription.IsActive)
                    {
                        continue;
                    }

                    destination.AddSubscription(subscription);
                    subscription.MoveTo(destination);
                }
            }

            _handlers.Clear();
        }

        private void AddSubscription(Subscription subscription)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NetworkClient));
            }

            if (!_handlers.TryGetValue(subscription.MsgId, out var handlers))
            {
                handlers = new List<Subscription>();
                _handlers.Add(subscription.MsgId, handlers);
            }

            if (!handlers.Contains(subscription))
            {
                handlers.Add(subscription);
            }
        }

        private static void LogDisconnectedSend(ushort msgId)
        {
            Debug.LogWarning(
                $"[NetworkClient] Send dropped because transport is disconnected. msgId={msgId}");
        }

        private uint AllocateSequenceLocked()
        {
            var candidate = _nextSeq == 0 ? 1u : _nextSeq;
            var start = candidate;
            while (_pending.ContainsKey(candidate))
            {
                candidate = NextSequence(candidate);
                if (candidate == start)
                {
                    throw new InvalidOperationException("No request sequence is available.");
                }
            }

            _nextSeq = NextSequence(candidate);
            return candidate;
        }

        private static uint NextSequence(uint seq)
        {
            return seq == uint.MaxValue ? 1u : seq + 1u;
        }

        private bool TryTakePending(uint seq, out PendingRequest pending)
        {
            lock (_pendingGate)
            {
                if (!_pending.TryGetValue(seq, out pending))
                {
                    return false;
                }

                _pending.Remove(seq);
                return true;
            }
        }

        private bool TryTakePending(uint seq, PendingRequest expected)
        {
            lock (_pendingGate)
            {
                if (!_pending.TryGetValue(seq, out var pending) || !ReferenceEquals(pending, expected))
                {
                    return false;
                }

                _pending.Remove(seq);
                return true;
            }
        }

        private void ReceiveResponse(ushort msgId, uint seq, byte[] body)
        {
            if (!TryTakePending(seq, out var pending))
            {
                Debug.LogWarning($"[NetworkClient] Dropped response for unknown seq={seq}. msgId={msgId}");
                return;
            }

            if (msgId == MsgID.Error)
            {
                if (!ProtocolMessageRegistry.TryParse(MsgID.Error, body, out ErrorResp error))
                {
                    CompleteFailure(pending, "Malformed protobuf error response.");
                    return;
                }

                CompleteFailure(pending, $"[{error.Code}] {error.Msg}");
                return;
            }

            if (msgId != pending.ResponseId)
            {
                CompleteFailure(pending,
                    $"Unexpected response message ID {msgId}; expected {pending.ResponseId}.");
                return;
            }

            try
            {
                if (!pending.TryCompleteSuccess(body))
                {
                    CompleteFailure(pending, "Malformed protobuf response body.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError($"[NetworkClient] Response callback failed for seq={seq}: {exception.Message}");
            }
        }

        private void FailAllPending(string reason)
        {
            List<PendingRequest> pending;
            lock (_pendingGate)
            {
                if (_pending.Count == 0)
                {
                    return;
                }

                pending = new List<PendingRequest>(_pending.Values);
                _pending.Clear();
            }

            CompleteFailures(pending, reason);
        }

        private void FailPendingForTransport(IWebSocketTransport transport, string reason)
        {
            if (transport == null)
            {
                return;
            }

            var pending = new List<PendingRequest>();
            var sequences = new List<uint>();
            lock (_pendingGate)
            {
                foreach (var entry in _pending)
                {
                    if (!ReferenceEquals(entry.Value.Transport, transport))
                    {
                        continue;
                    }

                    sequences.Add(entry.Key);
                    pending.Add(entry.Value);
                }

                foreach (var seq in sequences)
                {
                    _pending.Remove(seq);
                }
            }

            CompleteFailures(pending, reason);
        }

        private static void CompleteFailures(IEnumerable<PendingRequest> pending, string reason)
        {
            foreach (var request in pending)
            {
                CompleteFailure(request, reason);
            }
        }

        private static void CompleteFailure(PendingRequest pending, string reason)
        {
            InvokeFailure(pending.OnFailure, reason);
        }

        private static void InvokeFailure(Action<string> onFailure, string reason)
        {
            try
            {
                onFailure?.Invoke(reason);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[NetworkClient] Failure callback failed: {exception.Message}");
            }
        }

        private sealed class PendingRequest
        {
            internal PendingRequest(
                IWebSocketTransport transport,
                ushort responseId,
                Func<byte[], bool> tryCompleteSuccess,
                Action<string> onFailure)
            {
                Transport = transport;
                ResponseId = responseId;
                TryCompleteSuccess = tryCompleteSuccess;
                OnFailure = onFailure;
            }

            internal IWebSocketTransport Transport { get; }

            internal ushort ResponseId { get; }

            internal Func<byte[], bool> TryCompleteSuccess { get; }

            internal Action<string> OnFailure { get; }
        }

        private sealed class Subscription : IDisposable
        {
            private NetworkClient _owner;

            internal Subscription(NetworkClient owner, ushort msgId, Action<byte[]> handler)
            {
                _owner = owner;
                MsgId = msgId;
                Handler = handler;
                IsActive = true;
            }

            internal ushort MsgId { get; }

            internal Action<byte[]> Handler { get; }

            internal bool IsActive { get; private set; }

            public void Dispose()
            {
                if (!IsActive)
                {
                    return;
                }

                IsActive = false;
                var owner = _owner;
                _owner = null;
                owner?.Remove(this);
            }

            internal void Invalidate(NetworkClient owner)
            {
                if (!ReferenceEquals(_owner, owner))
                {
                    return;
                }

                IsActive = false;
                _owner = null;
            }

            internal void MoveTo(NetworkClient owner)
            {
                _owner = owner;
            }
        }
    }
}
