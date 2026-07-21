# Task 6 Report

STATUS: COMPLETE

## Commit

- Commit message: `feat: sequence online and combat requests`
- Local coordination branch only. Not merged or pushed.

## Changes

- `Assets/Scripts/Online/LoginSessionService.cs`
  - Replaced login response/error push subscriptions with a correlated `Request<LoginReq, LoginResp>`.
  - Tracks the active nonzero sequence and attempt token; cancellation invalidates business state before `CancelRequest`.
- `Assets/Scripts/Online/ArchiveSessionService.cs`
  - Replaced load/save response/error subscriptions with correlated requests.
  - Preserves one active archive operation, detached archive snapshots, stable disconnected errors, and cancellation isolation.
- `Assets/Scripts/Online/BattleSettlementService.cs`
  - Exposes the required callback/out-sequence `Send` boundary.
- `Assets/Scripts/Online/BattleSettlementCoordinator.cs`
  - Owns the active combat sequence and cancels it before forced resend/retry.
  - Allocates a new transport sequence while retaining the same protobuf `run_id`.
  - Ignores late success and `ErrorResp` frames for the cancelled sequence and preserves archive-only retry after an accepted combat response.
- `Assets/Scripts/Network/NetworkConnectionController.cs`
  - Sends heartbeat through `Request<HeartbeatReq, HeartbeatResp>` without adding a request timer.
  - Treats a physically alive transport as open for replacement-drain purposes even when its queued open notification has not yet been dispatched.
- Task 6 EditMode tests
  - Added all four named correlation/retry cases plus the queued-open replacement pending-drain regression.
  - Migrated existing Online fixtures to encode responses with the matching outgoing request sequence, without compatibility behavior.

## TDD Evidence

All Unity invocations used graphical batch mode: `-batchmode` was used, with no `-nographics`, `-quit`, merge, or push. Every result was read from XML.

1. RED command: the five new Task 6 regression methods.
   - XML: `Logs/task6-red.xml`
   - Result: total 5, passed 0, failed 5, skipped 0.
   - Expected failures: login/archive/combat responses could not complete correlated requests, heartbeat sequences were not pending, and replacement before queued open dispatch did not drain pending.
2. RED combat correction: after updating one obsolete test helper to decode `seq`.
   - XML: `Logs/task6-red-combat.xml`
   - Result: total 1, passed 0, failed 1, skipped 0.
   - Expected failure: the active second combat sequence did not start archive save.
3. Focused GREEN command: the same five Task 6 regression methods.
   - XML: `Logs/task6-focused-green-2.xml`
   - Result: total 5, passed 5, failed 0, skipped 0.
4. Broad Online/controller GREEN command after fixture migration.
   - XML: `Logs/task6-broad-green-2.xml`
   - Result: total 88, passed 88, failed 0, skipped 0.
5. Fresh completion verification using the required broad filter.
   - XML: `Logs/task6-final-green.xml`
   - Result: total 88, passed 88, failed 0, skipped 0.
6. `git diff --check` exited 0. Static scans found no Task 6 production legacy `Send`/response subscription owner and no old codec signature in scoped Task 6 tests.

## Self-Review Concerns

- Task 7 still owns manager/payment/GM migration and removal of the temporary legacy transport APIs; this task did not modify or hide that debt.
- Heartbeat deliberately has no request timer. If a live connection never replies, pending heartbeat requests remain until response, cancellation, disconnect, or disposal, as required by the approved design.
- Unity logs retain existing licensing/CDN timeout and malformed `.meta` GUID warnings; the final target XML is 88/88 with no test failures.
