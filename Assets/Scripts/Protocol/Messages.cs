// Messages.cs — 请求/响应消息结构体
//
// 每个结构体与服务器端 protocol/message.go 中的对应类型一一对应。
// 字段名使用 camelCase（JSON 序列化时匹配服务器的小写 json tag）。
//
// 为什么用 [Serializable] + JsonUtility？
//   - JsonUtility 是 Unity 内置的，零依赖，性能好
//   - 缺点：不支持 Dictionary 和嵌套对象序列化
//   - 如果需要这些功能，可以换用 Newtonsoft.Json（Unity 2020+ 内置）
//
// 注意：JsonUtility 不支持 camelCase，字段名必须与 JSON 完全一致。
// 服务器端 JSON 使用 snake_case（Go 的默认行为），
// 所以这里的字段名用 snake_case，与服务器保持一致。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Protocol
{
    // ========== 登录模块 ==========

    /// <summary>
    /// 登录请求 —— 微信小游戏登录流程：
    /// 1. 客户端调用 wx.login() 获取临时 code
    /// 2. 将 code 发给服务器
    /// 3. 服务器用 code 向微信 API 换取 openid + session_key
    /// </summary>
    [Serializable]
    public class LoginReq
    {
        public string code; // 微信登录临时凭证
    }

    /// <summary>
    /// 登录响应
    /// </summary>
    [Serializable]
    public class LoginResp
    {
        public long uid;       // 服务器分配的用户唯一ID
        public string nickname; // 玩家昵称
        public string token;    // 会话令牌
    }

    /// <summary>
    /// 心跳请求 —— 保持连接活跃
    /// </summary>
    [Serializable]
    public class HeartbeatReq
    {
        public long timestamp; // 客户端当前时间戳（毫秒）
    }

    /// <summary>
    /// 心跳响应
    /// </summary>
    [Serializable]
    public class HeartbeatResp
    {
        public long timestamp; // 服务器回显的时间戳
    }

    // ========== 游戏存档模块 ==========

    /// <summary>
    /// 保存存档请求
    /// </summary>
    [Serializable]
    public class SaveArchiveReq
    {
        public string data; // 存档数据，JSON字符串
    }

    /// <summary>
    /// 保存存档响应
    /// </summary>
    [Serializable]
    public class SaveArchiveResp
    {
        public bool success; // 是否保存成功
    }

    /// <summary>
    /// 加载存档请求（无参数）
    /// </summary>
    [Serializable]
    public class LoadArchiveReq {}

    /// <summary>
    /// 加载存档响应
    /// </summary>
    [Serializable]
    public class LoadArchiveResp
    {
        public string data; // 存档数据，JSON字符串
    }

    // ========== 排行榜模块 ==========

    /// <summary>
    /// 获取排行榜请求
    /// </summary>
    [Serializable]
    public class GetRankReq
    {
        public int rank_type; // 排行榜类型：1=最高分 2=击杀数
        public int start;     // 起始排名（从0开始）
        public int count;     // 请求数量
    }

    /// <summary>
    /// 排行榜单条记录
    /// </summary>
    [Serializable]
    public class RankItem
    {
        public long uid;       // 用户ID
        public string nickname; // 昵称
        public int level;      // 玩家等级
        public long score;     // 分数
        public int rank;       // 排名
    }

    /// <summary>
    /// 获取排行榜响应
    /// </summary>
    [Serializable]
    public class GetRankResp
    {
        // JsonUtility 不支持 List<T>，用数组代替
        public RankItem[] ranks; // 排行榜列表
    }

    /// <summary>
    /// 提交分数请求
    /// </summary>
    [Serializable]
    public class SubmitScoreReq
    {
        public long score;     // 本局分数
        public string metadata; // 附加数据（击杀数、存活时间等）
    }

    /// <summary>
    /// 提交分数响应
    /// </summary>
    [Serializable]
    public class SubmitScoreResp
    {
        public bool success;    // 是否提交成功
        public long best_score; // 该玩家的历史最高分
    }

    // ========== 战斗模块 ==========

    /// <summary>
    /// 战斗结算请求 —— 地牢通关或角色死亡后上报本局数据
    /// </summary>
    [Serializable]
    public class CombatResultReq
    {
        public int dungeon_level;   // 地牢等级
        public int score;           // 本局得分
        public int kills;           // 击杀数
        public float survival_time; // 存活时间（秒）
        public int style_id;        // 使用的流派ID（0=无）
        public string combat_log;   // 简化战斗日志JSON，用于反作弊
    }

    /// <summary>
    /// 战斗结算响应
    /// </summary>
    [Serializable]
    public class CombatResultResp
    {
        public bool success;       // 是否结算成功
        public int reward_gold;    // 获得金币
        public int reward_exp;     // 获得经验
        public long best_score;    // 更新后的个人最高分
    }

    /// <summary>
    /// 敌人配置条目
    /// </summary>
    [Serializable]
    public class EnemyConfigItem
    {
        public int id;             // 敌人类型ID
        public string name;        // 敌人名称
        public int hp;             // 生命值
        public int damage;         // 攻击伤害
        public float speed;        // 移动速度
        public float attack_range; // 攻击范围
        public string enemy_type;  // 类型标识：grunt/archer/elite/boss
    }

    /// <summary>
    /// 获取敌人配置请求
    /// </summary>
    [Serializable]
    public class GetEnemyConfigsReq {}

    /// <summary>
    /// 获取敌人配置响应
    /// </summary>
    [Serializable]
    public class GetEnemyConfigsResp
    {
        public EnemyConfigItem[] configs; // 敌人配置列表
    }

    /// <summary>
    /// 获取地牢配置请求
    /// </summary>
    [Serializable]
    public class GetDungeonConfigReq
    {
        public int level; // 请求的地牢等级
    }

    /// <summary>
    /// 获取地牢配置响应
    /// </summary>
    [Serializable]
    public class GetDungeonConfigResp
    {
        public int level;                    // 地牢等级
        public int room_count;               // 房间数量
        public float enemy_density;          // 敌人密度
        public int boss_id;                  // Boss类型ID
        public EnemyConfigItem[] enemy_configs; // 该等级可出现的敌人
    }

    /// <summary>
    /// 流派配置条目
    /// </summary>
    [Serializable]
    public class StyleConfigItem
    {
        public int style_id;                // 流派ID
        public string style_name;           // 流派名称
        public float damage_mult;           // 伤害倍率
        public float speed_mult;            // 速度倍率
        public float parry_mult;            // 弹反窗口倍率
        public float dash_speed_mult;       // 冲刺速度倍率
        public float dash_cost_mult;        // 冲刺消耗倍率
        public int special_resource_max;    // 特殊资源上限
        public string special_resource_name; // 特殊资源名称
        public string description;          // 流派描述
    }

    /// <summary>
    /// 获取流派配置请求
    /// </summary>
    [Serializable]
    public class GetStyleConfigsReq {}

    /// <summary>
    /// 获取流派配置响应
    /// </summary>
    [Serializable]
    public class GetStyleConfigsResp
    {
        public StyleConfigItem[] styles; // 流派配置列表
    }

    /// <summary>
    /// 解锁流派请求
    /// </summary>
    [Serializable]
    public class UnlockStyleReq
    {
        public int style_id; // 要解锁的流派ID
    }

    /// <summary>
    /// 解锁流派响应
    /// </summary>
    [Serializable]
    public class UnlockStyleResp
    {
        public bool success;   // 是否解锁成功
        public int gold_cost;  // 消耗金币
    }

    /// <summary>
    /// 获取玩家战斗属性请求
    /// </summary>
    [Serializable]
    public class GetPlayerStatsReq {}

    /// <summary>
    /// 获取玩家战斗属性响应
    /// </summary>
    [Serializable]
    public class GetPlayerStatsResp
    {
        public int level;            // 等级
        public int exp;              // 经验
        public int gold;             // 金币
        public int max_hp;           // 最大生命值
        public int max_stamina;      // 最大耐力
        public int attack_power;     // 攻击力
        public int[] unlocked_styles; // 已解锁流派ID列表
    }

    /// <summary>
    /// 更新玩家战斗属性请求
    /// </summary>
    [Serializable]
    public class UpdatePlayerStatsReq
    {
        public int level;
        public int exp;
        public int gold;
        public int max_hp;
        public int max_stamina;
        public int attack_power;
        public int[] unlocked_styles;
    }

    /// <summary>
    /// 更新玩家战斗属性响应
    /// </summary>
    [Serializable]
    public class UpdatePlayerStatsResp
    {
        public bool success;
    }

    // ========== 支付模块 ==========

    /// <summary>
    /// 创建订单请求
    /// </summary>
    [Serializable]
    public class CreateOrderReq
    {
        public int product_id; // 商品ID
    }

    /// <summary>
    /// 创建订单响应
    /// </summary>
    [Serializable]
    public class CreateOrderResp
    {
        public string order_no; // 订单号
    }

    // ========== GM指令模块 ==========

    /// <summary>
    /// GM指令请求
    /// </summary>
    [Serializable]
    public class GMCommandReq
    {
        public string cmd;  // 指令名称
        public string args; // 指令参数（JSON字符串）
    }

    /// <summary>
    /// GM指令响应
    /// </summary>
    [Serializable]
    public class GMCommandResp
    {
        public string cmd;    // 回显指令名称
        public string result; // 执行结果
    }

    // ========== 系统消息 ==========

    /// <summary>
    /// 通用错误响应
    /// </summary>
    [Serializable]
    public class ErrorResp
    {
        public int code;    // 错误码
        public string msg;  // 错误描述
    }
}
