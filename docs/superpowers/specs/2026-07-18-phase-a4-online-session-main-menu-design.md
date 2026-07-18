# Phase A4 在线会话、主菜单与真实后端联调设计

日期：2026-07-18

## 1. 目标

Phase A4 在 A3 已完成的网络边界上，交付一个可实际运行的在线闭环：

1. Unity Online 模式连接 `game-server-go` 的真实 WebSocket 服务。
2. 使用 Editor 开发凭证完成登录，加载服务器存档后进入 `MenuScene`。
3. 主菜单可以进入 `BattleScene`，战斗结束或暂停时可以回到主菜单。
4. 非主动断线由 A3 的连接控制器重连；重连成功后重新认证并恢复在线会话。
5. Offline 模式继续不创建在线会话，直接进入 `BattleScene`。

本阶段同时修复 `game-server-go` 当前无法编译和本地无数据库时无法联调的问题。真实微信 SDK、微信 `code2session` 凭证和生产部署属于 A5/部署阶段。

## 2. 已确认现状

### 2.1 客户端

- `GameApplication` 在 Online 模式仍显式抛出 `NotSupportedException`。
- Build Settings 只有 `BattleScene`；`MenuScene` 和 `LobbyScene` 只存在于代码字符串中。
- `LoginManager`、`ArchiveManager`、`MenuSceneSetup` 仍保留旧式隐式单例创建和 `DontDestroyOnLoad`。
- A3 已提供单一 Transport、主线程消息分发、连接状态机、心跳、重连和可释放订阅。
- 客户端和服务器的帧格式一致：4 字节小端总长度、2 字节小端消息号、JSON Body。

### 2.2 后端

- 对应仓库为 `liYue-feng/game-server-go`，WebSocket 地址为 `/ws`，协议消息号和 JSON 字段与客户端一致。
- 当前 `master` 因排行榜调用不存在的 `MySQLStore.GetPlayerStats` 而无法通过 `go build ./...`。
- 当前开发配置没有微信 AppID/AppSecret，但登录仍会调用微信 API，Editor 的 `test_dev_code` 无法成功。
- Docker、MySQL 和 Redis 在当前机器不可用。服务器虽然记录“降级运行”，但登录和存档 Handler 会解引用空 Store，不能形成可用降级模式。

### 2.3 参考项目边界

`E:/client/zhetian_client/Unity` 仅用于借鉴以下结构原则：

- 连接、认证、恢复和 UI 展示分层。
- 启动阶段显式推进，失败时停止后续步骤。
- 长期服务由统一根节点持有，退出时反向释放。
- 重连恢复由单一协调器负责，业务 UI 不直接管理 Socket。

不复制其 XLua、TCP/Protobuf 私有协议、资源、配置或业务实现。

## 3. 方案选择

### 方案 A：客户端 Fake Transport 闭环

优点是实现快、测试稳定。缺点是无法证明 WebSocket、Go 编解码和真实 Handler 可以协作，不满足“真实后端联调”。

### 方案 B：Go 内存开发模式 + Unity 在线会话协调器

后端保留生产 MySQL/Redis 路径，同时提供显式、不可误入生产的内存开发模式和开发登录交换器。客户端通过新的在线会话协调器驱动连接、认证、存档和场景。推荐此方案，因为它可以在当前机器上运行真实 Go 进程和真实 WebSocket，又不把测试依赖伪装成生产能力。

### 方案 C：迁移公司项目完整框架

XLua、资源热更、SDK 和复杂恢复体系不适合当前项目体量，会增加新的依赖与所有权问题。本阶段不采用。

## 4. 后端设计

### 4.1 修复编译契约

`Player` 增加持久化等级字段，排行榜通过现有 `GetPlayerByID` 读取等级，不再引用不存在的 Store API。默认等级为 1；异常或无效值回退为 1。该变化同时为后续战斗成长持久化提供最小数据基础。

### 4.2 显式运行配置

新增开发配置边界：

- 生产配置：MySQL 或 Redis 初始化失败即启动失败，不再把空 Store 传给 Handler。
- 开发配置：显式启用 `development.enabled`，使用进程内 Player、Session 和 Archive Store。
- 开发登录：仅当 `development.enabled` 与 `development.login_enabled` 同时为真时接受 `dev:<identity>` 凭证。
- 生产配置永远不接受开发凭证；微信登录仍由 `WechatClient` 处理。

`configs/config.dev.yaml` 用于本地联调，不包含真实密钥。开发存储随进程退出清空，这是明确的测试行为，不冒充生产持久化。

### 4.3 Store 与登录边界

登录和存档 Handler 依赖小接口，而不是具体的 `MySQLStore`/`RedisStore`：

- Player repository：查询、创建、更新玩家。
- Session repository：保存和刷新会话。
- Archive repository：保存和加载存档。
- Login code exchanger：开发凭证或微信 `code2session`。

MySQL/Redis 实现继续服务生产模式，`MemoryDevelopmentStore` 服务本地联调。排行榜、支付和战斗仍只在生产 Store 可用时注册；开发模式只承诺 A4 所需的登录、心跳和存档消息。

## 5. 客户端设计

### 5.1 在线领域服务

新增 `Game.Online` 程序集，依赖 `Game.Core` 和 `Game.Network`，包含：

