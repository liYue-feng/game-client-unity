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
- Unity Input System + 手势识别
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
- 入口: GameBootstrap、BattleSceneSetup

**当前缺失（项目无法在Unity中打开）**:
- ProjectSettings/ 目录缺失（Unity无法识别为项目）
- Assets/Scenes/ 目录缺失（没有.unity场景文件）
- Assets/Plugins/ 目录缺失（WebSocketSharp DLL未引入）

**开发优先级**: 战斗手感 > Roguelite > 流派 > 商业化

**How to apply**: 战斗手感是最核心的卖点，优先级最高。协议修改必须同步服务器端。项目目前只有代码，需要补全Unity工程文件才能在编辑器中运行。