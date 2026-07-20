using UnityEngine;
using System;
using Game.Network;

/// <summary>
/// 断线重连管理器 — 指数退避重试，最多 N 次
/// </summary>
[Obsolete("NetworkConnectionController owns connection policy.")]
public class ReconnectionManager : MonoBehaviour
{
    public static ReconnectionManager Instance { get; private set; }

    [Header("重连配置")]
    public int maxReconnectAttempts = 5;
    public float baseDelaySeconds = 2f;
    public float maxDelaySeconds = 30f;

    /// <summary> 重连状态变化 </summary>
    // Public legacy events are retained for source compatibility but intentionally inert under A3 ownership.
#pragma warning disable CS0067
    public event Action<ReconnectState> OnReconnectStateChanged;
    /// <summary> 重连成功 </summary>
    public event Action OnReconnected;
    /// <summary> 重连失败（超过最大次数） </summary>
    public event Action OnReconnectFailed;
#pragma warning restore CS0067

    public ReconnectState State => NetworkStatusAdapter.ToReconnectState(NetworkClient.Instance.ConnectionState);
    public int AttemptCount => 0;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 注册重连所需信息
    /// </summary>
    public void Register(string serverUrl, Action<string> connectAction)
    {
    }

    /// <summary> 开始重连流程 </summary>
    public void StartReconnect()
    {
    }

    /// <summary> 停止重连 </summary>
    public void StopReconnect()
    {
    }

    void OnDestroy()
    {
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }
}

public enum ReconnectState
{
    Idle,
    Reconnecting,
    Connected,
    Failed
}
