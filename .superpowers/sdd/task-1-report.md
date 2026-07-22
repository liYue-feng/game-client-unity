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
- `tools/art/.gitignore`
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

## Interprocess Serialization Fix

An independent review found that `os.replace` only made the final file replacement atomic. It did not serialize the preceding ledger read, cap check, and temporary-file write, so parallel callers could approve the same remaining budget and race on `budget.json.tmp`.

RED regression command:

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m unittest tools.art.tests.test_imagegen_budget -v
```

Result before the lock: `test_concurrent_reservations_preserve_hard_limit_and_clean_transaction_files` failed with `7 != 5` successful `$0.02` reservations against a ledger with `$19.90` already reserved.

The fix uses an atomically created `budget.json.lock` directory with a 10-second bounded acquisition loop. It holds that lock across the complete read/check/write transaction and removes it in `finally`. The regression starts ten spawned workers against the `$19.90` ledger, proves exactly five successes and five `BudgetError` failures, confirms unique operation IDs and a final `$20.00` total, then verifies valid JSON with no leftover `.tmp` or `.lock` path.

GREEN commands:

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m unittest tools.art.tests.test_imagegen_budget -v
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m unittest discover -s tools/art/tests -v
```

Results: focused budget suite `Ran 5 tests` / `OK`; complete art-tools suite `Ran 13 tests` / `OK`.

## Windows PermissionError Contention Fix

The lock directory solved stale reads, but a Windows re-review observed `PermissionError` while concurrent processes created or removed that directory. The original lock treated only `FileExistsError` as contention and released with one `os.rmdir` call, exposing raw `PermissionError` instead of the budget protocol's bounded retry behavior.

RED command:

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m unittest tools.art.tests.test_imagegen_budget -v
```

Result before the fix: `Ran 8 tests` with three expected errors. Acquisition, release, and release-after-body-failure tests each exposed raw `PermissionError: lock directory is busy`.

The lock now treats `PermissionError` as acquisition contention inside the same 10-second retry protocol. Release retries transient `PermissionError` through that deadline; a permanent cleanup failure raises `BudgetError` after a successful transaction, while a transaction failure remains the primary exception and records the cleanup failure as a note.

GREEN commands:

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
$python = 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$completed = 0
foreach ($run in 1..20) {
    $output = & $python -m unittest tools.art.tests.test_imagegen_budget -q 2>&1
    if ($LASTEXITCODE -ne 0) { $output; throw "focused budget run $run failed" }
    $completed += 1
}
"focused_runs=$completed tests_per_run=8 total_tests=$($completed * 8) result=OK"
& $python -m unittest discover -s tools/art/tests -v
```

Results: 20 consecutive focused runs passed for `160/160` test executions; the complete art-tools suite passed `Ran 16 tests` / `OK`.

## Final Lock Cleanup Correction

A final review found that cleanup inherited the acquisition deadline, so a long transaction could leave no retry window for a transient release `PermissionError`. It also found that a non-`BudgetError` cleanup failure could mask the original transaction exception.

RED command:

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
& 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' -m unittest tools.art.tests.test_imagegen_budget -v
```

Result before the correction: `Ran 10 tests` with two expected errors. A delayed transaction made the first release retry immediately time out, and `OSError: release failed` replaced `RuntimeError: body failed`.

Cleanup now receives a fresh 10-second deadline. Any cleanup exception propagates after a successful transaction so a stale lock is not silent; when the transaction body already failed, that original exception remains primary and receives a `budget lock cleanup failed: ...` note. The focused tests deterministically cover the fresh deadline, a transient release retry, and permanent cleanup failure with preserved transaction semantics.

GREEN commands:

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
$python = 'C:\Users\23906\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$completed = 0
foreach ($run in 1..20) {
    $output = & $python -m unittest tools.art.tests.test_imagegen_budget -q 2>&1
    if ($LASTEXITCODE -ne 0) { $output; throw "focused budget run $run failed" }
    $completed += 1
}
"focused_runs=$completed tests_per_run=10 total_tests=$($completed * 10) result=OK"
& $python -m unittest discover -s tools/art/tests -v
```

Results: 20 consecutive focused runs passed for `200/200` test executions; the complete art-tools suite passed `Ran 18 tests` / `OK`.

## Commits

- Code, data, and tests: `b96d18cb288b84cdd8da8247663442fe0341b25e` (`build: add cost-guarded combat art pipeline`).
- Serialization fix, concurrent regression, and bytecode ignore rule: `c2268b7` (`fix: serialize imagegen budget reservations`).
- Windows contention retry and deterministic acquisition/release tests: `820beeb` (`fix: retry imagegen budget lock contention`).

## Concerns

- The mandated dry-run helper printed that `OPENAI_API_KEY` is set, but did not print its value. It made no API call.
- Python created untracked `tools/art/**/__pycache__` files during earlier verification. Workspace policy rejected their removal; they were intentionally excluded from staging and are not part of either Task 1 commit.
- `tools/art/.gitignore` now ignores `__pycache__/` and `*.pyc`, so future local Python verification does not dirty the worktree with bytecode artifacts.
