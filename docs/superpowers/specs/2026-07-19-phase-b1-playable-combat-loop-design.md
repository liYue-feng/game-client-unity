# Phase B1 可玩战斗闭环设计

## 目标

Phase B1 把当前“场景能创建玩家、敌人和 HUD”的离线基线提升为一个可自动验证、可实际操作的战斗竖切：

1. 玩家轻击在没有动画资产时也有明确的前摇、有效帧和恢复帧，并能击杀第一只 Grunt。
2. 敌方攻击通过非空战斗命中契约传递可弹反属性；可弹攻击能被弹反，不可弹攻击仍造成伤害。
3. 重击、冲刺和弹反只有在动作获准后才消耗耐力；被拒绝的动作不扣资源。
4. 暂停、升级、卡帧、慢动作和战斗结算不再直接互相覆盖 `Time.timeScale`。
5. 玩家死亡或全部波次完成后进入唯一终局，显示结果并能重开；重开后时间、玩家、敌人和终局 UI 都是新战局状态。

该阶段继续使用程序化占位视觉和动态 `BattleSceneSetup`，不等待 Prefab、动画或音效资源。

## 现状与根因

- `AttackHitbox` 在 `Awake` 默认关闭，但仓库没有动画事件或其他 `EnableHitbox` 调用，玩家首击无法造成伤害。
- 敌人攻击把 `null` 作为来源 Hitbox 传给 `Hurtbox.ReceiveHit`；弹反窗口内会解引用 `sourceHitbox.isParryable`。
- `RequestHeavyAttack`、`RequestDash`、`RequestParry` 在确认状态转换合法之前消耗耐力。
- `PauseMenuUI` 与 `BattleSceneSetup` 同时读取 Escape；`InventoryUI` 与 `BattleSceneSetup` 同时读取 Tab。
- `PlayerStateMachine`、`HitStopController`、`PauseMenuUI`、`LevelUpUI` 和 `BattleSceneSetup` 都直接写 `Time.timeScale`，嵌套效果会错误恢复到 1。
- `WaveSpawner.OnAllWavesComplete` 无消费者；玩家死亡只上报成就，不显示终局。
- `GameOverUI` 跨场景常驻、每次显示都重新构建，且 Offline 模式下提供尚不存在的 `MenuScene` 入口。
- 当前 PlayMode 冒烟测试只验证对象图和生命周期，没有覆盖一次真实攻击、弹反、死亡或胜利。

## 方案选择

### 方案 A：直接在现有 MonoBehaviour 中补条件

改动最少，但攻击时序、动作准入和时间竞争仍只能依赖场景测试，未来动画接入后容易再次出现有效帧和资源扣除顺序问题。

### 方案 B：一次把全部战斗脚本迁入 Gameplay/Presentation 程序集

最终方向正确，但当前玩法脚本直接依赖 UI、Manager、Inventory、Audio 和运行时创建器。一次迁移会暴露大量循环依赖，超出最小战斗闭环。

### 方案 C：小型 Gameplay 核心 + 现有脚本适配

本阶段采用该方案。新增一个不依赖现有默认程序集的 `Game.Gameplay` 小程序集，只承载命中契约、攻击时间线、动作准入和时间请求模型。现有 MonoBehaviour 继续留在默认程序集并消费这些接口。这样既建立 EditMode 单测边界，又保持改动范围可控；后续 Phase B2/C 再逐步迁移完整 Gameplay 和 Presentation。

## 结构设计

### 1. Game.Gameplay 核心

新增 `Assets/Scripts/Gameplay/Game.Gameplay.asmdef`，包含：

- `CombatHit`：伤害、X 方向、击退、是否可弹反、`IParryResponder` 来源。
- `IParryResponder`：来源收到成功弹反后的唯一回调。
- `AttackTimeline`：前摇、有效帧、恢复帧；根据 elapsed 返回 `Windup/Active/Recovery/Complete`。
- `CombatActionPolicy`：只接收 `transitionAllowed`、`onCooldown`、当前耐力和费用等核心值，不引用仍位于 `Assembly-CSharp` 的 `PlayerState`；不直接修改 Unity 组件。
- `TimeScaleRequestSet`：每次请求返回唯一 token，最终取全部存活 token 的最小 scale；释放一个 token 不影响同 reason 的其他并发请求。
- `BattleRunState` / `BattleRunOutcome`：`Running`、`Victory`、`Defeat`、`Restarting`、`Disposed`，只允许第一次终局生效。

