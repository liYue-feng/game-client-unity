# Q版水墨战斗资源与逐帧动画 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 使用可追溯的 GPT Image 生成资源替换当前战斗色块与静态大图，并让主角、Grunt、Archer、Elite、Boss 的逐帧动画正确消费现有横版战斗状态机，最终完成真实 Boss 击杀和通关结算。

**Architecture:** 仓库外层 `SourceArt` 保存旧参考图、生成源图、提示词、成本台账和验收记录，Unity 只导入 `Assets/Resources/CombatArt` 中通过验收的透明 Sprite Sheet。`CombatSpriteLibrary` 负责强校验加载，`SpriteSequencePlayer` 只负责帧采样，玩家和敌人各自的 Presenter 把现有状态映射为动画，不改写战斗真值。

**Tech Stack:** Unity 2022.3、C#、NUnit EditMode/PlayMode、Python 3 + Pillow、OpenAI imagegen skill CLI、`gpt-image-2`、PNG RGBA、PowerShell 5.1、Git。

## Global Constraints

- 只在 `E:/Own_project/game-client-unity` 工作，最终代码进入并推送 `master`。
- 书面设计以 `docs/superpowers/specs/2026-07-22-q-ink-combat-art-animation-design.md` 为权威。
- API 生成成本硬上限为 `$20.00 USD`；任何预留会使累计值超过上限时必须在 API 调用前失败。
- 使用 imagegen skill 的 CLI 回退，默认模型固定为 `gpt-image-2`；不得修改技能脚本 `C:/Users/23906/.codex/skills/.system/imagegen/scripts/image_gen.py`。
- Python 固定使用 `C:/Users/23906/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe`；当前 Pillow 已安装，Task 1 只补安装缺失的 `openai` 包。
- 正式输出使用 `quality=high`；仅角色风格校准允许 `quality=low`。
- `gpt-image-2` 不使用 `background=transparent`。透明角色和特效统一使用纯 `#ff00ff` 色键背景，再调用 imagegen skill 自带的 `remove_chroma_key.py`，不得自行替换该去背景工具；不需要透明通道的菜单标题场景使用完整水墨环境。
- 旧 `Player.png`、`TitleCharacter.png`、`Boss.png` 仅可作为风格参考；带水印的旧 `Grunt.png` 不得上传为生成输入，也不得进入正式资源。
- 正式资源不得包含文字、Logo、水印、假透明棋盘格、纸纹背景、临时预览、失败候选或复制帧。
- 所有动作是整帧重绘，禁止用整张静态图的平移、旋转、缩放或拆件骨骼摆动冒充动画。
- 横版只生成朝右资源，朝左继续使用现有 `SpriteRenderer.flipX`。
- Player、Grunt、Archer、Elite 单帧为 `256x256`；Boss 单帧为 `384x384`；PPU 均为 `256`。
- Sprite 导入固定为 Multiple、脚底中心 Pivot `(0.5, 0)`、Bilinear、Clamp、关闭 MipMap、Alpha Is Transparency、Uncompressed、最大尺寸 `4096`。
- 战斗逻辑、伤害、Collider、Hitbox、AttackTimeline、EnemyAttackPlan、波次、死亡和结算仍是权威；动画只写 `SpriteRenderer.sprite`。
- 每个任务执行 RED-GREEN-REFACTOR，经过规格审查与代码质量审查后单独提交；主控制器每完成一个任务推送一次 `origin/master`。

## File Structure

### 生成与追溯

- `SourceArt/LegacyReference/`：从 Unity 运行时路径迁出的用户旧图及原始 `.meta`，只保留参考和哈希。
- `SourceArt/Generated/prompt-catalog.json`：角色外观、动作说明、网格和最终路径的唯一生成清单。
- `SourceArt/Generated/manifest.json`：每次生成调用、提示词、模型、质量、输入、输出哈希和验收状态。
- `SourceArt/Generated/budget.json`：`$20` 硬上限与逐次预留成本。
- `SourceArt/Generated/<Role>/`：低质量候选、正式源图和 rejected 结果。
- `tools/art/imagegen_budget.py`：API 调用前原子预留成本并拒绝超额。
- `tools/art/render_combat_prompt.py`：从固定清单生成单次 CLI 提示词文本，不调用 API。
- `tools/art/build_combat_sheet.py`：把去色键后的等格图缩放、排列为 Unity Sprite Sheet。
- `tools/art/validate_combat_art.py`：校验 Alpha、尺寸、帧差异、边缘留白、清单和禁止文件名。
- `tools/art/tests/`：上述 Python 工具的 `unittest`。

### Unity Editor 与运行时

