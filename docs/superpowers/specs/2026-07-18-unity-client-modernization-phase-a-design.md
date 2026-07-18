# Unity 客户端现代化 Phase A 设计

日期：2026-07-18

## 1. 目标

Phase A 建立一个可验证、可扩展的 Unity 客户端基础，使项目能够：

1. 在 Unity Editor 中离线进入战斗场景，不依赖后端服务。
2. 在同一套运行架构上启用在线连接、登录和存档流程。
3. 在基础稳定后继续开展 Phase B 战斗体验优化和 Phase C UI/资源工程化。

实施采用模块化渐进改造。保留现有玩法代码，通过兼容入口逐步迁移，不进行一次性重写。

### 1.1 实施范围

本文锁定 Phase A 的总体架构，但不把 A1 至 A5 合并成一个实施计划。每个子阶段必须在前一阶段验证完成后，分别执行计划、TDD、验证和代码审查。

本设计通过后的首个实施计划只覆盖 A1：

- 修复 `BattleScene.unity` 的脚本引用。
- 为 3 个重复 GUID 的目录 `.meta` 生成互不冲突的 GUID。
- 增加可自动执行的资源完整性检查及其测试。
- 验证当前战斗场景在不初始化联网流程的情况下创建玩家、敌人和 HUD。

A1 不修改网络架构、不迁移 Manager、不重构战斗逻辑，也不开始 UI Prefab 化。这些工作分别属于后续阶段。

## 2. 现状与证据

当前仓库使用 Unity `2022.3.47f1`，与参考项目版本一致。已确认的基线如下：

- `Assets/Scripts` 包含 111 个 C# 文件，约 14,594 行代码。
- 项目当前有 1 个场景、0 个 Prefab、0 个测试文件和 0 个自有程序集定义。
- 代码包含大量运行时对象创建、逐组件 `Update` 和全局单例访问。
- `BattleScene.unity` 的唯一业务 `MonoBehaviour` 使用错误脚本 GUID。
- 该 GUID 实际对应 3 个目录 `.meta` 文件，而非 `BattleSceneSetup.cs.meta`。
- `HeartbeatManager`、`ReconnectionManager` 和 `NetworkClient` 的心跳、连接状态与重连职责重叠。
- WebSocket 的打开、关闭和错误回调未统一切回 Unity 主线程。
- 网络消息注册没有可靠的对称注销机制，Manager 重建后存在重复接收回调的风险。
- 仓库内项目概览仍记录 ProjectSettings、Scenes 和 Plugins 缺失，与当前文件状态不一致。
- 当前机器未在标准位置发现 Unity Editor，因此不能把静态检查当作 Unity 编译或 PlayMode 证据。

## 3. 参考边界

`E:/client/zhetian_client/Unity` 仅作为只读架构参考。可以借鉴：

- 分阶段启动和反向释放。
- 明确的资源、UI、事件、更新和对象池所有权。
- 低内存处理、运行状态与工程验证思路。
- 框架层不依赖具体业务的边界原则。

不会复制公司资源、业务代码、私有包或配置，不会把 XLua、AssetBundle 和公司热更新体系移植到个人项目。公司 SVN 工作区不执行修改或 SVN 操作。

## 4. 总体架构

启动流程分为配置、基础服务、联网、场景和玩法五个阶段：

```text
Runtime Bootstrap
  -> Load Runtime Settings
  -> Initialize Core Services
  -> Select Offline or Online Flow
  -> Load Battle Scene
  -> Install Battle Runtime
```

### 4.1 运行模式

- `Offline`：Unity Editor 默认模式，跳过所有网络步骤，直接进入战斗。
- `Online`：初始化网络，完成连接、认证和存档加载后进入相同战斗流程。
- 微信小游戏适配使用独立平台配置，在 Editor 和后端联调稳定后接入。

### 4.2 生命周期

`GameApplication` 维护以下状态：

```text
Created -> Initializing -> Ready -> ShuttingDown -> Stopped
```

初始化失败时停止后续阶段，不进入半初始化状态。长期服务挂在统一持久化根节点下，并按照初始化的反向顺序释放。

### 4.3 场景职责

Phase A 保留 `BattleSceneSetup` 动态创建本局对象的方式，但它只负责玩家、敌人、关卡对象和战斗 HUD，不再初始化网络、登录、音频等全局服务。Phase B 再进一步拆分战斗安装器。

## 5. 组件边界

### 5.1 GameRuntimeSettings

集中保存运行模式、服务器地址、心跳间隔、超时和重连参数。Editor 默认值必须支持无服务器启动。

### 5.2 GameApplication

作为唯一启动协调者，显式执行各阶段并保存启动状态。它不实现网络或玩法细节，只协调具有明确生命周期的服务。

### 5.3 GameServices

显式持有主线程调度、网络、场景、音频和存档等长期服务。它不是可随意注册和查询的全局 Service Locator。

现有 `Instance` 属性在迁移期可以作为兼容入口，但必须转发到已创建服务，不得通过属性访问隐式创建新的 GameObject。

### 5.4 网络层

- `WebSocketTransport`：只负责建立连接、收发字节和关闭连接。
- `NetworkClient`：负责协议编解码、消息路由和请求发送。
- `NetworkConnectionController`：唯一负责连接状态、心跳、超时和指数退避重连。

