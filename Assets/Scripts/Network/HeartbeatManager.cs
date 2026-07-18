using UnityEngine;
using System;
using Game.Network;

/// <summary>
/// 心跳保活管理器 — 监控NetworkClient连接状态
/// </summary>
[Obsolete("NetworkConnectionController owns connection policy.")]
public class HeartbeatManager : MonoBehaviour
{
    public static HeartbeatManager Instance { get; private set; }

    /// <summary> 网络状态变化事件 </summary>
    public event Action<NetworkStatus> OnStatusChanged;
    /// <summary> 断线事件 </summary>
    public event Action OnDisconnected;

    private NetworkStatus _status = NetworkStatus.Disconnected;

    public NetworkStatus Status => _status;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // 监听NetworkClient事件
        if (NetworkClient.Instance != null)
        {
            NetworkClient.Instance.OnConnected += OnConnected;
            NetworkClient.Instance.OnDisconnected += OnDisconnectedHandler;
        }
    }

    void OnDestroy()
    {
        if (NetworkClient.Instance != null)
        {
            NetworkClient.Instance.OnConnected -= OnConnected;
            NetworkClient.Instance.OnDisconnected -= OnDisconnectedHandler;
        }

        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }

    private void OnConnected()
    {
        SetStatus(NetworkStatus.Connected);
    }

    private void OnDisconnectedHandler()
    {
        SetStatus(NetworkStatus.Disconnected);
        OnDisconnected?.Invoke();
    }

    void SetStatus(NetworkStatus newStatus)
    {
        if (_status != newStatus)
        {
            _status = newStatus;
            OnStatusChanged?.Invoke(_status);
            Debug.Log($"[Heartbeat] 网络状态: {_status}");
        }
    }

    // 这些方法保持空实现以兼容现有代码
    [Obsolete("NetworkConnectionController owns connection policy.")]
    public void StartHeartbeat(NetworkClient client) { }

    [Obsolete("NetworkConnectionController owns connection policy.")]
    public void StopHeartbeat() { }
}

public enum NetworkStatus
{
    Connected,
    Unstable,
    Reconnecting,
    Disconnected
}
