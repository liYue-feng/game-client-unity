# Phase B2 敌人战斗体验设计

## 目标

Phase B2 在 Phase B1 已验证的离线战斗闭环上，完成一条不依赖正式 Prefab、动画或在线服务的敌人体验竖切。完成后，玩家进入 `BattleScene` 应当稳定遇敌、看懂攻击、获得准确反馈，并能从波次 HUD 和 Boss HUD 判断当前目标。

本阶段固定交付五项能力：

1. 敌人始终出生在可接敌范围内，横向相机跟随玩家且不会越出战场。
2. 敌人池复用以不可变基础属性计算当前波次数据，不会因复用次数产生属性漂移。
3. 每次攻击先生成 `EnemyAttackPlan`，再进入 Telegraph；圆形或矩形预警与真实判定共享同一份几何数据，攻击协程有唯一所有者并可取消。
4. 命中反馈使用实际 HP 差值，统一驱动闪白、飘字、墨迹、卡帧和震屏；临时粒子只属于当前战局。
5. HUD 显示当前波次、剩余敌人和当前 Boss 的生命/阶段，并随战局生命周期正确解绑。

战斗规则继续以 B1 的 `Game.Gameplay`、`CombatHit`、`Hurtbox`、`BattleTimeController` 和 `BattleRunController` 为权威。公司项目只提供设计参照：借鉴“战斗真值只产生一次、表现分层消费、临时效果有明确生命周期”，不移植其 Lua、事件框架、对象池、Shader 或配置系统。

## 现状与根因

- `WaveSpawner` 位于世界坐标 x=6，但 `EnemySpawnEntry.spawnX` 又作为局部偏移叠加。部分右侧 Grunt 会出生在追击范围外，随后以默认方向继续远离玩家，真实波次可能无法结束。
- 主相机固定在世界原点。30 米地面宽于默认相机视野，玩家追敌时会离开画面；现有 B1 视觉测试通过临时重构镜头取证，没有验证正常游戏镜头。
- `ApplyWaveScaling` 从敌人当前 `maxHp`、`damage`、`moveSpeed` 再次乘倍率；同一实例回池后复用会复合增长。`EnemyBase.ResetForPool` 也没有恢复 Boss 狂暴、Archer 冷却或 Elite 攻击标志。
- `EnemyBase` 进入 Telegraph 后才依赖子类继续决定攻击。Elite 在前摇结束后才决定重击并修改前摇时间，因此配置的一秒重击预警实际不会发生；Elite/Boss 的连击协程还可能长于通用 Attack 状态。
- 当前前摇只改变敌人整身颜色，不能表达矩形冲锋、近战范围或圆形落地/AoE。协程被弹反、受击、死亡或回池打断时，颜色和后续命中也缺少统一清理边界。
- 玩家命中敌人时，事件携带的是请求伤害而不是防御结算后的 HP 差值；对敌飘字缺失。玩家虽然挂载 `HitEffectPlayer`，受伤链并未调用它。
- `InkParticlePool` 首次惰性创建会重复初始化，并使用 `DontDestroyOnLoad`。若旧玩家协程在 Restart 中被销毁，已取出的粒子可能跨战局残留或被重复归还。
- `WaveSpawner.OnWaveStart` 没有 HUD 消费者；`BossHPBar` 已存在但没有创建和绑定。当前 HUD 只能显示玩家 HP、耐力、经验和连击。

## 方案选择

### 方案 A：先增加更多特效和 HUD

该方案能快速改善截图，但无法解决敌人生成软锁、属性复合增长和攻击时序不一致。视觉会掩盖错误的战斗真值，后续还要返工。

### 方案 B：移植公司项目的战斗表现框架

公司项目已经具备 Lua 技能计划、复杂预警 Shader、统一飞字、镜头管理和目标 HUD，但这些能力依赖其配置表、Lua/C# 桥接、资源系统和事件总线。直接移植会把 Phase B2 扩大为框架迁移，并破坏当前小型 `Game.Gameplay` 边界。

### 方案 C：在现有权威上增量补齐五段体验

