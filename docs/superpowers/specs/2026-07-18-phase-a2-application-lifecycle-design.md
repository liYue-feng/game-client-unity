# Unity 客户端 Phase A2 启动与生命周期设计

日期：2026-07-18

## 1. 目标

Phase A2 在 A1 离线战斗基线之上建立唯一、可测试、可释放的应用启动流程。Unity Editor 点击 Play 后，无论当前打开哪个场景，都由统一入口以 Offline 模式启动并进入 `BattleScene`，且不会创建网络、登录或旧启动流程对象。

本阶段只处理启动和生命周期。WebSocket 分层、心跳、重连、消息订阅和后端联调属于 A3、A4，不在 A2 提前实现。

## 2. 现状与约束

当前项目存在三类启动来源：

- `GameBootstrap` 通过访问 `Instance` 隐式创建网络和业务 Manager。
- `MenuSceneSetup` 直接创建心跳、重连和登录对象。
- `BattleSceneSetup` 在玩法初始化时访问多个跨场景单例。

多个 `Instance` 属性可以自行创建 `DontDestroyOnLoad` 对象，因此启动顺序和所有权取决于首次访问位置。场景重载、关闭 Domain Reload 或旧入口误挂载时，容易出现重复服务、残留静态引用和事件重复注册。

参考项目 `E:/client/zhetian_client/Unity` 仅用于确认分阶段 `Init`、集中 `ReleaseGame`、低内存回调和反向释放思路。A2 不复制其 XLua、AssetBundle、SDK、业务代码或资源。

## 3. 选定方案

采用运行时自动入口，不新增 Bootstrap 场景，也不要求业务场景预挂启动脚本。

```text
SubsystemRegistration
  -> Reset static runtime state
BeforeSceneLoad
  -> RuntimeBootstrap creates [GameApplication]
  -> Load and validate GameRuntimeSettings
  -> Initialize [GameServices]
  -> Select Offline or Online flow
  -> Ensure BattleScene is active
  -> Application state becomes Ready
```

Editor 默认配置为 `Offline + BattleScene`。如果启动时已经处于 `BattleScene`，不重复加载；如果处于其他场景，则由应用入口加载 `BattleScene`。

## 4. 组件边界

### 4.1 RuntimeBootstrap

`RuntimeBootstrap` 是静态入口，具有两个职责：

1. 在 `SubsystemRegistration` 阶段清理静态运行状态，兼容 Editor 关闭 Domain Reload。
2. 在 `BeforeSceneLoad` 阶段确保全局只存在一个 `GameApplication`。

它不创建玩法对象、不访问网络、不实现服务细节。重复调用必须复用已有应用实例。

### 4.2 GameRuntimeSettings

`GameRuntimeSettings` 是 `Resources/GameRuntimeSettings.asset` 配置资产，包含：

- `RuntimeMode`：`Offline` 或 `Online`，A2 默认 `Offline`。
- `StartupSceneName`：A2 固定配置为 `BattleScene`。
- `ServerUrl`、心跳间隔、连接超时、重连次数和退避参数：A2 只集中保存并校验，A3 才消费。
- `MainThreadMaxTasksPerFrame`：主线程每帧最多处理的任务数，必须大于零。

配置校验规则明确如下：

- 模式必须是已定义枚举值。
- 启动场景名不能为空，且必须存在于启用的 Build Settings 中。
- 所有时间参数必须大于零，重连次数不得为负数。
- Online 模式要求 `ServerUrl` 是 `ws` 或 `wss` URI；Offline 模式不连接服务器，但配置资产本身仍需包含格式有效的默认地址。

配置资产缺失或无效时，应用进入 `Failed`，不创建服务、不加载玩法场景。

### 4.3 GameApplication

`GameApplication` 是唯一启动协调者，不实现具体服务或玩法。状态机为：

```text
Created -> Initializing -> Ready -> ShuttingDown -> Stopped
                         \-> Failed
```

它保存当前状态、失败阶段和失败原因。初始化中只允许一个流程；重复启动请求返回当前流程，不创建第二套服务或场景加载操作。

### 4.4 GameServices

`GameServices` 显式持有 A2 的跨场景服务，不提供任意类型注册或全局查询能力。服务统一实现 `Initialize` 和 `Shutdown` 契约。

A2 纳入统一所有权的服务固定为：

1. `MainThreadDispatcher`
2. `SceneTransitionManager`
3. `AudioManager`
4. `LoadingScreen`
5. `AchievementManager`

服务挂在 `[GameApplication]/[GameServices]` 下，按上述顺序初始化，按反向顺序释放。只有成功初始化的服务进入释放栈。

现有 `Instance` 作为迁移兼容入口，只能返回由 `GameServices` 安装的实例，不再通过属性访问隐式创建这些对象。若服务尚未安装，访问应记录包含服务名和应用状态的明确错误，不得静默生成第二个根节点。

战斗池、伤害数字、元素、召唤、背包、天赋和其他玩法单例不纳入 A2；它们仍由玩法域管理，后续在 Phase B 按玩法生命周期拆分。

### 4.5 BattleSceneSetup

`BattleSceneSetup` 继续负责玩家、敌人、关卡、HUD、战斗特效池和本局玩法绑定。它不再显式创建或预热 A2 列出的跨场景服务。

战斗开始时可以调用已经安装的音频、Loading 和成就服务，但不得把缺失服务当作创建入口。

### 4.6 旧入口兼容边界

