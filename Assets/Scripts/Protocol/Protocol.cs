// Protocol.cs — 消息ID和错误码定义
//
// 此文件与服务器端 protocol/message.go + protocol/common.go 一一对应。
// 修改消息ID时必须同步修改服务器代码，否则通信会失败。
//
// 消息ID分配规则：
//   1xxx  登录模块
//   2xxx  游戏存档模块
//   3xxx  排行榜模块
//   5xxx  支付模块
//   6xxx  GM指令模块
//   9xxx  系统消息

namespace Game.Protocol
{
    /// <summary>
    /// 消息ID常量，与服务器 protocol/message.go 中的 MsgID_xxx 完全对应。
    /// 修改时务必同步服务器代码。
    /// </summary>
    public static class MsgID
    {
        public const int CanonicalRouteCount = 32;

        // ---- 登录模块 ----
        public const ushort LoginReq      = 1001; // 登录请求
        public const ushort LoginResp     = 1002; // 登录响应
        public const ushort HeartbeatReq  = 1003; // 心跳请求
        public const ushort HeartbeatResp = 1004; // 心跳响应

        // ---- 游戏存档模块 ----
        public const ushort SaveArchiveReq  = 2001; // 保存存档请求
        public const ushort SaveArchiveResp = 2002; // 保存存档响应
        public const ushort LoadArchiveReq  = 2003; // 加载存档请求
        public const ushort LoadArchiveResp = 2004; // 加载存档响应

        // ---- 排行榜模块 ----
        public const ushort GetRankReq      = 3001; // 获取排行榜请求
        public const ushort GetRankResp     = 3002; // 获取排行榜响应
        public const ushort SubmitScoreReq  = 3003; // 提交分数请求
        public const ushort SubmitScoreResp = 3004; // 提交分数响应

        // ---- 战斗模块 ----
        public const ushort CombatResultReq      = 4001; // 战斗结算请求
        public const ushort CombatResultResp     = 4002; // 战斗结算响应
        public const ushort GetEnemyConfigsReq   = 4003; // 获取敌人配置请求
        public const ushort GetEnemyConfigsResp  = 4004; // 获取敌人配置响应
        public const ushort GetDungeonConfigReq  = 4005; // 获取地牢配置请求
        public const ushort GetDungeonConfigResp = 4006; // 获取地牢配置响应
        public const ushort GetStyleConfigsReq   = 4007; // 获取流派配置请求
        public const ushort GetStyleConfigsResp  = 4008; // 获取流派配置响应
        public const ushort UnlockStyleReq       = 4009; // 解锁流派请求
        public const ushort UnlockStyleResp      = 4010; // 解锁流派响应
        public const ushort GetPlayerStatsReq    = 4011; // 获取玩家属性请求
        public const ushort GetPlayerStatsResp   = 4012; // 获取玩家属性响应
        public const ushort UpdatePlayerStatsReq = 4013; // 更新玩家属性请求
        public const ushort UpdatePlayerStatsResp = 4014; // 更新玩家属性响应

        // ---- 支付模块 ----
        public const ushort CreateOrderReq  = 5001; // 创建订单请求
        public const ushort CreateOrderResp = 5002; // 创建订单响应
        public const ushort PayResultNotify = 5003; // 支付结果通知（服务器推送）

        // ---- GM指令模块 ----
        public const ushort GMCommandReq  = 6001; // GM指令请求
        public const ushort GMCommandResp = 6002; // GM指令响应

        // ---- 系统消息 ----
        public const ushort Error = 9999; // 通用错误消息
    }

    /// <summary>
    /// 错误码常量，与服务器 protocol/common.go 中的 ErrXxx 完全对应。
    /// 客户端根据错误码展示对应的本地化提示。
    /// </summary>
    public static class ErrCode
    {
        // 通用错误
        public const int Success       = 0;
        public const int Internal      = 10001; // 服务器内部错误
        public const int InvalidParam  = 10002; // 参数无效
        public const int TooFrequent   = 10003; // 请求过于频繁
        public const int Unauthorized  = 10004; // 未授权

        // 登录模块错误
        public const int LoginInvalidCode  = 20001; // 无效的微信登录code
        public const int LoginWechatFailed = 20002; // 微信API调用失败
        public const int LoginTokenExpired = 20003; // token已过期

        // 游戏存档模块错误
        public const int ArchiveSaveFailed = 30001; // 存档保存失败
        public const int ArchiveNotFound   = 30002; // 存档不存在

        // 排行榜模块错误
        public const int RankInvalidType  = 40001; // 无效的排行榜类型
        public const int RankInvalidRange = 40002; // 无效的排名范围

        // 战斗模块错误
        public const int CombatInvalidResult    = 50001; // 战斗结算数据无效
        public const int CombatCheatDetected    = 50002; // 检测到作弊
        public const int CombatConfigNotFound   = 50003; // 战斗配置不存在
        public const int CombatStyleLocked      = 50004; // 流派未解锁
        public const int CombatInsufficientGold = 50005; // 金币不足
    }
}