本阶段采用该方案。纯数据计算继续进入 `Game.Gameplay`；Unity 组件只负责场景适配和表现。先保证接敌与池复用确定性，再建立攻击计划，最后让反馈与 HUD 消费同一份已结算结果。这样既能形成最小可玩竖切，也不会提前进入 Phase C 的 Prefab 和资源工程化。

## 结构设计

### 1. 可靠接敌与相机跟随

#### 战场坐标权威

新增纯数据 `BattleArenaBounds` 和 `ArenaSpawnPlanner` 到 `Game.Gameplay`。`BattleArenaBounds` 只保存地面可用的 `MinX/MaxX`；`ArenaSpawnPlanner` 接收玩家 X、期望侧别、相机半宽、出生边距和敌人追击范围，返回最终世界坐标 X。

规划结果必须同时满足：

- 位于地面边界内，并为角色碰撞体保留安全边距。
- 出生点位于当前相机可视区外侧或边缘，避免贴脸出现。
- 出生点与玩家的距离不大于该敌人的有效接敌距离，Grunt 不会一出生就进入向外巡逻。
- 左右两侧使用同一算法；随机数只决定侧别，不再把随机局部偏移叠加到一个偏移后的 Spawner Transform。

当首选侧在战场边缘无法同时满足这些约束时，planner 先切换到另一侧。若战场窄到两侧都无法完全离开视区，则按“战场内且可接敌 > 远离玩家 > 视区外”的固定优先级选择最远合法点。相机可视宽度大于战场宽度时，Camera Rig 固定在战场中心。该降级规则保证边界输入也只有一个确定答案。

`WaveSpawner` 仍负责波次节奏，但每次出生都请求 planner 给出世界坐标。`EnemySpawnEntry` 继续描述类型和数量，不再把 `spawnX` 同时当作局部偏移和世界位置。`BattleSceneSetup` 把 `groundWidth`、玩家和相机参数显式配置给 Spawner。

#### 镜头组合

`BattleSceneSetup` 创建场景级 `[BattleCameraRig]`，Rig 在 `LateUpdate` 中只跟随玩家 X，并按 `BattleArenaBounds` 和相机半宽夹紧。`Main Camera` 作为 Rig 子物体保持稳定局部坐标；现有 `CameraShaker` 继续只修改 Camera 子物体的局部偏移。

这样拆分是为了避免“跟随脚本和震屏脚本同时写同一个 Transform”。Rig 拥有长期构图，`CameraShaker` 拥有短时偏移，两者销毁时都恢复自己的局部状态。

#### 生命周期

- Camera Rig、planner 配置和 WaveSpawner 都属于当前 `BattleScene`。
- 终局冻结时镜头保持最后构图，不再追随已禁用或死亡的 Player。
- Restart 后重新创建 Rig，并只绑定新战局 Player；旧相机跟随引用不得跨场景存活。

#### RED / GREEN 验收

- **EditMode RED**：用当前 `spawnerX + spawnX` 规则计算右侧首波位置，证明结果可超出 Grunt 的追击范围。
- **EditMode GREEN**：对左右侧、玩家位于边界附近、相机宽于剩余场地等边界表格测试，planner 返回值始终在战场内且可接敌；相同输入得到相同输出。
- **PlayMode RED**：加载真实 `BattleScene`，不冻结 AI、不搬运敌人，固定右侧出生后 Grunt 在限定时间内没有缩短与玩家的距离，或玩家移动后离开相机视口。
- **PlayMode GREEN**：左右各生成一只真实 Grunt，二者均在限定时间内缩短距离；玩家移动到地面两端时仍在相机安全视区，Camera Rig 中心不越出 arena clamp。

### 2. Immutable baseline 池复用与波次缩放

#### 不可变基础值

新增 `EnemyStatBaseline` 和 `EnemyWaveStats` 到 `Game.Gameplay`。baseline 至少包含 `MaxHp`、`Damage`、`MoveSpeed`、`DamageReduction`、默认 Telegraph 时长和默认攻击时长；构造后只读。

每种敌人在完成子类默认值配置后只捕获一次 baseline。`EnemyWaveScaling.Calculate(baseline, waveIndex, multipliers)` 是纯函数，波次 0 返回 baseline，波次 N 始终从 baseline 计算，禁止从当前运行时属性继续乘算。

