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