`GameBootstrap`、`MenuSceneSetup` 和现有 Manager 类型在 A2 不删除，避免一次性破坏未纳入 Build Settings 的旧场景或脚本引用。自动入口不会挂载或调用它们。

旧 `GameBootstrap` 或 `MenuSceneSetup` 检测到 `GameApplication` 后必须立即停止自身启动逻辑，只记录包含对象名的迁移警告，不得创建网络或业务 Manager。这样从包含旧入口的场景点击 Play 也会由新应用接管。A3 完成在线流程迁移后再删除旧网络启动职责。

## 5. 初始化与释放

### 5.1 Offline 初始化

```text
Validate settings
  -> Create persistent service root
  -> Initialize five A2 services
  -> Suppress legacy bootstrap components
  -> Load or reuse BattleScene
  -> Wait for scene activation
  -> Ready
```

Offline 流程不得访问 `NetworkClient.Instance`、`LoginManager.Instance`、`ArchiveManager.Instance` 或 `RankManager.Instance`。

### 5.2 失败回滚

任一阶段失败后立即停止后续初始化。已经成功初始化的服务按照反向顺序执行 `Shutdown`，随后销毁服务根，应用保存失败阶段和异常信息并进入 `Failed`。

失败状态不伪装为 Ready，也不自动重试。重新启动只能通过显式测试控制或下一次 Unity 运行完成。

### 5.3 Shutdown

`OnApplicationQuit`、`OnDestroy` 和显式关闭统一调用幂等 `Shutdown`。释放顺序为：

```text
AchievementManager
  -> LoadingScreen
  -> AudioManager
  -> SceneTransitionManager
  -> MainThreadDispatcher
  -> clear GameServices
  -> clear GameApplication static state
```

重复关闭不重复注销事件或销毁对象。退出 PlayMode 后所有 A2 静态引用必须为 `null`。

## 6. 场景重载

`GameApplication` 和服务根跨场景保留，`BattleSceneSetup` 及其玩法对象随场景销毁并重建。场景加载使用单一进行中标记，重复请求不会启动并行协程。

连续重载 `BattleScene` 后：

- `[GameApplication]` 和 `[GameServices]` 各保持一个。
- 五个 A2 服务各保持一个。
- 玩家、刷怪器和 HUD 属于新场景实例。
- 不出现网络、登录或旧 Bootstrap 对象。

## 7. 主线程调度边界

`MainThreadDispatcher` 仍以线程安全队列接收任务，但 A2 补齐以下生命周期规则：

- 每帧按可配置的最大任务数处理，避免单帧无限清空队列。
- 单个任务异常只记录该任务错误，不中断其余任务。
- `Shutdown` 后拒绝新任务并清空待执行队列。
- 静态重置同时清理实例和队列，避免关闭 Domain Reload 时复用旧任务。

A3 负责把 WebSocket 的打开、关闭、错误和消息回调全部接到该边界之后。

## 8. 测试设计

### 8.1 EditMode

- 有效 Offline 配置通过校验。
- 缺失场景、非法时间参数和非法 URI 被拒绝。
- 应用状态按合法路径迁移，非法重复启动不创建新流程。
- 服务按声明顺序初始化、反向顺序释放。
- 中途初始化失败只回滚已成功服务。
- 重复 `Shutdown` 只释放一次。

### 8.2 PlayMode

- 自动入口创建且只创建一个 `[GameApplication]` 和 `[GameServices]`。
- 默认 Offline 启动最终进入 `BattleScene` 和 `Ready`。
- 玩家、`WaveSpawner` 和 `[BattleHUD]` 正常创建。
- `[NetworkClient]`、`[LoginManager]`、`[GameBootstrap]` 不存在。
- 连续重载 `BattleScene` 后服务实例不变，场景玩法对象重新创建。
- 应用关闭后静态引用和主线程任务队列清空。

测试必须使用真实 Unity Test Framework 运行。配置、服务顺序和失败回滚测试不依赖后端。

## 9. 验证与交付

A2 完成必须提供以下新鲜证据：

1. Pester 资源检查器测试通过。
2. 项目资源完整性检查通过。
3. Unity 全新批处理编译通过，无 C#、ProjectSettings 或未处理异常。
4. 全部 EditMode 测试通过。
5. 全部 PlayMode 测试通过。
6. Editor 默认 Offline 配置启动后，应用状态为 Ready，活动场景为 `BattleScene`，核心玩法对象存在且网络对象不存在。
7. 工作区差异检查、提交历史和远端提交一致。

## 10. 非目标

A2 不执行以下工作：

- 不拆分 `WebSocketTransport`、消息客户端或连接控制器。
- 不合并心跳和重连实现。
- 不改变协议、登录请求、存档或服务端。
- 不接入微信 SDK。
- 不新增 Bootstrap 场景。
- 不进行 Prefab 化、UI 重做或战斗手感优化。
- 不复制参考项目的私有代码、资源、包或配置。

## 11. 完成标准

Phase A2 在以下条件全部满足时完成：

1. Editor 从任意场景点击 Play 都由唯一应用入口以 Offline 模式进入 `BattleScene`。
2. 应用生命周期状态、失败信息和反向释放行为可测试。
3. 五个 A2 跨场景服务只有一个明确所有者，场景重载后不重复。
4. Offline 流程不创建网络、登录、存档或旧启动对象。
5. 退出 PlayMode 后 A2 静态状态和队列已清理。
6. 自动测试、Unity 编译、实际 Editor 启动和远端交付均有可核对证据。
