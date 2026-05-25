using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// 心跳保活管理器 — 定时发送Ping，超时判定断线
/// </summary>
public class HeartbeatManager : MonoBehaviour
{
    public static HeartbeatManager Instance { get; private set; }

    [Header("心跳配置")]
    public float pingInterval = 10f;       // 心跳间隔
    public float pingTimeout = 5f;         // 单次超时
    public int maxRetries = 3;             // 最大重试次数

    /// <summary> 网络状态变化事件 </summary>
    public event Action<NetworkStatus> OnStatusChanged;
    /// <summary> 断线事件 </summary>
    public event Action OnDisconnected;
    /// <summary> RTT 变化（毫秒） </summary>
    public event Action<long> OnRTTUpdated;

    private NetworkClient _client;
    private Coroutine _heartbeatRoutine;
    private int _retryCount;
    private NetworkStatus _status = NetworkStatus.Disconnected;
    private long _lastRTT;

    public NetworkStatus Status => _status;
    public long LastRTT => _lastRTT;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary> 开始心跳（连接成功后调用） </summary>
    public void StartHeartbeat(NetworkClient client)
    {
        _client = client;
        _retryCount = 0;
        _heartbeatRoutine = StartCoroutine(HeartbeatLoop());
    }

    /// <summary> 停止心跳（断开时调用） </summary>
    public void StopHeartbeat()
    {
        if (_heartbeatRoutine != null)
        {
            StopCoroutine(_heartbeatRoutine);
            _heartbeatRoutine = null;
        }
    }

    IEnumerator HeartbeatLoop()
    {
        while (_client != null && _client.IsConnected)
        {
            yield return new WaitForSeconds(pingInterval);
            yield return StartCoroutine(SendPing());
        }
    }

    IEnumerator SendPing()
    {
        var startTime = DateTime.UtcNow;
        var received = false;

        void OnPong()
        {
            received = true;
            _lastRTT = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
            OnRTTUpdated?.Invoke(_lastRTT);
        }

        // 注册Pong回调
        _client.OnPongReceived += OnPong;

        // 发送Ping
        _client.SendPing();

        // 等待响应
        var elapsed = 0f;
        while (!received && elapsed < pingTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        _client.OnPongReceived -= OnPong;

        if (received)
        {
            _retryCount = 0;
            SetStatus(NetworkStatus.Connected);
        }
        else
        {
            _retryCount++;
            if (_retryCount >= maxRetries)
            {
                SetStatus(NetworkStatus.Disconnected);
                OnDisconnected?.Invoke();
                StopHeartbeat();
            }
            else
            {
                SetStatus(NetworkStatus.Unstable);
            }
        }
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

    void OnDestroy()
    {
        StopHeartbeat();
    }
}

public enum NetworkStatus
{
    Connected,
    Unstable,
    Reconnecting,
    Disconnected
}