using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Network;
using Game.Protocol;

/// <summary>
/// 战斗管理器：协调战斗相关的网络消息和本地状态。
/// 遵循项目统一的 Manager 单例模式：懒创建 + DontDestroyOnLoad + Awake防重复。
///
/// 职责：
/// - 上报战斗结算（CombatResult）
/// - 获取敌人/地牢/流派配置
/// - 管理玩家战斗属性
///
/// 不负责实时战斗逻辑（由 PlayerStateMachine 等组件处理）。
/// </summary>
public class CombatManager : MonoBehaviour
{
    private static CombatManager _instance;
    private readonly HashSet<uint> _pendingRequests = new HashSet<uint>();
    private bool _destroyed;
    public static CombatManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[CombatManager]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CombatManager>();
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 注册网络消息监听
    }

    private void OnDestroy()
    {
        _destroyed = true;
        foreach (var seq in new List<uint>(_pendingRequests))
        {
            NetworkClient.Instance.CancelRequest(seq);
        }

        _pendingRequests.Clear();
        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
        }
    }

    // ========== 本地状态 ==========

    /// <summary>玩家战斗属性</summary>
    public GetPlayerStatsResp PlayerStats { get; private set; }

    /// <summary>敌人配置缓存</summary>
    public EnemyConfigItem[] EnemyConfigs { get; private set; }

    /// <summary>流派配置缓存</summary>
    public StyleConfigItem[] StyleConfigs { get; private set; }

    // ========== 事件 ==========

    public event System.Action<GetEnemyConfigsResp> OnEnemyConfigsLoaded;
    public event System.Action<GetDungeonConfigResp> OnDungeonConfigLoaded;
    public event System.Action<GetStyleConfigsResp> OnStyleConfigsLoaded;
    public event System.Action<UnlockStyleResp> OnStyleUnlocked;
    public event System.Action<GetPlayerStatsResp> OnPlayerStatsLoaded;
    public event System.Action<string> OnError;

    // ========== 发送请求 ==========

    /// <summary>请求敌人配置</summary>
    public void RequestEnemyConfigs()
    {
        Request<GetEnemyConfigsReq, GetEnemyConfigsResp>(
            MsgID.GetEnemyConfigsReq, MsgID.GetEnemyConfigsResp, new GetEnemyConfigsReq(),
            HandleGetEnemyConfigsResp);
    }

    /// <summary>请求地牢配置</summary>
    public void RequestDungeonConfig(int level)
    {
        Request<GetDungeonConfigReq, GetDungeonConfigResp>(
            MsgID.GetDungeonConfigReq, MsgID.GetDungeonConfigResp,
            new GetDungeonConfigReq { Level = level }, HandleGetDungeonConfigResp);
    }

    /// <summary>请求流派配置</summary>
    public void RequestStyleConfigs()
    {
        Request<GetStyleConfigsReq, GetStyleConfigsResp>(
            MsgID.GetStyleConfigsReq, MsgID.GetStyleConfigsResp, new GetStyleConfigsReq(),
            HandleGetStyleConfigsResp);
    }

    /// <summary>请求解锁流派</summary>
    public void RequestUnlockStyle(int styleId)
    {
        Request<UnlockStyleReq, UnlockStyleResp>(
            MsgID.UnlockStyleReq, MsgID.UnlockStyleResp,
            new UnlockStyleReq { StyleId = styleId }, HandleUnlockStyleResp);
    }

    /// <summary>请求玩家战斗属性</summary>
    public void RequestPlayerStats()
    {
        Request<GetPlayerStatsReq, GetPlayerStatsResp>(
            MsgID.GetPlayerStatsReq, MsgID.GetPlayerStatsResp, new GetPlayerStatsReq(),
            HandleGetPlayerStatsResp);
    }

    public void UpdatePlayerStats(PlayerStatsData stats)
    {
        if (stats == null)
        {
            OnError?.Invoke("Player stats are required.");
            return;
        }

        var request = new UpdatePlayerStatsReq
        {
            Level = stats.Level,
            Exp = stats.Exp,
            Gold = stats.Gold,
            MaxHp = stats.MaxHp,
            MaxStamina = stats.MaxStamina,
            AttackPower = stats.AttackPower
        };
        request.UnlockedStyles.Add(stats.UnlockedStyles);
        Request<UpdatePlayerStatsReq, UpdatePlayerStatsResp>(
            MsgID.UpdatePlayerStatsReq, MsgID.UpdatePlayerStatsResp, request,
            HandleUpdatePlayerStatsResp);
    }

    // ========== 响应处理 ==========

    private void HandleGetEnemyConfigsResp(GetEnemyConfigsResp resp)
    {
        EnemyConfigs = resp.Configs.ToArray();
        OnEnemyConfigsLoaded?.Invoke(resp);
    }

    private void HandleGetDungeonConfigResp(GetDungeonConfigResp resp)
    {
        OnDungeonConfigLoaded?.Invoke(resp);
    }

    private void HandleGetStyleConfigsResp(GetStyleConfigsResp resp)
    {
        StyleConfigs = resp.Styles.ToArray();
        OnStyleConfigsLoaded?.Invoke(resp);
    }

    private void HandleUnlockStyleResp(UnlockStyleResp resp)
    {
        OnStyleUnlocked?.Invoke(resp);
    }

    private void HandleGetPlayerStatsResp(GetPlayerStatsResp resp)
    {
        PlayerStats = resp;
        OnPlayerStatsLoaded?.Invoke(resp);
    }

    private void HandleUpdatePlayerStatsResp(UpdatePlayerStatsResp resp)
    {
        if (!resp.Success)
        {
            OnError?.Invoke("更新玩家属性失败");
        }
    }

    private bool Request<TRequest, TResponse>(
        ushort requestId,
        ushort responseId,
        TRequest payload,
        Action<TResponse> onSuccess)
        where TRequest : class, Google.Protobuf.IMessage<TRequest>
        where TResponse : class, Google.Protobuf.IMessage<TResponse>
    {
        var completed = false;
        uint seq = 0;
        var sent = NetworkClient.Instance.Request<TRequest, TResponse>(
            requestId,
            responseId,
            payload,
            response =>
            {
                completed = true;
                _pendingRequests.Remove(seq);
                if (!_destroyed)
                {
                    onSuccess?.Invoke(response);
                }
            },
            reason =>
            {
                completed = true;
                _pendingRequests.Remove(seq);
                if (!_destroyed)
                {
                    OnError?.Invoke(reason);
                }
            },
            out seq);
        if (sent && !completed)
        {
            _pendingRequests.Add(seq);
        }

        return sent;
    }
}
