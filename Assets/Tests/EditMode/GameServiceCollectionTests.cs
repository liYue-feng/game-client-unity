using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public sealed class GameServiceCollectionTests
    {
        [Test]
        public void ConstructorRejectsNullServiceSequence()
        {
            Assert.Throws<ArgumentNullException>(() => new GameServiceCollection(null));
        }

        [Test]
        public void ConstructorRejectsNullServiceEntry()
        {
            var services = new IGameService[]
            {
                new RecordingService("a", new List<string>()),
                null
            };

            Assert.Throws<ArgumentNullException>(() => new GameServiceCollection(services));
        }

        [Test]
        public void InitializeAndShutdownUseForwardThenReverseOrder()
        {
            var events = new List<string>();
            var collection = CreateCollection(events, "a", "b");

            collection.InitializeAll();
            var errors = collection.ShutdownAll();

            CollectionAssert.AreEqual(
                new[] { "init:a", "init:b", "shutdown:b", "shutdown:a" },
                events);
            Assert.That(errors, Is.Empty);
            Assert.That(collection.IsInitialized, Is.False);
        }

        [Test]
        public void SuccessfulInitializeIsANoOpWhenRepeated()
        {
            var events = new List<string>();
            var collection = CreateCollection(events, "a", "b");

            collection.InitializeAll();
            collection.InitializeAll();

            CollectionAssert.AreEqual(new[] { "init:a", "init:b" }, events);
            Assert.That(collection.IsInitialized, Is.True);
        }

        [Test]
        public void InitializationFailureRollsBackOnlySuccessfulPrefixInReverseOrder()
        {
            var events = new List<string>();
            var failure = new InvalidOperationException("cannot initialize c");
            var services = new IGameService[]
            {
                new RecordingService("a", events),
                new RecordingService("b", events),
                new RecordingService("c", events, initializeException: failure),
                new RecordingService("d", events)
            };
            var collection = new GameServiceCollection(services);

            var exception = Assert.Throws<GameServiceInitializationException>(() => collection.InitializeAll());

            CollectionAssert.AreEqual(
                new[] { "init:a", "init:b", "shutdown:b", "shutdown:a" },
                events);
            Assert.That(collection.IsInitialized, Is.False);
            Assert.That(exception.ServiceName, Is.EqualTo("c"));
            Assert.That(exception.InnerException, Is.SameAs(failure));
            Assert.That(exception.RollbackErrors, Is.Empty);
        }

        [Test]
        public void RollbackContinuesAndCollectsShutdownErrors()
        {
            var events = new List<string>();
            var initializeFailure = new InvalidOperationException("cannot initialize c");
            var rollbackFailure = new InvalidOperationException("cannot shut down b");
            var services = new IGameService[]
            {
                new RecordingService("a", events),
                new RecordingService("b", events, shutdownException: rollbackFailure),
                new RecordingService("c", events, initializeException: initializeFailure)
            };
            var collection = new GameServiceCollection(services);

            var exception = Assert.Throws<GameServiceInitializationException>(() => collection.InitializeAll());

            CollectionAssert.AreEqual(
                new[] { "init:a", "init:b", "shutdown:b", "shutdown:a" },
                events);
            Assert.That(exception.RollbackErrors, Has.Count.EqualTo(1));
            Assert.That(exception.RollbackErrors[0], Is.SameAs(rollbackFailure));
            Assert.That(exception.InnerException, Is.SameAs(initializeFailure));
        }

        [Test]
        public void FailedInitializationCannotBeRetried()
        {
            var events = new List<string>();
            var services = new IGameService[]
            {
                new RecordingService("failed", events, initializeException: new Exception("failed"))
            };
            var collection = new GameServiceCollection(services);
            Assert.Throws<GameServiceInitializationException>(() => collection.InitializeAll());

            Assert.Throws<InvalidOperationException>(() => collection.InitializeAll());
            Assert.That(events, Is.Empty);
        }

        [Test]
        public void ShutdownIsIdempotentAfterSuccessfulInitialization()
        {
            var events = new List<string>();
            var first = new RecordingService("a", events);
            var second = new RecordingService("b", events);
            var collection = new GameServiceCollection(new IGameService[] { first, second });
            collection.InitializeAll();

            var firstErrors = collection.ShutdownAll();
            var secondErrors = collection.ShutdownAll();

            Assert.That(first.ShutdownCount, Is.EqualTo(1));
            Assert.That(second.ShutdownCount, Is.EqualTo(1));
            Assert.That(firstErrors, Is.Empty);
            Assert.That(secondErrors, Is.Empty);
            Assert.That(collection.IsInitialized, Is.False);
        }

        [Test]
        public void ShutdownContinuesAndReturnsErrorsInServiceShutdownOrder()
        {
            var events = new List<string>();
            var firstFailure = new InvalidOperationException("first");
            var secondFailure = new InvalidOperationException("second");
            var services = new IGameService[]
            {
                new RecordingService("a", events, shutdownException: firstFailure),
                new RecordingService("b", events),
                new RecordingService("c", events, shutdownException: secondFailure)
            };
            var collection = new GameServiceCollection(services);
            collection.InitializeAll();

            var errors = collection.ShutdownAll();

            CollectionAssert.AreEqual(
                new[] { "init:a", "init:b", "init:c", "shutdown:c", "shutdown:b", "shutdown:a" },
                events);
            CollectionAssert.AreEqual(new[] { secondFailure, firstFailure }, errors);
            Assert.That(collection.IsInitialized, Is.False);
        }

        private static GameServiceCollection CreateCollection(List<string> events, params string[] names)
        {
            var services = new List<IGameService>();
            foreach (var name in names)
            {
                services.Add(new RecordingService(name, events));
            }

            return new GameServiceCollection(services);
        }

        private sealed class RecordingService : IGameService
        {
            private readonly List<string> _events;
            private readonly Exception _initializeException;
            private readonly Exception _shutdownException;

            public RecordingService(
                string serviceName,
                List<string> events,
                Exception initializeException = null,
                Exception shutdownException = null)
            {
                ServiceName = serviceName;
                _events = events;
                _initializeException = initializeException;
                _shutdownException = shutdownException;
            }

            public string ServiceName { get; }
            public int ShutdownCount { get; private set; }

            public void Initialize()
            {
                if (_initializeException != null)
                {
                    throw _initializeException;
                }

                _events.Add($"init:{ServiceName}");
            }

            public void Shutdown()
            {
                ShutdownCount++;
                _events.Add($"shutdown:{ServiceName}");
                if (_shutdownException != null)
                {
                    throw _shutdownException;
                }
            }
        }
    }
}
