using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Network;
using Game.Online;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    public sealed class GameApplication : MonoBehaviour
    {
        private static GameApplication _instance;

        private readonly GameApplicationLifecycle _lifecycle = new GameApplicationLifecycle();
        private GameRuntimeSettings _settings;
        private GameServices _services;
        private bool _shutdownStarted;

        public static GameApplication Instance => _instance;
        public static bool HasInstance => _instance != null;
        public GameApplicationState State => _lifecycle.State;
        public string FailureStage { get; private set; }
        public string FailureReason { get; private set; }

        private void Awake()
        {
            if (_instance != null && !ReferenceEquals(_instance, this))
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            _lifecycle.BeginInitialization();

            try
            {
                FailureStage = "Settings.Load";
                _settings = Resources.Load<GameRuntimeSettings>("GameRuntimeSettings");
                if (_settings == null)
                {
                    throw new InvalidOperationException("Resources/GameRuntimeSettings asset is missing.");
                }

                FailureStage = "Settings.Validate";
                if (!_settings.TryValidate(Application.CanStreamedLevelBeLoaded, out var validationError))
                {
                    throw new InvalidOperationException(validationError);
                }

                FailureStage = "Mode.Select";
                switch (_settings.RuntimeMode)
                {
                    case RuntimeMode.Offline:
                        break;
                    case RuntimeMode.Online:
                        throw new NotSupportedException("Online runtime flow is not implemented in Phase A3");
                    default:
                        throw new InvalidOperationException($"RuntimeMode '{_settings.RuntimeMode}' is not supported.");
                }

                FailureStage = "Services.Initialize";
                _services = GameServices.Create(transform, _settings);
                FailureStage = null;
            }
            catch (Exception exception)
            {
                FailInitialization(exception);
            }
        }

        private IEnumerator Start()
        {
            if (State != GameApplicationState.Initializing)
            {
                yield break;
            }

            // Allow other runtime initialization observers to start before replacing the bootstrap scene.
            yield return null;

            if (!string.Equals(SceneManager.GetActiveScene().name, _settings.StartupSceneName, StringComparison.Ordinal))
            {
                FailureStage = "Scene.Load";
                AsyncOperation loadOperation;
                try
                {
                    loadOperation = SceneManager.LoadSceneAsync(_settings.StartupSceneName, LoadSceneMode.Single);
                    if (loadOperation == null)
                    {
                        throw new InvalidOperationException($"Failed to start loading scene '{_settings.StartupSceneName}'.");
                    }
                }
                catch (Exception exception)
                {
                    FailInitialization(exception);
                    yield break;
                }

                while (!loadOperation.isDone)
                {
                    yield return null;
                }

                if (!string.Equals(SceneManager.GetActiveScene().name, _settings.StartupSceneName, StringComparison.Ordinal))
                {
                    FailInitialization(new InvalidOperationException($"Scene '{_settings.StartupSceneName}' did not become active."));
                    yield break;
                }
            }

            FailureStage = null;
            _lifecycle.MarkReady();
        }

        public void Shutdown()
        {
            ReleaseCore();
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        internal static void ResetStaticState()
        {
            _instance = null;
            NetworkClient.ResetStaticState();
            OnlineSessionHost.ResetStaticState();
            MainThreadDispatcher.ResetStaticState();
            SceneTransitionManager.ResetStaticState();
            AudioManager.ResetStaticState();
            LoadingScreen.ResetStaticState();
            AchievementManager.ResetStaticState();
        }

        private void FailInitialization(Exception exception)
        {
            FailureReason = FormatFailureReason(exception);
            _services?.Shutdown();
            _services = null;
            _lifecycle.MarkFailed();
            Debug.LogError($"[GameApplication] Initialization failed at {FailureStage}: {FailureReason}");
            Debug.LogException(exception);
        }

        private static string FormatFailureReason(Exception exception)
        {
            if (exception == null)
            {
                return "Unknown failure.";
            }

            var parts = new List<string>();
            if (exception is GameServiceInitializationException serviceFailure)
            {
                parts.Add($"Service '{serviceFailure.ServiceName}' failed.");
                parts.Add($"Root cause: {GetInnermostMessage(serviceFailure.InnerException)}.");
                if (serviceFailure.RollbackErrors.Count > 0)
                {
                    var rollbackMessages = new List<string>();
                    foreach (var rollbackError in serviceFailure.RollbackErrors)
                    {
                        rollbackMessages.Add(GetInnermostMessage(rollbackError));
                    }

                    parts.Add($"Rollback errors: {string.Join("; ", rollbackMessages)}.");
                }
            }
            else
            {
                parts.Add($"Root cause: {GetInnermostMessage(exception)}.");
            }

            return string.Join(" ", parts);
        }

        private static string GetInnermostMessage(Exception exception)
        {
            if (exception == null)
            {
                return "Unknown failure";
            }

            while (exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            return exception.Message;
        }

        private void ReleaseCore()
        {
            if (_shutdownStarted)
            {
                return;
            }

            _shutdownStarted = true;
            if (State == GameApplicationState.Initializing)
            {
                _lifecycle.MarkFailed();
            }

            if (State == GameApplicationState.Ready || State == GameApplicationState.Failed)
            {
                _lifecycle.BeginShutdown();
            }

            _services?.Shutdown();
            _services = null;

            if (State == GameApplicationState.ShuttingDown)
            {
                _lifecycle.MarkStopped();
            }

            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

        private void OnDestroy()
        {
            ReleaseCore();
        }
    }
}