- `Assets/Editor/CombatSpriteSheetImporter.cs`：按清单切片并应用正式 TextureImporter 契约。
- `Assets/Editor/CombatSpriteSheetImporterTests.cs`：导入设置、切片名称、帧数和 Pivot 测试。
- `Assets/Scripts/Game/Visual/CombatSpriteClip.cs`：不可变帧序列和采样规则。
- `Assets/Scripts/Game/Visual/CombatSpriteLibrary.cs`：强校验加载并缓存动作资源。
- `Assets/Scripts/Game/Visual/SpriteSequencePlayer.cs`：循环、单次和保持末帧播放。
- `Assets/Scripts/Game/Visual/PlayerCombatSpritePresenter.cs`：PlayerState 到动作映射。
- `Assets/Scripts/Game/Visual/EnemyCombatSpritePresenter.cs`：EnemyState/AttackId 到动作映射。
- `Assets/Scripts/Game/BattleSceneSetup.cs`：创建玩家时挂载 Player Presenter。
- `Assets/Scripts/Game/Dungeon/WaveSpawner.cs`：创建敌人时挂载 Enemy Presenter。
- `Assets/Scripts/Game/Visual/AiSpriteLoader.cs`：保留标题/程序图形职责，移除五类战斗角色的正常色块回退。
- `Assets/Tests/EditMode/Visual/`：纯帧采样、映射和资源清单测试。
- `Assets/Tests/PlayMode/`：真实状态、对象池、战斗闭环和截图证据。

---

### Task 1: Cost Guard, Prompt Catalog, and Sprite-Sheet Tooling

**Files:**
- Create: `SourceArt/Generated/budget.json`
- Create: `SourceArt/Generated/manifest.json`
- Create: `SourceArt/Generated/prompt-catalog.json`
- Create: `tools/art/imagegen_budget.py`
- Create: `tools/art/render_combat_prompt.py`
- Create: `tools/art/build_combat_sheet.py`
- Create: `tools/art/validate_combat_art.py`
- Create: `tools/art/tests/test_imagegen_budget.py`
- Create: `tools/art/tests/test_render_combat_prompt.py`
- Create: `tools/art/tests/test_build_combat_sheet.py`
- Create: `tools/art/tests/test_validate_combat_art.py`

**Interfaces:**
- Consumes: `budget.json`, `manifest.json`, `prompt-catalog.json`,透明化后的等格 PNG。
- Produces: `reserve_budget(ledger_path, operation_id, estimate_usd)`、`render_prompt(catalog, asset_id)`、`build_sheet(source, rows, columns, frame_count, cell_size, output)`、`validate_asset(manifest_entry)`。

- [ ] **Step 1: Install the only missing CLI dependency**

Run:

```powershell
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m pip install openai
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -c "import openai, PIL; print(openai.__version__); print(PIL.__version__)"
```

Expected: both versions print; never print `OPENAI_API_KEY`.

- [ ] **Step 2: Write failing budget tests**

```python
def test_reserve_rejects_duplicate_and_hard_limit(self):
    with tempfile.TemporaryDirectory() as directory:
        ledger = Path(directory) / "budget.json"
        ledger.write_text('{"hard_limit_usd":"20.00","reservations":[]}', encoding="utf-8")
        reserve_budget(ledger, "player-idle-high", Decimal("0.28"))
        with self.assertRaisesRegex(BudgetError, "duplicate operation_id"):
            reserve_budget(ledger, "player-idle-high", Decimal("0.28"))
        with self.assertRaisesRegex(BudgetError, "hard limit"):
            reserve_budget(ledger, "overflow", Decimal("19.73"))
```

The test class derives from `unittest.TestCase` and uses `tempfile.TemporaryDirectory`. Also assert that writes use a temporary sibling plus `os.replace`, and that every amount is serialized as a decimal string with two fractional digits.

- [ ] **Step 3: Run budget RED**

```powershell
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m unittest tools.art.tests.test_imagegen_budget -v
```

Expected: FAIL because `tools.art.imagegen_budget` does not exist.

- [ ] **Step 4: Implement atomic budget reservation**

`budget.json` starts as:

```json
{
  "currency": "USD",
  "hard_limit_usd": "20.00",
  "pricing_snapshot_date": "2026-07-22",
  "reservations": []
}
```

The CLI is exact:

```powershell
python tools/art/imagegen_budget.py reserve --operation-id player-idle-high --estimate-usd 0.28
python tools/art/imagegen_budget.py status
```

`reserve_budget` parses all values with `Decimal`, rejects non-positive values and duplicate IDs, sums every prior reservation, rejects `new_total > hard_limit`, writes through `<ledger>.tmp`, then calls `os.replace`. `status` prints `reserved_usd`, `remaining_usd`, and the limit without printing secrets.

- [ ] **Step 5: Write prompt and sheet-tool RED tests**

Tests require:

```python
self.assertIn("perfectly flat solid #ff00ff chroma-key background", prompt)
self.assertIn("no watermark", prompt)
self.assertIn("complete redrawn body in every frame", prompt)
self.assertEqual(output.mode, "RGBA")
self.assertEqual(output.size, (frame_count * cell_size, cell_size))
self.assertGreater(frame_hash_count, 1)
```

Use synthetic 2x2 RGBA input with four different opaque shapes. Require packing order `top-left, top-right, bottom-left, bottom-right`, transparent corners, bottom-center placement, and one-pixel safety margin.

- [ ] **Step 6: Run tooling RED**

```powershell
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m unittest discover -s tools/art/tests -v
```

Expected: prompt, packer, and validator tests fail because the modules do not exist.

- [ ] **Step 7: Implement the prompt catalog and deterministic packer**

Every catalog entry contains exact fields:

