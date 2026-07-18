using System;
using Game.Core;
using Game.Network;
using UnityEngine;

namespace Game
{
    internal sealed class GameServices
    {
        private readonly GameObject _rootObject;
        private GameServiceCollection _lifecycle;
        private bool _shutdown;

        private GameServices(GameObject rootObject)
        {
            _rootObject = rootObject;
        }

        internal static GameServices Create(Transform applicationRoot, GameRuntimeSettings settings)
        {
            if (applicationRoot == null)
            {
                throw new ArgumentNullException(nameof(applicationRoot));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var rootObject = new GameObject("[GameServices]");
            rootObject.transform.SetParent(applicationRoot, false);
            var services = new GameServices(rootObject);

            try
            {
                var root = rootObject.transform;
                var dispatcher = MainThreadDispatcher.Install(root, settings.MainThreadMaxTasksPerFrame);
                var sceneTransition = SceneTransitionManager.Install(root);
                var audio = AudioManager.Install(root);
                var loading = LoadingScreen.Install(root);
                var achievements = AchievementManager.Install(root);

                services._lifecycle = new GameServiceCollection(new IGameService[]
                {
                    dispatcher,
                    sceneTransition,
                    audio,
                    loading,
                    achievements
                });
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

            MainThreadDispatcher.ResetStaticState();
            SceneTransitionManager.ResetStaticState();
            AudioManager.ResetStaticState();
            LoadingScreen.ResetStaticState();
            AchievementManager.ResetStaticState();

            if (_rootObject != null)
            {
                UnityEngine.Object.Destroy(_rootObject);
            }
        }
    }
}
