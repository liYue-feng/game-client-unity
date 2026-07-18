using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Game.Network;
using Game.Tests.EditMode.Network.TestDoubles;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Network
{
    public sealed class LegacyNetworkCompatibilityTests
    {
        [TearDown]
        public void TearDown()
        {
            NetworkClient.ResetStaticState();
        }

        [Test]
        public void LegacyTypesAreObsoleteAndOwnNoTimersOrCoroutines()
        {
            Assert.That(Attribute.IsDefined(typeof(HeartbeatManager), typeof(ObsoleteAttribute)), Is.True);
            Assert.That(Attribute.IsDefined(typeof(ReconnectionManager), typeof(ObsoleteAttribute)), Is.True);
            Assert.That(typeof(ReconnectionManager).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(Coroutine)), Is.False);
            Assert.That(typeof(ReconnectionManager).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(method => typeof(IEnumerator).IsAssignableFrom(method.ReturnType)), Is.False);
        }

        [Test]
        public void HeartbeatCompatibilityMethodsAreNoOps()
        {
            var gateway = new FakeNetworkConnectionGateway();
            var client = new NetworkClient();
            client.BindConnectionGateway(gateway);
            var go = new GameObject("heartbeat-legacy-test");
            var manager = go.AddComponent<HeartbeatManager>();
            manager.StartHeartbeat(client);
            manager.StopHeartbeat();
            Assert.That(gateway.ConnectCalls, Is.Zero);
            Assert.That(gateway.DisconnectCalls, Is.Zero);
            Object.DestroyImmediate(go);
            client.Dispose();
        }

        [TestCase(NetworkConnectionState.Disconnected, NetworkStatus.Disconnected, ReconnectState.Idle)]
        [TestCase(NetworkConnectionState.Connecting, NetworkStatus.Unstable, ReconnectState.Idle)]
        [TestCase(NetworkConnectionState.Connected, NetworkStatus.Connected, ReconnectState.Connected)]
        [TestCase(NetworkConnectionState.Authenticating, NetworkStatus.Connected, ReconnectState.Connected)]
        [TestCase(NetworkConnectionState.Ready, NetworkStatus.Connected, ReconnectState.Connected)]
        [TestCase(NetworkConnectionState.Reconnecting, NetworkStatus.Reconnecting, ReconnectState.Reconnecting)]
        [TestCase(NetworkConnectionState.Failed, NetworkStatus.Disconnected, ReconnectState.Failed)]
        public void StatusAdapterUsesExactLegacyMapping(
            NetworkConnectionState state,
            NetworkStatus networkStatus,
            ReconnectState reconnectState)
        {
            Assert.That(NetworkStatusAdapter.ToNetworkStatus(state), Is.EqualTo(networkStatus));
            Assert.That(NetworkStatusAdapter.ToReconnectState(state), Is.EqualTo(reconnectState));
        }
    }
}
