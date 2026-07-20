using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Core
{
    public sealed class GameRuntimeSettings : ScriptableObject
    {
        [SerializeField] private RuntimeMode _runtimeMode = RuntimeMode.Offline;
        [FormerlySerializedAs("_startupSceneName")]
        [SerializeField] private string _offlineStartupSceneName = "BattleScene";
        [SerializeField] private string _onlineStartupSceneName = "MenuScene";
        [SerializeField] private string _editorLoginIdentity = "editor-001";
        [SerializeField] private float _onlineSessionTimeoutSeconds = 20f;
        [SerializeField] private string _serverUrl = "ws://localhost:8080/ws";
        [SerializeField] private float _heartbeatIntervalSeconds = 30f;
        [SerializeField] private float _connectionTimeoutSeconds = 10f;
        [SerializeField] private int _maxReconnectAttempts = 5;
        [SerializeField] private float _initialReconnectBackoffSeconds = 1f;
        [SerializeField] private float _maxReconnectBackoffSeconds = 30f;
        [SerializeField] private int _mainThreadMaxTasksPerFrame = 64;

        public RuntimeMode RuntimeMode => _runtimeMode;
        public string OfflineStartupSceneName => _offlineStartupSceneName;
        public string OnlineStartupSceneName => _onlineStartupSceneName;
        public string StartupSceneName => _runtimeMode == RuntimeMode.Online
            ? _onlineStartupSceneName
            : _offlineStartupSceneName;
        public string EditorLoginIdentity => _editorLoginIdentity;
        public float OnlineSessionTimeoutSeconds => _onlineSessionTimeoutSeconds;
        public string ServerUrl => _serverUrl;
        public float HeartbeatIntervalSeconds => _heartbeatIntervalSeconds;
        public float ConnectionTimeoutSeconds => _connectionTimeoutSeconds;
        public int MaxReconnectAttempts => _maxReconnectAttempts;
        public float InitialReconnectBackoffSeconds => _initialReconnectBackoffSeconds;
        public float MaxReconnectBackoffSeconds => _maxReconnectBackoffSeconds;
        public int MainThreadMaxTasksPerFrame => _mainThreadMaxTasksPerFrame;

        public bool TryValidate(Func<string, bool> canLoadScene, out string error)
        {
            if (!Enum.IsDefined(typeof(RuntimeMode), _runtimeMode))
            {
                error = $"RuntimeMode value '{_runtimeMode}' is not defined.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(StartupSceneName))
            {
                error = "StartupSceneName cannot be null or whitespace.";
                return false;
            }

            if (canLoadScene == null)
            {
                error = "StartupSceneName scene availability check required.";
                return false;
            }

            if (!canLoadScene(StartupSceneName))
            {
                error = $"StartupSceneName '{StartupSceneName}' is not available in the build.";
                return false;
            }

            if (!Uri.TryCreate(_serverUrl, UriKind.Absolute, out var serverUri) ||
                (!string.Equals(serverUri.Scheme, "ws", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(serverUri.Scheme, "wss", StringComparison.OrdinalIgnoreCase)))
            {
                error = "ServerUrl must be an absolute ws:// or wss:// URI.";
                return false;
            }

            if (_runtimeMode == RuntimeMode.Online && string.IsNullOrWhiteSpace(_editorLoginIdentity))
            {
                error = "EditorLoginIdentity cannot be null or whitespace in Online mode.";
                return false;
            }

            if (!(_onlineSessionTimeoutSeconds > 0f) || float.IsNaN(_onlineSessionTimeoutSeconds) ||
                float.IsInfinity(_onlineSessionTimeoutSeconds))
            {
                error = "OnlineSessionTimeoutSeconds must be finite and greater than zero.";
                return false;
            }

            if (!(_heartbeatIntervalSeconds > 0f))
            {
                error = "HeartbeatIntervalSeconds must be greater than zero.";
                return false;
            }

            if (!(_connectionTimeoutSeconds > 0f))
            {
                error = "ConnectionTimeoutSeconds must be greater than zero.";
                return false;
            }

            if (_maxReconnectAttempts < 0)
            {
                error = "MaxReconnectAttempts must not be negative.";
                return false;
            }

            if (!(_initialReconnectBackoffSeconds > 0f))
            {
                error = "InitialReconnectBackoffSeconds must be greater than zero.";
                return false;
            }

            if (!(_maxReconnectBackoffSeconds > 0f))
            {
                error = "MaxReconnectBackoffSeconds must be greater than zero.";
                return false;
            }

            if (_maxReconnectBackoffSeconds < _initialReconnectBackoffSeconds)
            {
                error = "MaxReconnectBackoffSeconds must be greater than or equal to InitialReconnectBackoffSeconds.";
                return false;
            }

            if (_mainThreadMaxTasksPerFrame <= 0)
            {
                error = "MainThreadMaxTasksPerFrame must be greater than zero.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
