using System;
using Game.Core;
using Game.Network;
using Game.Online;
using UnityEngine;

namespace Game
{
    internal sealed class GameServices
    {
        private readonly GameObject _rootObject;
        private GameServiceCollection _lifecycle;
        private NetworkClient _networkClient;
        private bool _shutdown;

        internal OnlineSessionHost OnlineSession { get; private set; }

        private GameServices(GameObject rootObject)
        {
            _rootObject = rootObject;
        }

        internal static GameServices Create(
            Transform applicationRoot,
            GameRuntimeSettings settings,
            IWebSocketTransportFactory transportFactory = null,
            ILoginCodeProvider loginCodeProvider = null)
        {
            if (applicationRoot == null)
            {
                throw new ArgumentNullException(nameof(applicationRoot));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (settings.RuntimeMode == RuntimeMode.Online && OnlineSessionHost.Instance != null)
            {
                throw new InvalidOperationException("Online GameServices are already installed.");
            }

            var rootObject = new GameObject("[GameServices]");
            rootObject.transform.SetParent(applicationRoot, false);
            var services = new GameServices(rootObject);

            try
            {
                var root = rootObject.transform;
                var dispatcher = MainThreadDispatcher.Install(root, settings.MainThreadMaxTasksPerFrame);
                var client = new NetworkClient
                {
                    serverUrl = settings.ServerUrl
                };
                NetworkClient.RegisterInstance(client);
                services._networkClient = client;
                var networkHost = NetworkConnectionControllerHost.Install(
                    root,
                    client,
                    transportFactory ?? new WebSocketTransportFactory(),
                    settings);
                if (settings.RuntimeMode == RuntimeMode.Online)
                {
                    services.OnlineSession = OnlineSessionHost.Install(
                        root,
                        client,
                        networkHost,
                        settings,
                        loginCodeProvider);
                }

                var sceneTransition = SceneTransitionManager.Install(root);
                var audio = AudioManager.Install(root);
                var loading = LoadingScreen.Install(root);
                var achievements = AchievementManager.Install(root);

                var lifecycle = new System.Collections.Generic.List<IGameService>
                {
                    dispatcher,
                    networkHost
                };
                if (services.OnlineSession != null)
                {
                    lifecycle.Add(services.OnlineSession);
                }

                lifecycle.AddRange(new IGameService[]
                {
                    sceneTransition,
                    audio,
                    loading,
                    achievements
                });
                services._lifecycle = new GameServiceCollection(lifecycle);
                services._lifecycle.InitializeAll();
                return services;
            }
            catch
            {
                services.Shutdown();
                throw;
            }
        }

        internal void Shutdown()
        {
            if (_shutdown)
            {
                return;
            }

            _shutdown = true;
            if (_lifecycle != null)
            {
                foreach (var exception in _lifecycle.ShutdownAll())
                {
                    Debug.LogException(exception);
                }
            }

            OnlineSession?.Shutdown();
            OnlineSession = null;

            if (_networkClient != null)
            {
                NetworkClient.UnregisterInstance(_networkClient);
                _networkClient.Dispose();
                _networkClient = null;
            }

            NetworkClient.ResetStaticState();
            MainThreadDispatcher.ResetStaticState();
            SceneTransitionManager.ResetStaticState();
            AudioManager.ResetStaticState();
            LoadingScreen.ResetStaticState();
            AchievementManager.ResetStaticState();

            if (_rootObject != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_rootObject);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_rootObject);
                }
            }
        }
    }
}
