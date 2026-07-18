using Game.Network;
using NUnit.Framework;

namespace Game.Tests.EditMode.Network
{
    public sealed class WebSocketTransportContractTests
    {
        [Test]
        public void CloseInfoStoresCodeAndReason()
        {
            var close = new NetworkCloseInfo(1000, "normal");
            Assert.AreEqual(1000, close.Code);
            Assert.AreEqual("normal", close.Reason);
        }

        [Test]
        public void FactoryCreatesTransportWithoutConnecting()
        {
            using (var transport = new WebSocketTransportFactory().Create("ws://localhost:8080/ws"))
            {
                Assert.IsFalse(transport.IsAlive);
            }
        }
    }
}