`Game.Core.EditModeTests.asmdef` 与 `Game.PlayModeTests.asmdef` 增加 `Game.Gameplay` 引用。EditMode 测试直接引用核心类型；场景适配器仍在 `Assembly-CSharp`，PlayMode 测试沿用现有反射接缝访问 `PlayerStateMachine`、`Hurtbox`、`EnemyBase` 和 `BattleRunController`，避免 asmdef 反向引用预定义程序集。

### 2. 玩家攻击时间线

`PlayerStateMachine` 成为玩家攻击有效帧的唯一所有者：

- `BattleSceneSetup` 创建 Hitbox 后显式调用 `ConfigureAttackHitbox`。
- 进入 Attack1/2/3/Heavy 时根据该段总时长创建 timeline。
- 首次进入 Active 时调用 `EnableHitbox`；离开 Active、退出攻击或组件禁用时调用 `DisableHitbox`。
- `Hitbox` 的 HashSet 保证同一有效帧对同一 Hurtbox 只命中一次，命中后继续调用 `MarkHit` 支持连击。
- 未来动画事件只作为 timeline 驱动适配器，不能成为无动画时唯一的伤害入口。

### 3. 战斗命中与弹反

`Hurtbox.ReceiveHit` 改为接收 `CombatHit`，不再依赖 nullable `Hitbox`：

- 玩家位于弹反窗口且 `IsParryable` 时，不扣 HP，调用 `PlayerStateMachine.OnParrySuccess` 和来源 `OnParried`。
- 不可弹攻击或不在窗口时走正常伤害、受击和击退。
- `EnemyBase` 实现 `IParryResponder`，成功弹反进入 Stunned。
- `Projectile` 实现 `IParryResponder`，成功弹反调用现有 `Deflect`。
- 玩家 Hitbox、Grunt、Archer、Elite、Boss 和 Projectile 都构造完整 `CombatHit`，禁止再传 `null` 表达攻击语义。
- 删除 `ParryHitbox.OnTriggerEnter2D` 直接调用 `OnParrySuccess` 的第二入口，并从 `BattleSceneSetup` 移除该组件。成功弹反只能发生在 `Hurtbox.ReceiveHit(CombatHit)`，每次成功只回调来源一次。

### 4. 动作准入与耐力

动作请求按固定顺序执行：

1. 判断死亡、受击、当前状态、取消规则和冷却。
2. 用 `CombatActionPolicy` 判断耐力是否足够。
3. 仅在动作确定可进入时调用 `TryUseStamina`。
4. 扣除成功后切换状态并播放反馈。

重击反击窗口、Dash 冷却和 Parry 取消规则都遵循该顺序。拒绝动作不得触发音效、状态变化或资源变化。

### 5. 时间所有权

新增场景级 `BattleTimeController`，它持有 `TimeScaleRequestSet` 并成为唯一 `Time.timeScale` 写入者。每个调用者持有并释放自己的 token；两个重叠 HitStop 即使 reason 相同也有不同 token，较早协程结束不会解除较晚卡帧。reason 至少包括：

- `Pause`
- `LevelUp`
- `HitStop`
- `ParrySlowMotion`
- `BattleResult`

`PauseMenuUI`、`LevelUpUI`、`HitStopController` 和 `PlayerStateMachine` 只申请/释放自己持有的 token。任一 0 scale 请求存在时保持暂停；清除慢动作不会解除菜单暂停。组件禁用和场景释放必须释放自己持有的全部 token，controller 销毁时恢复 1。

Escape/Tab 只由 `InputMediator -> BattleSceneSetup` 消费；`PauseMenuUI` 和 `InventoryUI` 移除直接键盘轮询。

### 6. 战局终局

新增 `BattleRunController`，由 `BattleSceneSetup` 创建并初始化 Player、`CharacterStats`、`PlayerStateMachine`、`WaveSpawner`、`BattleTimeController` 和 `GameOverUI`。

