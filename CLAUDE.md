# Claude Code 配置：Unity 游戏客户端

## 项目概述

Unity C# 游戏客户端，面向"吸血鬼幸存者"类微信小游戏（代号：剑）。
横版 2D ARPG + Roguelite + 手势操作。

配合后端仓库：`game-server-go`（Go WebSocket 服务器）

## 技术栈

- **引擎**: Unity 2022.3 LTS
- **语言**: C#
- **网络**: WebSocketSharp（WebSocket 客户端）
- **序列化**: JsonUtility（Unity 内置）
- **输入**: Unity Legacy Input + 自定义手势识别（Input System 延后到平台适配阶段）
- **协议**: 二进制帧头(4B长度+2B消息ID) + JSON载荷（与服务器完全一致）

## 核心原则

1. **流程归 superpowers**：plan、brainstorm、debug、TDD、verify、code review，默认走 superpowers
2. **证据优先**：没有测试/截图/QA 报告不算完成
3. **歧义先 brainstorm**：任何创造性工作前先调用 brainstorming
4. **最短路径优先**：能用一个 skill 解决的，不升级为完整闭环

## 项目结构

```
game_client_unity/
├── Assets/
│   ├── Scripts/
│   │   ├── Protocol/          # 通信协议（与服务器一一对应）
│   │   │   ├── Protocol.cs    #   消息ID + 错误码
│   │   │   ├── Messages.cs    #   请求/响应结构体
│   │   │   └── Codec.cs       #   二进制帧编解码
│   │   ├── Network/           # 网络层
│   │   │   └── NetworkClient.cs         # WebSocket 客户端
│   │   ├── Core/              # 生命周期核心 + 主线程调度
│   │   ├── Application/       # 自动入口 + 服务组合根
│   │   ├── Managers/          # 业务管理器
│   │   │   ├── LoginManager.cs   # 登录管理
│   │   │   ├── ArchiveManager.cs # 存档管理
│   │   │   └── RankManager.cs    # 排行榜管理
│   │   ├── Game/              # 游戏逻辑
│   │   │   ├── State/         #   角色状态机
│   │   │   └── Combat/        #   战斗系统
│   │   ├── UI/                # UI 界面
│   │   └── GameBootstrap.cs   # 旧在线入口（A2 下保持惰性）
│   ├── Plugins/               # 第三方插件
│   ├── Scenes/                # 场景
│   └── Resources/             # 资源文件
├── Packages/manifest.json     # Unity 包依赖
└── ProjectSettings/           # 项目设置
```

## 协议格式（与服务器完全一致）

```
+-------------------+-------------------+-------------------+
| Length (4 bytes)  | MsgID  (2 bytes)  | Body   (N bytes)  |
+-------------------+-------------------+-------------------+
小端序 uint32        小端序 uint16        JSON 编码的消息体
```

**修改协议时必须同步修改服务器端代码！**

## 编码规范

- 遵循 Unity C# 命名规范：PascalCase 类/方法，camelCase 私有字段
- 注释写好 WHY，新手需要参考学习
- Phase A2 跨场景服务的 `Instance` 只返回 `GameApplication` 已安装实例，不得自行创建 GameObject；仅 `[GameApplication]` 使用 `DontDestroyOnLoad`
- WebSocket 回调在工作线程，UI 操作必须通过 MainThreadDispatcher 切回主线程
- 网络消息通过 On<T>() 注册监听，不直接在 NetworkClient 中写业务逻辑
- JsonUtility 不支持 camelCase，字段名用 snake_case 与服务器 JSON tag 一致

## 关键依赖

| 包 | 用途 |
|----|------|
| WebSocketSharp | WebSocket 客户端（需放入 Assets/Plugins/） |

当前战斗使用 `UnityEngine.Input` 和自定义 `GestureInput`。未使用的 Input System 包会破坏当前手写 ProjectSettings 下的 PlayMode 初始化，因此 Phase A1 已移除；需要新输入后端时必须连同有效配置和回归测试一起接入。

## Phase A2 应用生命周期（2026-07-18 已验证）

- `RuntimeBootstrap` 在场景加载前自动创建 Offline `[GameApplication]`，默认入口为 `BattleScene`
- `[GameApplication]` 通过唯一 `[GameServices]` 根持有 `MainThreadDispatcher`、`SceneTransitionManager`、`AudioManager`、`LoadingScreen`、`AchievementManager`
- `BattleScene` 重载时应用根、服务根和五个服务实例保持不变，场景内 `Player` 使用新实例，Offline 禁止类型未创建
- Unity EditMode `54/54`、PlayMode `10/10` 通过；Pester 资源验证 `5/5` 通过，fresh compile 成功
- `Online` 模式在 A2 阶段明确失败关闭；A3 完成网络整合后再启用在线启动
- 未执行手工可视化试玩；Phase A3 仍需统一网络连接、心跳、重连和 WebSocket 主线程回调边界

## 服务器仓库

Go 后端：`game-server-go`
- 协议层 `internal/protocol/` 必须与此项目的 `Assets/Scripts/Protocol/` 保持同步

## Change Delivery Gate

声明完成前必须满足：
1. 已完成相关验证并如实报告
2. 代码无编译错误
3. 关键验证无法执行时明确说明原因
4. 禁止虚构命令输出
5. 没有验证证据，不得声称完成

## 跨设备记忆

项目记忆存储在 `.claude/memory/` 目录，随 git 同步。
新机器 clone 后运行 `make setup`，Claude Code 会读取这些文件恢复上下文。

**配合仓库**: `game-server-go`（Go 后端，协议层必须与此项目同步）