```json
{
  "asset_id": "player-idle",
  "role": "Player",
  "action": "Idle",
  "frame_count": 6,
  "rows": 2,
  "columns": 3,
  "source_size": "1536x1024",
  "cell_size": 256,
  "target": "Assets/Resources/CombatArt/Player/Idle.png",
  "action_description": "subtle breathing cycle, weight shifts through hips and knees, sword hand and robe hem move independently, seamless loop"
}
```

`render_combat_prompt.py` combines the entry with these fixed invariants:

```text
Use case: stylized-concept
Asset type: production 2D side-scrolling game animation contact sheet
Primary request: create the exact action described by this catalog entry for the same character as Image 1
Style/medium: original Q-version Chinese ink-wash game sprite art, full raster redraw per frame
Composition/framing: exact equal grid declared by the catalog, chronological left-to-right then top-to-bottom, one complete full body centered in every cell, feet on one shared baseline, default facing right
Scene/backdrop: perfectly flat solid #ff00ff chroma-key background for local removal, no grid lines
Constraints: preserve face, proportions, costume, palette and weapon from Image 1; complete redrawn body in every frame; visible center-of-mass, limb, clothing and ink-trail changes; generous cell padding; no text; no watermark; no logo; no signature; no cast shadow; no paper texture; no checkerboard; do not use #ff00ff in the subject
Avoid: paper-doll motion, duplicated frames, cropped weapons, extra limbs, merged hands, camera movement, perspective changes, multiple characters in one cell
```

`build_combat_sheet.py` crops the exact grid using integer boundary interpolation, resizes each whole cell without per-frame content rescaling, centers it on a transparent square cell, and concatenates frames horizontally in chronological order. `validate_combat_art.py` rejects wrong dimensions, missing alpha, opaque corners, identical consecutive frames, edge-touching alpha, and forbidden filename tokens `placeholder|temp|preview|rejected`.

- [ ] **Step 8: Run tooling GREEN and CLI dry run**

```powershell
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m unittest discover -s tools/art/tests -v
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' 'C:\Users\23906\.codex\skills\.system\imagegen\scripts\image_gen.py' generate --prompt 'cost guard dry run' --quality low --size 1024x1024 --out tmp/imagegen/dry-run.png --dry-run
```

Expected: all tests PASS; dry run prints the `gpt-image-2` payload and performs no API call.

- [ ] **Step 9: Commit and push after two-stage review**

```powershell
git add SourceArt/Generated tools/art
git commit -m "build: add cost-guarded combat art pipeline"
git push origin master
```

---

### Task 2: Generate and Approve Five Character Anchors

**Files:**
- Create: `SourceArt/Generated/Player/anchor-*.png`
- Create: `SourceArt/Generated/Grunt/anchor-*.png`
- Create: `SourceArt/Generated/Archer/anchor-*.png`
- Create: `SourceArt/Generated/Elite/anchor-*.png`
- Create: `SourceArt/Generated/Boss/anchor-*.png`
- Modify: `SourceArt/Generated/manifest.json`
- Modify: `SourceArt/Generated/budget.json`

**Interfaces:**
- Consumes: user references `Player.png`, `TitleCharacter.png`, `Boss.png`; Grunt never consumes its watermarked old image.
- Produces: five accepted `anchor-high.png` identity references for later edit calls.

- [ ] **Step 1: Add failing anchor validation entries**

Manifest requires `Player`, `Grunt`, `Archer`, `Elite`, `Boss` anchors with `status=accepted`, SHA-256, model `gpt-image-2`, exact prompt path, output path, and visual review fields `identity`, `silhouette`, `palette`, `weapon`, `no_watermark` all `true`.

- [ ] **Step 2: Run anchor RED**

```powershell
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' tools/art/validate_combat_art.py --manifest SourceArt/Generated/manifest.json --group anchors
```

Expected: FAIL listing all five missing accepted anchors.

- [ ] **Step 3: Generate two low-cost candidates per role**

Before every call reserve `$0.05` with a unique ID. Use `image_gen.py edit`, `quality low`, `size 1024x1024`, and versioned output paths. Player uses old Player plus TitleCharacter as style references; Grunt, Archer, Elite use TitleCharacter only; Boss uses old Boss plus TitleCharacter. The full role prompts are:

```text
Player: same black-robed young swordsman identity, Q-version 1:2.5 head ratio, short tied black hair, readable silver jian, calm determined face, cold-gray accent.
Grunt: original cute hopping jiangshi enemy, torn dark-cyan robe, paper talisman without readable writing, stiff arms but bent knees and clear body volume, mischievous rather than horrific.
Archer: original Q-version bamboo-forest archer, layered black and bamboo-green travel robe, compact recurve bow, quiver visible, alert narrow silhouette.
Elite: original Q-version armored sword enforcer, dark armor, restrained cinnabar-red cloth and aged-gold fittings, broad grounded silhouette, heavy dao.
Boss: original rounded living ink-spirit boss, large expressive eyes, dense black core, translucent feathered ink edge, visible squash-and-stretch volume, small cinnabar corruption marks.
```

All prompts include one full-body neutral right-facing 3/4 side pose on flat `#ff00ff`, no shadow, no scenery, no text, no watermark, and no extra character.

- [ ] **Step 4: Inspect candidates and generate one high anchor per role**

