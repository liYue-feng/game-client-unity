# Q版水墨战斗资源与逐帧动画设计

## 状态

- 日期：2026-07-22
- 设计范围：首批正式战斗美术资源（方案 B）
- 用户确认：所有最终资源由本项目重新生成，不使用临时占位图，不接受纸片式动画
- 目标分支：`master`

## 目标

本阶段把当前依赖色块和静态大图的战斗表现替换为可正式使用的 Q 版水墨资源，并让资源直接服务现有横版战斗状态机。完成后，玩家进入 `BattleScene` 应看到造型统一、背景透明、动作有重心变化的主角和四类敌人，并可正常完成移动、攻击、受击、死亡、Boss 战和通关结算。

核心目标如下：

1. 最终战斗图片全部重新生成；现有图片只作为画风和角色轮廓参考。
2. 主角与 Grunt 使用完整逐帧动作；Archer、Elite、Boss 具备与自身战斗行为对应的最小完整动作组。
3. 所有角色动作使用整帧重绘，不以整张静态图的平移、旋转、缩放或拆件骨骼摆动冒充动画。
4. 动画只消费现有战斗状态，不改变伤害、判定、前摇、有效帧、后摇、死亡或结算真值。
5. 生成过程、提示词、筛选结果、源图和最终图可追溯；任何占位图、带水印图、测试拼图或未验收生成物不得进入发布资源路径。

## 非目标

- 不把公司项目的美术文件、代码或受限制资源复制到本项目。
- 不在本阶段修改 Protobuf、后端协议或战斗数值。
- 不制作四向或八向角色动画。当前战斗是横版 2D，右向资源由现有 `SpriteRenderer.flipX` 镜像为左向。
- 不用 Spine、Live2D 或 Unity 2D Animation 的拆件骨骼方案替代逐帧动画。
- 不在本阶段全面重做主菜单、HUD、字体和音频；只处理完成战斗所需的角色、投射物和命中视觉资源。

## 当前资源问题

### 原始图片

- `Player.png`：造型方向可用，但人物与宣纸、水墨山石背景已合成，不适合作为直接运行时 Sprite。
- `TitleCharacter.png`：完整双人对战构图，适合作为菜单画面参考，不适合拆成战斗角色。
- `Boss.png`：墨团怪造型可用，但白色纸纹与半透明水墨边缘需要重新生成透明版本。
- `Grunt.png`：棋盘格和重复水印已烘入像素，禁止进入正式资源。
- `Archer.png`、`Elite.png`：当前为程序生成的低分辨率占位图，和目标画风不一致。

### Unity 导入与运行时

旧的 2048x2048 图片没有可靠的 `TextureImporter` Sprite 配置，`AiSpriteLoader` 加载失败时会退回 `PlaceholderSpriteFactory` 色块。当前角色只挂载单个 `SpriteRenderer`，没有动画表现组件；战斗逻辑已经提供玩家状态和敌人状态，可作为动画的唯一驱动来源。

## 方案选择

### 方案 A：完整逐帧资源与状态同步（采用）

为每个动作生成独立的透明 Sprite Sheet，每帧完整绘制角色。运行时组件监听状态切换，并按状态持续时间采样相应帧。该方案能表现脚步、重心、衣摆、武器弧线和墨迹残影的真实变化，最符合“不要纸片人”的要求。

### 方案 B：2D 骨骼拆件动画（不采用）

把头、躯干、四肢和武器拆开后旋转插值，生产较快，但关节容易僵硬，水墨轮廓也会在拆件边缘断开，直接产生纸偶感。

### 方案 C：静态图加程序抖动和粒子（不采用）

静态角色配合上下浮动、缩放、拖尾和墨点能改善截图，但角色轮廓本身没有动作变化，无法支撑三段攻击、弹反和 Boss 多招式。

## 视觉语言

### 统一风格

