using System;

namespace Game.Online
{
    public interface ILoginCodeProvider
    {
        void RequestCode(Action<string> succeeded, Action<string> failed);
    }
}
