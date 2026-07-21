using System;
using Game.Network;
using Game.Protocol;

namespace Game.Online
{
    public sealed class ArchiveSessionService : IDisposable
    {
        private const string DisconnectedError = "Network client is not connected.";
        private readonly NetworkClient _client;
        private PlayerArchive _currentArchive = new PlayerArchive();
        private ArchiveOperation _activeOperation;
        private uint _activeSeq;
        private int _attempt;
        private bool _disposed;

        public ArchiveSessionService(NetworkClient client = null)
        {
            _client = client ?? NetworkClient.Instance;
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

            return SendRequest<LoadArchiveReq, LoadArchiveResp>(
                MsgID.LoadArchiveReq,
                MsgID.LoadArchiveResp,
                new LoadArchiveReq(),
                ArchiveOperation.Load,
                HandleLoadResponse);
        }

        public bool Save(PlayerArchive archive)
        {
            if (!TryBegin(ArchiveOperation.Save))
            {
                return false;
            }

            return SendRequest<SaveArchiveReq, SaveArchiveResp>(
                MsgID.SaveArchiveReq,
                MsgID.SaveArchiveResp,
                new SaveArchiveReq
                {
                    Archive = archive?.Clone() ?? new PlayerArchive()
                },
                ArchiveOperation.Save,
                HandleSaveResponse);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            CancelActiveOperation();
            Loaded = null;
            Saved = null;
            Failed = null;
        }

        internal void CancelActiveOperation()
        {
            var seq = _activeSeq;
            _activeOperation = ArchiveOperation.None;
            _activeSeq = 0;
            _attempt++;
            if (seq != 0)
            {
                _client.CancelRequest(seq);
            }
        }

        private bool TryBegin(ArchiveOperation operation)
        {
            if (_disposed)
            {
                return false;
            }

            if (_activeOperation != ArchiveOperation.None)
            {
                return false;
            }

            _activeOperation = operation;
            _attempt++;
            return true;
        }

        private bool SendRequest<TRequest, TResponse>(
            ushort requestId,
            ushort responseId,
            TRequest request,
            ArchiveOperation operation,
            Action<TResponse> onSuccess)
            where TRequest : class, Google.Protobuf.IMessage<TRequest>
            where TResponse : class, Google.Protobuf.IMessage<TResponse>
        {
            var attempt = _attempt;
            var requestReturned = false;
            string synchronousFailure = null;
            var sent = _client.Request<TRequest, TResponse>(
                requestId,
                responseId,
                request,
                response =>
                {
                    if (IsActiveAttempt(operation, attempt))
                    {
                        onSuccess(response);
                    }
                },
                reason =>
                {
                    if (!requestReturned)
                    {
                        synchronousFailure = reason;
                        return;
                    }

                    HandleFailure(operation, attempt, reason);
                },
                out var seq);
            requestReturned = true;
            if (sent && IsActiveAttempt(operation, attempt))
            {
                _activeSeq = seq;
            }

            if (synchronousFailure != null && IsActiveAttempt(operation, attempt))
            {
                HandleFailure(operation, attempt, sent ? synchronousFailure : DisconnectedError);
            }
            else if (!sent && IsActiveAttempt(operation, attempt))
            {
                HandleFailure(operation, attempt, DisconnectedError);
            }

            return sent;
        }

        private bool IsActiveAttempt(ArchiveOperation operation, int attempt)
        {
            return !_disposed && _activeOperation == operation && _attempt == attempt;
        }

        private void HandleLoadResponse(LoadArchiveResp response)
        {
            if (_activeOperation != ArchiveOperation.Load)
            {
                return;
            }

            _activeOperation = ArchiveOperation.None;
            _activeSeq = 0;
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
            _activeSeq = 0;
            if (response.Success)
            {
                Saved?.Invoke();
                return;
            }

            Failed?.Invoke("Archive save failed.");
        }

        private void HandleFailure(ArchiveOperation operation, int attempt, string reason)
        {
            if (!IsActiveAttempt(operation, attempt))
            {
                return;
            }

            _activeOperation = ArchiveOperation.None;
            _activeSeq = 0;
            Failed?.Invoke(reason);
        }

        private enum ArchiveOperation
        {
            None,
            Load,
            Save
        }
    }
}