Use `view_image` for every candidate. Reject any extra limbs, wrong weapon, cropped silhouette, identity drift, paper background, checkerboard, watermark, or weak outline. Reserve `$0.25` per high call, use only the best low candidate plus permitted user style reference as inputs, and save `anchor-high.png` without overwriting candidates.

- [ ] **Step 5: Remove chroma key and validate anchors**

```powershell
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' 'C:\Users\23906\.codex\skills\.system\imagegen\scripts\remove_chroma_key.py' --input SourceArt/Generated/Player/anchor-high.png --out SourceArt/Generated/Player/anchor-high-alpha.png --auto-key border --soft-matte --transparent-threshold 12 --opaque-threshold 220 --despill
```

Repeat for all five roles. Retry once with `--edge-contract 1` only when inspection finds a thin magenta fringe. Update manifest hashes and acceptance fields, then run the anchor validator.

- [ ] **Step 6: Commit and push accepted anchors**

```powershell
git add SourceArt/Generated
git commit -m "art: establish q ink combat character anchors"
git push origin master
```

---

### Task 3: Generate Production Player Animation Sheets

**Files:**
- Create: `SourceArt/Generated/Player/actions/*.png`
- Create: `Assets/Resources/CombatArt/Player/*.png`
- Modify: `SourceArt/Generated/manifest.json`
- Modify: `SourceArt/Generated/budget.json`

**Interfaces:**
- Consumes: accepted Player anchor and catalog entries.
- Produces: `Idle(6)`, `Run(8)`, `Attack1(6)`, `Attack2(6)`, `Attack3(8)`, `HeavyAttack(10)`, `Dash(6)`, `Parry(6)`, `ParrySuccess(6)`, `Hurt(4)`, `Die(10)`.

- [ ] **Step 1: Run Player asset RED**

```powershell
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' tools/art/validate_combat_art.py --manifest SourceArt/Generated/manifest.json --group player
```

Expected: FAIL listing eleven missing final sheets.

- [ ] **Step 2: Render exact prompts and reserve cost**

For each Player catalog ID, render `tmp/imagegen/<asset-id>.txt`, reserve `$0.30`, and verify `budget status` remains below `$20`. Grid sizes are exact: 4 frames `1024x1024` 2x2; 6 frames `1536x1024` 3x2; 8 frames `2048x1024` 4x2; 10 frames `2560x1024` 5x2.

- [ ] **Step 3: Generate high-quality action sources**

```powershell
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' 'C:\Users\23906\.codex\skills\.system\imagegen\scripts\image_gen.py' edit --model gpt-image-2 --image SourceArt/Generated/Player/anchor-high.png --prompt-file tmp/imagegen/player-idle.txt --no-augment --quality high --size 1536x1024 --out SourceArt/Generated/Player/actions/player-idle-source.png
```

Repeat with each catalog size and semantic output. Never pass `--force`; a retry uses `-v2.png` and a new reservation ID.

- [ ] **Step 4: Chroma-remove, pack, and inspect every action**

Run the installed chroma helper, then `build_combat_sheet.py` with catalog rows/columns/frame count and `cell-size 256`. Inspect source and final sheet with `view_image`; reject identity drift, duplicate poses, frozen torso, incorrect hand/weapon, cropped sword, inconsistent scale, or a whole-image translation masquerading as motion.

- [ ] **Step 5: Run Player GREEN**

```powershell
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' tools/art/validate_combat_art.py --manifest SourceArt/Generated/manifest.json --group player
```

Expected: PASS with 76 total Player frames and no forbidden resources.

- [ ] **Step 6: Commit and push Player art**

```powershell
git add SourceArt/Generated Assets/Resources/CombatArt/Player
git commit -m "art: add production player frame animation"
git push origin master
```

---

### Task 4: Generate Grunt and Archer Animation Sheets

**Files:**
- Create: `SourceArt/Generated/Grunt/actions/*.png`
- Create: `SourceArt/Generated/Archer/actions/*.png`
- Create: `Assets/Resources/CombatArt/Enemies/Grunt/*.png`
- Create: `Assets/Resources/CombatArt/Enemies/Archer/*.png`
- Create: `Assets/Resources/CombatArt/Effects/Arrow.png`
- Create: `Assets/Resources/CombatArt/Effects/ArrowDeflected.png`
- Modify: `SourceArt/Generated/manifest.json`
- Modify: `SourceArt/Generated/budget.json`

**Interfaces:**
- Produces Grunt `Idle(6), Chase(8), Telegraph(4), Slash(6), Hurt(4), Stunned(4), Die(8)` and Archer `Idle(6), Chase(8), Aim(6), Shoot(6), Hurt(4), Die(8)` plus two single-frame arrow resources.

- [ ] **Step 1: Verify RED for both groups**

Run the validator with `--group grunt` and `--group archer`; both must fail with exact missing action IDs.

- [ ] **Step 2: Generate, reserve, and version every source**

Use the same high-quality edit command as Task 3 with the accepted role anchor, `$0.30` reservation per call, exact catalog size, and versioned retry names. Grunt Slash must show preparation, body lunge, both feet/robe reacting, contact, and recovery. Archer Aim/Shoot must show bow draw weight, string release, torso recoil, sleeve and quiver movement; the bow cannot merely rotate as one rigid cutout.

