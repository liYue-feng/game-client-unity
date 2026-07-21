# Task 7 Report

STATUS: COMPLETE

## Scope

- Migrated `LoginManager`, `ArchiveManager`, `RankManager`, and `CombatManager` from reply subscriptions plus `NetworkClient.Send` to correlated `Request<TRequest,TResponse>` calls.
- Preserved manager public events, validation, singleton cleanup, archive state behavior, and response handlers. Managers cancel their tracked pending requests during destruction.
- Added `CombatManager.UpdatePlayerStats(PlayerStatsData stats)` with request/response IDs 4013/4014 and explicit field copying into the flat generated `UpdatePlayerStatsReq`.
- Added disposable `PaymentSessionService` and `GmCommandService` boundaries.
  - Payment order creation and GM execution use nonzero correlated request sequences.
  - `PayResultNotify` and GM broadcast delivery are the only `seq=0` push subscriptions.
  - Push subscriptions are disposed by their owning service and multicast observers are isolated with per-observer exception handling.
- Migrated all remaining tests to explicit sequenced codec calls.
- Removed the temporary public `NetworkClient.Send` API and the throwing old `Codec.Encode/TryDecode` overloads without adding compatibility replacements.

## TDD Evidence

All Unity commands used graphical batch mode with `-batchmode`. No invocation used `-nographics`, `-quit`, merge, or push. Each test invocation was followed until Unity exited and its XML was read when compilation permitted XML generation.

1. Pre-edit manager baseline.
   - XML: `Logs/task7-baseline.xml`
   - Result: total 1, passed 0, failed 1, skipped 0.
   - Expected failure: `ManagerNetworkSubscriptionTests` reached the temporary two-argument `Codec.Encode` overload and threw `A protocol sequence is required.`
2. Required RED suite after adding Task 7 tests.
   - Log: `Logs/task7-red.log`
   - No XML was emitted because the intended missing API caused compilation to stop.
   - Expected failures: six `CS0246` errors, all for the absent `PaymentSessionService` and `GmCommandService` types at their new test call sites. No unrelated compiler error appeared.
3. First GREEN attempt after adding services and manager correlation.
   - XML: `Logs/task7-green-1.xml`
   - Result: total 8, passed 6, failed 2, skipped 0.
   - The two failures were a test-fixture leak: the shared callback counter was not reset between NUnit cases. Teardown was corrected without changing production behavior.
4. Focused Task 7 GREEN.
   - XML: `Logs/task7-focused-green.xml`
   - Result: total 8, passed 8, failed 0, skipped 0.
5. Post-legacy-removal refactor gate covering manager/payment/GM, `NetworkClient`, controller, and protocol codec tests.
   - XML: `Logs/task7-refactor-green.xml`
   - Result: total 52, passed 52, failed 0, skipped 0.
6. Complete EditMode run.
   - XML: `Logs/task7-all-editmode.xml`
   - Result: total 272, passed 272, failed 0, skipped 0.
7. Fresh completion EditMode run.
   - XML: `Logs/task7-final-editmode.xml`
   - Result: total 272, passed 272, failed 0, skipped 0.

The required tests include `ManagerRequestsUseNonZeroCorrelation`, `PaymentCreateOrderUsesRequestSeq`, `PaymentNotificationUsesZeroSeqPush`, `GmCommandResponseUsesRequestSeq`, `GmBroadcastUsesZeroSeqPush`, and `CombatManagerCanUpdatePlayerStats`. They inspect real outgoing frames, reject unrelated sequences, inject real protobuf responses/pushes, prove correlated responses do not become pushes, and prove payment/GM push subscriptions stop after disposal.

## Client Verification

- Pester command: `Invoke-Pester -Script tools/protobuf/GeneratedProtocol.Tests.ps1,tools/validation/UnityAssetIntegrity.Tests.ps1 -EnableExit`
  - Result: passed 15, failed 0, skipped 0.
- Generated protocol verifier: `tools/protobuf/Verify-GeneratedProtocol.ps1 -BackendRoot E:/Own_project/game-server-go/.worktrees/sequenced-protobuf-transport`
  - Result: exit 0.
  - Schema SHA-256: `F874C64F1C0F121197DA1BE13A79FD88E6F6460ECE1172263C8EE360573BB2DE`.
  - Generated C# SHA-256: `24CD58483339661D1CDCC4D45F4A39A50C7C25B592F9BD1EA6DD86EAC3C19F08`.
- Asset wrapper: `tools/validation/Test-UnityAssetIntegrity.ps1`
  - Result: `Unity asset integrity check passed.`
