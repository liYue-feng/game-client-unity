using System;
using System.Collections.Generic;
using Game.Network;
using Game.Protocol;

namespace Game.Online
{
    public sealed class LoginSessionService : IDisposable
    {
        private const string DisconnectedError = "Network client is not connected.";
        private readonly NetworkClient _client;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private bool _loginActive;
        private bool _disposed;

        public LoginSessionService(NetworkClient client = null)
        {
            _client = client ?? NetworkClient.Instance;
            _subscriptions.Add(_client.On<LoginResp>(MsgID.LoginResp, HandleLoginResponse));
            _subscriptions.Add(_client.On<ErrorResp>(MsgID.Error, HandleErrorResponse));
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

            _loginActive = true;
            if (_client.Send(MsgID.LoginReq, new LoginReq { code = code }))
            {
                return true;
            }

            _loginActive = false;
            Failed?.Invoke(DisconnectedError);
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _loginActive = false;
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
            Succeeded = null;
            Failed = null;
        }

        internal void CancelActiveOperation()
        {
            _loginActive = false;
        }

        private void HandleLoginResponse(LoginResp response)
        {
            if (!_loginActive)
            {
                return;
            }

            _loginActive = false;
            _client.SetLoginInfo(response.uid, response.token);
            Succeeded?.Invoke(response);
        }

        private void HandleErrorResponse(ErrorResp response)
        {
            if (!_loginActive)
            {
                return;
            }

            _loginActive = false;
            Failed?.Invoke($"[{response.code}] {response.msg}");
        }
    }
}
