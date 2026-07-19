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

## 当前工程阶段（2026-07-19 已验证）

- Phase A2 已建立自动 Offline 生命周期：`RuntimeBootstrap -> [GameApplication] -> [GameServices]`，仅应用根跨场景持久化，默认进入 `BattleScene`
- Phase A3 已完成 service-owned 网络边界：`NetworkClient`、WebSocket transport、主线程 dispatcher、连接 host 及唯一心跳/重连策略均有自动测试；A3 收口验证为 EditMode `88/88`、PlayMode `11/11`
- Phase A4 当前只有设计与实施计划，尚未在本分支交付 Online 会话、`MenuScene`、真实后端登录/存档联调；Offline 仍是默认且唯一已交付用户路径
- Phase B1 已建立可玩的离线战斗权威：`CombatHit` 是命中/弹反唯一契约，`BattleTimeController` 是唯一 `Time.timeScale` 写入者，场景内 `ObjectPool`/`DamageNumberPool`/`WaveSpawner` 随战局释放；左向攻击会在攻击开始时镜像真实 hitbox，武器 teardown 不会懒创建替代池
- `BattleRunController` 统一 Victory/Defeat/Restart；终局冻结动作、移动与热键，结果 UI 只提供 Restart，重开后 Player、Pool、Spawner、UI、EventSystem 和时间状态均为新战局
- B1 自动截图探针已生成并由父级最终批准 `Logs/phase-b1-combat.png` 与 `Logs/phase-b1-result.png`，两图均为 960x540；combat 为 dark `2639`、chromatic `117796`、variance `304.99`、Player `57.60px`、Grunt `48.00px`、SHA-256 `59E202689676AE66397A1315A4B014C0BF777FB890314AEDF61BD457F1941E93`，result 为 dark `297973`、light `217068`、variance `6281.16`、SHA-256 `9FA4EC1CCE2B36D3B935DC2D133A6B36445038446843AFCF6E3D52AC9932F966`
- B1 最终有效回归为 focused visual `2/2` 连续三次、combat `33/33`、五次重载 smoke `3/3`、EditMode `111/111`、PlayMode `47/47` 连续两次、Pester `5/5`；资源完整性、canonical hit/parry、唯一时间写入和场景/武器 teardown 门禁均通过
- 后续工作为 Phase B2 战斗表现/敌人体验，以及 Phase C UI、Prefab、动画、资源加载和打包工程化

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
