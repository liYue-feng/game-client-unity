using System;
using Game.Core;
using Game.Protocol;
using UnityEngine;

namespace Game.Network
{
    public sealed class NetworkConnectionController : INetworkConnectionGateway, IDisposable
    {
        private readonly NetworkClient _client;
        private readonly IWebSocketTransportFactory _factory;
        private readonly INetworkDispatcher _dispatcher;
        private readonly GameRuntimeSettings _settings;

        private NetworkConnectionState _state = NetworkConnectionState.Disconnected;
        private int _generation;
        private IWebSocketTransport _transport;
        private string _url;
        private bool _intentionalClose;
        private bool _terminalHandledForGeneration;
        private int _attempt;
        private float _timeoutRemaining;
        private float _reconnectDelayRemaining;
        private float _nextBackoffSeconds;
        private float _heartbeatRemaining;
        private bool _disposed;

        public NetworkConnectionController(
            NetworkClient client,
            IWebSocketTransportFactory factory,
            INetworkDispatcher dispatcher,
            GameRuntimeSettings settings)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _nextBackoffSeconds = settings.InitialReconnectBackoffSeconds;
        }

        public NetworkConnectionState State => _state;

        public bool IsConnected => _state == NetworkConnectionState.Connected ||
                                   _state == NetworkConnectionState.Authenticating ||
                                   _state == NetworkConnectionState.Ready;

        public event Action<NetworkConnectionState> StateChanged;

        public void Connect(string url)
        {
            if (_disposed)
            {
                return;
            }

            var reconnecting = _state == NetworkConnectionState.Reconnecting;
            if (!reconnecting)
            {
                _attempt = 0;
                _nextBackoffSeconds = _settings.InitialReconnectBackoffSeconds;
            }

            var replacingOpenTransport = _transport != null && IsConnected;
            _generation++;
            if (_transport != null)
            {
                CloseAndDisposeTransport(1000, "Connection replaced");
            }

            _url = url;
            _intentionalClose = false;
            _terminalHandledForGeneration = false;
            var callbackGeneration = _generation;
            var transport = _factory.Create(url);
            _transport = transport;
            _client.SetTransport(transport);
            _timeoutRemaining = _settings.ConnectionTimeoutSeconds;
            SetState(NetworkConnectionState.Connecting);

            transport.Opened += () =>
                _dispatcher.Enqueue(() => HandleOpened(callbackGeneration));
            transport.MessageReceived += payload =>
                _dispatcher.Enqueue(() => HandleMessageReceived(callbackGeneration, payload));
            transport.Closed += closeInfo =>
                _dispatcher.Enqueue(() => HandleClosed(callbackGeneration, closeInfo));
            transport.Error += message =>
                _dispatcher.Enqueue(() => HandleError(callbackGeneration, message));
            transport.ConnectAsync();
            if (replacingOpenTransport)
            {
                _client.NotifyDisconnected();
            }
        }

