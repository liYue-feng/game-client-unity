using System;
using System.Collections.Generic;
using Game.Protocol;
using Google.Protobuf;
using UnityEngine;

namespace Game.Network
{
    public sealed class NetworkClient : IDisposable
    {
        private static NetworkClient _facade = new NetworkClient();
        private static NetworkClient _registeredInstance;

        private readonly Dictionary<ushort, List<Subscription>> _handlers =
            new Dictionary<ushort, List<Subscription>>();

        private INetworkConnectionGateway _connectionGateway = NoOpNetworkConnectionGateway.Instance;
        private IWebSocketTransport _transport;
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

            var transport = _transport;
            if (transport == null || !transport.IsAlive)
            {
                LogDisconnectedSend(msgId);
                return false;
            }

            transport.Send(Codec.Encode(msgId, payload));
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
            _connectionGateway.Disconnect();
        }

        public void ReceiveFrame(byte[] frame)
        {
            if (!Codec.TryDecode(frame, out var msgId, out var body))
            {
                Debug.LogWarning("[NetworkClient] Dropped malformed frame.");
                return;
            }

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
            if (_disposed)
            {
                return;
            }

            _disposed = true;
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
            OnDisconnected?.Invoke();
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
