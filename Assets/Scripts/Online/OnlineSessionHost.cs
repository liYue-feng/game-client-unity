using System;
using Game.Core;
using Game.Network;
using Game.Protocol;
using UnityEngine;

namespace Game.Online
{
    public sealed class OnlineSessionHost : MonoBehaviour, IGameService
    {
        private OnlineSessionCoordinator _coordinator;
        private OnlineConnectionAdapter _adapter;
        private LoginSessionService _loginService;
        private ArchiveSessionService _archiveService;
        private OnlineSessionState _lastState = OnlineSessionState.Idle;
        private string _lastFailureReason;
        private string _lastNickname;
        private PlayerArchive _lastArchive = new PlayerArchive();
        private bool _initialized;
        private bool _shutdown;

        public static OnlineSessionHost Instance { get; private set; }

        public string ServiceName => nameof(OnlineSessionHost);
        public OnlineSessionState State => _coordinator?.State ?? _lastState;
        public string FailureReason => _coordinator?.FailureReason ?? _lastFailureReason;
        public string Nickname => _coordinator?.Nickname ?? _lastNickname;
        public PlayerArchive Archive => _coordinator?.Archive ?? _lastArchive;

        public event Action<OnlineSessionState> StateChanged;
        public event Action ArchiveSaved;

        public static OnlineSessionHost Install(
            Transform parent,
            NetworkClient client,
            NetworkConnectionControllerHost networkHost,
            GameRuntimeSettings settings,
            ILoginCodeProvider loginCodeProvider = null)
        {
            if (networkHost == null)
            {
                throw new ArgumentNullException(nameof(networkHost));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var adapter = new OnlineConnectionAdapter(client, networkHost);
            try
            {
                var host = InstallCore(
                    parent,
                    client,
                    settings.ServerUrl,
                    adapter,
                    loginCodeProvider ?? new EditorLoginCodeProvider(settings.EditorLoginIdentity));
                host._adapter = adapter;
                return host;
            }
            catch
            {
                adapter.Dispose();
                throw;
            }
        }

        internal static OnlineSessionHost Install(
            Transform parent,
            NetworkClient client,
            string serverUrl,
            IOnlineConnection connection,
            ILoginCodeProvider loginCodeProvider)
        {
            return InstallCore(parent, client, serverUrl, connection, loginCodeProvider);
        }

        public void Initialize()
        {
            if (_shutdown || _initialized)
            {
                return;
            }

            _initialized = true;
        }

        public void Shutdown()
        {
            if (_shutdown)
            {
                return;
            }

            _shutdown = true;
            CacheCoordinatorState();
            if (_coordinator != null)
            {
                _coordinator.StateChanged -= HandleStateChanged;
                _coordinator.ArchiveSaved -= HandleArchiveSaved;
                _coordinator.Dispose();
                _lastState = OnlineSessionState.Stopped;
                _coordinator = null;
            }

            _adapter?.Dispose();
            _adapter = null;
            _loginService?.Dispose();
            _loginService = null;
            _archiveService?.Dispose();
            _archiveService = null;
            _initialized = false;
            StateChanged = null;
            ArchiveSaved = null;

            if (ReferenceEquals(Instance, this))
            {
                Instance = null;
            }
        }

        public void StartSession()
        {
            if (!_shutdown && _initialized)
            {
                _coordinator?.Start();
            }
        }

        public void Retry()
        {
            if (!_shutdown && _initialized)
            {
                _coordinator?.Retry();
            }
        }

        public bool SaveArchive(PlayerArchive archive = null)
        {
            return !_shutdown && _initialized && (_coordinator?.SaveArchive(archive) ?? false);
        }

        public bool ReloadArchive()
        {
            return !_shutdown && _initialized && (_coordinator?.ReloadArchive() ?? false);
        }

        public static void ResetStaticState()
        {
            Instance = null;
        }

        private static OnlineSessionHost InstallCore(
            Transform parent,
            NetworkClient client,
            string serverUrl,
            IOnlineConnection connection,
            ILoginCodeProvider loginCodeProvider)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (loginCodeProvider == null)
            {
                throw new ArgumentNullException(nameof(loginCodeProvider));
            }

            if (Instance != null)
            {
                throw new InvalidOperationException("OnlineSessionHost is already installed.");
            }

            var serviceObject = new GameObject("[OnlineSessionHost]");
            serviceObject.transform.SetParent(parent, false);
            var host = serviceObject.AddComponent<OnlineSessionHost>();
            try
            {
                host._loginService = new LoginSessionService(client);
                host._archiveService = new ArchiveSessionService(client);
                host._coordinator = new OnlineSessionCoordinator(
                    connection,
                    loginCodeProvider,
                    host._loginService,
                    host._archiveService,
                    client,
                    serverUrl);
                host._coordinator.StateChanged += host.HandleStateChanged;
                host._coordinator.ArchiveSaved += host.HandleArchiveSaved;
                Instance = host;
                return host;
            }
            catch
            {
                host._archiveService?.Dispose();
                host._loginService?.Dispose();
                UnityEngine.Object.DestroyImmediate(serviceObject);
                throw;
            }
        }

        private void CacheCoordinatorState()
        {
            if (_coordinator == null)
            {
                return;
            }

            _lastState = _coordinator.State;
            _lastFailureReason = _coordinator.FailureReason;
            _lastNickname = _coordinator.Nickname;
            _lastArchive = _coordinator.Archive;
        }

        private void HandleStateChanged(OnlineSessionState state)
        {
            _lastState = state;
            _lastFailureReason = _coordinator?.FailureReason;
            _lastNickname = _coordinator?.Nickname;
            _lastArchive = _coordinator?.Archive;
            StateChanged?.Invoke(state);
        }

        private void HandleArchiveSaved()
        {
            _lastArchive = _coordinator?.Archive;
            ArchiveSaved?.Invoke();
        }

        private void OnDestroy()
        {
            Shutdown();
        }
    }
}
