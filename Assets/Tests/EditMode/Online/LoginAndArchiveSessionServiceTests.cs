using System;
using System.Linq;
using System.Text.RegularExpressions;
using Game.Network;
using Game.Online;
using Game.Protocol;
using Game.Tests.EditMode.Network.TestDoubles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Online
{
    public sealed class LoginAndArchiveSessionServiceTests
    {
        private NetworkClient _client;
        private FakeWebSocketTransport _transport;

        [SetUp]
        public void SetUp()
        {
            _client = new NetworkClient();
            _transport = new FakeWebSocketTransport();
            _client.SetTransport(_transport);
            NetworkClient.RegisterInstance(_client);
        }

        [TearDown]
        public void TearDown()
        {
            NetworkClient.ResetStaticState();
        }

        [Test]
        public void EditorProviderPrefixesConfiguredIdentityExactlyOnceAndRejectsBlankIdentity()
        {
            var provider = new EditorLoginCodeProvider("editor-001");
            var callbackCount = 0;
            string code = null;
            provider.RequestCode(value =>
            {
                callbackCount++;
                code = value;
            }, _ => Assert.Fail("configured identity must not fail"));

            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(code, Is.EqualTo("dev:editor-001"));

            var prefixedProvider = new EditorLoginCodeProvider("dev:editor-001");
            prefixedProvider.RequestCode(value => code = value, _ => Assert.Fail("prefixed identity must not fail"));
            Assert.That(code, Is.EqualTo("dev:editor-001"));

            var whitespacePrefixedProvider = new EditorLoginCodeProvider(" dev:editor-001 ");
            whitespacePrefixedProvider.RequestCode(value => code = value,
                _ => Assert.Fail("trimmed prefixed identity must not fail"));
            Assert.That(code, Is.EqualTo("dev:editor-001"));

            var blankProvider = new EditorLoginCodeProvider(" ");
            string error = null;
            blankProvider.RequestCode(_ => Assert.Fail("blank identity must not succeed"), value => error = value);
            Assert.That(error, Is.EqualTo("Editor login identity is required."));
        }

        [Test]
        public void DisconnectedOperationsFailWithStableError()
        {
            using (var login = new LoginSessionService())
            using (var archive = new ArchiveSessionService())
            {
                string loginError = null;
                string archiveError = null;
                login.Failed += value => loginError = value;
                archive.Failed += value => archiveError = value;

                LogAssert.Expect(LogType.Warning, new Regex("msgId=1001"));
                Assert.That(login.Begin("dev:editor-001"), Is.False);
                Assert.That(loginError, Is.EqualTo("Network client is not connected."));

                LogAssert.Expect(LogType.Warning, new Regex("msgId=2003"));
                Assert.That(archive.Load(), Is.False);
                Assert.That(archiveError, Is.EqualTo("Network client is not connected."));

                LogAssert.Expect(LogType.Warning, new Regex("msgId=2001"));
                Assert.That(archive.Save("{}"), Is.False);
                Assert.That(archiveError, Is.EqualTo("Network client is not connected."));
            }
        }

        [Test]
        public void BeginSendsExactCodeAndLoginResponseStoresSessionBeforeSuccess()
        {
            _transport.RaiseOpened();
            using (var service = new LoginSessionService())
            {
                LoginResp succeeded = null;
                var count = 0;
                service.Succeeded += response =>
                {
                    count++;
                    Assert.That(_client.UID, Is.EqualTo(response.uid));
                    Assert.That(_client.Token, Is.EqualTo(response.token));
                    succeeded = response;
                };

                Assert.That(service.Begin("dev:editor-001"), Is.True);
                Assert.That(Codec.TryDecode(_transport.SentPayloads.Single(), out var sentId, out var body), Is.True);
                Assert.That(sentId, Is.EqualTo(MsgID.LoginReq));
                Assert.That(JsonUtility.FromJson<LoginReq>(body).code, Is.EqualTo("dev:editor-001"));

                _client.ReceiveFrame(Codec.Encode(MsgID.LoginResp,
                    new LoginResp { uid = 42, token = "session-token" }));

                Assert.That(count, Is.EqualTo(1));
                Assert.That(succeeded.uid, Is.EqualTo(42));
                Assert.That(_client.UID, Is.EqualTo(42));
                Assert.That(_client.Token, Is.EqualTo("session-token"));
            }
        }

        [Test]
        public void LoadUpdatesCurrentDataAndEmitsExactServerJson()
        {
            _transport.RaiseOpened();
            using (var service = new ArchiveSessionService())
            {
                string loaded = null;
                service.Loaded += value => loaded = value;

                Assert.That(service.Load(), Is.True);
                Assert.That(Codec.TryDecode(_transport.SentPayloads.Single(), out var sentId, out _), Is.True);
                Assert.That(sentId, Is.EqualTo(MsgID.LoadArchiveReq));

                const string data = "{\"gold\":9}";
                _client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp, new LoadArchiveResp { data = data }));

                Assert.That(service.CurrentData, Is.EqualTo(data));
                Assert.That(loaded, Is.EqualTo(data));
            }
        }

        [Test]
        public void SaveEmitsOnlyAfterSuccessfulResponse()
        {
            _transport.RaiseOpened();
            using (var service = new ArchiveSessionService())
            {
                var savedCount = 0;
                service.Saved += () => savedCount++;

                Assert.That(service.Save("{\"gold\":9}"), Is.True);
                Assert.That(Codec.TryDecode(_transport.SentPayloads.Single(), out var sentId, out var body), Is.True);
                Assert.That(sentId, Is.EqualTo(MsgID.SaveArchiveReq));
                Assert.That(JsonUtility.FromJson<SaveArchiveReq>(body).data, Is.EqualTo("{\"gold\":9}"));

                _client.ReceiveFrame(Codec.Encode(MsgID.SaveArchiveResp, new SaveArchiveResp { success = false }));
                Assert.That(savedCount, Is.Zero);

                Assert.That(service.Save("{\"gold\":10}"), Is.True);
                _client.ReceiveFrame(Codec.Encode(MsgID.SaveArchiveResp, new SaveArchiveResp { success = true }));
                Assert.That(savedCount, Is.EqualTo(1));
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void SaveRejectsBlankDataBeforeSendingOrStartingAnOperation(string data)
        {
            _transport.RaiseOpened();
            using (var service = new ArchiveSessionService())
            {
                string error = null;
                var savedCount = 0;
                service.Failed += value => error = value;
                service.Saved += () => savedCount++;

                Assert.That(service.Save(data), Is.False);
                Assert.That(error, Is.EqualTo("Archive data is required."));
                Assert.That(_transport.SentPayloads, Is.Empty);

                const string validData = "{\"gold\":11}";
                Assert.That(service.Save(validData), Is.True, "invalid save data must not occupy the active operation slot");
                Assert.That(_transport.SentPayloads, Has.Count.EqualTo(1));
                Assert.That(Codec.TryDecode(_transport.SentPayloads.Single(), out var sentId, out var body), Is.True);
                Assert.That(sentId, Is.EqualTo(MsgID.SaveArchiveReq));
                Assert.That(JsonUtility.FromJson<SaveArchiveReq>(body).data, Is.EqualTo(validData));

                _client.ReceiveFrame(Codec.Encode(MsgID.SaveArchiveResp, new SaveArchiveResp { success = true }));
                Assert.That(savedCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void ErrorResponseRoutesToTheActiveOperation()
        {
            _transport.RaiseOpened();
            using (var login = new LoginSessionService())
            using (var archive = new ArchiveSessionService())
            {
                string loginError = null;
                string archiveError = null;
                login.Failed += value => loginError = value;
                archive.Failed += value => archiveError = value;

                Assert.That(login.Begin("dev:editor-001"), Is.True);
                _client.ReceiveFrame(Codec.Encode(MsgID.Error, new ErrorResp { code = 9999, msg = "login failed" }));
                Assert.That(loginError, Is.EqualTo("[9999] login failed"));

                Assert.That(archive.Load(), Is.True);
                _client.ReceiveFrame(Codec.Encode(MsgID.Error, new ErrorResp { code = 9999, msg = "archive failed" }));
                Assert.That(archiveError, Is.EqualTo("[9999] archive failed"));
            }
        }

        [Test]
        public void DisposeMakesLaterProtocolFramesInert()
        {
            _transport.RaiseOpened();
            var login = new LoginSessionService();
            var archive = new ArchiveSessionService();
            var loginSuccesses = 0;
            var archiveLoads = 0;
            login.Succeeded += _ => loginSuccesses++;
            archive.Loaded += _ => archiveLoads++;

            Assert.That(login.Begin("dev:editor-001"), Is.True);
            Assert.That(archive.Load(), Is.True);
            login.Dispose();
            archive.Dispose();

            _client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, new LoginResp { uid = 42, token = "session-token" }));
            _client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp, new LoadArchiveResp { data = "{}" }));

            Assert.That(loginSuccesses, Is.Zero);
            Assert.That(archiveLoads, Is.Zero);
            Assert.That(_client.IsLoggedIn, Is.False);
        }
    }
}
