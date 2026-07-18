namespace Game.Network
{
    public interface IWebSocketTransportFactory
    {
        IWebSocketTransport Create(string url);
    }
}
