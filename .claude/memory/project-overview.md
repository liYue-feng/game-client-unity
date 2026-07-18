---
name: game-client-unity-overview
description: Unity游戏客户端项目概览
metadata:
  type: project
---

Unity C# 游戏客户端，面向"吸血鬼幸存者"类微信小游戏（代号：剑）。

**游戏类型**: 横版 2D ARPG + Roguelite + 手势操作

**配合后端**: game-server-go（Go WebSocket 服务器，仓库 liYue-feng/game-server-go）

**核心卖点**: "见招拆招"的战斗手感 — 弹反、耐力管理、流派切换

**技术选型**:
- Unity 2022.3 LTS + C#
- WebSocketSharp（WebSocket客户端）
- JsonUtility（序列化）
- Unity Legacy Input + 自定义手势识别（Input System 延后到平台适配阶段）
- 协议: 二进制帧头(4B+2B) + JSON载荷（与服务器完全一致）

**已完成模块**:
- Protocol层: 消息ID、错误码、编解码（与Go服务器一一对应）
- Network层: NetworkClient单例 + MainThreadDispatcher
- Manager层: LoginManager、ArchiveManager、RankManager、CombatManager
- 战斗系统: PlayerStateMachine、CombatEvents、Hitbox/Hurtbox、ParryController/ParryHitbox、StaminaController
- 敌人系统: EnemyBase、EnemyAI、EnemyState、Grunt、Archer、Elite、Boss、Projectile
- 地牢系统: DungeonGrid、DungeonManager、RoomNode、WaveSpawner、RewardItem、MinimapRenderer
- 流派系统: StyleDatabase、StyleManager、StyleSwitchController、5种风格(Blade/Seal/Poison/Blood/Sword)
- 输入系统: InputHandler、InputMediator、GestureInput、PlayerInputBridge
- UI: BattleHUD、PlayerHPBar、StaminaBar、ComboCounter、StyleIndicator、BossHPBar、RoomClearBanner、DungeonResultScreen、MinimapUI
- 视觉: PlaceholderSpriteFactory、HitEffectPlayer、InkParticlePool、InkHitEffect、InkSlashEffect
- 入口: RuntimeBootstrap、GameApplication、BattleSceneSetup

**当前工程状态（2026-07-18 已核对）**:
- `ProjectSettings/`、`Assets/Scenes/` 和 `Assets/Plugins/websocket-sharp.dll` 已存在
- Phase A1 已修复战斗场景脚本引用和重复资源 GUID，并由自动资源检查验证
- `BattleScene` 已通过离线 PlayMode 冒烟测试，可创建地面、玩家、刷怪器和战斗 HUD，且不会启动网络或登录流程
- Phase A2 已建立自动 Offline 入口：`RuntimeBootstrap` 在场景加载前创建唯一 `[GameApplication]`，默认进入 `BattleScene`
- `[GameApplication]` 通过 `[GameServices]` 持有 `MainThreadDispatcher`、`SceneTransitionManager`、`AudioManager`、`LoadingScreen`、`AchievementManager` 五个跨场景服务
- 自动重载测试已验证应用、服务根和五个服务实例保持，`Player` 重建，静态 `Instance` 仍指向存活 owner，Offline 禁止类型缺席
- Phase A2 最终自动验证：EditMode `54/54`、PlayMode `10/10`、Pester `5/5`，资源完整性与 fresh Unity compile 通过；未执行手工可视化试玩
- `Online` 模式在 Phase A2 明确 fail-closed，不会伪装为 Ready；真实联网启动由 Phase A3 实现
- 当前仍没有 Prefab；A1 已建立 PlayMode 测试程序集，网络和玩法的完整自动测试将在后续阶段补齐
- 当前战斗代码只使用旧输入 API；未使用且阻塞 PlayMode 的 Input System 包已移除，后续平台适配时按真实需求重新接入
- Phase A3 仍需处理网络连接、心跳/重连职责重复及部分 WebSocket 回调未统一切回 Unity 主线程的问题
- 现代化顺序为 Phase A 工程基础、Phase B 战斗体验、Phase C UI/资源工程化
- Phase A 设计见 `docs/superpowers/specs/2026-07-18-unity-client-modernization-phase-a-design.md`

**开发优先级**: 战斗手感 > Roguelite > 流派 > 商业化

**How to apply**: 先完成 Phase A 的资源完整性、离线运行、生命周期和网络基线，再继续战斗手感优化。协议修改必须同步服务器端。任何 Unity 编译、测试或 PlayMode 结论都需要真实运行证据。
