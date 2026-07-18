# Phase A2 整分支最终审查修复报告

## 范围

- 修复基线：`904e229`
- Finding：`GameRuntimeSettings` 接受 `RuntimeMode.Online`，但 `GameApplication` 未选择运行模式，错误地创建 Offline 五服务、加载 `BattleScene` 并进入 `Ready`。
- 边界：仅实现 A2 fail-closed；未实现或启动任何 Online 网络、登录、存档、心跳、重连流程，未 merge/push。

## TDD RED

新增 `OnlineMode_FailsClosedBeforeCreatingServices` PlayMode 黑盒测试，通过反射临时把 `Resources/GameRuntimeSettings` 的 `_runtimeMode` 改为 Online，关闭当前应用并同帧反射调用 `RuntimeBootstrap.EnsureApplication`。

第一轮 RED 使用最终预期日志约束运行，结果 `0/1`，提示缺少：

```text
[GameApplication] Initialization failed at Mode.Select: Root cause: Online runtime flow is not implemented in Phase A2.
```

为了保留明确的错误行为证据，临时只移除两条未来日志期待后重新运行相同场景，结果仍为 `0/1`：

```text
Expected: "Failed"
But was:  "Ready"
```

这证明当前代码不是单纯缺少日志，而是确实把 Online 运行成了 Offline Ready，并创建了服务。随后恢复精确 `LogAssert.Expect` 再进入 GREEN 实现。

RED 命令：

```powershell
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.ApplicationOfflineStartupTests.OnlineMode_FailsClosedBeforeCreatingServices -testResults Logs/A2-final-review-online-behavior-red.xml -logFile Logs/A2-final-review-online-behavior-red.log
```

## 最小修复

`GameApplication.Awake` 在 settings `TryValidate` 成功后、`GameServices.Create` 前设置 `FailureStage = "Mode.Select"` 并显式 switch：

- `Offline`：继续现有五服务与启动场景流程。
- `Online`：抛出 `NotSupportedException("Online runtime flow is not implemented in Phase A2")`，由现有失败路径格式化 reason、记录完整异常并进入 `Failed`。
- 未定义值：保留防御性错误；正常情况下已由 settings validation 拒绝。

Online 失败发生在服务根创建之前，`Start` 看到非 `Initializing` 状态后立即退出，不创建 `[GameServices]`、五服务或任何网络对象，也不发起场景加载。

测试 `finally` 无条件恢复原 Offline enum 值，关闭 Online 应用并立即重建应用；离开 finally 后等待新应用回到 `Ready`，避免污染后续测试。

## GREEN 与全门禁

| 验证 | 结果 |
|---|---|
| Focused Online | `Passed 1/1`，failed `0` |
| Focused application lifecycle | `Passed 8/8`，failed `0` |
| Complete EditMode | `Passed 54/54`，failed `0`，skipped `0`，inconclusive `0` |
| Complete PlayMode | `Passed 10/10`，failed `0`，skipped `0`，inconclusive `0` |
| Fresh Unity compile | exit `0`，compiler errors `0` |
| Pester `UnityAssetIntegrity.Tests.ps1` | `Passed 5/5`，failed `0` |
| Executable asset validation | `Unity asset integrity check passed.` |
| Settings serialization | Offline、`BattleScene`、默认 ws URL、时间/重连参数、budget `64` 全部匹配 |
| `git diff --check` | exit `0` |
| Cleanup audit | project Unity processes `0`；temporary `InitTestScene` files `0` |

最终命令：

```powershell
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.ApplicationOfflineStartupTests.OnlineMode_FailsClosedBeforeCreatingServices -testResults Logs/A2-final-review-online-green-verified.xml -logFile Logs/A2-final-review-online-green-verified.log
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter Game.Tests.PlayMode.ApplicationOfflineStartupTests -testResults Logs/A2-final-review-focused.xml -logFile Logs/A2-final-review-focused.log
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform EditMode -testFilter Game.Tests.EditMode -testResults Logs/A2-final-review-editmode.xml -logFile Logs/A2-final-review-editmode.log
Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testResults Logs/A2-final-review-playmode.xml -logFile Logs/A2-final-review-playmode.log
Unity.exe -batchmode -nographics -quit -projectPath . -logFile Logs/A2-final-review-compile.log
Invoke-Pester -Script tools/validation/UnityAssetIntegrity.Tests.ps1 -PassThru
& tools/validation/Test-UnityAssetIntegrity.ps1 -ProjectRoot (Get-Location).Path
git diff --check
```

Unity License client 仍有既有签名/握手噪声，但随后成功连接 Unity 2022.3 licensing client；全部测试与 compile 均正常完成，因此未据此修改产品代码。
