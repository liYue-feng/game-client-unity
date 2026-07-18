using System;
using System.Collections.Generic;

namespace Game.Core
{
    public sealed class GameServiceInitializationException : Exception
    {
        internal GameServiceInitializationException(
            string serviceName,
            Exception innerException,
            IReadOnlyList<Exception> rollbackErrors)
            : base($"Failed to initialize service '{serviceName}'.", innerException)
        {
            ServiceName = serviceName;
            RollbackErrors = rollbackErrors;
        }

        public string ServiceName { get; }
        public IReadOnlyList<Exception> RollbackErrors { get; }
    }

    public sealed class GameServiceCollection
    {
        private readonly List<IGameService> _services;
        private bool _initializationAttempted;
        private int _initializedServiceCount;

        public GameServiceCollection(IEnumerable<IGameService> services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            _services = new List<IGameService>();
            foreach (var service in services)
            {
                if (service == null)
                {
                    throw new ArgumentNullException(nameof(services), "Service collection cannot contain null entries.");
                }

                _services.Add(service);
            }
        }

        public bool IsInitialized { get; private set; }

        public void InitializeAll()
        {
            if (IsInitialized)
            {
                return;
            }

            if (_initializationAttempted)
            {
                throw new InvalidOperationException("Service initialization has already been attempted.");
            }

            _initializationAttempted = true;
            for (var index = 0; index < _services.Count; index++)
            {
                var service = _services[index];
                try
                {
                    service.Initialize();
                    _initializedServiceCount++;
                }
                catch (Exception exception)
                {
                    var rollbackErrors = ShutdownInitializedServices();
                    throw new GameServiceInitializationException(
                        service.ServiceName,
                        exception,
                        rollbackErrors);
                }
            }

            IsInitialized = true;
        }

        public IReadOnlyList<Exception> ShutdownAll()
        {
            if (!IsInitialized)
            {
                return Array.Empty<Exception>();
            }

            var errors = ShutdownInitializedServices();
            IsInitialized = false;
            return errors;
        }

        private IReadOnlyList<Exception> ShutdownInitializedServices()
        {
            var errors = new List<Exception>();
            for (var index = _initializedServiceCount - 1; index >= 0; index--)
            {
                try
                {
                    _services[index].Shutdown();
                }
                catch (Exception exception)
                {
                    errors.Add(exception);
                }
            }

            _initializedServiceCount = 0;
            return errors.AsReadOnly();
        }
    }
}