baseline 的唯一初始化点固定在 `WaveSpawner.CreateEnemy`：对象保持 inactive，子类 `Awake` 完成默认值后，工厂显式调用一次 `InitializeCombatBaseline`。该方法执行现有一级属性换算、把初始 HP 归满，再捕获只读 baseline。`EnemyBase.Start` 不再重算属性。这样既保留现有属性换算，又不会在 ObjectPool 激活后覆盖当前波次结果。

#### 出池顺序

B2 不修改通用 `ObjectPool.Get` API。当前 Enemy 没有依赖池中旧状态的 `OnEnable` 行为，因此 `WaveSpawner` 在 `ObjectPool.Get` 返回后的同一调用栈立即调用 `PrepareForSpawn`，并保证它发生在首个 Physics2D step、`Start` 和 `Update` 之前。该窄约束避免为了现有敌人提前重构通用池；未来若 Enemy 必须在激活前初始化，再由 Phase C 统一设计 pool lease/activation 接口。

`PrepareForSpawn` 按以下顺序执行：

1. 取消旧攻击、前摇、受击和死亡协程。
2. 恢复 baseline，并应用一次当前波次 `EnemyWaveStats`。
3. 重置 HP、状态计时器、朝向、Sprite 基础色/透明度、Collider、Rigidbody 和死亡标志。
4. 调用子类 `ResetSubclassState`：Archer 清冷却，Elite 清连击/重击标志，Boss 清狂暴/攻击模式及其派生倍率。
5. 绑定本次出生的死亡回调和波次归属。

`WaveSpawner` 在完成上述步骤前不得 yield、启动攻击或把 Enemy 暴露给波次 HUD。B2 期间 Enemy 也不得新增读取运行时战斗状态的 `OnEnable`；首个 `Start`、Physics2D step 和 `Update` 必须观察到完整的新状态。

归还时先取消所有 enemy-owned 行为和反馈，再禁用并回到池根。`ResetSubclassState` 使用窄虚方法，而不是由 WaveSpawner 反射或判断具体敌人类型。

#### 生命周期

- baseline 与 Enemy 实例同寿命，但内容不可变。
- wave stats 只服务一次出生，不缓存成下一次计算的输入。
- WaveSpawner Dispose 先停止生成，再取消所有活跃 Enemy 的攻击，随后解绑死亡事件并清池。
- Boss 狂暴带来的速度、伤害、颜色和 Telegraph 变化只属于当前一次出生。

#### RED / GREEN 验收

- **EditMode RED**：连续以当前运行时数值应用两次同波次倍率，证明第二次结果大于 `baseline * multiplier`。
- **EditMode GREEN**：同一 baseline 在不同复用次数、不同调用顺序下计算同一波次，`EnemyWaveStats` 完全相等；零/负 waveIndex 归一为波次 0。
- **PlayMode RED**：让同一个 pooled Enemy 经历受击、颜色变化、Boss 狂暴或 Archer 冷却后回池，再次生成时至少一个字段保留旧值或继续复合增长。
- **PlayMode GREEN**：同一实例完成两次出池后，HP/伤害/速度严格等于各自波次的纯函数结果；颜色、速度、Collider、冷却、连击、狂暴和攻击协程均为新状态，并在首个 Physics2D step/Update 前完成准备。

### 3. PrepareAttackPlan、AttackTelegraphView 与攻击协程所有权

#### 先计划，再前摇

新增不可变 `EnemyAttackPlan` 到 `Game.Gameplay`，至少包含：

- `AttackId`
- `TelegraphDuration`
- `CommitDuration`
- `RecoveryDuration`
- `IsParryable`
- `Shape`：B2 只支持 `Circle` 与 `Box`
- `LocalOffset`、`Size` 或 `Radius`
- 攻击开始时冻结的 `FacingDirection` / `AimDirection`
- `HitCount` 与 `HitInterval`，单段攻击固定为 1 和 0
- `Damage` 与 `Knockback`

每个敌人实现 `PrepareAttackPlan()`。`EnemyBase` 只有在取得合法 plan 后才允许进入 Telegraph，并把该 plan 保存为本次攻击唯一快照。Elite 必须在 Telegraph 前决定三连或重击；Boss 必须在 Telegraph 前决定连斩、冲锋、跳劈或 AoE。Telegraph 期间不得再修改当前 plan 的时长、可弹反性或几何。