- [ ] **Step 3: Generate arrows as original flat game assets**

Use text generation, `1024x1024 low` for candidate and `1024x1024 high` for final. Normal arrow uses black bamboo/iron; deflected arrow keeps identical silhouette with a cold cyan-white ink glow. Both face right, have flat magenta background, no shadow or text, and remain readable at 64 pixels.

- [ ] **Step 4: Remove chroma, pack, inspect, and validate**

Build 256-cell strips, update manifest SHA-256 and acceptance checks, then require both validators to pass. Explicitly scan final Grunt outputs for watermark-like repeated diagonals and reject any match seen during visual inspection.

- [ ] **Step 5: Commit and push**

```powershell
git add SourceArt/Generated Assets/Resources/CombatArt/Enemies/Grunt Assets/Resources/CombatArt/Enemies/Archer Assets/Resources/CombatArt/Effects
git commit -m "art: add grunt and archer frame animation"
git push origin master
```

---

### Task 5: Generate Elite, Boss, and Ink Attack Effects

**Files:**
- Create: `SourceArt/Generated/Elite/actions/*.png`
- Create: `SourceArt/Generated/Boss/actions/*.png`
- Create: `SourceArt/Generated/Effects/*.png`
- Create: `Assets/Resources/CombatArt/Enemies/Elite/*.png`
- Create: `Assets/Resources/CombatArt/Enemies/Boss/*.png`
- Create: `Assets/Resources/CombatArt/Effects/PlayerSlash.png`
- Create: `Assets/Resources/CombatArt/Effects/InkHit.png`
- Create: `Assets/Resources/CombatArt/Effects/BossChargeTrail.png`
- Create: `Assets/Resources/CombatArt/Effects/BossSlamMark.png`
- Create: `Assets/Resources/CombatArt/Effects/BossAoeMark.png`
- Create: `SourceArt/Generated/Menu/title-scene.png`
- Create: `Assets/Resources/CombatArt/Menu/TitleCharacter.png`
- Modify: `SourceArt/Generated/manifest.json`
- Modify: `SourceArt/Generated/budget.json`

**Interfaces:**
- Produces Elite `Idle(6), Chase(8), Telegraph(6), Combo(10), HeavyTelegraph(8), Heavy(10), Hurt(4), Stunned(4), Die(10)`.
- Produces Boss `Idle(8), Chase(8), Telegraph(8), Charge(10), Slam(10), Aoe(10), Slash(10), Hurt(5), Stunned(6), Die(12)` with 384-cell final sheets.

- [ ] **Step 1: Run Elite/Boss/effects RED**

Expected: validator lists nineteen missing animation sheets and five missing effects.

- [ ] **Step 2: Generate Elite sheets**

Use exact catalog prompts and accepted Elite anchor. `Combo` must contain distinct multi-hit body mechanics, while `Heavy` must show grounded preparation and large follow-through. Telegraph actions visibly store force but do not show contact.

- [ ] **Step 3: Generate Boss sheets**

Use 384 target cells. `Charge` stretches horizontally with compressed lead edge; `Slam` rises then spreads weight into the floor; `Aoe` expands the ink body radially; `Slash` forms one readable directed ink limb; `Die` collapses and disperses over twelve frames. Eyes and cinnabar marks preserve identity across every action.

- [ ] **Step 4: Generate five matching effect assets**

Effects are separate centered assets on flat magenta: a right-facing crescent Player slash, compact impact splash, long horizontal Boss charge trail, circular Boss slam mark, and wider concentric Boss AoE seal. They contain no characters, text, paper, floor plane, or shadows.

- [ ] **Step 5: Generate the replacement menu title scene**

Reserve `$0.35` and use `gpt-image-2 edit`, `quality high`, `size 2048x1152`, with old `TitleCharacter.png` only as a composition/style reference and accepted Player/Boss anchors as identity references. Generate an original Q-version ink-wash rainy confrontation scene with the accepted black-robed swordsman facing the accepted ink-spirit Boss, full environment, readable silhouettes, safe central negative space for the existing title UI, no text, no logo, no watermark, and no copied pixels from the reference. Save the opaque final at `Assets/Resources/CombatArt/Menu/TitleCharacter.png`; add a `kind=menu_scene` catalog entry and record prompt, inputs and SHA-256 in the manifest.

- [ ] **Step 6: Remove chroma, pack, inspect, and validate**

Run per-role validators and inspect all Boss source grids at original resolution. Confirm translucent ink fringes survive alpha extraction without magenta contamination. If chroma removal cannot preserve the fringe after one `edge-contract` retry, stop before any `gpt-image-1.5` call and request explicit native-transparency approval.

- [ ] **Step 7: Commit and push**

```powershell
git add SourceArt/Generated Assets/Resources/CombatArt/Enemies/Elite Assets/Resources/CombatArt/Enemies/Boss Assets/Resources/CombatArt/Effects Assets/Resources/CombatArt/Menu
git commit -m "art: add elite boss and combat ink effects"
git push origin master
```

---

### Task 6: Import Contract, CombatSpriteLibrary, and Frame Player

