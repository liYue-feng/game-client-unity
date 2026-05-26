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
        var client = NetworkClient.Instance;
        client.On<CombatResultResp>(MsgID.CombatResultResp, HandleCombatResultResp);
        client.On<GetEnemyConfigsResp>(MsgID.GetEnemyConfigsResp, HandleGetEnemyConfigsResp);
        client.On<GetDungeonConfigResp>(MsgID.GetDungeonConfigResp, HandleGetDungeonConfigResp);
        client.On<GetStyleConfigsResp>(MsgID.GetStyleConfigsResp, HandleGetStyleConfigsResp);
        client.On<UnlockStyleResp>(MsgID.UnlockStyleResp, HandleUnlockStyleResp);
        client.On<GetPlayerStatsResp>(MsgID.GetPlayerStatsResp, HandleGetPlayerStatsResp);
        client.On<UpdatePlayerStatsResp>(MsgID.UpdatePlayerStatsResp, HandleUpdatePlayerStatsResp);
    }

    // ========== 本地状态 ==========

    /// <summary>玩家战斗属性</summary>
    public GetPlayerStatsResp PlayerStats { get; private set; }

    /// <summary>敌人配置缓存</summary>
    public EnemyConfigItem[] EnemyConfigs { get; private set; }

    /// <summary>流派配置缓存</summary>
    public StyleConfigItem[] StyleConfigs { get; private set; }

    // ========== 事件 ==========

    public event System.Action<CombatResultResp> OnCombatResult;
    public event System.Action<GetEnemyConfigsResp> OnEnemyConfigsLoaded;
    public event System.Action<GetDungeonConfigResp> OnDungeonConfigLoaded;
    public event System.Action<GetStyleConfigsResp> OnStyleConfigsLoaded;
    public event System.Action<UnlockStyleResp> OnStyleUnlocked;
    public event System.Action<GetPlayerStatsResp> OnPlayerStatsLoaded;
    public event System.Action<string> OnError;

    // ========== 发送请求 ==========

    /// <summary>上报战斗结算</summary>
    public void ReportCombatResult(CombatResultReq req)
    {
        NetworkClient.Instance.Send(MsgID.CombatResultReq, req);
    }

    /// <summary>请求敌人配置</summary>
    public void RequestEnemyConfigs()
    {
        NetworkClient.Instance.Send(MsgID.GetEnemyConfigsReq, new GetEnemyConfigsReq());
    }

    /// <summary>请求地牢配置</summary>
    public void RequestDungeonConfig(int level)
    {
        NetworkClient.Instance.Send(MsgID.GetDungeonConfigReq, new GetDungeonConfigReq { level = level });
    }

    /// <summary>请求流派配置</summary>
    public void RequestStyleConfigs()
    {
        NetworkClient.Instance.Send(MsgID.GetStyleConfigsReq, new GetStyleConfigsReq());
    }

    /// <summary>请求解锁流派</summary>
    public void RequestUnlockStyle(int styleId)
    {
        NetworkClient.Instance.Send(MsgID.UnlockStyleReq, new UnlockStyleReq { style_id = styleId });
    }

    /// <summary>请求玩家战斗属性</summary>
    public void RequestPlayerStats()
    {
        NetworkClient.Instance.Send(MsgID.GetPlayerStatsReq, new GetPlayerStatsReq());
    }

    // ========== 响应处理 ==========

    private void HandleCombatResultResp(CombatResultResp resp)
    {
        OnCombatResult?.Invoke(resp);
    }

    private void HandleGetEnemyConfigsResp(GetEnemyConfigsResp resp)
    {
        EnemyConfigs = resp.configs;
        OnEnemyConfigsLoaded?.Invoke(resp);
    }

    private void HandleGetDungeonConfigResp(GetDungeonConfigResp resp)
    {
        OnDungeonConfigLoaded?.Invoke(resp);
    }

    private void HandleGetStyleConfigsResp(GetStyleConfigsResp resp)
    {
        StyleConfigs = resp.styles;
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
        if (!resp.success)
        {
            OnError?.Invoke("更新玩家属性失败");
        }
    }
}
