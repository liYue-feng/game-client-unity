# Task 1 Report: Cost Guard, Prompt Catalog, and Sprite-Sheet Tooling

Status: complete with operational concerns noted below.

## Changed Files

- `SourceArt/Generated/budget.json`
- `SourceArt/Generated/manifest.json`
- `SourceArt/Generated/prompt-catalog.json`
- `tools/art/imagegen_budget.py`
- `tools/art/render_combat_prompt.py`
- `tools/art/build_combat_sheet.py`
- `tools/art/validate_combat_art.py`
- `tools/art/tests/test_imagegen_budget.py`
- `tools/art/tests/test_render_combat_prompt.py`
- `tools/art/tests/test_build_combat_sheet.py`
- `tools/art/tests/test_validate_combat_art.py`

## RED Evidence

```powershell
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m unittest tools.art.tests.test_imagegen_budget -v
```

Result: failed as intended before implementation with `ModuleNotFoundError: No module named 'tools.art.imagegen_budget'`.

```powershell
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m unittest discover -s tools/art/tests -v
```

Result: failed as intended before tooling implementation with missing-module errors for `tools.art.build_combat_sheet`, `tools.art.render_combat_prompt`, and `tools.art.validate_combat_art`; the three existing budget tests passed.

Independent review also exposed a cap-bypass regression: `Decimal("20.004")` was accepted against a `20.00` limit because the supplied amount was rounded before comparison. A test requiring `BudgetError` for sub-cent values failed before the fix, then passed after exact-cent validation was added.

## GREEN Verification

Prerequisite installed with the mandated bundled runtime:

```powershell
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m pip install openai
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -c "import openai, PIL; print(openai.__version__); print(PIL.__version__)"
```

Result: `openai 2.46.0`, `Pillow 12.2.0`; no key value was printed.

Fresh final verification used the same mandated runtime with bytecode generation disabled:

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m unittest discover -s tools/art/tests -v
```

Result: `Ran 12 tests in 0.040s` and `OK`.

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' 'C:\Users\23906\.codex\skills\.system\imagegen\scripts\image_gen.py' generate --prompt 'cost guard dry run' --quality low --size 1024x1024 --out tmp/imagegen/dry-run.png --dry-run
```

Result: exited 0, printed the `gpt-image-2` generation payload for `tmp\\imagegen\\dry-run.png`, and made no API call.

Additional checks:

```powershell
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m py_compile tools/art/imagegen_budget.py tools/art/render_combat_prompt.py tools/art/build_combat_sheet.py tools/art/validate_combat_art.py
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m json.tool SourceArt/Generated/budget.json
git diff --check
```

Result: all succeeded; the generated JSON files parsed and the Task 1 diff had no whitespace errors.

## Self-Review

- Budget reservations use `Decimal`, reject non-positive, duplicate, sub-cent, and cap-exceeding estimates, serialize fixed two-decimal strings, and write through `budget.json.tmp` with `os.replace`.
- The renderer includes the catalog action plus every fixed prompt invariant.
- The packer uses integer grid boundaries, chronological order, RGBA output, and a one-pixel transparent safety margin.
- The validator rejects wrong dimensions, missing alpha, opaque corners, edge-touching alpha, identical consecutive frames, and forbidden filename tokens.
- Independent read-only review found the sub-cent cap-bypass issue above; it was fixed with a RED-GREEN regression test before final verification.

## Commits

- Code, data, and tests: `b96d18cb288b84cdd8da8247663442fe0341b25e` (`build: add cost-guarded combat art pipeline`).

## Concerns

- The mandated dry-run helper printed that `OPENAI_API_KEY` is set, but did not print its value. It made no API call.
- Python created untracked `tools/art/**/__pycache__` files during earlier verification. Workspace policy rejected their removal; they were intentionally excluded from staging and are not part of either Task 1 commit.