网络状态如下：

```text
Disconnected
  -> Connecting
  -> Connected
  -> Authenticating
  -> Ready
  -> Reconnecting or Failed
```

同一时间只允许一个连接或重连流程。每次连接带有版本号，旧连接产生的延迟回调必须失效。

### 5.5 消息订阅

消息注册返回可释放的订阅句柄。业务 Manager 在释放时统一注销订阅，避免场景重载、重新登录或测试重建后出现重复回调。

### 5.6 程序集迁移

Phase A 先建立可独立测试的 `Game.Core` 和 `Game.Network`。旧玩法暂时保留在默认程序集，通过新基础层向下迁移。Phase B 和 Phase C 在依赖方向稳定后再拆分 `Gameplay` 和 `Presentation`，避免一次暴露并处理所有循环依赖。

## 6. 数据流

### 6.1 离线启动

```text
Runtime Settings (Offline)
  -> GameApplication
  -> Core Services
  -> BattleScene
  -> BattleSceneSetup
  -> Gameplay Ready
```

### 6.2 在线启动

```text
Runtime Settings (Online)
  -> NetworkConnectionController
  -> Connect
  -> Login
  -> Load Archive
  -> BattleScene
  -> Gameplay Ready
```

### 6.3 入站网络消息

```text
Socket Worker Thread
  -> WebSocketTransport
  -> MainThreadDispatcher
  -> NetworkClient and Codec
  -> Business Manager
  -> Domain State or Event
  -> UI and Gameplay
```

所有 Unity 对象、事件和协程操作都必须位于主线程边界之后。

## 7. 错误处理

- 启动错误包含失败阶段、原因和建议动作。
- Offline 模式不产生服务器不可用错误。
- 主线程队列逐项隔离异常，并设置单帧处理预算。
- 场景加载失败时退出 Loading 状态并保留可操作的恢复入口。
- 主动退出、模式切换和认证失败不会触发盲目重连。
- 重连采用有上限的指数退避；退避计算独立于 Unity 时间，便于测试。
- 玩家提示与诊断日志分离，日志记录状态、连接版本和失败上下文。
- 收到 Unity 低内存事件时释放非关键缓存和可回收池对象，不破坏本局必要状态。

## 8. 测试与验证

### 8.1 自动测试

引入 Unity Test Framework，并增加：

- EditMode：协议编解码、消息订阅与注销、启动顺序、网络状态迁移、退避计算。
- PlayMode：离线启动、战斗场景创建、重复进出场景、服务释放、回调不重复。
- 网络测试使用可替换的假 Transport，不依赖真实服务器。
- 后端登录、存档和断线重连作为单独的集成验证。

### 8.2 资源完整性

增加 Editor 检查，至少覆盖：

- 重复资源 GUID。
- Scene 或 Prefab 中丢失的脚本引用。
- Build Settings 中不存在或无效的场景。
- 必需运行配置缺失或值无效。

### 8.3 完成证据

每个里程碑需要执行并记录：

1. Unity 编译结果。
2. EditMode 测试结果。
3. PlayMode 测试结果。
4. 对应模式的最小人工游玩结果。
5. 无法执行的验证及具体原因。

在未实际运行 Unity Editor、测试或游戏前，不得声称这些验证通过。

## 9. 实施阶段

### A1：资源与离线基线

- 修复场景脚本引用和重复 GUID。
- 增加资源完整性检查及对应自动测试。
- 保持 `BattleScene` 不初始化 `GameBootstrap` 或任何联网流程。
- 确认战斗场景可以创建玩家、敌人和 HUD。

### A2：启动与生命周期

- 引入 `GameApplication`、`GameRuntimeSettings` 和显式服务根节点。
- 把全局服务初始化移出 `BattleSceneSetup`。
- 建立反向释放流程和场景重载保护。

### A3：网络整合

- 分离 Transport、消息客户端和连接控制器。
- 合并心跳与重连权威实现。
- 所有网络回调切回主线程。
- 引入可释放的消息订阅。

### A4：自动化与后端联调

- 增加 EditMode、PlayMode 和假 Transport 测试。
- 接通登录、存档和断线重连集成流程。
- 记录真实运行证据。

### A5：微信适配准备

- 抽象平台登录入口。
- 保留 Editor 测试登录实现。
- 在基础和后端流程通过后接入微信 SDK。

## 10. Phase A 验收标准

1. `BattleScene` 没有丢失脚本，资源 GUID 唯一。
2. Unity Editor 离线启动不连接服务器，可以创建玩家、敌人和 HUD 并进入战斗。
3. 重载场景不会创建重复服务或产生重复网络回调。
4. 网络事件全部在 Unity 主线程处理。
5. 心跳和重连只有一个权威实现。
6. 自动测试通过；任何无法执行的验证被明确记录。
7. 每组独立改动按仓库约定提交，阶段结束后推送远程。

## 11. 后续阶段

- Phase B：战斗状态机、输入、对象池、敌人行为和战斗反馈优化。
- Phase C：Prefab 化、统一 UI 生命周期、资源加载与缓存工程化。

每个后续阶段单独执行设计、计划、TDD、验证和代码审查流程。
