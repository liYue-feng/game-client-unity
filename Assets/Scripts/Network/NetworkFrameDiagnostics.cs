using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Network
{
    public enum NetworkFrameDirection
    {
        Outbound,
        Inbound
    }

    public static class NetworkFrameDiagnostics
    {
        private static readonly object Gate = new object();
        private static readonly List<Subscription> Subscriptions = new List<Subscription>();

        public static IDisposable Observe(Action<NetworkFrameDirection, byte[]> observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            var subscription = new Subscription(observer);
            lock (Gate)
            {
                Subscriptions.Add(subscription);
            }

            return subscription;
        }

        internal static void Publish(NetworkFrameDirection direction, byte[] frame)
        {
            if (frame == null)
            {
                return;
            }

            Subscription[] snapshot;
            lock (Gate)
            {
                snapshot = Subscriptions.ToArray();
            }

            foreach (var subscription in snapshot)
            {
                subscription.Invoke(direction, frame);
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action<NetworkFrameDirection, byte[]> _observer;

            internal Subscription(Action<NetworkFrameDirection, byte[]> observer)
            {
                _observer = observer;
            }

            internal void Invoke(NetworkFrameDirection direction, byte[] frame)
            {
                var observer = _observer;
                if (observer == null)
                {
                    return;
                }

                try
                {
                    observer(direction, (byte[])frame.Clone());
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[NetworkFrameDiagnostics] Frame observer failed: {exception.Message}");
                }
            }

            public void Dispose()
            {
                lock (Gate)
                {
                    if (_observer == null)
                    {
                        return;
                    }

                    _observer = null;
                    Subscriptions.Remove(this);
                }
            }
        }
    }
}
