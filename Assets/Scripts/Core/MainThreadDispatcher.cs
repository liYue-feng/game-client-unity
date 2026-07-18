using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Network
{
    public class MainThreadDispatcher : MonoBehaviour, IGameService
    {
        private static readonly Queue<Action> Queue = new Queue<Action>();
        private static readonly object QueueLock = new object();

        private static MainThreadDispatcher _instance;
        private static bool _accepting;

        private int _maxTasksPerFrame;

        public string ServiceName => nameof(MainThreadDispatcher);

        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.LogError("[MainThreadDispatcher] Install must be called before Instance.");
                }

                return _instance;
            }
        }

        public static int PendingCount
        {
            get
            {
                lock (QueueLock)
                {
                    return Queue.Count;
                }
            }
        }

        public static MainThreadDispatcher Install(Transform parent, int maxTasksPerFrame)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (maxTasksPerFrame <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTasksPerFrame));
            }

            if (_instance != null)
            {
                return _instance;
            }

            var serviceObject = new GameObject("[MainThreadDispatcher]");
            serviceObject.transform.SetParent(parent, false);

            var dispatcher = serviceObject.AddComponent<MainThreadDispatcher>();
            dispatcher._maxTasksPerFrame = maxTasksPerFrame;
            _instance = dispatcher;
            return dispatcher;
        }

        public void Initialize()
        {
            lock (QueueLock)
            {
                if (!ReferenceEquals(_instance, this))
                {
                    return;
                }

                _accepting = true;
            }
        }

        public void Shutdown()
        {
            lock (QueueLock)
            {
                if (!ReferenceEquals(_instance, this))
                {
                    return;
                }

                _accepting = false;
                Queue.Clear();
            }
        }

        public static bool Enqueue(Action action)
        {
            lock (QueueLock)
            {
                if (!_accepting)
                {
                    return false;
                }

                Queue.Enqueue(action);
                return true;
            }
        }

        public void ProcessPending()
        {
            for (var processed = 0; processed < _maxTasksPerFrame; processed++)
            {
                Action action;
                lock (QueueLock)
                {
                    if (!ReferenceEquals(_instance, this) || Queue.Count == 0)
                    {
                        return;
                    }

                    action = Queue.Dequeue();
                }

                try
                {
                    action?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        public static void ResetStaticState()
        {
            lock (QueueLock)
            {
                _accepting = false;
                Queue.Clear();
                _instance = null;
            }
        }

        private void Update()
        {
            ProcessPending();
        }

        private void OnDestroy()
        {
            lock (QueueLock)
            {
                if (!ReferenceEquals(_instance, this))
                {
                    return;
                }

                _accepting = false;
                Queue.Clear();
                _instance = null;
            }
        }
    }
}
