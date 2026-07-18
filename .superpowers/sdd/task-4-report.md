# Phase A2 Task 4 实现报告

## 状态

- Task：Battle Scene Reload Stability and A2 Delivery
- 基线提交：`4b16f4e4579bd020e3f2344130584778d7fd7aba`
- 范围：完成 Task 4 Steps 1-8；未执行 Step 9 的合并、推送或 worktree 清理

## 实现

- 新增 `BattleScene` 重载 PlayMode 测试，比较 `[GameApplication]`、`[GameServices]` 和五个 A2 服务的实例 ID，验证 `Player` 重建、服务唯一性、服务 root 所有权和静态 `Instance` owner。
- 重载后扫描完整 Offline 禁止类型：`NetworkClient`、`LoginManager`、`GameBootstrap`、`ArchiveManager`、`RankManager`、`HeartbeatManager`、`ReconnectionManager`。
- 原 A1 smoke 保留 Ground、Player、WaveSpawner、HUD 和 network/login/bootstrap 缺席断言，继续保存并恢复 `LogAssert.ignoreFailingMessages`，继续捕获 `Error`、`Exception`、`Assert`，新增 active `BattleScene` 和 application `Ready` 断言。
- `BattleSceneSetup` 仅移除 `AchievementManager.Instance` 跨场景预热；保留 sprite preload、DamageNumber/Elemental gameplay pools 和 `SummonManager`。`AudioManager`、`LoadingScreen` 缺失时抛出包含服务名的初始化异常，不创建替代实例。
- 项目文档记录自动 Offline 入口、五个 owned services、最终自动测试计数和仍待 Phase A3 处理的网络生命周期边界；未声称手工可视化试玩。

## TDD 证据

- reload integration characterization：`Logs/A2-task4-reload.xml`，`Passed 1/1`，证明 Task 3 基线已经满足重载生命周期要求。
- service guard RED：`Logs/A2-task4-service-guard-red.xml`，`Failed 0/1`，准确失败于 `BattleSceneSetup must guard every preinstalled service dependency`，`RequireService` 为 null。
- service guard GREEN：`Logs/A2-task4-service-guard-green.xml`，`Passed 1/1`。
- GREEN 首轮编译暴露 `using System` 与现有 `UnityEngine.Random` 的命名歧义；按 systematic-debugging 定位后只将新异常写为 `System.InvalidOperationException`，未改动玩法随机逻辑。

## 最终验证

| 门禁 | 结果 | 证据 |
|---|---|---|
| EditMode | `Passed 54/54`，failed `0` | `Logs/A2-final-verified-editmode.xml` |
| PlayMode | `Passed 9/9`，failed `0` | `Logs/A2-final-verified-playmode.xml` |
| Unity 日志禁用标记扫描 | `error CS`、compiler errors、`NullReferenceException`、ProjectSettings、batchmode abort 均为 `0` | `Logs/A2-final-verified-*.log` |
| Pester | `Passed 5/5`，failed `0` | `tools/validation/UnityAssetIntegrity.Tests.ps1` |
| Asset integrity | `Unity asset integrity check passed.` | `tools/validation/Test-UnityAssetIntegrity.ps1` |
| `git diff --check` | exit `0` | 最终验证命令 |
| fresh Unity compile | exit `0`，`Exiting batchmode successfully now!` | `Logs/A2-final-verified-compile.log` |

## 自审

- 逐项对照 Task 4 brief：Steps 1-7 已实现并验证，Step 8 由本次 Task 4 提交完成，Step 9 留给主代理。
- 自审发现最初服务 ID 捕获会在服务缺失时直接解引用；已改为先明确断言五服务存在，并补充 application/service root 唯一性断言，随后复跑完整 EditMode、PlayMode 和全部交付门禁。
- 未发现 Critical 或 Important 未解决问题。

## 关注点

- Unity licensing client 启动阶段仍有既有握手噪声，但随后成功取得 entitlement；最终 XML、退出码和 compile success marker 均正常，本任务未据此修改产品代码。
- 本任务未执行手工可视化试玩，也未合并、推送或删除 worktree。

