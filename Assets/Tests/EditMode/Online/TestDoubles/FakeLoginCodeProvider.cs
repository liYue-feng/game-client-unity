using System;
using System.Collections.Generic;
using Game.Online;

namespace Game.Tests.EditMode.Online.TestDoubles
{
    public sealed class FakeLoginCodeProvider : ILoginCodeProvider
    {
        private readonly List<Request> _requests = new List<Request>();

        public int RequestCount => _requests.Count;

        public void RequestCode(Action<string> succeeded, Action<string> failed)
        {
            _requests.Add(new Request(succeeded, failed));
        }

        public void Succeed(int index, string code = "dev:editor-001")
        {
            _requests[index].Succeeded(code);
        }

        public void Fail(int index, string reason)
        {
            _requests[index].Failed(reason);
        }

        private sealed class Request
        {
            public Request(Action<string> succeeded, Action<string> failed)
            {
                Succeeded = succeeded;
                Failed = failed;
            }

            public Action<string> Succeeded { get; }
            public Action<string> Failed { get; }
        }
    }
}
