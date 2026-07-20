using System;
using System.Collections.Generic;
using Game.Network;
using Game.Protocol;

namespace Game.Online
{
    public sealed class ArchiveSessionService : IDisposable
    {
        private const string DisconnectedError = "Network client is not connected.";
        private readonly NetworkClient _client;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private PlayerArchive _currentArchive = new PlayerArchive();
        private ArchiveOperation _activeOperation;
        private bool _disposed;

        public ArchiveSessionService(NetworkClient client = null)
        {
            _client = client ?? NetworkClient.Instance;
            _subscriptions.Add(_client.On<LoadArchiveResp>(MsgID.LoadArchiveResp, HandleLoadResponse));
            _subscriptions.Add(_client.On<SaveArchiveResp>(MsgID.SaveArchiveResp, HandleSaveResponse));
            _subscriptions.Add(_client.On<ErrorResp>(MsgID.Error, HandleErrorResponse));
        }

        public PlayerArchive CurrentArchive => _currentArchive.Clone();
        public event Action<PlayerArchive> Loaded;
        public event Action Saved;
        public event Action<string> Failed;

        public bool Load()
        {
            if (!TryBegin(ArchiveOperation.Load))
            {
                return false;
            }

            if (_client.Send(MsgID.LoadArchiveReq, new LoadArchiveReq()))
            {
                return true;
            }

            _activeOperation = ArchiveOperation.None;
            Failed?.Invoke(DisconnectedError);
            return false;
        }

        public bool Save(PlayerArchive archive)
        {
            if (!TryBegin(ArchiveOperation.Save))
            {
                return false;
            }

            if (_client.Send(MsgID.SaveArchiveReq, new SaveArchiveReq
            {
                Archive = archive?.Clone() ?? new PlayerArchive()
            }))
            {
                return true;
            }

            _activeOperation = ArchiveOperation.None;
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
            _activeOperation = ArchiveOperation.None;
            foreach (var subscription in _subscriptions)
            {
                subscription.Dispose();
            }

            _subscriptions.Clear();
            Loaded = null;
            Saved = null;
            Failed = null;
        }

        internal void CancelActiveOperation()
        {
            _activeOperation = ArchiveOperation.None;
        }

        private bool TryBegin(ArchiveOperation operation)
        {
            if (_disposed)
            {
                return false;
            }

            if (_activeOperation != ArchiveOperation.None)
            {
                Failed?.Invoke("Archive operation is already active.");
                return false;
            }

            _activeOperation = operation;
            return true;
        }

        private void HandleLoadResponse(LoadArchiveResp response)
        {
            if (_activeOperation != ArchiveOperation.Load)
            {
                return;
            }

            _activeOperation = ArchiveOperation.None;
            _currentArchive = response.Found && response.Archive != null
                ? response.Archive.Clone()
                : new PlayerArchive();
            Loaded?.Invoke(_currentArchive.Clone());
        }

        private void HandleSaveResponse(SaveArchiveResp response)
        {
            if (_activeOperation != ArchiveOperation.Save)
            {
                return;
            }

            _activeOperation = ArchiveOperation.None;
            if (response.Success)
            {
                Saved?.Invoke();
                return;
            }

            Failed?.Invoke("Archive save failed.");
        }

        private void HandleErrorResponse(ErrorResp response)
        {
            if (_activeOperation == ArchiveOperation.None)
            {
                return;
            }

            _activeOperation = ArchiveOperation.None;
            Failed?.Invoke($"[{response.Code}] {response.Msg}");
        }

        private enum ArchiveOperation
        {
            None,
            Load,
            Save
        }
    }
}
