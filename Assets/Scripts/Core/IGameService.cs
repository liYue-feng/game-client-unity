namespace Game.Core
{
    public interface IGameService
    {
        string ServiceName { get; }
        void Initialize();
        void Shutdown();
    }
}
