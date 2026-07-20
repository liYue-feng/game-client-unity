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
        public void TearDown() => NetworkClient.ResetStaticState();

        [Test]
        public void EditorProviderPrefixesConfiguredIdentityExactlyOnceAndRejectsBlankIdentity()
        {
            var provider = new EditorLoginCodeProvider("editor-001");
            string code = null;
            provider.RequestCode(value => code = value, _ => Assert.Fail("configured identity must not fail"));
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
                LogAssert.Expect(LogType.Warning, new Regex("msgId=2001"));
                Assert.That(archive.Save(new PlayerArchive()), Is.False);
                Assert.That(archiveError, Is.EqualTo("Network client is not connected."));
            }
        }

        [Test]
        public void BeginSendsGeneratedLoginAndStoresSessionBeforeSuccess()
        {
            _transport.RaiseOpened();
            using (var service = new LoginSessionService())
            {
                LoginResp succeeded = null;
                service.Succeeded += response => succeeded = response;
                Assert.That(service.Begin("dev:editor-001"), Is.True);
                Assert.That(Codec.TryDecode(_transport.SentPayloads.Single(), out var sentId, out var body), Is.True);
                Assert.That(sentId, Is.EqualTo(MsgID.LoginReq));
                Assert.That(LoginReq.Parser.ParseFrom(body).Code, Is.EqualTo("dev:editor-001"));
                _client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, new LoginResp { Uid = 42, Token = "session-token" }));
                Assert.That(succeeded.Uid, Is.EqualTo(42));
                Assert.That(_client.Token, Is.EqualTo("session-token"));
            }
        }

        [Test]
        public void LoadUsesGeneratedArchiveAndMissingArchiveDefaultsEmpty()
        {
            _transport.RaiseOpened();
            using (var service = new ArchiveSessionService())
            {
                Assert.That(service.CurrentArchive, Is.Not.Null);
                Assert.That(service.CurrentArchive.Gold, Is.Zero);
                PlayerArchive loaded = null;
                service.Loaded += value => loaded = value;
                Assert.That(service.Load(), Is.True);
                _client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp, new LoadArchiveResp { Found = false }));
                Assert.That(service.CurrentArchive, Is.Not.Null);
                Assert.That(loaded.Gold, Is.Zero);
            }
        }

        [Test]
        public void LoadAndSaveDetachArchiveMessagesFromCallerMutation()
        {
            _transport.RaiseOpened();
            using (var service = new ArchiveSessionService())
            {
                var incoming = new PlayerArchive { Gold = 7, UnlockedStyles = { 1, 3 } };
                Assert.That(service.Load(), Is.True);
                _client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp,
                    new LoadArchiveResp { Found = true, Archive = incoming }));
                incoming.Gold = 99;
                incoming.UnlockedStyles[0] = 99;
                Assert.That(service.CurrentArchive.Gold, Is.EqualTo(7));
                Assert.That(service.CurrentArchive.UnlockedStyles, Is.EqualTo(new[] { 1, 3 }));
                var exposedCurrent = service.CurrentArchive;
                exposedCurrent.Gold = 55;
                exposedCurrent.UnlockedStyles[0] = 55;
                Assert.That(service.CurrentArchive.Gold, Is.EqualTo(7));
                Assert.That(service.CurrentArchive.UnlockedStyles, Is.EqualTo(new[] { 1, 3 }));

                var outgoing = new PlayerArchive { Gold = 11, UnlockedStyles = { 2, 4 } };
                Assert.That(service.Save(outgoing), Is.True);
                outgoing.Gold = 99;
                outgoing.UnlockedStyles[0] = 99;
                Assert.That(Codec.TryDecode(_transport.SentPayloads.Last(), out _, out var body), Is.True);
                var sent = SaveArchiveReq.Parser.ParseFrom(body).Archive;
                Assert.That(sent.Gold, Is.EqualTo(11));
                Assert.That(sent.UnlockedStyles, Is.EqualTo(new[] { 2, 4 }));
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
                Assert.That(service.Save(new PlayerArchive { Gold = 9 }), Is.True);
                Assert.That(Codec.TryDecode(_transport.SentPayloads.Single(), out var sentId, out var body), Is.True);
                Assert.That(sentId, Is.EqualTo(MsgID.SaveArchiveReq));
                Assert.That(SaveArchiveReq.Parser.ParseFrom(body).Archive.Gold, Is.EqualTo(9));
                _client.ReceiveFrame(Codec.Encode(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = false }));
                Assert.That(savedCount, Is.Zero);
                Assert.That(service.Save(new PlayerArchive { Gold = 10 }), Is.True);
                _client.ReceiveFrame(Codec.Encode(MsgID.SaveArchiveResp, new SaveArchiveResp { Success = true }));
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
                _client.ReceiveFrame(Codec.Encode(MsgID.Error, new ErrorResp { Code = 9999, Msg = "login failed" }));
                Assert.That(loginError, Is.EqualTo("[9999] login failed"));
                Assert.That(archive.Load(), Is.True);
                _client.ReceiveFrame(Codec.Encode(MsgID.Error, new ErrorResp { Code = 9999, Msg = "archive failed" }));
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
            _client.ReceiveFrame(Codec.Encode(MsgID.LoginResp, new LoginResp { Uid = 42, Token = "session-token" }));
            _client.ReceiveFrame(Codec.Encode(MsgID.LoadArchiveResp, new LoadArchiveResp { Found = true, Archive = new PlayerArchive() }));
            Assert.That(loginSuccesses, Is.Zero);
            Assert.That(archiveLoads, Is.Zero);
            Assert.That(_client.IsLoggedIn, Is.False);
        }
    }
}
