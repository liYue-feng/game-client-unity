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
  - Treats only the network layer's exact transport-termination failure provenance as recoverable; live-transport protocol, server, and business failures remain terminal even when a replacement is already alive.
- `Assets/Scripts/Network/NetworkClient.cs`
  - Associates pending requests with their accepting transport so termination drains only that generation's requests.
  - Exposes a narrow transport-termination failure classifier for session-owned combat recovery.
- `Assets/Scripts/Network/NetworkConnectionController.cs`
  - Sends heartbeat through `Request<HeartbeatReq, HeartbeatResp>` without adding a request timer.
  - Treats a physically alive transport as open for replacement-drain purposes even when its queued open notification has not yet been dispatched.
  - Drains pending on close/timeout before queued open dispatch and keeps heartbeat requests single-flight.
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

## Independent Review Fix Evidence

1. Close/timeout before queued open dispatch RED.
   - XML: `Logs/task6-review1-red.xml`
   - Result: total 2, passed 0, failed 2, skipped 0.
   - Expected failures: both current-transport termination paths left the accepted request pending.
2. Generation-safe pending drain GREEN, including replacement and ordinary remote-close coverage.
   - XML: `Logs/task6-review1-green.xml`
   - Result: total 4, passed 4, failed 0, skipped 0.
3. Actual controller/adapter/session combat-disconnect RED.
   - XML: `Logs/task6-review2-red.xml`
   - Result: total 1, passed 0, failed 1, skipped 0.
   - Expected failure: transport drain moved battle settlement to `Failed` before session `Reconnecting` could own recovery.
4. Combat transport-loss recovery GREEN plus terminal server-`ErrorResp` counterexample.
   - XML: `Logs/task6-review2-green.xml`
   - Result: total 2, passed 2, failed 0, skipped 0.
5. Heartbeat single-flight RED.
   - XML: `Logs/task6-review3-red.xml`
   - Result: total 1, passed 0, failed 1, skipped 0.
   - Expected failure: two cadence ticks emitted two unresolved heartbeat requests.
6. Heartbeat single-flight GREEN.
   - XML: `Logs/task6-review3-green.xml`
   - Result: total 1, passed 1, failed 0, skipped 0.
7. Corrective broad Online/controller gate.
   - XML: `Logs/task6-review-broad-2.xml`
   - Result: total 91, passed 91, failed 0, skipped 0.
8. Shared request/controller core verification after transport ownership changes.
   - XML: `Logs/task6-review-core-green.xml`
   - Result: total 40, passed 40, failed 0, skipped 0.
9. Fresh corrective completion verification using the required Task 6 broad filter.
   - XML: `Logs/task6-review-final.xml`
   - Result: total 91, passed 91, failed 0, skipped 0.
10. Fresh `NetworkClientTests` verification after pending transport association.
    - XML: `Logs/task6-review-networkclient-final.xml`
    - Result: total 21, passed 21, failed 0, skipped 0.
11. Synchronously alive replacement provenance RED.
    - XML: `Logs/task6-review4-red.xml`
    - Result: total 1, passed 0, failed 1, skipped 0.
    - Expected failure: old combat became terminal because classification read the already-alive replacement transport.
12. Replacement provenance GREEN plus actual-disconnect and terminal-`ErrorResp` counterexamples.
    - XML: `Logs/task6-review4-green.xml`
    - Result: total 3, passed 3, failed 0, skipped 0.
13. Full Task 6 broad gate after the replacement provenance fix.
    - XML: `Logs/task6-review4-broad.xml`
    - Result: total 92, passed 92, failed 0, skipped 0.
14. `NetworkClientTests` after the replacement provenance fix.
    - XML: `Logs/task6-review4-networkclient.xml`
    - Result: total 21, passed 21, failed 0, skipped 0.

## Self-Review Concerns

- Task 7 still owns manager/payment/GM migration and removal of the temporary legacy transport APIs; this task did not modify or hide that debt.
- Heartbeat deliberately has no request timer and permits only one unresolved request. Later cadence ticks wait for matching completion, cancellation, disconnect, replacement, or disposal.
- Unity logs retain existing licensing/CDN timeout and malformed `.meta` GUID warnings; the latest corrective broad XML is 92/92 with no test failures.
