namespace Game.Network
{
    public readonly struct NetworkCloseInfo
    {
        public NetworkCloseInfo(ushort code, string reason)
        {
            Code = code;
            Reason = reason;
        }

        public ushort Code { get; }

        public string Reason { get; }
    }
}