真实攻击判定直接读取 plan 的 `FacingDirection/AimDirection` 与 `LocalOffset/Size/Radius`。Archer 的 Projectile 也使用冻结的 AimDirection 发射，不在 Commit 时重新追踪已移动的 Player。表现和物理共用一份参数是为了防止“画出来的安全区”和实际受击范围不一致。

#### 独立预警视图

新增场景表现组件 `AttackTelegraphView`，不放入 `Game.Gameplay`。它只接收 plan 和攻击者 Transform：

- `Box` 使用程序化 SpriteRenderer 或 LineRenderer 绘制矩形范围。
- `Circle` 使用程序化圆环/填充绘制范围。
- 可弹反为藤黄色，不可弹反为朱砂红。
- 通过 plan 的 Telegraph 进度表现由淡到实；它不包含 Collider，也不能造成伤害。
- `Hide` 幂等，并恢复敌人自身 Sprite 的基础色。受击闪白不得把 Telegraph 色错误保存成长期基础色。

本阶段不实现扇形、环形、Shader 扫光或正式美术资源。Boss AoE/跳劈使用圆形，Grunt/Elite 近战和 Boss 冲锋使用矩形，Archer 使用窄矩形瞄准通道。

#### 唯一攻击协程

`EnemyBase` 持有 `_attackRoutine`，并提供受保护的 `StartOwnedAttack`、`CompleteOwnedAttack` 与 `CancelOwnedAttack`。子类不得直接启动脱离基类生命周期的攻击协程。

攻击状态流固定为：

```text
PrepareAttackPlan -> Telegraph -> Commit -> Recovery -> Chase
                           |          |
                           +-- cancel-+
```

- Telegraph 结束后隐藏预警，再进入 Commit。
- Commit 按 plan 约定执行一次或明确段数的命中；三连击的整个序列属于同一个 owned routine。
- 只有 routine 完成 Recovery 后才能回到 Chase，通用 `_stateTimer` 不得提前结束仍在执行的攻击。
- Hurt、Stunned、Die、OnDisable、回池、WaveSpawner Dispose 和 BattleRun 终局全部调用 `CancelOwnedAttack`。
- 弹反通过现有 `IParryResponder.OnParried` 进入 Stunned，并取消未提交的命中和连击余段；不得使用 `StopAllCoroutines` 误杀死亡淡出或其他独立生命周期。

#### RED / GREEN 验收

- **EditMode RED**：当前 Elite 在 Telegraph 结束后才修改重击前摇，当前 Attack 状态也短于三连击协程。
- **EditMode GREEN**：测试四类敌人的 plan 选择与归一化；同一 plan 的视觉几何和命中几何引用相同值；非法负时长/尺寸被归一或拒绝。
- **PlayMode RED**：驱动真实 Elite/Boss 状态，观察攻击在回到 Chase 后仍产生后续命中，或重击没有获得配置的完整预警。
- **PlayMode GREEN**：Grunt、Archer、Elite、Boss 均按 `Prepare -> Telegraph -> Commit -> Recovery` 顺序运行；Telegraph 内玩家不受伤，Commit 只产生约定命中；取消后无后续伤害且视图立即隐藏。
- **PlayMode GREEN**：对可弹和不可弹 plan 分别断言颜色、`IsParryable` 和真实 `CombatHit` 一致；圆/矩形视图的世界边界覆盖对应 Physics2D 判定边界。

### 4. 实际 HP 差值驱动的统一命中反馈与场景粒子生命周期

#### 单一结算结果

在 `Game.Gameplay` 新增不可变 `CombatHitOutcome`，保留 B1 的 `CombatHitResult` 语义，并增加 `AppliedDamage`。`AppliedDamage` 必须由目标 HP 的结算前后差值产生：

```text
AppliedDamage = max(0, hpBefore - hpAfter)
```

