using System;
using System.Collections.Generic;
using Game.Network;
using Game.Protocol;
using UnityEngine;

namespace Game.Managers
{
    public class ArchiveManager : MonoBehaviour
    {
        private static ArchiveManager _instance;
        private readonly HashSet<uint> _pendingRequests = new HashSet<uint>();
        private bool _isSaving;
        private bool _destroyed;

        public static ArchiveManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[ArchiveManager]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<ArchiveManager>();
                }

                return _instance;
            }
        }

        public PlayerArchive CurrentArchive { get; private set; }
        public event Action<PlayerArchive> OnLoadSuccess;
        public event Action OnSaveSuccess;
        public event Action<string> OnError;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            _destroyed = true;
            foreach (var seq in new List<uint>(_pendingRequests))
            {
                NetworkClient.Instance.CancelRequest(seq);
            }

            _pendingRequests.Clear();
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

        public void LoadArchive()
        {
            if (!NetworkClient.Instance.IsLoggedIn)
            {
                OnError?.Invoke("Not logged in.");
                return;
            }

            Request<LoadArchiveReq, LoadArchiveResp>(
                MsgID.LoadArchiveReq,
                MsgID.LoadArchiveResp,
                new LoadArchiveReq(),
                HandleLoadResp,
                reason => OnError?.Invoke(reason));
        }

        public void SaveArchive(PlayerArchive archive = null, bool immediate = false)
        {
            if (!NetworkClient.Instance.IsLoggedIn)
            {
                OnError?.Invoke("Not logged in.");
                return;
            }

            if (_isSaving)
            {
                return;
            }

            _isSaving = true;
            CurrentArchive = archive ?? new PlayerArchive();
            SendArchiveSave(CurrentArchive);
        }

        public void LoadArchive(int slotIndex)
        {
            LoadArchive();
        }

        public void DeleteArchive(int slotIndex)
        {
            SaveArchive(new PlayerArchive());
        }

        private void HandleSaveResp(SaveArchiveResp response)
        {
            _isSaving = false;
            if (response.Success)
            {
                OnSaveSuccess?.Invoke();
                return;
            }

            OnError?.Invoke("Archive save failed.");
        }

        private void HandleLoadResp(LoadArchiveResp response)
        {
            CurrentArchive = response.Found && response.Archive != null
                ? response.Archive
                : new PlayerArchive();
            OnLoadSuccess?.Invoke(CurrentArchive);
        }

        private void SendArchiveSave(PlayerArchive archive)
        {
            Request<SaveArchiveReq, SaveArchiveResp>(
                MsgID.SaveArchiveReq,
                MsgID.SaveArchiveResp,
                new SaveArchiveReq { Archive = archive },
                HandleSaveResp,
                reason =>
                {
                    _isSaving = false;
                    OnError?.Invoke(reason);
                });
        }

        private bool Request<TRequest, TResponse>(
            ushort requestId,
            ushort responseId,
            TRequest payload,
            Action<TResponse> onSuccess,
            Action<string> onFailure)
            where TRequest : class, Google.Protobuf.IMessage<TRequest>
            where TResponse : class, Google.Protobuf.IMessage<TResponse>
        {
            var completed = false;
            uint seq = 0;
            var sent = NetworkClient.Instance.Request<TRequest, TResponse>(
                requestId,
                responseId,
                payload,
                response =>
                {
                    completed = true;
                    _pendingRequests.Remove(seq);
                    if (!_destroyed)
                    {
                        onSuccess?.Invoke(response);
                    }
                },
                reason =>
                {
                    completed = true;
                    _pendingRequests.Remove(seq);
                    if (!_destroyed)
                    {
                        onFailure?.Invoke(reason);
                    }
                },
                out seq);
            if (sent && !completed)
            {
                _pendingRequests.Add(seq);
            }

            return sent;
        }

        private void OnApplicationQuit()
        {
            if (CurrentArchive != null && NetworkClient.Instance.IsLoggedIn)
            {
                SendArchiveSave(CurrentArchive);
            }
        }
    }
}
