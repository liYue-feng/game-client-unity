using System;
using System.Linq;
using System.Reflection;
using Game.Core;
using Game.Network;
using Game.Online;
using Game.Tests.EditMode.Network.TestDoubles;
using Game.Tests.EditMode.Online.TestDoubles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Online
{
    public sealed class OnlineSessionHostTests
    {
        private const string ServerUrl = "ws://127.0.0.1:8080/ws";

        private GameObject _root;
        private NetworkClient _client;
        private FakeOnlineConnection _connection;
        private FakeLoginCodeProvider _provider;
        private OnlineSessionHost _host;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("online-session-host-test-root");
            _client = new NetworkClient();
            _connection = new FakeOnlineConnection();
            _provider = new FakeLoginCodeProvider();
            _host = InvokeInjectedHostInstall(
                _root.transform,
                _client,
                ServerUrl,
                _connection,
                _provider);
        }

        [TearDown]
        public void TearDown()
        {
            _host?.Shutdown();
            _client?.Dispose();
            Object.DestroyImmediate(_root);
            NetworkClient.ResetStaticState();
        }

        [Test]
        public void InstallParentsHostAndInitializeWiresExactlyOneCoordinatorWithoutConnecting()
        {
            Assert.That(_host.gameObject.name, Is.EqualTo("[OnlineSessionHost]"));
            Assert.That(_host.transform.parent, Is.SameAs(_root.transform));
            Assert.That(GetCoordinator(_host), Is.Not.Null);

            _host.Initialize();
            _host.Initialize();

            Assert.That(GetCoordinator(_host), Is.Not.Null);
            Assert.That(_connection.ConnectCalls, Is.Zero,
                "GameApplication owns the online start decision; Initialize only wires the session.");
            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Idle));
        }

        [Test]
        public void ShutdownIsIdempotentClearsInstanceDisconnectsAndMakesCallbacksInert()
        {
            var stateChanges = 0;
            _host.StateChanged += _ => stateChanges++;
            _host.Initialize();
            _host.StartSession();
            Assert.That(_connection.ConnectCalls, Is.EqualTo(1));

            _host.Shutdown();
            _host.Shutdown();
            var changesAfterShutdown = stateChanges;
            _connection.RaiseConnected();
            _connection.RaiseError("late error");

            Assert.That(OnlineSessionHost.Instance, Is.Null);
            Assert.That(_connection.DisconnectCalls, Is.EqualTo(1));
            Assert.That(_host.State, Is.EqualTo(OnlineSessionState.Stopped));
            Assert.That(stateChanges, Is.EqualTo(changesAfterShutdown));
        }

        [Test]
        public void OnlineGameServicesCreateInstallsHostUnderServiceRootAndShutdownRemovesIt()
        {
            _host.Shutdown();
            Object.DestroyImmediate(_host.gameObject);
            _host = null;

            var applicationRoot = new GameObject("online-game-services-root");
            var settings = CreateOnlineSettings();
            var transportFactory = new FakeWebSocketTransportFactory();
            var provider = new FakeLoginCodeProvider();
            object services = null;
            try
            {
                services = InvokeGameServicesCreate(applicationRoot.transform, settings, transportFactory, provider);
                var onlineSession = services.GetType()
                    .GetProperty("OnlineSession", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(services) as OnlineSessionHost;

                Assert.That(onlineSession, Is.Not.Null);
                Assert.That(onlineSession.transform.parent.gameObject.name, Is.EqualTo("[GameServices]"));
                Assert.That(FindObjectsNamed("[OnlineSessionHost]").Count, Is.EqualTo(1));
                Assert.That(transportFactory.Created, Is.Empty,
                    "Creating Online services must not open a socket before GameApplication starts a session.");

                InvokeShutdown(services);
                services = null;

                Assert.That(OnlineSessionHost.Instance, Is.Null);
                Assert.That(FindObjectsNamed("[OnlineSessionHost]"), Is.Empty);
            }
            finally
            {
                if (services != null)
                {
                    InvokeShutdown(services);
                }

                Object.DestroyImmediate(applicationRoot);
                Object.DestroyImmediate(settings);
            }
        }

        private static OnlineSessionCoordinator GetCoordinator(OnlineSessionHost host)
        {
            return typeof(OnlineSessionHost)
                .GetField("_coordinator", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(host) as OnlineSessionCoordinator;
        }

        private static OnlineSessionHost InvokeInjectedHostInstall(
            Transform parent,
            NetworkClient client,
            string serverUrl,
            IOnlineConnection connection,
            ILoginCodeProvider provider)
        {
            var install = typeof(OnlineSessionHost)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .SingleOrDefault(method =>
                {
                    if (method.Name != "Install")
                    {
                        return false;
                    }

                    var parameters = method.GetParameters();
                    return parameters.Length == 5 && parameters[3].ParameterType == typeof(IOnlineConnection);
                });

            Assert.That(install, Is.Not.Null,
                "OnlineSessionHost must expose an internal fake-connection composition seam.");
            return (OnlineSessionHost)install.Invoke(
                null,
                new object[] { parent, client, serverUrl, connection, provider });
        }

        private static GameRuntimeSettings CreateOnlineSettings()
        {
            var settings = ScriptableObject.CreateInstance<GameRuntimeSettings>();
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("_runtimeMode").enumValueIndex = (int)RuntimeMode.Online;
            serialized.FindProperty("_serverUrl").stringValue = ServerUrl;
            serialized.FindProperty("_editorLoginIdentity").stringValue = "editor-001";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return settings;
        }

        private static object InvokeGameServicesCreate(
            Transform applicationRoot,
            GameRuntimeSettings settings,
            IWebSocketTransportFactory transportFactory,
            ILoginCodeProvider provider)
        {
            var servicesType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Game.GameServices", false))
                .FirstOrDefault(type => type != null);
            var create = servicesType?.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .SingleOrDefault(method => method.Name == "Create" && method.GetParameters().Length == 4);

            Assert.That(create, Is.Not.Null,
                "GameServices.Create must expose transport and login provider injection for Online composition tests.");
            return create.Invoke(null, new object[] { applicationRoot, settings, transportFactory, provider });
        }

        private static void InvokeShutdown(object services)
        {
            services.GetType()
                .GetMethod("Shutdown", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(services, null);
        }

        private static System.Collections.Generic.List<GameObject> FindObjectsNamed(string name)
        {
            return Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(item => item.scene.IsValid() && item.name == name)
                .ToList();
        }
    }
}