保留 B1 的兼容入口 `Hurtbox.ReceiveHit(CombatHit) -> CombatHitResult`。新增 `ResolveHit(CombatHit) -> CombatHitOutcome` 作为 B2 生产权威；`ReceiveHit` 只委托给 `ResolveHit` 并返回 `outcome.Result`，不复制第二套弹反、伤害或事件逻辑。现有反射测试和兼容调用因此不破坏，玩家 Hitbox、敌人攻击、Projectile、AutoWeapon 和 Summon 等生产调用者在 B2 内逐步迁移到 `ResolveHit`。Projectile 读取 `outcome.Result` 决定生命周期，表现层只读取 `AppliedDamage`。被弹反或忽略时 AppliedDamage 为 0，所有生产伤害最终都发布同一种已结算结果，禁止各自猜测显示值。

`CombatEvents` 增加 `OnHitResolved(CombatFeedbackContext)`。该场景事件 context 至少包含 source/target GameObject、命中位置、来源类别（玩家近战、武器、召唤、敌人、Projectile）、结果、实际伤害和反馈强度档位。现有 `OnHitLanded`、`OnDamageTaken`、`OnParrySuccess` 在迁移期保留为兼容事件，但同一个表现组件只能订阅新旧路径之一，不能双重播放。

#### 反馈消费

新增 scene-owned `CombatFeedbackController`，负责一次订阅并调度现有表现组件：

- 敌人受伤：目标 `HitEffectPlayer`、实际伤害飘字、`InkHitEffect`、近战墨线、现有 `HitStopController` 和 `CameraShaker`。
- 玩家受伤：玩家 `HitEffectPlayer`、实际伤害飘字、较强震屏和短 HitStop。
- 弹反：零伤害、单次弹反文字、较强 HitStop/震屏；现有 `IParryResponder` 回调仍由 `Hurtbox` 的 B1 权威路径执行。
- Heavy/Boss 档位只改变现有 CameraShaker/HitStop 参数，不新增第二套相机系统。

`BattleSceneSetup` 不再分别持有多组命中表现订阅；它只创建和配置 controller。战斗数值、死亡和波次逻辑不依赖表现是否存在，表现异常不得改变 `CombatHitOutcome`。

#### 粒子生命周期

`InkParticlePool` 改为 `BattleScene` 所有，移除 `DontDestroyOnLoad`，由 `BattleSceneSetup` 显式创建。初始化必须幂等，不能由 Instance getter 和 Awake 各预热一次。

每次 `Get` 返回带 generation 的 `InkParticleHandle`；`Return(handle)` 只有在 generation 仍匹配且粒子处于 leased 集合时才成功。这样旧协程即使晚到，也不能把已被复用的粒子重复塞回 available 队列。归还时清零 Rigidbody 速度、恢复颜色/缩放、停止粒子自身临时状态并重新挂回场景池根。

BattleRun 进入终局前，`CombatFeedbackController` 必须取消并归还当前短反馈；现有结果状态会把 time scale 设为 0，不能依赖 scaled-time 协程自然结束。Restart/OnDestroy 随后立即 invalidate 所有 handle 并销毁当前池。新战局只允许存在一个属于新 `BattleScene` 的 InkParticlePool。

#### RED / GREEN 验收

- **EditMode RED**：构造有减伤的敌人，证明当前事件伤害与实际 HP 差值不同；重复初始化当前 InkParticlePool 会产生超过 poolSize 的对象。
- **EditMode GREEN**：`CombatHitOutcome` 对 Damaged/Parried/Ignored 返回正确 AppliedDamage；generation 不匹配或重复 Return 均被忽略，available/leased 集合保持互斥。
- **PlayMode RED**：真实玩家命中后没有对敌飘字；真实玩家受伤时玩家 Sprite 不闪白；效果进行中 Restart 可观察到旧场景粒子或重复池。
- **PlayMode GREEN**：一次真实玩家命中产生一次敌人闪白、一次墨迹、一个等于目标 HP 差值的数字和一组受控镜头反馈；一次玩家受伤产生一次玩家闪白与准确数字。
- **PlayMode GREEN**：弹反不扣 HP，只产生一次弹反反馈；战斗反馈播放中 Restart 后，旧 handle/粒子/订阅全部失效，新场景只有一个当前池且没有旧数字或墨点。

### 5. Wave / Boss HUD

#### 事件模型

扩展 `WaveSpawner` 的场景事件，但不引入公司事件总线：