**Files:**
- Create: `Assets/Editor/CombatSpriteSheetImporter.cs`
- Create: `Assets/Editor/CombatSpriteSheetImporterTests.cs`
- Create: `Assets/Scripts/Game/Visual/CombatSpriteClip.cs`
- Create: `Assets/Scripts/Game/Visual/CombatSpriteLibrary.cs`
- Create: `Assets/Scripts/Game/Visual/SpriteSequencePlayer.cs`
- Create: `Assets/Tests/EditMode/Visual/CombatSpriteClipTests.cs`
- Create: `Assets/Tests/PlayMode/CombatSpriteLibraryTests.cs`

**Interfaces:**
- `CombatSpriteClip(string id, IReadOnlyList<Sprite> frames, bool loop, bool holdLast)`.
- `Sprite SampleLoop(float elapsedSeconds, float framesPerSecond)`.
- `Sprite SampleNormalized(float normalizedTime)`.
- `CombatSpriteLibrary.LoadRequired(string resourcePath, int expectedFrameCount, bool loop, bool holdLast)`.
- `SpriteSequencePlayer.Play(CombatSpriteClip clip, float durationSeconds)` and `ResetPlayback()`.

- [ ] **Step 1: Write failing pure sampling tests**

Require normalized time clamps to `[0,1]`, loop wraps without selecting an out-of-range frame, one-shot holds the last frame, zero duration selects frame zero, and changing clips resets elapsed time.

- [ ] **Step 2: Run EditMode RED**

```powershell
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -projectPath 'E:\Own_project\game-client-unity' -runTests -testPlatform EditMode -testFilter 'Game.Tests.EditMode.Visual.CombatSpriteClipTests' -testResults 'Logs\combat-sprite-clip-red.xml' -logFile 'Logs\combat-sprite-clip-red.log'
```

Expected: FAIL because types do not exist.

- [ ] **Step 3: Implement immutable clip and frame player**

`SampleNormalized` computes `index = Min(frameCount - 1, FloorToInt(Clamp01(t) * frameCount))`. `SampleLoop` uses positive modulo over `FloorToInt(Max(0, elapsed) * fps)`. `SpriteSequencePlayer.Update()` writes only the sampled Sprite and never mutates transform, color, collider, state, or time scale.

- [ ] **Step 4: Write importer and resource RED tests**

For every animation catalog target, require `TextureImporterType.Sprite`, Multiple, PPU 256, Bilinear, Clamp, no mipmaps, uncompressed, correct cell count, stable frame names, pivot `(0.5,0)`, and `Resources.LoadAll<Sprite>` sorted by numeric suffix. For `kind=menu_scene`, require Single, PPU 100, Bilinear, Clamp, no mipmaps and `Resources.Load<Sprite>("CombatArt/Menu/TitleCharacter")`.

- [ ] **Step 5: Implement importer and strict library**

`CombatSpriteSheetImporter.ImportAll()` reads the committed catalog. Animation entries create one `SpriteMetaData` per horizontal cell, name it `<Role>_<Action>_<index:00>`, call `SaveAndReimport`, and assert load count; the menu scene is configured as one Single Sprite. `CombatSpriteLibrary` throws `InvalidOperationException` with resource path and expected/actual counts; it never returns `PlaceholderSpriteFactory` output.

- [ ] **Step 6: Run GREEN**

Run focused EditMode and PlayMode filters, then `tools/art/validate_combat_art.py --all`. Expected: all pass.

- [ ] **Step 7: Commit and push**

```powershell
git add Assets/Editor Assets/Scripts/Game/Visual Assets/Tests/EditMode/Visual Assets/Tests/PlayMode
git commit -m "feat: load validated combat sprite sequences"
git push origin master
```

---

### Task 7: Bind PlayerState to Full Frame Animation

**Files:**
- Create: `Assets/Scripts/Game/Visual/PlayerCombatSpritePresenter.cs`
- Create: `Assets/Tests/PlayMode/PlayerCombatSpritePresenterTests.cs`
- Modify: `Assets/Scripts/Game/BattleSceneSetup.cs`

**Interfaces:**
- Consumes: `PlayerStateMachine.OnStateChanged`, public state durations, `SpriteSequencePlayer`.
- Produces: `CurrentActionId`, `CurrentFrameIndex`, and exact state-to-resource mapping for tests.

- [ ] **Step 1: Write Player Presenter RED tests**

Create a real Player object with `SpriteRenderer`, `CharacterStats`, `PlayerStateMachine`, and Presenter. Drive Idle, Run, Attack1/2/3, HeavyAttack, Dash, Parry, ParrySuccess, Hurt, Die; assert expected action ID, changing frames, loop/hold behavior, and unchanged transform/collider/hitbox values.

- [ ] **Step 2: Run RED**

Expected: Presenter type missing and `BattleSceneSetup` does not attach it.

- [ ] **Step 3: Implement mapping and duration authority**

Map every enum value exactly to the design action name. Attack durations use `attackDurations[0..3]`; Dash uses `CharacterStats.dashDuration`; Hurt uses `hurtDuration`; Parry and ParrySuccess hold until state change; Idle and Run loop at catalog FPS. Subscribe in `OnEnable`, unsubscribe in `OnDisable`, and reset to current state on reuse.

