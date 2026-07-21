# Claude Code 配置：Unity 游戏客户端

## 项目概述

Unity C# 游戏客户端，面向"吸血鬼幸存者"类微信小游戏（代号：剑）。
横版 2D ARPG + Roguelite + 手势操作。

配合后端仓库：`game-server-go`（Go WebSocket 服务器）

## 技术栈

- **引擎**: Unity 2022.3 LTS
- **语言**: C#
- **网络**: WebSocketSharp（WebSocket 客户端）
- **序列化**: Google.Protobuf 3.35.1 + protoc 生成消息
- **输入**: Unity Legacy Input + 自定义手势识别（Input System 延后到平台适配阶段）
- **协议**: 10 字节小端帧头（4B 长度 + 2B 消息 ID + 4B seq）+ protobuf 二进制载荷（与服务器完全一致）

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
│   │   │   ├── Generated/Game.cs # protoc 生成的请求/响应消息
│   │   │   └── Codec.cs       #   二进制帧编解码
│   │   ├── Network/           # 网络层
│   │   │   └── NetworkClient.cs         # WebSocket 客户端
│   │   ├── Core/              # 生命周期核心 + 主线程调度
│   │   ├── Application/       # 自动入口 + 服务组合根
│   │   ├── Online/            # 在线会话、存档和战斗结算协调器
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
+-------------------+-------------------+-------------------+-------------------+
| Length (4 bytes)  | MsgID  (2 bytes)  | Seq    (4 bytes)  | Body   (N bytes)  |
+-------------------+-------------------+-------------------+-------------------+
小端序 uint32        小端序 uint16        小端序 uint32        protobuf 消息字节
```

Transport contract: 10-byte little-endian [Length uint32][MsgID uint16][Seq uint32]; Length includes the 10-byte header; request seq is nonzero; responses and errors echo the exact request seq; pushes use seq 0; Body is protobuf binary.

**修改协议时必须同步修改服务器端代码！**

## 编码规范

- 遵循 Unity C# 命名规范：PascalCase 类/方法，camelCase 私有字段
- 注释写好 WHY，新手需要参考学习
- Phase A2 跨场景服务的 `Instance` 只返回 `GameApplication` 已安装实例，不得自行创建 GameObject；仅 `[GameApplication]` 使用 `DontDestroyOnLoad`
- WebSocket 回调在工作线程，UI 操作必须通过 MainThreadDispatcher 切回主线程
- 网络消息通过 On<T>() 注册监听，不直接在 NetworkClient 中写业务逻辑
- 协议消息只使用 `Generated/Game.cs` 中的生成类型；不得手改生成文件或添加 JSON 兼容别名

## 关键依赖

| 包 | 用途 |
|----|------|
| WebSocketSharp | WebSocket 客户端（需放入 Assets/Plugins/） |
| Google.Protobuf 3.35.1 | protobuf 生成消息运行时（随工程提交） |
| System.Runtime.CompilerServices.Unsafe | Google.Protobuf 在 Unity 2022.3 下的加载依赖 |

当前战斗使用 `UnityEngine.Input` 和自定义 `GestureInput`。未使用的 Input System 包会破坏当前手写 ProjectSettings 下的 PlayMode 初始化，因此 Phase A1 已移除；需要新输入后端时必须连同有效配置和回归测试一起接入。

## 当前工程阶段（2026-07-20 已验证）

- Phase A2 已建立自动 Offline 生命周期：`RuntimeBootstrap -> [GameApplication] -> [GameServices]`，仅应用根跨场景持久化；`Assets/Resources/GameRuntimeSettings.asset` 仍以 Offline 为默认模式，启动后直接进入 `BattleScene`，且不创建 `OnlineSessionHost` 或旧 `LoginManager`/`ArchiveManager`
- Phase A3 已完成 service-owned 网络边界：`NetworkClient`、WebSocket transport、主线程 dispatcher、连接 host 及唯一心跳/重连策略均有自动测试；A3 收口验证为 EditMode `88/88`、PlayMode `11/11`
- Phase A4 已交付 Online 启动闭环：`GameApplication -> OnlineSessionHost -> OnlineSessionCoordinator -> Connect -> Login -> LoadArchive -> MenuScene`；重连后由同一个 generation-safe coordinator 重新认证和加载存档，业务失败会先清理旧 transport，Retry 使用新连接代际，失败或超时不会加载半初始化菜单
- `MenuScene` 与 `BattleScene` 已按此顺序加入 Build Settings；主菜单提供开始战斗、场景内空排行榜、设置和安全退出命令，排行榜不会创建 `RankManager`，开始战斗进入 `BattleScene`，返回菜单统一进入 `MenuScene`
- 终局 UI 同时提供 Restart 和返回菜单；`BattleRunController` 在释放冻结、输入和时间状态前先验证场景跳转服务，服务不可用时 fail closed，成功时至多跳转一次并回到唯一 `MenuCanvas`
- `InkPanel.Configure` 按最终尺寸重建单一 `RawImage` 纹理；组件只销毁自身创建的纹理，保留外部提供或替换的纹理，关闭排行榜和销毁结果面板时不会越权释放资源
- 本地开发后端联调命令为 `& .\tools\integration\Invoke-A4BackendIntegration.ps1 -BackendRoot 'E:\Own_project\game-server-go\.worktrees\protobuf-battle-completion'`；`BackendRoot` 可指向其他有效后端根目录，省略时解析同级 `game-server-go`。runner 使用 `configs/config.dev.yaml` 启动真实后端和 devprobe，运行存档往返、Victory 持久化、Defeat 结算共 3 个 PlayMode 用例，打印后端/devprobe/Unity 的精确 PID，并在退出时恢复 `GAME_BACKEND_INTEGRATION`、清理自有进程和临时可执行文件、确认端口 `8080/8081` 释放
- A4 最终规范审查和质量审查均为 APPROVED；修复后全新回归为资源完整性 PASS、Pester `5/5`、EditMode `210/210`、常规 PlayMode `98` passed + `1` 个 opt-in real-backend skip、真实后端 PlayMode `1/1`；交付代码 head 为 `1b9b7d8`，Go 后端 `master` 为 `874e68e`
- Phase B1 已建立可玩的离线战斗权威：`CombatHit` 是命中/弹反唯一契约，`BattleTimeController` 是唯一 `Time.timeScale` 写入者，场景内 `ObjectPool`/`DamageNumberPool`/`WaveSpawner` 随战局释放；左向攻击会在攻击开始时镜像真实 hitbox，武器 teardown 不会懒创建替代池
- `BattleRunController` 统一 Victory/Defeat/Restart/返回菜单；终局冻结动作、移动与热键，重开后 Player、Pool、Spawner、UI、EventSystem 和时间状态均为新战局，返回菜单会释放战局状态后进入 `MenuScene`
- B1 自动截图探针已生成并由父级最终批准 `Logs/phase-b1-combat.png` 与 `Logs/phase-b1-result.png`，两图均为 960x540；combat 为 dark `2639`、chromatic `117796`、variance `304.99`、Player `57.60px`、Grunt `48.00px`、SHA-256 `59E202689676AE66397A1315A4B014C0BF777FB890314AEDF61BD457F1941E93`，result 为 dark `297973`、light `217068`、variance `6281.16`、SHA-256 `9FA4EC1CCE2B36D3B935DC2D133A6B36445038446843AFCF6E3D52AC9932F966`
- B1 最终有效回归为 focused visual `2/2` 连续三次、combat `33/33`、五次重载 smoke `3/3`、EditMode `111/111`、PlayMode `47/47` 连续两次、Pester `5/5`；资源完整性、canonical hit/parry、唯一时间写入和场景/武器 teardown 门禁均通过
- Phase B2 已完成敌人战斗体验竖切：确定性波次缩放与池复用、左右出生和战场相机、四类敌人冻结攻击计划/预警/结算、实际 HP 差值反馈、场景内墨粒子、波次目标与单 Boss HUD 均由真实运行路径驱动
- Boss 攻击在进入 Telegraph 前同步停止并冻结 `localToWorldMatrix`，冲锋/砸地结算不再随 Commit 位移偏离预警；`PoisonDot` 在敌人池租约结束和新租约准备时清空层数、计时器与来源，旧租约不会污染复用敌人
- B2 最终有效验证为 visual `2/2` 连续三次、core `49/49`、enemy `39/39`、combat `37/37`、EditMode `160/160`、PlayMode `92/92` 连续两次、smoke `3/3`、Pester `5/5`；Task 7 规范审查、质量复审和完整分支复审均 PASS
- 父级最终批准两张 960x540 证据图：`Logs/phase-b2-wave-combat.png`（101674 bytes，dark `8473`、light `506624`、chromatic `73868`、colors `112`、variance `560.11`、Player `29.12px`、Grunt `24.27px`、SHA-256 `2AEABB48FDB548F7F8E3CA072B0ECB2AA5999CCC7B83250A0BC7A07B33B74DF0`）和 `Logs/phase-b2-boss-telegraph.png`（122543 bytes，dark `12998`、light `500312`、chromatic `86814`、colors `139`、variance `809.23`、Boss `48.54px`、Circle `485.39px`、SHA-256 `68B6022A192CE43FBF69EAB5265B7A695A52CE6F19AB84125445FE570DD37350`）
- Protobuf 战斗交付已覆盖全部 32 个 WebSocket route：10 字节小端 envelope 携带 seq，普通请求使用非零 seq，响应/错误精确回显请求 seq，推送使用 seq 0，body 统一为生成 protobuf 消息；存档使用 typed `PlayerArchive`，战斗结算以 `run_id` 在后端保证 exactly-once，客户端结果 UI 明确区分 Pending/Saved/Failed 并只重试未完成的保存阶段
- 支付消息 ID 5001-5003 当前仅为兼容保留；生产支付已禁用，创建订单会收到与请求 seq 关联的 `60001 payment is disabled`，不会触发支付结果推送
- 生产 `BattleScene` 配置已由运行测试证明包含 10 波、181 个敌人和最终 Boss 波；短流程测试通过真实 `WaveSpawner -> 敌人受伤/死亡 -> OnAllWavesComplete` 到达 Victory，Defeat 则通过真实玩家致死路径进入同一结算所有权
- Task 7 阶段证据为 `Logs/Task7A-final-editmode.xml` 的 EditMode `238/238`、`Logs/Task7B-full-playmode-final.xml` 的 PlayMode `104` passed + `1` opt-in skip，以及 `Logs/A4-real-backend-20260721-035422.xml` 的真实后端 Victory/Defeat/Reload `3/3`；这些是分阶段已验证数据，不替代交付前的全分支 fresh rerun
- 后续工作为 Phase A5 的真实微信 SDK、`code2session` 凭证与生产部署，以及 Phase C 的 UI、Prefab、动画、资源加载和打包工程化；Phase B1/B2 战斗竖切已经完成

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
