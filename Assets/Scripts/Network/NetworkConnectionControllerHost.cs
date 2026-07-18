using System;
using Game.Core;
using UnityEngine;

namespace Game.Network
{
    public sealed class NetworkConnectionControllerHost : MonoBehaviour, IGameService, INetworkConnectionGateway
    {
        private NetworkClient _client;
        private NetworkConnectionController _controller;
        private Func<float> _deltaSecondsProvider;
        private bool _initialized;
        private bool _shutdown;

        public static NetworkConnectionControllerHost Instance { get; private set; }

        public string ServiceName => nameof(NetworkConnectionControllerHost);

        public NetworkConnectionState State => _controller?.State ?? NetworkConnectionState.Disconnected;

        public bool IsConnected => _controller?.IsConnected ?? false;

        public static NetworkConnectionControllerHost Install(
            Transform parent,
            NetworkClient client,
            IWebSocketTransportFactory factory,
            GameRuntimeSettings settings,
            INetworkDispatcher dispatcher = null,
            Func<float> deltaSecondsProvider = null)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var serviceObject = new GameObject("[NetworkConnectionControllerHost]");
            serviceObject.transform.SetParent(parent, false);

            var host = serviceObject.AddComponent<NetworkConnectionControllerHost>();
            host._client = client;
            host._controller = new NetworkConnectionController(
                client,
                factory,
                dispatcher ?? new MainThreadNetworkDispatcher(),
                settings);
            host._deltaSecondsProvider = deltaSecondsProvider ?? DefaultDeltaProvider;
            Instance = host;
            return host;
        }

        public void Initialize()
        {
            if (_shutdown || _initialized)
            {
                return;
            }

            _client.BindConnectionGateway(this);
            _initialized = true;
        }

        public void Shutdown()
        {
            if (_shutdown)
            {
                return;
            }

            _shutdown = true;
            var client = _client;
            _controller?.Dispose();
            _controller = null;
            client?.UnbindConnectionGateway(this);
            _client = null;
            _deltaSecondsProvider = null;
            _initialized = false;
            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        public void Connect(string url)
        {
            if (_shutdown)
            {
                return;
            }

            _controller?.Connect(url);
        }

        public void Disconnect()
        {
            if (_shutdown)
            {
                return;
            }

            _controller?.Disconnect();
        }

        private static float DefaultDeltaProvider()
        {
            return Time.deltaTime;
        }

        private void Update()
        {
            if (_shutdown)
            {
                return;
            }

            _controller?.Tick((_deltaSecondsProvider ?? DefaultDeltaProvider)());
        }

        private void OnDestroy()
        {
            Shutdown();
        }
    }
}