- [ ] **Step 4: Attach Presenter after SpriteRenderer creation**

`BattleSceneSetup.CreatePlayer()` adds `SpriteSequencePlayer` and `PlayerCombatSpritePresenter` after `PlayerStateMachine` exists. Initial Sprite comes from required `CombatArt/Player/Idle` frame zero, not `AiSpriteLoader.PlayerSprite()`.

- [ ] **Step 5: Run Player GREEN and existing combat tests**

Run focused Presenter test plus `BattleCombatLoopTests`, `BattleSceneOfflineSmokeTests`, and `OnlineBattleCompletionTests`. Expected: all pass and no battle truth changes.

- [ ] **Step 6: Commit and push**

```powershell
git add Assets/Scripts/Game/Visual/PlayerCombatSpritePresenter.cs Assets/Scripts/Game/BattleSceneSetup.cs Assets/Tests/PlayMode/PlayerCombatSpritePresenterTests.cs
git commit -m "feat: animate player from combat state"
git push origin master
```

---

### Task 8: Bind EnemyState and AttackId to Full Frame Animation

**Files:**
- Create: `Assets/Scripts/Game/Visual/EnemyCombatSpritePresenter.cs`
- Create: `Assets/Tests/PlayMode/EnemyCombatSpritePresenterTests.cs`
- Modify: `Assets/Scripts/Game/Dungeon/WaveSpawner.cs`

**Interfaces:**
- Consumes: `EnemyBase.CurrentState`, `EnemyBase.CurrentAttackPlan.AttackId`, `EnemyBase.CurrentAttackPhase`.
- Produces: role/action mapping with clean object-pool reset.

- [ ] **Step 1: Write enemy mapping and pool RED tests**

Test all mappings: `grunt_slash`, `archer_shot`, `elite_combo`, `elite_heavy`, `boss_charge`, `boss_slam`, `boss_aoe`, `boss_slash`; also Idle/Patrol, Chase, Telegraph, Hurt, Stunned, Die. Disable and reactivate a pooled enemy, then assert frame index, alpha, color and action reset.

- [ ] **Step 2: Run RED**

Expected: Presenter missing and spawned enemies still use `AiSpriteLoader` static sprites.

- [ ] **Step 3: Implement polling presenter without changing combat state ownership**

Presenter observes `(CurrentState, CurrentAttackPlan.AttackId, CurrentAttackPhase)` each Update and changes clips only when the tuple changes. It maps Patrol to Chase, uses AttackId-specific Telegraph/Attack where available, uses Hurt/Stunned/Die one-shots, and calls `ResetPlayback` in `OnDisable` and `OnEnable`. Recovery holds the attack last frame until Chase.

- [ ] **Step 4: Attach role configuration in WaveSpawner**

Each enemy creation adds one `SpriteSequencePlayer` and one `EnemyCombatSpritePresenter`, configured with exact role name. Initial Sprite is required Idle frame zero. Remove `AiSpriteLoader.GruntSprite/ArcherSprite/EliteSprite/BossSprite` assignments from normal enemy creation.

- [ ] **Step 5: Run GREEN and enemy experience regression**

Run Presenter tests, `BattleEnemyExperienceTests`, `BattleEnemyVisualEvidenceTests`, and `BattleCombatLoopTests`. Expected: attack timing and pooling pass unchanged while frames visibly change.

- [ ] **Step 6: Commit and push**

```powershell
git add Assets/Scripts/Game/Visual/EnemyCombatSpritePresenter.cs Assets/Scripts/Game/Dungeon/WaveSpawner.cs Assets/Tests/PlayMode/EnemyCombatSpritePresenterTests.cs
git commit -m "feat: animate enemies from combat plans"
git push origin master
```

---

### Task 9: Archive Legacy Runtime Art and Remove Publishable Placeholders

**Files:**
- Move: `Assets/Resources/Sprites/Characters/Player.png` -> `SourceArt/LegacyReference/Player.png`
- Move: `Assets/Resources/Sprites/Characters/TitleCharacter.png` -> `SourceArt/LegacyReference/TitleCharacter.png`
- Move: `Assets/Resources/Sprites/Enemies/Grunt.png` -> `SourceArt/LegacyReference/Grunt-watermarked.png`
- Move: `Assets/Resources/Sprites/Enemies/Boss.png` -> `SourceArt/LegacyReference/Boss.png`
- Move: `Assets/Resources/Sprites/Enemies/Archer.png` -> `SourceArt/LegacyReference/Archer-placeholder.png`
- Move: `Assets/Resources/Sprites/Enemies/Elite.png` -> `SourceArt/LegacyReference/Elite-placeholder.png`
- Modify: `Assets/Editor/CombatAssetGenerator.cs`
- Modify: `Assets/Scripts/Game/Visual/AiSpriteLoader.cs`
- Modify: `Assets/Tests/PlayMode/GeneratedCombatResourceTests.cs`
- Create: `tools/validation/CombatArtRelease.Tests.ps1`
- Modify: `docs/combat-resource-gap-report.md`

**Interfaces:**
- Produces: runtime path with no old/watermarked/generated-placeholder character resources and a recoverable reference archive.

- [ ] **Step 1: Write release RED tests**