- Q 版比例：主角和人形敌人约 1:2.3 至 1:2.7 头身比。
- 横版 3/4 侧身视角，默认朝右，脚底落在统一基线。
- 黑、灰、宣纸白为主色，角色身份色只作局部点缀：主角冷灰，Grunt 暗青，Archer 竹绿，Elite 朱红与旧金，Boss 墨黑与少量猩红。
- 轮廓使用有粗细变化的毛笔线，内部使用可辨识的淡墨层次，避免大面积纯黑糊成一团。
- 动作中保留水墨飞白和少量墨迹拖尾，但不得遮住面部、武器方向或攻击预警。
- 禁止文字、签名、Logo、水印、假透明棋盘格和纸张背景进入透明角色资源。

### 非纸片动画标准

每个动作必须至少包含下列两类变化：

- 身体重心或躯干姿态发生可见变化。
- 脚步、手臂、武器或头部中的两个以上部位发生独立形变。
- 衣摆、发梢、袖口或墨迹产生跟随动作的次级运动。
- 攻击帧有准备、发力和回收，不得只有武器角度改变。
- 受击帧必须改变躯干弧线和面部方向；死亡帧必须完成倒地、散墨或消散过程。

整帧位置可以为保持脚底锚点而做微调，但不能通过移动整张静态图制造奔跑或攻击。

## 正式资源清单

### 主角 Player

动作集对应 `PlayerState`：

| 动作 | 目标帧数 | 播放规则 |
| --- | ---: | --- |
| Idle | 6 | 循环，呼吸、衣摆和握剑手轻微变化 |
| Run | 8 | 循环，完整左右脚落地与腾空节奏 |
| Attack1 | 6 | 单次，轻斩 |
| Attack2 | 6 | 单次，反向衔接斩 |
| Attack3 | 8 | 单次，终结斩并带墨弧 |
| HeavyAttack | 10 | 单次，蓄力、命中、回收完整 |
| Dash | 6 | 单次，压低重心并形成短墨迹残影 |
| Parry | 6 | 保持末帧直到状态结束 |
| ParrySuccess | 6 | 单次，明确的弹反反馈姿态 |
| Hurt | 4 | 单次，结束后保持末帧至状态切换 |
| Die | 10 | 单次，倒地或散墨后保持末帧 |

### Grunt

| 动作 | 目标帧数 | 对应行为 |
| --- | ---: | --- |
| Idle | 6 | `Idle` |
| Chase | 8 | `Chase` |
| Telegraph | 4 | 攻击前摇 |
| Slash | 6 | `grunt_slash` |
| Hurt | 4 | `Hurt` |
| Stunned | 4 | `Stunned` 循环 |
| Die | 8 | `Die` |

### Archer

| 动作 | 目标帧数 | 对应行为 |
| --- | ---: | --- |
| Idle | 6 | `Idle` |
| Chase | 8 | 调整射击距离 |
| Aim | 6 | `archer_shot` 前摇 |
| Shoot | 6 | 放箭与后坐 |
| Hurt | 4 | `Hurt` |
| Die | 8 | `Die` |

另生成透明箭矢和弹反后的高亮箭矢资源。箭头方向必须和真实飞行方向一致。

### Elite

| 动作 | 目标帧数 | 对应行为 |
| --- | ---: | --- |
| Idle | 6 | `Idle` |
| Chase | 8 | `Chase` |
| Telegraph | 6 | 普通连击前摇 |
| Combo | 10 | `elite_combo` |
| HeavyTelegraph | 8 | 重击前摇 |
| Heavy | 10 | `elite_heavy` |
| Hurt | 4 | `Hurt` |
| Stunned | 4 | `Stunned` 循环 |
| Die | 10 | `Die` |

### Boss

Boss 保留“Q 版墨团怪”语义，但重新生成有体积、可压缩和可展开的完整动作：

| 动作 | 目标帧数 | 对应行为 |
| --- | ---: | --- |
| Idle | 8 | 墨团呼吸和边缘晕染循环 |
| Chase | 8 | 有挤压、伸展和落地重量 |
| Telegraph | 8 | 通用危险蓄力，可按招式着色 |
| Charge | 10 | `boss_charge` |
| Slam | 10 | `boss_slam` |
| Aoe | 10 | `boss_aoe` |
| Slash | 10 | `boss_slash` |
| Hurt | 5 | `Hurt` |
| Stunned | 6 | `Stunned` 循环 |
| Die | 12 | 墨团坍缩、炸墨并消散 |

