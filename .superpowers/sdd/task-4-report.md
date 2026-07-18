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