Pester requires no PNG directly below old character/enemy runtime paths, no five battle role methods in `AiSpriteLoader`, no forbidden token in `Assets/Resources/CombatArt`, valid manifest hash for every PNG, and exact accepted frame counts.

- [ ] **Step 2: Run RED**

Expected: old PNGs and old AiSpriteLoader role methods are reported.

- [ ] **Step 3: Move legacy files and preserve evidence**

Use `git mv` for PNG and `.meta` pairs. Record original SHA-256 in `SourceArt/LegacyReference/README.md`; state explicitly that `Grunt-watermarked.png` is reference-only and was not used as image generation input.

- [ ] **Step 4: Remove old runtime APIs and update tests/docs**

Delete five combat-role caches/load methods from `AiSpriteLoader`; point its title cache to `CombatArt/Menu/TitleCharacter` and preserve ground/panel/ink procedural APIs still consumed by menus and UI. Remove Archer/Elite PNG generation from `CombatAssetGenerator.GenerateAll` while preserving the committed SoundCatalog WAV workflow, so running the editor menu cannot recreate retired placeholders. Rewrite `GeneratedCombatResourceTests` around `CombatSpriteLibrary`. Update gap report to mark formal character art and animation complete while leaving licensed audio/font gaps truthful.

- [ ] **Step 5: Run release GREEN and asset integrity**

```powershell
powershell.exe -NoProfile -Command "Invoke-Pester -Script tools/validation/CombatArtRelease.Tests.ps1 -EnableExit"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-UnityAssetIntegrity.ps1
```

Expected: PASS; no duplicate or unresolved GUID.

- [ ] **Step 6: Commit and push**

```powershell
git add Assets SourceArt tools/validation docs/combat-resource-gap-report.md
git commit -m "refactor: retire legacy combat placeholders"
git push origin master
```

---

### Task 10: Full Combat, Visual Evidence, and Release Verification

**Files:**
- Modify: `Assets/Tests/PlayMode/BattleVisualEvidenceTests.cs`
- Modify: `Assets/Tests/PlayMode/BattleEnemyVisualEvidenceTests.cs`
- Create: `Assets/Tests/PlayMode/CombatAnimationEvidenceTests.cs`
- Create after tests: `Logs/q-ink-combat-960x540.png`
- Create after tests: `Logs/q-ink-combat-narrow.png`
- Create after tests: `Logs/q-ink-animation-evidence/`
- Modify: `docs/combat-resource-gap-report.md`

**Interfaces:**
- Produces: executable proof that real resources animate through normal battle, Boss death, settlement, and restart.

- [ ] **Step 1: Write evidence RED assertions**

Require all five roles to render non-empty alpha bounds, occupy expected projected heights, stay off HUD, change at least 64 pixels between two frames of Run/Chase/Attack, and keep foot baseline within four screen pixels. Capture Player combo/parry, Grunt Slash, Archer Shoot, Elite Heavy, and all four Boss attacks.

- [ ] **Step 2: Run focused RED**

Expected: missing new evidence capture or insufficient frame deltas until every integration task is present.

- [ ] **Step 3: Run focused GREEN at two viewports**

Capture `960x540` and `720x540`. Assert no blank frame, clipping, magenta fringe, white rectangle, checkerboard, watermark-like repeated overlay, or UI overlap. Use real camera and real `BattleScene`; do not replace Sprites during the evidence test.

- [ ] **Step 4: Run the complete Unity and static suites**

```powershell
powershell.exe -NoProfile -Command "Invoke-Pester -Script tools/validation/*.Tests.ps1 -EnableExit"
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -projectPath 'E:\Own_project\game-client-unity' -runTests -testPlatform EditMode -testResults 'Logs\q-ink-editmode.xml' -logFile 'Logs\q-ink-editmode.log'
& 'D:\Unity_Soft\2022\Editor\Unity.exe' -batchmode -projectPath 'E:\Own_project\game-client-unity' -runTests -testPlatform PlayMode -testResults 'Logs\q-ink-playmode.xml' -logFile 'Logs\q-ink-playmode.log'
```

Expected: all suites pass with zero failure/error.

- [ ] **Step 5: Prove the complete battle flow**

Run `BattleSceneOfflineSmokeTests`, `BattleCombatLoopTests`, `BattleEnemyExperienceTests`, `OnlineBattleCompletionTests`, and the new animation evidence test together. Require normal waves, Boss kill, dungeon result, protobuf settlement path, and restart with clean frame/color/alpha state.

- [ ] **Step 6: Manually inspect all saved evidence**

Use `view_image` on both full-scene captures and representative action frames. Reject any visible paper-doll movement, identity drift, extra limb, bad weapon direction, hard cutout halo, foot sliding, UI overlap, or inconsistent role scale. Regeneration remains subject to the `$20` ledger; never exceed it silently.

- [ ] **Step 7: Update report, verify Git, commit, and push**

```powershell
git diff --check
git status --short
git add Assets/Tests docs/combat-resource-gap-report.md
git commit -m "test: verify production q ink combat experience"
git push origin master
git status --short
git log -10 --oneline --decorate
```

Expected: final worktree clean, `master` equals `origin/master`, latest ten commits show one reviewed delivery per task, and the report links the final evidence paths.