        public void Disconnect()
        {
            if (_disposed)
            {
                return;
            }

            var wasConnected = IsConnected;
            _intentionalClose = true;
            _generation++;
            _terminalHandledForGeneration = true;
            CloseAndDisposeTransport(1000, "Client disconnect");
            _timeoutRemaining = 0f;
            _reconnectDelayRemaining = 0f;
            _heartbeatRemaining = 0f;
            SetState(NetworkConnectionState.Disconnected);
            if (wasConnected)
            {
                _client.NotifyDisconnected();
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed || deltaSeconds <= 0f)
            {
                return;
            }

            if (_state == NetworkConnectionState.Connecting)
            {
                _timeoutRemaining -= deltaSeconds;
                if (_timeoutRemaining <= 0f)
                {
                    HandleConnectionTimeout();
                }

                return;
            }

            if (_state == NetworkConnectionState.Reconnecting)
            {
                _reconnectDelayRemaining -= deltaSeconds;
                if (_reconnectDelayRemaining <= 0f)
                {
                    Connect(_url);
                }

                return;
            }

            if (!IsConnected)
            {
                return;
            }

            if (deltaSeconds >= _heartbeatRemaining)
            {
                _client.Send(MsgID.HeartbeatReq, new HeartbeatReq
                {
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                _heartbeatRemaining = _settings.HeartbeatIntervalSeconds;
                return;
            }

            _heartbeatRemaining -= deltaSeconds;
        }

        public void BeginAuthentication()
        {
            if (_disposed || _state != NetworkConnectionState.Connected)
            {
                return;
            }

            SetState(NetworkConnectionState.Authenticating);
        }

        public void MarkReady()
        {
            if (_disposed || _state != NetworkConnectionState.Authenticating)
            {
                return;
            }

            SetState(NetworkConnectionState.Ready);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Disconnect();
            _disposed = true;
            StateChanged = null;
        }

        private void HandleOpened(int callbackGeneration)
        {
            if (callbackGeneration != _generation || _disposed)
            {
                return;
            }

            if (_state != NetworkConnectionState.Connecting)
            {
                return;
            }

            _attempt = 0;
            _nextBackoffSeconds = _settings.InitialReconnectBackoffSeconds;
            _timeoutRemaining = 0f;
            SetState(NetworkConnectionState.Connected);
            _heartbeatRemaining = _settings.HeartbeatIntervalSeconds;
            _client.NotifyConnected();
        }

        private void HandleMessageReceived(int callbackGeneration, byte[] payload)
        {
            if (callbackGeneration != _generation || _disposed)
            {
                return;
            }

            _client.ReceiveFrame(payload);
        }

        private void HandleClosed(int callbackGeneration, NetworkCloseInfo closeInfo)
        {
            if (callbackGeneration != _generation || _disposed)
            {
                return;
            }

            HandleTerminal(callbackGeneration,
                $"WebSocket closed with code {closeInfo.Code}: {closeInfo.Reason}");
        }

        private void HandleError(int callbackGeneration, string message)
        {
            if (callbackGeneration != _generation || _disposed)
            {
                return;
            }

            Debug.LogError(
                $"[NetworkConnectionController] WebSocket error in state {State} generation {callbackGeneration}: {message}");
            HandleTerminal(callbackGeneration, message);
        }

        private void HandleConnectionTimeout()
        {
            if (_terminalHandledForGeneration)
            {
                return;
            }

            _terminalHandledForGeneration = true;
            _generation++;
            CloseAndDisposeTransport(1001, "Connection timeout");
            ScheduleReconnectOrFail("Connection timeout");
        }

        private void HandleTerminal(int callbackGeneration, string errorMessage)
        {
            if (callbackGeneration != _generation || _disposed ||
                _intentionalClose || _terminalHandledForGeneration)
            {
                return;
            }

            var wasConnected = IsConnected;
            _terminalHandledForGeneration = true;
            _generation++;
            DisposeTransport();
            if (wasConnected)
            {
                _client.NotifyDisconnected();
            }

            ScheduleReconnectOrFail(errorMessage);
        }

        private void ScheduleReconnectOrFail(string errorMessage)
        {
            _timeoutRemaining = 0f;
            _heartbeatRemaining = 0f;
            if (_attempt >= _settings.MaxReconnectAttempts)
            {
                _reconnectDelayRemaining = 0f;
                SetState(NetworkConnectionState.Failed);
                _client.NotifyError(errorMessage);
                return;
            }

            _attempt++;
            _reconnectDelayRemaining = _nextBackoffSeconds;
            _nextBackoffSeconds = Math.Min(
                _nextBackoffSeconds * 2f,
                _settings.MaxReconnectBackoffSeconds);
            SetState(NetworkConnectionState.Reconnecting);
        }

        private void CloseAndDisposeTransport(ushort code, string reason)
        {
            var transport = _transport;
            if (transport == null)
            {
                _client.SetTransport(null);
                return;
            }

            _transport = null;
            transport.Close(code, reason);
            transport.Dispose();
            _client.SetTransport(null);
        }

        private void DisposeTransport()
        {
            var transport = _transport;
            _transport = null;
            transport?.Dispose();
            _client.SetTransport(null);
        }

        private void SetState(NetworkConnectionState state)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
            StateChanged?.Invoke(state);
        }
    }
}
