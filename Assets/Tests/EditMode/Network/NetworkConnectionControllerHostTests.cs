using System.Linq;
using System.Reflection;
using Game.Network;
using Game.Tests.EditMode.Network.TestDoubles;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Network
{
    public sealed class NetworkConnectionControllerHostTests
    {
        [Test]
        public void HostInitializeBindsGatewayUpdateTicksAndShutdownUnbinds()
        {
            var root = new GameObject("host-test-root");
            var client = new NetworkClient();
            var factory = new FakeWebSocketTransportFactory();
            var dispatcher = new FakeNetworkDispatcher();
            var settings = NetworkTestSettings.Create(timeout: 2f);
            var host = NetworkConnectionControllerHost.Install(
                root.transform,
                client,
                factory,
                settings,
                dispatcher,
                () => 2f);
            try
            {
                host.Initialize();
                client.Connect(settings.ServerUrl);
                Assert.That(factory.Created.Count, Is.EqualTo(1), "Initialize must bind the facade gateway");

                InvokeUpdate(host);
                Assert.That(factory.Created[0].CloseCalls.Single().Code, Is.EqualTo(1001),
                    "Update must tick the controller with the injected delta provider");

                var queued = factory.Created[0];
                queued.RaiseOpened();
                host.Shutdown();
                dispatcher.PumpAll();
                InvokeUpdate(host);
                Assert.That(host.State, Is.EqualTo(NetworkConnectionState.Disconnected));
                Assert.That(factory.Created.Count, Is.EqualTo(1),
                    "Shutdown must cancel the reconnect delay that timeout scheduled");

                client.Connect(settings.ServerUrl);
                Assert.That(factory.Created.Count, Is.EqualTo(1), "Shutdown must restore the no-op gateway");
            }
            finally
            {
                host.Shutdown();
                client.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void AuthenticationCommandsAdvanceAnOpenedConnectionToReady()
        {
            var root = new GameObject("host-authentication-test-root");
            var client = new NetworkClient();
            var factory = new FakeWebSocketTransportFactory();
            var dispatcher = new FakeNetworkDispatcher();
            var settings = NetworkTestSettings.Create();
            var host = NetworkConnectionControllerHost.Install(
                root.transform,
                client,
                factory,
                settings,
                dispatcher);
            try
            {
                host.Initialize();
                client.Connect(settings.ServerUrl);
                factory.LastTransport.RaiseOpened();
                dispatcher.PumpAll();

                Assert.That(host.State, Is.EqualTo(NetworkConnectionState.Connected));
                host.BeginAuthentication();
                Assert.That(host.State, Is.EqualTo(NetworkConnectionState.Authenticating));
                host.MarkReady();
                Assert.That(host.State, Is.EqualTo(NetworkConnectionState.Ready));
            }
            finally
            {
                host.Shutdown();
                client.Dispose();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(settings);
            }
        }

        private static void InvokeUpdate(NetworkConnectionControllerHost host)
        {
            typeof(NetworkConnectionControllerHost)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(host, null);
        }
    }
}
