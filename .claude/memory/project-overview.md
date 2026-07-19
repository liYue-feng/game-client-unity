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
- 战斗系统: Game.Gameplay、PlayerStateMachine、CombatHit、Hitbox/Hurtbox、BattleTimeController、BattleRunController
- 敌人系统: EnemyBase、EnemyAI、EnemyState、Grunt、Archer、Elite、Boss、Projectile
- 地牢系统: DungeonGrid、DungeonManager、RoomNode、WaveSpawner、RewardItem、MinimapRenderer
- 流派系统: StyleDatabase、StyleManager、StyleSwitchController、5种风格(Blade/Seal/Poison/Blood/Sword)
- 输入系统: InputHandler、InputMediator、GestureInput、PlayerInputBridge
- UI: BattleHUD、PlayerHPBar、StaminaBar、ComboCounter、StyleIndicator、BossHPBar、RoomClearBanner、DungeonResultScreen、MinimapUI
- 视觉: PlaceholderSpriteFactory、HitEffectPlayer、InkParticlePool、InkHitEffect、InkSlashEffect
- 入口: RuntimeBootstrap、GameApplication、BattleSceneSetup

**当前工程状态（2026-07-20 已核对）**:
- `ProjectSettings/`、`Assets/Scenes/` 和 `Assets/Plugins/websocket-sharp.dll` 已存在
- Phase A1 已修复战斗场景脚本引用和重复资源 GUID，并由自动资源检查验证
- `BattleScene` 已通过离线 PlayMode 冒烟测试，可创建地面、玩家、刷怪器和战斗 HUD，且不会启动网络或登录流程
- Phase A2 已建立自动 Offline 入口：`RuntimeBootstrap` 在场景加载前创建唯一 `[GameApplication]`，默认进入 `BattleScene`
- `[GameApplication]` 通过 `[GameServices]` 持有 `MainThreadDispatcher`、`SceneTransitionManager`、`AudioManager`、`LoadingScreen`、`AchievementManager` 五个跨场景服务
- 自动重载测试已验证应用、服务根和五个服务实例保持，`Player` 重建，静态 `Instance` 仍指向存活 owner，Offline 禁止类型缺席
- Phase A3 已完成 service-owned 网络栈、WebSocket transport、主线程回调、连接 host 以及唯一心跳/重连职责；阶段验证为 EditMode `88/88`、PlayMode `11/11`
- Phase A4 已完成设计和实施计划，但 Online 会话、`MenuScene`、真实后端登录/存档联调尚未在本分支实现；当前默认路径仍是 Offline `BattleScene`
- Phase B1 已完成真实攻击有效帧、canonical `CombatHit`/单一弹反入口、动作准入后耐力扣除、统一时间 token、场景内对象池/波次生命周期；左向攻击在 timeline 开始时镜像真实 hitbox，`InkSwirlSpawner`/`AutoWeapon` teardown 只读取现有池，不会在场景销毁时懒创建替代池
- `BattleRunController` 统一 Victory/Defeat/Restart，终局冻结 PlayerInputBridge、PlayerController 和战斗热键，清零速度并显示单一 scene-owned 结果 UI；重开验证覆盖新 Player/Pool/Spawner/UI/EventSystem 和时间恢复
- B1 最终有效自动验证为 focused visual `2/2` 连续三次、combat `33/33`、五次重载 smoke `3/3`、EditMode `111/111`、PlayMode `47/47` 连续两次、Pester `5/5`，资源完整性与静态门禁通过
- 父级已最终批准 960x540 自动证据图：`Logs/phase-b1-combat.png`（dark `2639`、chromatic `117796`、variance `304.99`、Player `57.60px`、Grunt `48.00px`、SHA-256 `59E202689676AE66397A1315A4B014C0BF777FB890314AEDF61BD457F1941E93`）和 `Logs/phase-b1-result.png`（dark `297973`、light `217068`、variance `6281.16`、SHA-256 `9FA4EC1CCE2B36D3B935DC2D133A6B36445038446843AFCF6E3D52AC9932F966`）；combat 无旧 `100000` 或跨局残留，result 无文字/按钮重叠
- Phase B2 由包含本记录的提交交付：确定性波次缩放/池复用、左右出生/相机构图、四类敌人冻结攻击计划与预警结算、统一命中反馈、场景内 Ink 生命周期、波次目标和单 Boss HUD；Boss Telegraph 使用冻结世界矩阵，Poison 状态按敌人池租约重置
- B2 最终自动验证为 visual `2/2` 连续三次、core `49/49`、enemy `39/39`、combat `37/37`、EditMode `160/160`、PlayMode `92/92` 连续两次、smoke `3/3`、Pester `5/5`，规范/质量/完整分支复审均 PASS
- 父级 APPROVED 的 960x540 证据为 `Logs/phase-b2-wave-combat.png`（101674 bytes，SHA-256 `2AEABB48FDB548F7F8E3CA072B0ECB2AA5999CCC7B83250A0BC7A07B33B74DF0`）与 `Logs/phase-b2-boss-telegraph.png`（122543 bytes，SHA-256 `68B6022A192CE43FBF69EAB5265B7A695A52CE6F19AB84125445FE570DD37350`）；测试同时验证 DamageNumber、Ink 和 Circle 的可归属像素差
- 当前仍没有 Prefab；剩余工作为 Phase A4 Online/MainMenu/真实后端联调，以及 Phase C UI、动画、Prefab、资源加载和打包工程化
- 当前战斗代码只使用旧输入 API；未使用且阻塞 PlayMode 的 Input System 包已移除，后续平台适配时按真实需求重新接入
- 现代化顺序为已完成的 Phase A 工程/网络基础、当前 Phase B 战斗体验、后续 Phase C UI/资源工程化
- Phase A 设计见 `docs/superpowers/specs/2026-07-18-unity-client-modernization-phase-a-design.md`

**开发优先级**: 战斗手感 > Roguelite > 流派 > 商业化

**How to apply**: 以已验证的 A3 网络边界和 B1 离线战斗闭环为基线；A4 Online/MainMenu 必须通过真实后端联调后才可声明交付。协议修改必须同步服务器端，任何 Unity 编译、测试、PlayMode 或视觉结论都需要真实运行证据。
