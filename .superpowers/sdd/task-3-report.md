# Phase A2 Task 3 实现报告

## 状态

- Task：Automatic Application and Explicit Service Root
- 基线提交：`a941619183133b6de7b4da7ca4ae56f34614ad1f`
- 范围：仅实现 Task 3；未实施 Task 4 的场景重载测试或文档交付，未修改公司参考工程。

## 实现

- 新增 `GameApplication`、`GameServices` 与 `RuntimeBootstrap`。`BeforeSceneLoad` 自动创建唯一 `[GameApplication]`，其 `Awake` 同步加载并校验配置、安装服务；根对象是唯一 `DontDestroyOnLoad` 所有者。
- `[GameServices]` 按固定顺序安装并初始化 `MainThreadDispatcher`、`SceneTransitionManager`、`AudioManager`、`LoadingScreen`、`AchievementManager`，释放由 `GameServiceCollection` 反向执行并逐项记录异常。
- 四个旧服务移除 `Instance` 隐式创建和服务级持久化，改为显式 `Install`、幂等 `Initialize`/`Shutdown`、owner-aware `OnDestroy` 与只清静态状态的 `ResetStaticState`。
- `GameApplication` 暴露 `Instance`、`HasInstance`、`State`、`FailureStage`、`FailureReason`、`Shutdown()`；初始化失败记录阶段与原因并回滚，显式关闭使用重入保护并清理服务、静态所有权和应用对象。
- `GameBootstrap.Start` 与 `MenuSceneSetup.Awake` 在新应用存在时立即禁用，不进入网络、登录、存档、心跳或重连旧流程。
- Editor utility 通过菜单或 `-executeMethod GameRuntimeSettingsAssetCreator.CreateDefaultAsset` 仅在资产缺失时创建默认 `Assets/Resources/GameRuntimeSettings.asset`。

## RED 证据

### 自动启动 RED

命令：

```powershell
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.ApplicationOfflineStartupTests.AutomaticStartup_CreatesOfflineApplicationAndServices -testResults Logs/A2-task3-startup-red.xml -logFile Logs/A2-task3-startup-red.log
```

结果：`0/1` passed。首个断言得到 `Expected: 1, But was: 0`，准确证明基线没有 `[GameApplication]`，不是测试编译或反射错误。

### 显式关闭 RED

在删除尚未由测试约束的 public `Shutdown` 后运行：

```powershell
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.ApplicationOfflineStartupTests.Shutdown_RemovesApplicationAndAllServices -testResults Logs/A2-task3-shutdown-red.xml -logFile Logs/A2-task3-shutdown-red.log
```

结果：`0/1` passed。失败为 `GameApplication must expose public Shutdown()`，`Expected: not null, But was: null`。随后恢复最小 public 入口，复用已存在的幂等释放核心。

### Review 同帧重启 RED

独立代码审查发现：`Shutdown()` 立即清除应用 singleton，但服务根使用 deferred `Destroy`，同帧 `RuntimeBootstrap.EnsureApplication` 可能从静态字段取得旧 stopped service。新增测试在 shutdown 后不 yield，立即反射调用 `EnsureApplication`，要求新应用 `Ready`、五服务具有新 instance ID 且都属于新 `[GameServices]`。

首次运行直接在 `Services.Initialize` 复用旧 `LoadingScreen` 后进入 `Failed`；收紧日志处理后的稳定 RED 为 `0/1` passed，`GameApplication did not reach Ready within 120 frames.`。最小修复是在 `GameServices.Shutdown` 反向释放完成后、deferred root destroy 前同步调用五服务 `ResetStaticState`；旧对象随后 `OnDestroy` 时由 `ReferenceEquals` owner 检查隔离，不能清除新实例。

## PlayMode 停滞根因与修复

首轮 GREEN 尝试进入 `BattleScene` 后持续高 CPU，结果 XML 未生成。保留日志并仅终止命令行精确包含当前 worktree `projectPath` 的 Unity 进程后，加入临时边界日志复现：

- frame 0：`RuntimeBootstrap` 在 `InitTestScene...` 创建应用，`Awake` 同步完成五服务安装。
- frame 1：`GameApplication.Start` 从 `InitTestScene...` 发起 `LoadSceneMode.Single`。
- frame 3：`BattleScene` 激活，应用进入 `Ready`。
- 60 秒内测试方法从未进入，证明不是应用状态、服务初始化或场景 AsyncOperation 卡住。

同仓库的既有 smoke test 是先进入测试枚举器，再由测试发起 `Single` 加载。单变量实验仅在 `Start` 场景请求前 `yield return null`：frame 2 发起加载、frame 5 `Ready`、frame 11 测试方法进入，focused `1/1` 通过。根因是 frame 1 的 `Single` 加载抢先替换 Test Runner 的初始化场景；最终保留一帧作为正常运行生命周期边界，没有 test-only 条件，`Awake` 的配置校验与服务安装仍保持同步。

## GREEN 与最终验证

| 验证 | 结果 |
|---|---|
| Focused startup + shutdown + immediate restart | `Passed 3/3`，failed `0` |
| Complete EditMode `Game.Tests.EditMode` | `Passed 54/54`，failed `0`，skipped `0`，inconclusive `0` |
| Complete PlayMode | `Passed 4/4`，failed `0`，skipped `0`，inconclusive `0` |
| Unity import/compile | exit `0`，compiler errors `0` |
| 默认配置序列化校验 | Offline、`BattleScene`、`ws://localhost:8080/ws`、正数时间参数、retries `5`、budget `64` |
| `tools/validation/Test-UnityAssetIntegrity.ps1` | `Unity asset integrity check passed.` |
| `git diff --check` | exit `0` |
| 运行后清理 | `InitTestScene*.unity*` 为 `0`；当前 worktree Unity 进程为 `0` |

最终命令：

```powershell
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.ApplicationOfflineStartupTests -testResults Logs/A2-task3-focused-review-green.xml -logFile Logs/A2-task3-focused-review-green.log
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode -testResults Logs/A2-task3-review-editmode.xml -logFile Logs/A2-task3-review-editmode.log
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults Logs/A2-task3-review-playmode.xml -logFile Logs/A2-task3-review-playmode.log
Unity.exe -batchmode -nographics -quit -projectPath . -logFile Logs/A2-task3-review-compile.log
& 'tools/validation/Test-UnityAssetIntegrity.ps1' -ProjectRoot (Get-Location).Path
git diff --check
```

## 自审与关注点

- 新应用代码未引用 `NetworkClient`、`LoginManager`、`ArchiveManager`、`HeartbeatManager`、`ReconnectionManager` 或连接/登录 API；默认 Offline 启动不会触碰在线单例。
- PlayMode 测试只通过对象名和反射检查默认程序集类型，没有给测试程序集增加 `Assembly-CSharp` 静态引用。
- 所有服务 `ResetStaticState` 只清静态字段；Unity 对象销毁只发生在正常关闭/`OnDestroy` 路径。
- Review Important 已闭环：shutdown 同帧重建不会复用旧 stopped service，新服务均挂在新 root 下，旧对象销毁不会误清新静态 owner。
- Unity License client 仍有既有握手噪声，但编辑器最终获得许可，测试与 compile 均正常完成；未据此修改产品代码。
