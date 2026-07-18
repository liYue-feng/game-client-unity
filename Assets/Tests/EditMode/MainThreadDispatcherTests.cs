using System;
using System.Text.RegularExpressions;
using Game.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode
{
    public class MainThreadDispatcherTests
    {
        private GameObject _root;
        private MainThreadDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            MainThreadDispatcher.ResetStaticState();
            _root = new GameObject("[DispatcherTestRoot]");
            _dispatcher = MainThreadDispatcher.Install(_root.transform, 2);
            _dispatcher.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _dispatcher?.Shutdown();
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }

            MainThreadDispatcher.ResetStaticState();
        }

        [Test]
        public void ProcessPending_ExecutesOnlyConfiguredBudget()
        {
            var executions = 0;
            Assert.That(MainThreadDispatcher.Enqueue(() => executions++), Is.True);
            Assert.That(MainThreadDispatcher.Enqueue(() => executions++), Is.True);
            Assert.That(MainThreadDispatcher.Enqueue(() => executions++), Is.True);

            _dispatcher.ProcessPending();

            Assert.That(executions, Is.EqualTo(2));
            Assert.That(MainThreadDispatcher.PendingCount, Is.EqualTo(1));
        }

        [Test]
        public void ProcessPending_WhenActionThrows_ContinuesWithNextAction()
        {
            var executions = 0;
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: expected"));
            MainThreadDispatcher.Enqueue(() => throw new InvalidOperationException("expected"));
            MainThreadDispatcher.Enqueue(() => executions++);

            _dispatcher.ProcessPending();

            Assert.That(executions, Is.EqualTo(1));
            Assert.That(MainThreadDispatcher.PendingCount, Is.Zero);
        }

        [Test]
        public void Shutdown_ClearsQueueAndRejectsNewActions()
        {
            MainThreadDispatcher.Enqueue(() => { });

            _dispatcher.Shutdown();

            Assert.That(MainThreadDispatcher.PendingCount, Is.Zero);
            Assert.That(MainThreadDispatcher.Enqueue(() => { }), Is.False);
        }

        [Test]
        public void Instance_BeforeInstall_LogsErrorWithoutCreatingGameObject()
        {
            _dispatcher.Shutdown();
            UnityEngine.Object.DestroyImmediate(_root);
            _dispatcher = null;
            _root = null;
            MainThreadDispatcher.ResetStaticState();

            MainThreadDispatcher unexpectedInstance = null;
            try
            {
                LogAssert.Expect(LogType.Error, "[MainThreadDispatcher] Install must be called before Instance.");

                unexpectedInstance = MainThreadDispatcher.Instance;

                Assert.That(unexpectedInstance, Is.Null);
                Assert.That(GameObject.Find("[MainThreadDispatcher]"), Is.Null);
            }
            finally
            {
                if (unexpectedInstance != null)
                {
                    UnityEngine.Object.DestroyImmediate(unexpectedInstance.gameObject);
                }
            }
        }
    }
}