- `ILoginCodeProvider`：异步取得平台登录凭证。
- `EditorLoginCodeProvider`：返回配置中的 `dev:<identity>` 凭证。
- `LoginSessionService`：发送 `LoginReq`，处理 `LoginResp` 和通用错误。
- `ArchiveSessionService`：发送 `LoadArchiveReq`，处理存档响应和错误。
- `OnlineSessionCoordinator`：唯一编排连接、认证、存档、重连恢复和失败重试。
- `OnlineSessionHost`：Unity 生命周期宿主，由 `GameServices` 创建和释放。

旧 `LoginManager`/`ArchiveManager` 保留兼容，但不参与新 Online 启动，不得再由 `MenuSceneSetup` 隐式创建。

### 5.2 状态机

在线会话状态为：

```text
Idle
  -> Connecting
  -> Authenticating
  -> LoadingArchive
  -> Ready
  -> Reconnecting -> Authenticating -> LoadingArchive -> Ready
  -> Failed -> Retry -> Connecting
```

连接控制状态仍由 A3 的 `NetworkConnectionController` 持有。在线协调器只调用 `BeginAuthentication`、`MarkReady` 和 `Connect`，不复制重连、心跳或 Transport 所有权。

每次启动或重连使用递增会话版本。旧登录/存档回调与已取消的凭证获取不得推进新会话。主动 Shutdown 不触发重试。

### 5.3 GameApplication 流程

`GameRuntimeSettings` 增加 Online 菜单场景名、Editor 开发身份和在线会话超时。启动流程调整为：

```text
Offline:
  Core Services -> BattleScene -> Ready

Online:
  Core Services -> Start OnlineSession
  -> Connect -> Login -> Load Archive
  -> MenuScene -> Ready
```

在线失败时 `GameApplication` 进入 `Failed`，记录明确阶段和根因，不加载半初始化主菜单。

### 5.4 MenuScene 与 UI

新增最小 `MenuScene` 并加入 Build Settings。`MenuSceneSetup` 只创建场景 UI，不创建跨场景服务。

主菜单第一屏显示：

- 游戏名称与当前玩家昵称。
- 网络/登录状态。
- 开始游戏、排行榜、设置和退出命令。
- 失败时显示错误与重试命令。

“开始游戏”直接进入现有 `BattleScene`，不再跳转到不存在的 `LobbyScene`。返回主菜单统一加载 `MenuScene`。UI 订阅 `OnlineSessionHost`，在销毁时解除订阅。

## 6. 错误处理

- 无效开发凭证、微信失败、服务器通用错误、存档失败和会话超时都进入可诊断失败状态。
- 网络瞬断由 A3 自动重连，不立即把应用判为失败；达到最大重连次数后才失败。
- 登录或存档失败不进入主菜单。
- 生产后端缺少数据库/Redis 时进程启动失败，禁止运行到请求阶段再空引用。
- Offline 模式不读取开发登录配置，也不创建 `Game.Online` 宿主。

## 7. 测试与真实联调

### 7.1 Go

- 单元测试：开发凭证边界、内存 Player/Session/Archive、等级回退、配置模式。
- 全量门禁：`go test ./...`、`go build ./...`、`gofmt`/`go vet ./...`。

### 7.2 Unity

- EditMode：在线状态推进、错误、重试、重连恢复、旧回调失效、服务释放。
- PlayMode：Offline 回归、Online Fake Transport 启动、`MenuScene` 创建、进入/返回战斗、Shutdown 无残留。
- 资产完整性：新增场景、脚本和 Build Settings 引用有效。

### 7.3 真实 WebSocket 证据

联调脚本执行以下闭环：

1. 以 `config.dev.yaml` 启动真实 Go 服务器进程。
2. Unity 使用真实 `WebSocketTransport` 连接 `ws://127.0.0.1:8080/ws`。
3. 发送开发登录，验证 `LoginResp`。
4. 加载空存档、保存测试存档、再次加载并验证内容。
5. 记录 Unity XML/日志与服务器日志，并关闭进程。

Fake Transport 测试和真实 WebSocket 测试都通过后，才声明 A4 联调完成。

## 8. 非目标

- 不接入微信小游戏 SDK 或真实 AppSecret。
- 不实现支付、GM、完整排行榜或服务器权威战斗。
- 不迁移公司项目的 XLua、AssetBundle、SDK 或资源。
- 不在 A4 重做全部 UI Prefab；Phase C 再进行资源和 UI 工程化。
- 不在 A4 改造战斗手感、AI 和对象池；这些属于 Phase B。

## 9. 验收标准

1. `game-server-go` 在生产配置下依赖失败会明确退出，在开发配置下无需外部数据库即可启动。
2. Go 全量测试、构建和静态检查通过。
3. Unity Offline 启动和现有 88 个 EditMode、11 个 PlayMode 基线不回归。
4. Unity Online 使用 Fake Transport 完成登录、存档、菜单和重连恢复。
5. `MenuScene` 存在于 Build Settings，主菜单开始/返回路径不再引用缺失场景。
6. 真实 Go 进程与真实 Unity WebSocket 完成登录、存档保存和重新加载闭环。
7. 两个仓库分别提交并推送，远端 SHA 与本地一致。