- 订阅 `CharacterStats.OnDeath` 与 `WaveSpawner.OnAllWavesComplete`。
- 玩家死亡时强制 Die、发布一次兼容的 `CombatEvents.OnPlayerDeath`、进入 Defeat。
- 全波完成时进入 Victory。
- 第一个终局获胜，后续死亡/胜利事件被忽略。
- 终局申请 `BattleResult` 时间 token，同时关闭 `PlayerInputBridge.InputEnabled` 和 `BattleSceneSetup.BattleHotkeysEnabled`，停止攻击、移动、暂停和背包热键，再显示一次结果 UI。不能只依赖 `timeScale == 0`，因为 Unity 仍会执行 `Update`。
- Restart 先恢复时间、释放订阅，再通过 `SceneTransitionManager` 重载 `BattleScene`。
- `Dispose` 幂等；Restart 和 `OnDestroy` 都调用它，并恢复输入门禁、释放时间 token、解绑 Player/Wave 事件。

`PlayerStateMachine` 不再通过 `RaiseDeathEvent` 重复发布死亡；`Hurtbox` 也不直接发布玩家死亡。死亡源统一为 `CharacterStats.OnDeath`。

### 7. 战局对象池

`ObjectPool` 属于战局而不是应用服务。B1 移除它的 `DontDestroyOnLoad`，让池根、可用对象和所有取出中的敌人/武器随 `BattleScene` 一起销毁；`OnDestroy` 清空静态 `Instance` 和工厂表。

`WaveSpawner.Dispose` 必须幂等：停止所有刷怪/延迟回收协程，解绑每个活跃敌人的死亡回调，清空活跃集合，并在场景重载前释放对工厂和旧 Spawner 的引用。新场景注册全新的工厂。该边界优先保证战局隔离，跨战局缓存留给 Phase C 的资源所有权设计。

### 8. 终局 UI 与 EventSystem

- `GameOverUI` 改为场景所有，不使用 `DontDestroyOnLoad`，重复显示不重复建树。
- `BattleSceneSetup` 保证场景内存在唯一 `EventSystem + StandaloneInputModule`，使暂停、升级和结算按钮可点击。
- Offline 阶段结果 UI 只展示 Restart；`MenuScene` 真正交付后再启用 Return to Menu。
- 场景销毁时清理运行时纹理和静态 `Instance`。

## 测试策略

### EditMode

- AttackTimeline 各阶段边界、零/负配置归一化。
- CombatActionPolicy：状态拒绝、冷却、耐力不足、成功准入。
- TimeScaleRequestSet：嵌套暂停/慢动作的添加、更新、移除和清空。
- TimeScaleRequestSet：两个同 reason token 反序释放时，后一个请求仍保持生效。
- BattleRunState：Victory/Defeat 只接受第一次，Restart/Dispose 转换合法。

### PlayMode

- 加载真实 `BattleScene`，把第一只 Grunt 放入攻击范围，触发轻击后 HP 下降且单个有效帧只命中一次。
- 在 Hurt 状态请求重击/Dash/Parry，耐力不变。
- 可弹 `CombatHit` 在窗口内不扣玩家 HP，并回调来源；不可弹命中正常扣 HP。
- 玩家死亡只进入一次 Defeat，出现可点击结果 UI；重开后 `Time.timeScale == 1`、Player 为新实例、旧结果 UI 消失。
- 模拟全部波次完成进入 Victory。
- Pause 与 ParrySlowMotion/HitStop 嵌套释放后不会错误恢复。
- 重开后旧敌人、旧池根、旧 WaveSpawner 工厂和死亡回调均不存在；新场景可正常注册同名池。
- 保留现有 Offline 启动和场景重载生命周期测试。

## 验收标准

- EditMode、PlayMode、Pester 和资源完整性检查全部通过。
- 真实 Unity PlayMode 中可用 J 轻击杀死首个 Grunt，用 K 成功弹反可弹攻击。
- 不允许的重击、Dash、Parry 不扣耐力。
- 玩家死亡和清波均显示唯一结果层；Restart 能开始干净新战局。
- 场景中只有一个 EventSystem，暂停/结算按钮可交互。
- 离开/重载战斗后无旧事件订阅、终局 UI 或非 1 时间缩放残留。

## 非目标

- 不在 B1 接入五流派完整行为、元素/召唤升级、真实音效/动画或 Prefab 化。
- 不实现服务器权威战斗、战斗协议修改或 Online/MainMenu。
- 不复制公司项目的 XLua、AssetBundle、私有协议、配置或资源。
- 不在本阶段完成 Phase C 资源目录、Addressables 或低内存释放体系。