另生成 Boss 落地墨圈、冲锋墨痕和 AoE 墨印。攻击预警几何仍以现有 `EnemyAttackPlan` 为权威，图片只增强表现。

## 文件与追溯结构

生成源和 Unity 运行时资源分离：

```text
SourceArt/
  LegacyReference/             # 用户旧图，仅参考，不由 Unity 导入
  Generated/
    manifest.json              # 工具、日期、提示词、输入引用、输出哈希、筛选状态
    Player/
    Grunt/
    Archer/
    Elite/
    Boss/
    Effects/

Assets/Resources/CombatArt/
  Player/<Action>.png
  Enemies/Grunt/<Action>.png
  Enemies/Archer/<Action>.png
  Enemies/Elite/<Action>.png
  Enemies/Boss/<Action>.png
  Effects/<Effect>.png
```

旧图移动到 `SourceArt/LegacyReference` 后保留原始文件和哈希，不做水印清除。最终运行时路径中不得出现旧图、生成预览、失败候选或参考拼图。

`manifest.json` 对每个生成批次记录：

- 资源 ID、角色、动作和版本。
- 使用的生成工具与模型标识。
- 完整提示词和输入参考文件。
- 原始输出文件、处理后文件和 SHA-256。
- 透明化、裁切、缩放和切帧步骤。
- 人工/自动验收结果以及被拒绝原因。

## 生成与筛选流程

1. 使用 image generation skill 为每个角色建立一张正式造型基准图；角色基准先通过比例、轮廓、配色和武器方向检查。
2. 以已通过的基准图作为后续动作输入，按动作单独生成固定网格的连续关键帧，避免跨角色或跨动作一次性大拼图造成身份漂移。
3. 优先要求纯色抠图背景；生成工具不能可靠直接输出透明背景时，使用明确色键背景并在本地去色键，不能把棋盘格当作透明。
4. 自动检测画布尺寸、帧格数量、空帧、边界裁切、Alpha 覆盖、脚底基线和连续帧差异。
5. 视觉复核角色脸型、服饰、武器、左右手、手指数、轮廓连贯和动作逻辑。失败候选保留在 `SourceArt/Generated` 并标记 rejected，不进入 `Assets`。
6. 对通过的帧统一画布、透明边缘和脚底锚点后，输出最终 Sprite Sheet。

任何自动去背景都不得抹掉剑尖、发梢、衣袖飞白或 Boss 半透明墨缘。发现明显伪影时重新生成，不用涂抹修补掩盖问题。

## Unity 导入契约

- Player、Grunt、Archer、Elite 单帧画布：`256x256`。
- Boss 单帧画布：`384x384`。
- Sprite Mode：`Multiple`。
- Pivot：脚底中心 `(0.5, 0)`；特殊倒地帧仍保持同一世界基线。
- Pixels Per Unit：`256`，Boss 同样使用 `256` 以自然获得更大世界尺寸。
- Filter Mode：`Bilinear`。
- Wrap Mode：`Clamp`。
- Mip Maps：关闭。
- Alpha Is Transparency：开启。
- Texture Compression：`Uncompressed`，先保证水墨边缘和自动化像素证据稳定；发布包体优化另行评估。
- Max Size：至少覆盖整张 Sheet，最长边不超过 `4096`。
- 帧名称稳定为 `<Character>_<Action>_<FrameIndex:00>`。

导入设置由 Editor 工具配置并 `SaveAndReimport()`，不得继续提交手写的伪 `NativeFormatImporter` 元数据。

## 运行时架构

### CombatSpriteLibrary

新增只读资源库，按角色和动作加载 `Resources.LoadAll<Sprite>`，按帧名排序并缓存。加载失败、帧数不足、重名或导入设置错误时抛出明确错误；正式构建不允许静默回退到色块。

