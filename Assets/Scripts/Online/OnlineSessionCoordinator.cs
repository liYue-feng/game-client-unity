using System;
using Game.Network;
using Game.Protocol;

namespace Game.Online
{
    public sealed class OnlineSessionCoordinator : IDisposable
    {
        private readonly IOnlineConnection _connection;
        private readonly ILoginCodeProvider _loginCodeProvider;
        private readonly LoginSessionService _loginService;
        private readonly ArchiveSessionService _archiveService;
        private readonly NetworkClient _client;
        private readonly string _serverUrl;

        private int _generation;
        private bool _hasConnected;
        private bool _reloadActive;
        private bool _subscriptionsActive = true;
        private bool _stopped;
        private bool _disposed;

        public OnlineSessionCoordinator(
            IOnlineConnection connection,
            ILoginCodeProvider loginCodeProvider,
            LoginSessionService loginService,
            ArchiveSessionService archiveService,
            NetworkClient client,
            string serverUrl)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _loginCodeProvider = loginCodeProvider ?? throw new ArgumentNullException(nameof(loginCodeProvider));
            _loginService = loginService ?? throw new ArgumentNullException(nameof(loginService));
            _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                throw new ArgumentException("Server URL is required.", nameof(serverUrl));
            }

            _serverUrl = serverUrl.Trim();
            Subscribe();
        }

        public OnlineSessionState State { get; private set; } = OnlineSessionState.Idle;
        public string FailureReason { get; private set; }
        public string Nickname { get; private set; }
        public string ArchiveData { get; private set; }

        public event Action<OnlineSessionState> StateChanged;
        public event Action ArchiveSaved;

        public void Start()
        {
            if (_disposed || _stopped || State != OnlineSessionState.Idle)
            {
                return;
            }

            BeginConnection();
        }

        public void Retry()
        {
            if (_disposed || _stopped || State != OnlineSessionState.Failed)
            {
                return;
            }

            BeginConnection();
        }

        public bool SaveArchive(string data)
        {
            if (_disposed || _stopped || State != OnlineSessionState.Ready)
            {
                return false;
            }

            var accepted = _archiveService.Save(data);
            if (!accepted && State == OnlineSessionState.Ready)
            {
                Fail("Archive save could not start.");
            }

            return accepted;
        }

        public bool ReloadArchive()
        {
            if (_disposed || _stopped || State != OnlineSessionState.Ready)
            {
                return false;
            }

            _reloadActive = true;
            var accepted = _archiveService.Load();
            if (!accepted && State == OnlineSessionState.Ready)
            {
                Fail("Archive reload could not start.");
            }

            return accepted;
        }

        public void Stop()
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            _generation++;
            CancelActiveOperations();
            Unsubscribe();
            _client.ClearLoginInfo();
            _connection.Disconnect();
            TransitionTo(OnlineSessionState.Stopped);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _disposed = true;
            StateChanged = null;
            ArchiveSaved = null;
        }

        private void Subscribe()
        {
            _connection.Connected += HandleConnected;
            _connection.Disconnected += HandleDisconnected;
            _connection.Error += HandleConnectionError;
            _loginService.Succeeded += HandleLoginSucceeded;
            _loginService.Failed += HandleOperationFailed;
            _archiveService.Loaded += HandleArchiveLoaded;
            _archiveService.Saved += HandleArchiveSaved;
            _archiveService.Failed += HandleOperationFailed;
        }

        private void Unsubscribe()
        {
            if (!_subscriptionsActive)
            {
                return;
            }

            _subscriptionsActive = false;
            _connection.Connected -= HandleConnected;
            _connection.Disconnected -= HandleDisconnected;
            _connection.Error -= HandleConnectionError;
            _loginService.Succeeded -= HandleLoginSucceeded;
            _loginService.Failed -= HandleOperationFailed;
            _archiveService.Loaded -= HandleArchiveLoaded;
            _archiveService.Saved -= HandleArchiveSaved;
            _archiveService.Failed -= HandleOperationFailed;
        }

        private void BeginConnection()
        {
            _generation++;
            _hasConnected = false;
            FailureReason = null;
            Nickname = null;
            ArchiveData = null;
            _client.ClearLoginInfo();
            CancelActiveOperations();
            TransitionTo(OnlineSessionState.Connecting);
            _connection.Connect(_serverUrl);
        }

        private void HandleConnected()
        {
            if (!IsActive ||
                (State != OnlineSessionState.Connecting && State != OnlineSessionState.Reconnecting))
            {
                return;
            }

            _hasConnected = true;
            _connection.BeginAuthentication();
            TransitionTo(OnlineSessionState.Authenticating);
            var callbackGeneration = _generation;
            _loginCodeProvider.RequestCode(
                code => HandleLoginCode(callbackGeneration, code),
                reason => HandleProviderFailure(callbackGeneration, reason));
        }

        private void HandleLoginCode(int callbackGeneration, string code)
        {
            if (!IsCurrent(callbackGeneration) || State != OnlineSessionState.Authenticating)
            {
                return;
            }

            var accepted = _loginService.Begin(code);
            if (!accepted && State == OnlineSessionState.Authenticating)
            {
                Fail("Login request could not start.");
            }
        }

        private void HandleProviderFailure(int callbackGeneration, string reason)
        {
            if (!IsCurrent(callbackGeneration) || State != OnlineSessionState.Authenticating)
            {
                return;
            }

            Fail(string.IsNullOrWhiteSpace(reason) ? "Login code request failed." : reason);
        }

        private void HandleLoginSucceeded(LoginResp response)
        {
            if (!IsActive || State != OnlineSessionState.Authenticating)
            {
                return;
            }

            Nickname = response?.nickname;
            TransitionTo(OnlineSessionState.LoadingArchive);
            var accepted = _archiveService.Load();
            if (!accepted && State == OnlineSessionState.LoadingArchive)
            {
                Fail("Archive load could not start.");
            }
        }

        private void HandleArchiveLoaded(string data)
        {
            if (!IsActive)
            {
                return;
            }

            if (State == OnlineSessionState.LoadingArchive)
            {
                ArchiveData = data;
                _connection.MarkReady();
                TransitionTo(OnlineSessionState.Ready);
                return;
            }

            if (State == OnlineSessionState.Ready && _reloadActive)
            {
                _reloadActive = false;
                ArchiveData = data;
            }
        }

        private void HandleArchiveSaved()
        {
            if (IsActive && State == OnlineSessionState.Ready)
            {
                ArchiveSaved?.Invoke();
            }
        }

        private void HandleDisconnected()
        {
            if (!IsActive ||
                State == OnlineSessionState.Idle ||
                State == OnlineSessionState.Failed ||
                State == OnlineSessionState.Reconnecting)
            {
                return;
            }

            if (!_hasConnected)
            {
                Fail("Connection closed before the online session was established.");
                return;
            }

            _generation++;
            _client.ClearLoginInfo();
            CancelActiveOperations();
            TransitionTo(OnlineSessionState.Reconnecting);
        }

        private void HandleConnectionError(string reason)
        {
            if (IsActive && State != OnlineSessionState.Idle && State != OnlineSessionState.Failed)
            {
                Fail(string.IsNullOrWhiteSpace(reason) ? "Connection failed." : reason);
            }
        }

        private void HandleOperationFailed(string reason)
        {
            if (IsActive)
            {
                Fail(string.IsNullOrWhiteSpace(reason) ? "Online operation failed." : reason);
            }
        }

        private void Fail(string reason)
        {
            if (!IsActive || State == OnlineSessionState.Failed)
            {
                return;
            }

            _generation++;
            FailureReason = reason;
            CancelActiveOperations();
            TransitionTo(OnlineSessionState.Failed);
        }

        private void CancelActiveOperations()
        {
            _reloadActive = false;
            _loginService.CancelActiveOperation();
            _archiveService.CancelActiveOperation();
        }

        private bool IsActive => !_disposed && !_stopped;

        private bool IsCurrent(int generation)
        {
            return IsActive && generation == _generation;
        }

        private void TransitionTo(OnlineSessionState next)
        {
            if (State == next)
            {
                return;
            }

            State = next;
            StateChanged?.Invoke(next);
        }
    }
}
