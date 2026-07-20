# Task 4 Report: Typed Archive Load, Hydration, Save, And Real Login Regression

## Scope

- Backend commit: `767078336b9d436ab1df69ed05ff3f645a0cf47e` `feat: persist protobuf player archives`.
- Client commit: `e129b3ba7f63c8eadb5c8b78e8dca2f20a06093e` `feat: hydrate protobuf player progress`.
- Neither repository was pushed.
- Excluded from staging: client `.superpowers/sdd/.gitignore`, client `.superpowers/sdd/task-3-report.md`, all logs, and the backend's ten content-identical pseudo-dirty files.

## Behavior Delivered

- `PlayerProgressState` owns all generated `PlayerArchive` fields and deep-copies `UnlockedStyles` in both directions.
- Archive service boundaries clone protobuf messages on load and save; coordinator hydrates progress before `MarkReady`/`Ready`, applies saves only after acknowledgement, and replaces progress on reload.
- The host exposes immutable progress, a cloned `Archive`, `SaveArchive(PlayerArchive)`, and reload behavior.
- The Go devprobe uses generated protobuf request/response types, proves login, `found=false`, a complete typed save, and a protobuf-equal reload.
- The real runner captures backend/devprobe/Unity PIDs, restores `GAME_BACKEND_INTEGRATION`, checks ports 8080 and 8081 before and after, removes temporary executables, and requires durable XML/log evidence.

## TDD Evidence

- Historical copy-isolation RED: `Logs/A4-archive-copy-red.xml` is `Failed`, `total=1`, `passed=0`, `failed=1`.
- Historical copy-isolation GREEN: `Logs/A4-archive-copy-green.xml` and `Logs/A4-archive-copy-green-retry.xml` are each `Passed 1/1`.
- Historical progress-field GREEN: `Logs/A4-player-progress.xml` is `Passed 1/1`.
- Runner ownership RED: `tools/integration/Invoke-A4BackendIntegration.Tests.ps1` failed when no captured probe PID existed; GREEN is `Passed 1/1` after capturing, reporting, waiting for, and cleaning up the probe.
- Runner wait RED: the captured short-lived probe had an empty exit code after manual polling; its stdout still proved the probe passed and ports were free. The test then required `Start-Process -Wait`; GREEN is `Passed 1/1`.
- Runner evidence RED: a real run produced `A4-real-backend-20260720-235003.xml Passed 1/1` but failed only because the runner searched for an obsolete `dataLen` server token. The server intentionally logs no archive payload. The replacement contract requires the exact devprobe stdout evidence `protobuf login found=false typed save typed reload`; Go devprobe and runner Pester were GREEN before the final real run.

## Final Verification

| Gate | Result | Evidence |
| --- | --- | --- |
| Backend focused | PASS | `go test ./internal/game ./internal/store ./cmd/devprobe -count=1` |
| Backend full | PASS | `go test ./... -count=1`, `go vet ./...`, `go build ./...`, protobuf drift verification, `git diff --check` |
| Client protocol | PASS | `tools/protobuf/Verify-GeneratedProtocol.ps1`; SHA256 `50B20EF609A0718D72E4740F910904181F5461941F00235828F5B1B43ACEFC29`; Pester `4/4` |
| Client assets | PASS | validation Pester `5/5` and `Test-UnityAssetIntegrity.ps1` |
| Client focused EditMode | PASS | `Logs/A4-client-focused-final.xml`, `44/44` |
| Client full EditMode | PASS | `Logs/A4-client-full-final-20260720-234713.xml`, `217/217` |
| Real backend PlayMode | PASS | `Logs/A4-real-backend-20260720-235246.xml`, `1/1` |

Final real-run stdout:

- `BACKEND_PID=43264`
- `DEVPROBE_PID=47032`
- `UNITY_PID=28344`
- `DEVPROBE_EVIDENCE=typed_archive_round_trip:1`
- `SERVER_EVIDENCE=login:1`
- `UNITY_RESULT=total=1 passed=1 failed=0 skipped=0 exit_code=0`
- XML: `Logs/A4-real-backend-20260720-235246.xml`
- Unity log: `Logs/A4-real-backend-20260720-235246.log`
- Server log: backend `logs/a4-integration-server-20260720-235246.stdout.log`

The final runner exited `0`; its cleanup completed without errors. A post-run listener check confirmed ports `8080` and `8081` are free.

## Concern

`internal/model/player.go` has an existing comment that still describes archive data as JSON, although the verified BLOB implementation stores protobuf bytes. It was a content-identical pseudo-dirty file outside this Task 4 commit and was intentionally not changed.
