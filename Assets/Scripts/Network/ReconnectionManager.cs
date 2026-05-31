using UnityEngine;
using System;
using System.Collections;
using Game.Network;

/// <summary>
/// 断线重连管理器 — 指数退避重试，最多 N 次
/// </summary>
public class ReconnectionManager : MonoBehaviour
{
    public static ReconnectionManager Instance { get; private set; }

    [Header("重连配置")]
    public int maxReconnectAttempts = 5;
    public float baseDelaySeconds = 2f;
    public float maxDelaySeconds = 30f;

    /// <summary> 重连状态变化 </summary>
    public event Action<ReconnectState> OnReconnectStateChanged;
    /// <summary> 重连成功 </summary>
    public event Action OnReconnected;
    /// <summary> 重连失败（超过最大次数） </summary>
    public event Action OnReconnectFailed;

    private int _attemptCount;
    private Coroutine _reconnectRoutine;
    private string _lastUrl;
    private Action<string> _connectAction;

    public ReconnectState State { get; private set; } = ReconnectState.Idle;
    public int AttemptCount => _attemptCount;

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
        _lastUrl = serverUrl;
        _connectAction = connectAction;
    }

    /// <summary> 开始重连流程 </summary>
    public void StartReconnect()
    {
        if (_reconnectRoutine != null) return;
        if (string.IsNullOrEmpty(_lastUrl)) return;

        _attemptCount = 0;
        _reconnectRoutine = StartCoroutine(ReconnectLoop());
    }

    /// <summary> 停止重连 </summary>
    public void StopReconnect()
    {
        if (_reconnectRoutine != null)
        {
            StopCoroutine(_reconnectRoutine);
            _reconnectRoutine = null;
        }
        SetState(ReconnectState.Idle);
    }

    IEnumerator ReconnectLoop()
    {
        SetState(ReconnectState.Reconnecting);

        while (_attemptCount < maxReconnectAttempts)
        {
            _attemptCount++;

            // 指数退避: 2s → 4s → 8s → 16s → 30s
            var delay = Mathf.Min(baseDelaySeconds * Mathf.Pow(2, _attemptCount - 1), maxDelaySeconds);
            Debug.Log($"[Reconnect] 第 {_attemptCount}/{maxReconnectAttempts} 次重连，等待 {delay:F1}s");

            yield return new WaitForSeconds(delay);

            try
            {
                _connectAction?.Invoke(_lastUrl);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Reconnect] 连接失败: {e.Message}");
            }

            // 连接成功由 HeartbeatManager 判定
            yield return new WaitForSeconds(0.5f);

            if (HeartbeatManager.Instance != null && HeartbeatManager.Instance.Status == NetworkStatus.Connected)
            {
                SetState(ReconnectState.Connected);
                OnReconnected?.Invoke();
                yield break;
            }
        }

        // 全部失败
        SetState(ReconnectState.Failed);
        OnReconnectFailed?.Invoke();
    }

    void SetState(ReconnectState state)
    {
        State = state;
        OnReconnectStateChanged?.Invoke(state);
    }

    void OnDestroy()
    {
        StopReconnect();
    }
}

public enum ReconnectState
{
    Idle,
    Reconnecting,
    Connected,
    Failed
}