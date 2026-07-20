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
        private readonly List<IDisposable> _networkSubscriptions = new List<IDisposable>();
        private bool _isSaving;

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
            var client = NetworkClient.Instance;
            _networkSubscriptions.Add(client.On<SaveArchiveResp>(MsgID.SaveArchiveResp, HandleSaveResp));
            _networkSubscriptions.Add(client.On<LoadArchiveResp>(MsgID.LoadArchiveResp, HandleLoadResp));
        }

        private void OnDestroy()
        {
            foreach (var subscription in _networkSubscriptions)
            {
                subscription.Dispose();
            }

            _networkSubscriptions.Clear();
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

            NetworkClient.Instance.Send(MsgID.LoadArchiveReq, new LoadArchiveReq());
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
            NetworkClient.Instance.Send(MsgID.SaveArchiveReq, new SaveArchiveReq { Archive = CurrentArchive });
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

        private void OnApplicationQuit()
        {
            if (CurrentArchive != null && NetworkClient.Instance.IsLoggedIn)
            {
                NetworkClient.Instance.Send(MsgID.SaveArchiveReq, new SaveArchiveReq { Archive = CurrentArchive });
            }
        }
    }
}