## Review Important 修复：场景事件订阅生命周期

- Review 发现 `BattleSceneSetup` 向静态 `CombatEvents` 注册 7 个匿名 handler，向持久 `Inventory.Instance.OnItemChanged` 注册 1 个匿名 handler，且没有解绑；实际代码复核还发现向 `DontDestroyOnLoad` 的 `PauseMenuUI` 注册了 `OnBackToMenu`、`OnSettings` 两个匿名 handler。
- 新 focused reload 测试通过反射读取 event backing delegates，覆盖 `OnHitLanded`、`OnDamageTaken`、`OnParrySuccess`、`OnPlayerDeath`、`OnEnemyDeath`、`Inventory.OnItemChanged` 和两个 PauseMenu events；同时检查 handler 数量、当前 `BattleSceneSetup` owner、destroyed Unity scene reference，并用伤害值 `0` 的 `OnHitLanded` 安全信号探针验证单次派发。测试继续捕获 `Error`、`Exception`、`Assert` 日志。
- Combat/Inventory RED：`Logs/A2-task4-review-events-red.xml`，focused reload `Failed 0/1`；初始场景替换后 `OnHitLanded` 为 `6`（期望 `3`），其余四个 CombatEvents 和 Inventory 均为 `2`（期望 `1`）。
- PauseMenu RED：`Logs/A2-task4-review-pause-events-red.xml`，`OnBackToMenu`、`OnSettings` 均为 `2`（期望 `1`）。
- 产品修复将 10 个订阅全部改为 named handlers，保存 publisher/target 并使用幂等订阅状态；`OnDestroy` 只对本 owner 的相同 delegate 逐一 `-=`，没有全局清空其他 listeners。
- 首轮 GREEN 精确暴露 Unity 销毁顺序：Player effect component 已 fake-null，条件清理遗漏 ink/slash 两个 delegate，导致 `OnHitLanded` 为 `5`。最终改为在订阅组有效时无条件移除 named delegates，`Logs/A2-task4-review-events-green2.xml` 为 `Passed 1/1`。
- 最终 focused：BattleScene reload/smoke `Passed 2/2`；service guard `Passed 1/1`。完整 EditMode `54/54`、PlayMode `9/9`、Pester `5/5`、asset integrity、`git diff --check`、fresh compile 全部通过；最终完整验证日志为 `Logs/A2-task4-review-verified-*`。

## Review Important 修复：PauseMenu 场景所有权

- 复审确认之前的测试跨所有 `PauseMenuUI` 求 Setup handler 总数，会把旧菜单 `0` handler 加当前菜单 `1` handler 误判为正常；旧菜单仍因 `DontDestroyOnLoad` 存活，并在每帧 `Update` 监听 Escape。
- reload 测试改为在 Test Runner 首次替换场景后和显式二次 reload 后分别使用 `Resources.FindObjectsOfTypeAll` 的 valid-scene 结果断言全局 `PauseMenuUI` 恰好 `1`；事件 backing delegate 只检查该唯一实例，不再跨实例求和。
- RED：`Logs/A2-task4-review-pause-instance-red.xml`，focused reload `Failed 0/1`，首次 Test Runner scene replacement 后实际 `PauseMenuUI` 为 `2`、期望 `1`。
- 最小产品修复仅移除 `BattleSceneSetup.CreatePauseMenu` 的 `DontDestroyOnLoad`；PauseMenu 保持本局 scene-owned，不引入 singleton，Setup 的 named handler 精确解绑保持不变。
- GREEN：`Logs/A2-task4-review-pause-instance-green.xml`，focused reload `Passed 1/1`。最终 BattleScene focused `2/2`、guard `1/1`、EditMode `54/54`、PlayMode `9/9`、Pester `5/5`、asset integrity、diff check、fresh compile 全部通过；最终日志为 `Logs/A2-task4-review2-final-*`。