- `OnWaveStarted(currentWave, totalWaves)`：当前波次已准备并开始生成。
- `OnAliveEnemyCountChanged(aliveCount)`：每次成功出生和确认死亡后发布。
- `OnBossSpawned(Boss boss)`：Boss 完成出池重置和波次缩放后发布，确保 HUD 读取到最终 MaxHp。
- `OnBossRemoved(Boss boss)`：Boss 死亡、回池、Dispose 或战局终止时发布。

事件只描述当前战局事实。`BattleHUD` 不读取 `_aliveEnemies` 私有集合，也不通过 `FindObjectOfType` 每帧轮询。

#### HUD 组成

`BattleHUD` 在现有 Canvas 上增加紧凑的 `WaveObjectiveView`，显示 `波次 current/total` 与 `剩余 alive`。该视图属于战斗反馈内容，不进行 Phase C 的整体布局、主题或 Prefab 重做。

复用并改造现有 `BossHPBar`：

- `BindBoss` 时订阅 Boss 的 `OnHealthChanged`、`OnPhaseChanged` 和死亡/移除事件。
- 显示名称、当前/最大 HP 和阶段；当前 B2 只支持一个活动 Boss。
- Boss 进入第二阶段时通过事件更新文本，不再在 `Update` 中每帧轮询 HP。
- Boss 死亡、回池或绑定对象被替换时先解绑旧事件，再隐藏。

Boss 需要发布最小 `OnHealthChanged(current, max)` 和 `OnPhaseChanged(phase)`。事件由实际伤害和狂暴切换产生，UI 不自行用 50% 阈值重新推导另一份阶段真值。

#### 生命周期

- `BattleHUD.InitializeForBattle` 显式接收 WaveSpawner，并在 OnDisable/OnDestroy 幂等解绑。
- WaveSpawner Dispose 在清空自己的委托前先发布 Boss removed/目标结束，随后 HUD 自行解绑。
- BattleRun 终局停止更新计数，保留最后一帧目标信息直到结果 UI 出现。
- Restart 后新 HUD 只订阅新 Spawner/Boss；旧 HUD、BossHPBar 和 delegate 必须全部销毁。

#### RED / GREEN 验收

- **EditMode GREEN**：波次显示模型对 0-based 内部索引转换成 1-based 用户文本，空/越界总波次有明确归一规则；Boss 阶段事件只在阶段真正变化时发布一次。
- **PlayMode RED**：加载当前 BattleScene 后不存在波次/剩余敌人文本，BossHPBar 也不会随真实 Boss 出生出现。
- **PlayMode GREEN**：真实 WaveSpawner 开始波次、生成敌人、敌人死亡时，HUD 文本按事件顺序更新；计数不出现负数或重复扣减。
- **PlayMode GREEN**：真实 Boss 出生后 BossHPBar 可见且 MaxHp 为当前波次缩放值；伤害与阶段切换立即更新，死亡/回池后隐藏。
- **PlayMode GREEN**：连续 Restart 后只有一个 BattleHUD、一个 BossHPBar，每个新事件只触发一次；960x540 自动截图中波次区、Boss 血条、玩家状态区互不重叠。

## 统一生命周期与错误处理

`BattleSceneSetup` 是 B2 场景组合根，创建并连接 Arena/Camera、WaveSpawner、CombatFeedbackController、InkParticlePool 和 BattleHUD。推荐销毁顺序为：

1. `BattleRunController.Dispose` 关闭输入与终局订阅。
2. `WaveSpawner.Dispose` 停止生成，取消 Enemy-owned attacks，发布移除事件并解绑死亡回调。
3. `CombatFeedbackController.Dispose` 解绑 `CombatEvents`，阻止新临时反馈。
4. HUD 解绑 Wave/Boss 事件。
5. 场景池销毁并 invalidate 所有粒子 handle。
6. Camera Rig 与当前 Player 一起由场景销毁。

所有 Dispose/Cancel/Hide/Return 都必须幂等。缺少可选表现组件时记录一次警告并跳过表现，不得阻断伤害或波次；缺少 `BattleArenaBounds`、WaveSpawner、Hurtbox 等权威依赖时初始化应快速失败，而不是静默创建第二个 owner。

