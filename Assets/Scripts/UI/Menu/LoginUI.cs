using Game.Online;
using UnityEngine;

public sealed class LoginUI : MonoBehaviour
{
    public void Retry()
    {
        OnlineSessionHost.Instance?.Retry();
    }
}
