using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Network.TestDoubles
{
    internal static class NetworkTestSettings
    {
        public static GameRuntimeSettings Create(
            float heartbeat = 10f,
            float timeout = 5f,
            int maxAttempts = 3,
            float initialBackoff = 1f,
            float maxBackoff = 4f)
        {
            var settings = ScriptableObject.CreateInstance<GameRuntimeSettings>();
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("_serverUrl").stringValue = "ws://unit.test/ws";
            serialized.FindProperty("_heartbeatIntervalSeconds").floatValue = heartbeat;
            serialized.FindProperty("_connectionTimeoutSeconds").floatValue = timeout;
            serialized.FindProperty("_maxReconnectAttempts").intValue = maxAttempts;
            serialized.FindProperty("_initialReconnectBackoffSeconds").floatValue = initialBackoff;
            serialized.FindProperty("_maxReconnectBackoffSeconds").floatValue = maxBackoff;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return settings;
        }
    }
}