代码注释只写不明显的 WHY，重点覆盖以下边界：为什么 Spawn 使用世界坐标规划、为什么 baseline 不可变、为什么 plan 必须先于 Telegraph、为什么旧粒子 Return 需要 generation、为什么 HUD 使用事件而不是轮询。不要为字段赋值或简单条件添加叙述性注释。

## 测试与交付门禁

### RED 证据

每个实现任务先增加能在 `18b5acfd` 基线上稳定失败的最小测试，并记录失败原因。RED 必须验证真实缺口，不能依靠缺失类型或故意抛异常代替行为失败。

### GREEN 回归

- `Game.Gameplay` 的 planner、baseline scaling、attack plan、hit outcome 和粒子 generation 规则使用 EditMode 表格测试。
- 真实 `BattleScene` PlayMode 覆盖不搬运敌人的接敌、相机边界、同实例回池复用、四类敌人攻击状态、准确反馈、Wave/Boss HUD 和 Restart 清理。
- 现有 B1 EditMode、PlayMode、离线重载、结果 UI、弹反 Projectile、时间 token 和资源完整性测试必须全部继续通过。
- 增加至少两张 960x540 自动证据图：普通波次战斗与 Boss 前摇/HUD。测试删除旧输出，父级实际打开图片检查构图、预警范围、文本和重叠。
- 静态门禁继续保证只有 `BattleTimeController` 写 `Time.timeScale`，所有伤害仍通过 canonical CombatHit/Hurtbox 或明确迁移后的同一结算入口。
- 静态门禁要求生产伤害调用使用 `ResolveHit`；`ReceiveHit` 只允许作为兼容委托和 B1 契约测试入口存在。

## 验收标准

- 玩家无需寻找屏幕外失联敌人即可完成左右两侧波次，镜头始终保持有效战场构图。
- 同一个 pooled Enemy 的第 N 波属性与复用次数无关，派生状态和协程不会跨出生残留。
- Grunt、Archer、Elite、Boss 的预警颜色、形状、持续时间和真实判定一致；取消攻击后不再产生延迟命中。
- 飘字等表现显示实际 HP 差值；玩家受伤、敌人受伤和弹反各自只播放一次正确反馈。
- Restart 后不存在旧 Ink 粒子、旧 Camera follow target、旧 Enemy attack、旧 HUD 或重复事件订阅。
- 波次/剩余敌人和单 Boss HP/阶段均由事件更新，终局与 Restart 仍保持 B1 的唯一权威。

## 非目标

- 不实现 A4 Online、MainMenu、真实后端登录/存档联调、服务器权威战斗或战斗协议修改。
- 不进行 Phase C 的 Prefab 化、Animator/动画事件接入、Addressables/AssetBundle、资源目录重组、通用缓存或跨战局资源池设计。
- 不为 B2 改造通用 ObjectPool 的激活协议、lease API 或跨类型生命周期；Enemy 只在 WaveSpawner 当前调用栈内完成 `PrepareForSpawn`。
- 不移植公司项目的 Lua/C# 桥接、事件总线、对象池、配置表生成、Shader、材质或技能框架。
- 不实现扇形/环形/复杂组合预警、Cinemachine、DOTween、Boss 出场演出、正式音效、美术资源或 GPU 飘字。
- 不实现完整掉落经济、自动吸附、多 Boss HUD、完整目标类型系统、五流派重做、元素/召唤完整行为或敌人框架大迁移。
- 不把 `BattleSceneSetup`、全部 Gameplay 和 Presentation 一次性拆成新程序集；只增加五段竖切所需的窄边界。

## 实施证据（2026-07-20）

Phase B2 已按本设计完成实现和复审。最终行为包括确定性波次缩放与同实例复用、左右出生与战场相机、Grunt/Archer/Elite/Boss 冻结攻击计划、Telegraph/Commit/Recovery、实际 HP 差值反馈、场景内 Ink 生命周期、波次目标和单 Boss HUD。完整分支复审额外关闭了三个真实生命周期问题：Boss Commit 位移后的命中区域使用攻击开始矩阵；进入 Telegraph 前同步清零刚体线速度/角速度，避免下一个物理步拖动预警；`PoisonDot` 在敌人池租约结束和新租约准备时清空层数、计时器及来源。