- `git diff --check`: exit 0.

The first verifier attempt used the server main checkout and correctly failed because that checkout does not yet own `proto/game.proto`. It made no changes. The verifier was rerun against the Task 1-4 server coordination worktree and passed.

## Static Gates

Scans covered `Assets/Scripts` and `Assets/Tests` C# files.

- `.Send(MsgID.`: no matches.
- Two-argument `Codec.Encode`: no matches.
- Three-out-argument `Codec.TryDecode`: no matches.
- Legacy public `NetworkClient.Send` and old codec overload declarations: no matches.
- Production `On<T>` subscriptions: exactly two matches:
  - `GmCommandService` subscribes to `GMCommandResp` for intentional `seq=0` broadcasts.
  - `PaymentSessionService` subscribes to `PayResultNotify` for `seq=0` payment pushes.

## Warnings And Concerns

- Unity emitted its existing licensing-client noise during batch startup.
- Unity emitted existing YAML parser fallback warnings for 10 pre-existing `.meta` files, including `BattleScene.unity.meta` and existing combat/UI script metas. The independent asset integrity Pester suite and wrapper both passed.
- Full EditMode logs include expected exceptions and malformed-frame warnings asserted by existing tests; the final XML has zero failures.
- Git reports the repository's existing LF-to-CRLF checkout warnings during diff checks; `git diff --check` remains clean.
- No server checkout, main checkout, remote branch, or deployment state was modified.

## Review Corrective Pass

The Task 7 review found two lifecycle gaps and requested additional concurrency coverage.

- `PaymentSessionService` and `GmCommandService` now delegate correlated calls to `PendingRequestOwner`, which tracks every active sequence and request-specific active state.
  - Concurrent requests remain independent.
  - Success, `ErrorResp`, and synchronous send failure complete at most once.
  - Disposal marks the owner and every request state inactive before calling `CancelRequest`, so cancellation failure callbacks cannot escape after disposal.
  - If disposal occurs reentrantly during `transport.Send`, the returned pending sequence is cancelled instead of being registered after disposal.
- All four manager request helpers now recheck `_destroyed` after `NetworkClient.Request` returns. A request returned across destroy-during-send reentrancy is cancelled instead of being added to the manager set.
- Manager tests now issue an actual request before destruction, inject late matching success and `ErrorResp` frames, and prove no callback or login-state mutation. The EditMode fixture explicitly invokes private `OnDestroy` before `DestroyImmediate` because ordinary MonoBehaviour lifecycle callbacks are not automatically dispatched in this EditMode setup; manually invoking `Awake` is invalid because these managers call `DontDestroyOnLoad`.
- Added out-of-order, same-response-ID rank coverage using two distinct sequences.

Corrective RED-GREEN evidence:

1. Corrective RED.
   - XML: `Logs/task7-review-red.xml`
   - Result: total 16, passed 10, failed 6, skipped 0.
   - Expected failures: payment and GM each leaked one callback after concurrent disposal; payment, GM, and the representative manager each retained a request created across dispose/destroy-during-send reentrancy; the initial manager lifecycle fixture exposed that its pending request was still accepted after object destruction.
2. Manager lifecycle isolation.
   - XML: `Logs/task7-review-manager-isolated-5.xml`
   - Result: total 1, passed 0, failed 1, skipped 0.
   - The manager owned one sequence before destruction, but `NetworkClient.CancelRequest(seq)` still returned true afterward. This proved the late `LoginResp` was accepted through the pending path rather than delivered as a `seq=0` push.
3. Corrective focused GREEN.
   - XML: `Logs/task7-review-focused-green.xml`
   - Result: total 16, passed 16, failed 0, skipped 0.
4. Corrective refactor gate covering manager/payment/GM, `NetworkClient`, controller, and protocol tests.
   - XML: `Logs/task7-review-refactor-green.xml`
   - Result: total 60, passed 60, failed 0, skipped 0.
5. Corrective full EditMode.
   - XML: `Logs/task7-review-all-editmode.xml`
   - Result: total 280, passed 280, failed 0, skipped 0.

Fresh corrective non-Unity verification also passed:

- Protocol plus asset Pester: passed 15, failed 0, skipped 0.
- Generated protocol verifier: exit 0 with unchanged schema and C# SHA-256 values.
- Asset integrity wrapper: passed.
- Static legacy API/call gates: no matches.
- Production push subscriptions: exactly `PayResultNotify` and GM `GMCommandResp`.
- Static post-request destruction guard check: passed for Login, Archive, Rank, and Combat managers.
- `git diff --check`: exit 0.
