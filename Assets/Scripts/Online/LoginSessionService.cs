using System;
using Game.Network;
using Game.Protocol;

namespace Game.Online
{
    public sealed class LoginSessionService : IDisposable
    {
        private const string DisconnectedError = "Network client is not connected.";
        private readonly NetworkClient _client;
        private uint _activeSeq;
        private int _attempt;
        private bool _loginActive;
        private bool _disposed;

        public LoginSessionService(NetworkClient client = null)
        {
            _client = client ?? NetworkClient.Instance;
        }

        public event Action<LoginResp> Succeeded;
        public event Action<string> Failed;

        public bool Begin(string code)
        {
            if (_disposed)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                Failed?.Invoke("Login code is required.");
                return false;
            }

            if (_loginActive)
            {
                Failed?.Invoke("Login request is already active.");
                return false;
            }

            var attempt = ++_attempt;
            _loginActive = true;
            var requestReturned = false;
            string synchronousFailure = null;
            var sent = _client.Request<LoginReq, LoginResp>(
                MsgID.LoginReq,
                MsgID.LoginResp,
                new LoginReq { Code = code },
                response => HandleLoginResponse(attempt, response),
                reason =>
                {
                    if (!requestReturned)
                    {
                        synchronousFailure = reason;
                        return;
                    }

                    HandleFailure(attempt, reason);
                },
                out var seq);
            requestReturned = true;
            if (sent && IsActiveAttempt(attempt))
            {
                _activeSeq = seq;
            }

            if (synchronousFailure != null && IsActiveAttempt(attempt))
            {
                HandleFailure(attempt, sent ? synchronousFailure : DisconnectedError);
            }
            else if (!sent && IsActiveAttempt(attempt))
            {
                HandleFailure(attempt, DisconnectedError);
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
            CancelActiveOperation();
            Succeeded = null;
            Failed = null;
        }

        internal void CancelActiveOperation()
        {
            var seq = _activeSeq;
            _loginActive = false;
            _activeSeq = 0;
            _attempt++;
            if (seq != 0)
            {
                _client.CancelRequest(seq);
            }
        }

        private bool IsActiveAttempt(int attempt)
        {
            return !_disposed && _loginActive && attempt == _attempt;
        }

        private void HandleLoginResponse(int attempt, LoginResp response)
        {
            if (!IsActiveAttempt(attempt))
            {
                return;
            }

            _loginActive = false;
            _activeSeq = 0;
            _client.SetLoginInfo(response.Uid, response.Token);
            Succeeded?.Invoke(response);
        }

        private void HandleFailure(int attempt, string reason)
        {
            if (!IsActiveAttempt(attempt))
            {
                return;
            }

            _loginActive = false;
            _activeSeq = 0;
            Failed?.Invoke(reason);
        }
    }
}