任务提交（Task 1-6）为：

- Task 1 `d05cd073cca600cd2aaabe482bccab392d5f6be2`
- Task 2 `613487771d9a1c0c65bf0ac460da0c507d365460`
- Task 3 `c4b3e83445f8bfc2a36b2e4bdb28c6e3289f07f5`
- Task 4 `02396ace5562c2471a4ac667d7e52f311241b9a2`
- Task 5 `5bcf2a29c2c3ec8c1903ac1364a8540c8f0b9289`
- Task 6 `06b8c93d823b6961f590286bf6851635027b0fe5`
- Task 7 由包含本记录的提交交付；Git 提交无法稳定内嵌自身 SHA，因此不记录自引用 SHA，也不写占位值。

最终 TDD/回归证据：

- 基线行为 RED：`Logs/B2-task7-red-wave.xml` 命中 `B2_RED_WAVE_HUD`，`Logs/B2-task7-red-boss.xml` 命中 `B2_RED_BOSS_TELEGRAPH`，均为 `1/0/1`。
- 视觉所有权 RED：真实相机比例、HUD 布局、随机状态、反馈 ROI 和 Circle ROI 均有闭合 XML；最终质量修复 RED 为 `Logs/B2-task7-random-state-red.xml`、`Logs/B2-task7-feedback-pixels-red.xml`、`Logs/B2-task7-telegraph-pixels-red.xml`，均为 `1/0/1` 且命中精确 marker。
- 完整分支复审 RED：`Logs/B2-whole-review-boss-charge-red.xml`、`B2-whole-review-boss-slam-red.xml`、`B2-whole-review-telegraph-drift-red.xml`、`B2-whole-review-poison-lease-red.xml` 均为 `1/0/1`；对应单测 GREEN 均为 `1/1`。
- 最终 focused：`Logs/B2-task7-quality-visual-1.xml`、`-2.xml`、`-3.xml` 均为 `2/2`；`B2-final-review-core-green.xml` 为 `49/49`，`B2-final-review-enemy-green.xml` 为 `39/39`，`B2-final-review-combat-green.xml` 为 `37/37`。
- 最终 full：`Logs/B2-final-reviewed-full-editmode.xml` 为 `160/160`；`B2-final-reviewed-full-playmode-1.xml` 和 `-2.xml` 均为 `92/92`；`B2-final-reviewed-smoke.xml` 为 `3/3`。全部 skipped `0`、编译错误 `0`、native crash marker `0`，Unity 正常退出。
- `tools/validation/Test-UnityAssetIntegrity.ps1` PASS，Pester `5/5` PASS；静态门禁确认唯一 `Time.timeScale` 写入、canonical `ResolveHit`、Enemy-owned 协程、非持久 Ink pool、ObjectPool 基线无改动和 `git diff --check` 均通过。

父级最终打开并 APPROVED 两张精确 960x540 PNG：

- `Logs/phase-b2-wave-combat.png`：101674 bytes，opaque `518272`、dark `8473`、light `506624`、chromatic `73868`、colors `112`、variance `560.11`、Player `29.12px`、Grunt `24.27px`、damage `20`、ink `7`，SHA-256 `2AEABB48FDB548F7F8E3CA072B0ECB2AA5999CCC7B83250A0BC7A07B33B74DF0`。
- `Logs/phase-b2-boss-telegraph.png`：122543 bytes，opaque `518400`、dark `12998`、light `500312`、chromatic `86814`、colors `139`、variance `809.23`、Boss `48.54px`、Circle `485.39px`、radius `4.00`，SHA-256 `68B6022A192CE43FBF69EAB5265B7A695A52CE6F19AB84125445FE570DD37350`。

Task 7 规范审查 PASS，质量初审发现的两个 Important 已修复后复审 PASS，独立完整分支审查发现的三个 Important 已逐项 TDD 修复，最终复审 Critical/Important/Minor 均为 0。没有跳过任何要求的测试或审查；提交、推送、master fast-forward 和工作树清理由本证据提交之后的本地/远端 SHA 核验及最终交付报告证明。剩余产品范围仍为 Phase A4 Online/MainMenu/真实后端联调和 Phase C Prefab/Animator/资源加载/打包工程化。