### PlayerCombatSpriteAnimator

监听 `PlayerStateMachine.OnStateChanged`，把 `PlayerState` 映射到动作集。循环动作按固定帧率播放；单次动作按状态权威持续时间归一化采样。攻击状态使用现有 `AttackTimeline` 的前摇、有效帧和后摇，确保刀光高潮与 Hitbox 有效期一致。

### EnemyCombatSpriteAnimator

监听 `EnemyBase` 的状态变化和本次 `EnemyAttackPlan.AttackId`。`Telegraph` 播放对应前摇，`Attack` 根据 `grunt_slash`、`archer_shot`、`elite_combo`、`elite_heavy`、`boss_charge`、`boss_slam`、`boss_aoe`、`boss_slash` 选择动作。对象回池时清除旧播放进度、颜色和末帧状态。

动画组件只写 `SpriteRenderer.sprite`。朝向仍由现有控制器维护，伤害、移动速度、位移、Collider、Hitbox 和状态转换继续由原组件负责。

## 降级与失败策略

- Editor 和测试环境发现正式资源缺失时直接失败并报告路径。
- 运行时开发构建可显示高可见度的错误 Sprite 并记录错误，但该降级只用于定位缺失，不是可发布资源。
- 发布验收要求 `PlaceholderSpriteFactory` 不再被 Player、Grunt、Archer、Elite、Boss 的正常加载路径消费。
- 任何动作缺帧时不得复制同一帧凑数；必须重新生成或明确缩小动作范围并重新评审。

## 测试与验收

### 静态与 EditMode

- 验证所有正式路径存在并具有唯一有效 GUID。
- 验证 Sprite Sheet 尺寸可被单帧尺寸整除，帧数符合清单。
- 验证 `Multiple`、PPU、Pivot、Filter、Wrap、MipMap、Alpha 和压缩设置。
- 验证连续帧不是完全相同文件或相同像素内容。
- 验证 Alpha 边界不贴画布边缘，剑尖、发梢、衣袖和 Boss 墨缘没有被裁断。
- 验证正式资源目录不存在文件名或清单状态为 placeholder、temp、preview、rejected 的文件。

### PlayMode

- 加载真实 `BattleScene`，确认五类角色均使用正式 Sprite，且世界尺寸与 Collider 合理。
- 驱动 Player 每个状态，确认状态切换会切换动作，循环和单次动作能正确结束。
- 驱动每种敌人攻击计划，确认前摇、命中高潮和恢复阶段按正确顺序显示。
- 受击、眩晕、死亡和对象回池再生成后，不保留旧帧、透明度或颜色。
- 左右移动时镜像正确，武器和投射物方向与 Hitbox/速度一致。
- 完成普通波次、Boss 击杀、通关结算和重新开始，战斗逻辑保持通畅。

### 视觉证据

- 在 `960x540` 和至少一个更窄窗口截取真实运行画面。
- 检查角色不与 HUD 重叠、脚底落地、透明边缘无白边、动作不裁切。
- 检查 Player、Grunt、Archer、Elite、Boss 在同一画面中的风格、比例和明度层级一致。
- 录制或逐帧截取主角奔跑、三连击、弹反、Grunt 攻击、Archer 射击、Elite 重击和 Boss 四招式，人工确认没有整图滑动的纸片感。

## 完成定义

本阶段只有同时满足以下条件才可声称完成：

1. 所有清单资源均由本阶段生成，提示词、源文件、哈希和筛选状态已留档。
2. 旧图和占位资源不在正式运行时加载路径中，带水印 Grunt 永不进入 `Assets/Resources/CombatArt`。
3. 主角和四类敌人的状态动画均能在真实战斗中播放，命中与状态时间一致。
4. 全部静态、EditMode、PlayMode、战斗闭环和视觉证据测试通过。
5. 真实完成 Boss 击杀并进入通关结算，重新开始后资源与动画状态干净。
6. 视觉复核未发现明显身份漂移、肢体错误、裁切、白边、假透明或纸片式动作。
