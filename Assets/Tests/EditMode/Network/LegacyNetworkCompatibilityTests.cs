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
            // This test verifies the intentional obsolete compatibility surface.
#pragma warning disable CS0618
            Assert.That(Attribute.IsDefined(typeof(HeartbeatManager), typeof(ObsoleteAttribute)), Is.True);
            Assert.That(Attribute.IsDefined(typeof(ReconnectionManager), typeof(ObsoleteAttribute)), Is.True);
            Assert.That(typeof(ReconnectionManager).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(Coroutine)), Is.False);
            Assert.That(typeof(ReconnectionManager).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(method => typeof(IEnumerator).IsAssignableFrom(method.ReturnType)), Is.False);
#pragma warning restore CS0618
        }

        [Test]
        public void HeartbeatCompatibilityMethodsAreNoOps()
        {
            var gateway = new FakeNetworkConnectionGateway();
            var client = new NetworkClient();
            client.BindConnectionGateway(gateway);
            var go = new GameObject("heartbeat-legacy-test");
            // This test invokes the intentional obsolete no-op API.
#pragma warning disable CS0618
            var manager = go.AddComponent<HeartbeatManager>();
            manager.StartHeartbeat(client);
            manager.StopHeartbeat();
#pragma warning restore CS0618
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
