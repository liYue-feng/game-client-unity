# Protobuf Online Battle Completion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace all game WebSocket JSON payloads with generated protobuf messages and deliver an exactly-once online battle-to-victory/defeat settlement and persistence loop.

**Architecture:** The backend owns one protobuf schema and generates Go and Unity C# types. The existing six-byte frame and MsgID router remain, while body serialization becomes protobuf. A server-idempotent combat settlement service and a client settlement coordinator connect the real `BattleRunController` terminal path to reward application, typed archive save, result UI, menu return, and reload.

**Tech Stack:** Unity 2022.3.47f1, C#, Google.Protobuf 3.35.1, protoc 35.0, Go 1.25, google.golang.org/protobuf v1.36.11, Unity Test Framework 1.1.33, NUnit, PowerShell 5.1, Gorilla WebSocket, GORM.

## Global Constraints

- Canonical schema: backend repository path `proto/game/v1/messages.proto`.
- Keep the existing `uint32 LE length + uint16 LE MsgID` frame and existing MsgID numeric values.
- Every WebSocket request, response, push, and error body uses protobuf; no JSON compatibility mode or encoding sniffing.
- HTTP WeChat callbacks may remain JSON because they are outside the game WebSocket protocol.
- Save/load uses typed `PlayerArchive`, not a JSON string.
- `run_id` is required and server settlement is idempotent on `(player_id, run_id)`.
- Preserve A3 ownership of transport, heartbeat, reconnect, and connection generation.
- Preserve Offline mode and current client-authoritative combat.
- Use RED-GREEN-REFACTOR for handwritten production behavior; generated code is verified by golden/drift tests.
- Do not push either repository until all tasks and final reviews are complete; push each repository once.

---

### Task 1: Canonical Schema, Pinned Generation, And Golden Frames

**Files:**
- Create: backend `proto/game/v1/messages.proto`
- Create: backend `tools/protobuf/Generate-Protocol.ps1`
- Create: backend `tools/protobuf/Verify-Protocol.ps1`
- Create: backend `internal/protocolpb/messages.pb.go`
- Create: client staging output `tools/protobuf/generated/Messages.cs`
- Create: client `tools/protobuf/Verify-GeneratedProtocol.ps1`
- Modify: backend `go.mod`, `go.sum`

**Interfaces:**
- Produces: generated `game-server/internal/protocolpb` Go package and staged `Game.Protocol` C# source outside Unity's `Assets` compilation boundary.
- Produces: `MessageId`, `BattleOutcome`, `PlayerArchive`, all 32 routed messages, and nested rank/combat/config messages.

- [ ] **Step 1: Add failing schema/toolchain contract tests**

Add PowerShell assertions for the exact pinned versions, one canonical schema, generated output paths, all route numbers, no `JsonUtility` annotations in staged generated code, and no duplicate client `.proto`. Add the Go golden assertion for `LoginReq{code:"abc"}` body `0A03616263` and frame `0B000000E9030A03616263`; the compiled C# golden assertion belongs to Task 3 after handwritten classes are removed.

- [ ] **Step 2: Run RED checks**

Run backend `go test ./internal/protocolpb ./internal/protocol -count=1` and client `Invoke-Pester tools/protobuf/*.Tests.ps1`. Expected: missing schema/generated packages and missing verifier failures.

- [ ] **Step 3: Define the complete schema and generation script**

Define all existing messages, `PayResultNotify`, typed `PlayerArchive`, `ScoreMetadata`, `BattleOutcome`, and the new combat settlement fields exactly as specified in the design. Generate Go with `protoc-gen-go v1.36.11` and stage C# with `protoc 35.0`. Pin and verify the `Google.Protobuf 3.35.1` net45 package source here; Task 3 installs its runtime DLL when generated C# enters Unity's compilation boundary.

- [ ] **Step 4: Run GREEN generation and golden checks**

Run generation twice, `Verify-Protocol.ps1`, `go test ./internal/protocolpb ./internal/protocol -count=1`, client PowerShell staged-output verification, and `git diff --check` in both repositories. Expected: deterministic output and exact Go golden bytes.

- [ ] **Step 5: Commit locally**

Commit backend as `feat: define protobuf game protocol` and client as `feat: add generated protobuf protocol` without pushing.

### Task 2: Go Protobuf Codec, Kernel, And Handler Migration

**Files:**
- Modify: backend `internal/protocol/codec.go`, `codec_test.go`
- Modify: backend `internal/kernel/kernel.go`, `kernel_test.go`
- Modify: backend `internal/session/session.go`, `session_test.go`
- Modify: backend `internal/transport/connection.go`, `server_test.go`
- Modify: backend handlers under `internal/login`, `game`, `rank`, `combat`, `payment`, `gm`
- Modify: backend archive repository/model/service files enough to accept and persist protobuf archive bytes while retaining their existing service boundaries
- Remove: backend handwritten payload structs from `internal/protocol/message.go`; retain MsgID compatibility constants in `internal/protocol/ids.go`
- Test: affected package tests and new compile-time route coverage test in `cmd/server/main_test.go`

**Interfaces:**
- Consumes: generated `protocolpb` messages.
- Produces: `protocol.Encode(msgID uint16, payload proto.Message) ([]byte, error)` and protobuf kernel dispatch.

- [ ] **Step 1: Write failing protobuf codec and registration tests**

Test exact golden frame output, malformed protobuf rejection, wrong handler request/response types, protobuf ErrorResp delivery, repeated fields, empty messages, and route coverage for all production IDs.

- [ ] **Step 2: Verify RED**

Run `go test ./internal/protocol ./internal/kernel ./internal/session ./internal/transport -count=1`. Expected: JSON codec and reflection dispatch violate protobuf assertions.

- [ ] **Step 3: Implement protobuf codec and kernel**

Use `proto.Marshal`, `proto.Unmarshal`, `proto.Message`, generated request prototypes, the unchanged frame header, and the 64 KiB application limit. Keep payment callback JSON isolated in `internal/payment`.

- [ ] **Step 4: Migrate every handler signature and constructor**

Replace handwritten request/response types with `protocolpb` types. Convert repeated generated pointer slices and enum fields deliberately. `GMCommandReq.ArgsJson` remains bytes and is decoded only inside the GM handler. Adapt archive storage/service signatures to protobuf bytes so the generated `SaveArchiveReq.Archive` and `LoadArchiveResp` compile without a temporary JSON bridge. Adapt the existing combat handler to the new typed duration/outcome fields without claiming idempotency; Task 5 replaces its persistence path.

- [ ] **Step 5: Run GREEN package and full tests**

Run focused packages, `go test ./... -count=1`, `go vet ./...`, `go build ./...`, generation drift verification, and `git diff --check`.

- [ ] **Step 6: Review and commit locally**

Complete task spec/quality review, fix all Critical/Important findings, and commit `refactor: use protobuf websocket payloads` without pushing.

### Task 3: Unity Protobuf Codec, Registry, And Consumer Migration

**Files:**
- Modify: client `Assets/Scripts/Protocol/Codec.cs`, `Protocol.cs`, `README.md`
- Remove: client handwritten payload classes from `Assets/Scripts/Protocol/Messages.cs`
- Copy: client `tools/protobuf/generated/Messages.cs` to `Assets/Scripts/Protocol/Generated/Messages.cs` and add `.meta`
- Create: client `Assets/Plugins/Google.Protobuf/Google.Protobuf.dll` and `.meta`
- Create: client `Assets/Scripts/Protocol/ProtocolMessageRegistry.cs` and `.meta`
- Modify: client `Assets/Scripts/Network/NetworkClient.cs`
- Modify: client Online services and Managers that construct or read protocol messages
- Modify: client EditMode network/protocol tests
- Create: client `Assets/Tests/EditMode/Protocol/ProtobufGoldenFrameTests.cs` and `.meta`

**Interfaces:**
- Consumes: generated `IMessage` classes and parsers.
- Produces: `Codec.Encode(ushort, IMessage)`, `Codec.TryDecode(..., out ushort, out byte[])`, and typed `NetworkClient.On<T>` through the registry.

- [ ] **Step 1: Write failing codec, registry, and lifecycle tests**

Cover the golden frame, malformed protobuf, parser/type mismatch, repeated messages, empty messages, subscription move/dispose, late callbacks, and disconnected sends.

- [ ] **Step 2: Verify RED in focused EditMode**

Run the protocol and NetworkClient filters. Expected: string/JsonUtility codec and missing registry failures.

- [ ] **Step 3: Implement the protobuf client boundary**

Use `IMessage.ToByteArray`, generated `MessageParser`, explicit MsgID parser registration, byte-body subscriptions, and safe parse errors. Retain current transport ownership and subscription semantics.

- [ ] **Step 4: Migrate call sites to generated PascalCase properties**

Update Login, Heartbeat, Archive, Rank, Combat, Payment, GM, tests, and fake frame helpers. Adapt archive call sites to pass generated `PlayerArchive` messages, with default empty archives until Task 4 installs hydration ownership. Do not add lowercase compatibility aliases to generated partial classes.

- [ ] **Step 5: Run GREEN client gates**

Run generation verification, asset integrity, Pester `5/5`, focused EditMode, full EditMode, and `git diff --check`.

- [ ] **Step 6: Review and commit locally**

Complete task spec/quality review and commit `refactor: use protobuf network client` without pushing.

### Task 4: Typed Archive Load, Hydration, Save, And Real Login Regression

**Files:**
- Modify: backend `internal/model/player.go`, `internal/store/mysql.go`, `memory_development.go`, repository interfaces and tests
- Modify: backend `internal/game/service.go`, `handler.go`, tests
- Modify: backend `cmd/devprobe/main.go`, tests
- Modify: client `Assets/Scripts/Online/ArchiveSessionService.cs`, `OnlineSessionCoordinator.cs`, `OnlineSessionHost.cs`
- Create: client `Assets/Scripts/Online/PlayerProgressState.cs` and `.meta`
- Modify: client Online EditMode tests and `RealBackendOnlineFlowTests.cs`
- Modify: client `tools/integration/Invoke-A4BackendIntegration.ps1`

**Interfaces:**
- Produces: immutable-copy `PlayerProgressState` hydrated from `PlayerArchive`.
- Produces: `OnlineSessionHost.Archive` and `SaveArchive(PlayerArchive archive)`.

- [ ] **Step 1: Write failing archive storage and hydration tests**

Backend tests require protobuf bytes, copy isolation, missing `found=false`, malformed archive error, and MySQL binary upsert. Client tests require default archive, field hydration, copy isolation, load-before-menu, and typed save.

- [ ] **Step 2: Verify RED on both repositories**

Run backend game/store tests and client Online service/coordinator tests. Expected: string archive APIs fail the typed contracts.

- [ ] **Step 3: Implement typed archive persistence and client state**

Store `proto.Marshal(PlayerArchive)` bytes, parse on load, and never convert to JSON. Hydrate the online host before MenuScene. Preserve Offline PlayerPrefs behavior.

- [ ] **Step 4: Update the real login/archive runner**

The runner must prove protobuf login, `found=false`, typed save, typed reload, log evidence, exact PIDs, environment restoration, and free ports.

- [ ] **Step 5: Run GREEN and review**

Run backend full gates, client focused/full EditMode, and real backend PlayMode `1/1`. Complete task review and fix all blocking findings.

- [ ] **Step 6: Commit locally**

Commit backend `feat: persist protobuf player archives` and client `feat: hydrate protobuf player progress` without pushing.

### Task 5: Idempotent Combat Settlement And Development Runtime

**Files:**
- Create: backend `internal/model/combat_settlement.go`
- Modify: backend `internal/store/mysql.go`, `memory_development.go`, repository interfaces and tests
- Create: backend `internal/combat/service.go`, `service_test.go`
- Modify: backend `internal/combat/handler.go`, `validator.go`, tests
- Modify: backend `cmd/server/main.go`, `main_test.go`
- Modify: backend `cmd/devprobe/main.go`, tests

**Interfaces:**
- Produces: `CombatSettlementRepository.Settle(uid int64, req *protocolpb.CombatResultReq) (*protocolpb.CombatResultResp, error)`.
- Produces: development routes for CombatResult and GetPlayerStats without external stores.

- [ ] **Step 1: Write failing validation and idempotency tests**

Require nonempty bounded `run_id`, explicit outcome, bounded counters/duration/style/player level, one reward application, duplicate stored response, Victory clear advancement, Defeat no advancement, transaction rollback, concurrent duplicate convergence, and byte-for-byte response stability.

- [ ] **Step 2: Verify RED**

Run `go test ./internal/combat ./internal/store ./cmd/server -count=1`. Expected: missing service/model/repository and duplicate rewards.

- [ ] **Step 3: Implement memory and MySQL settlement**

Use a memory mutex and a MySQL transaction with unique `(player_id, run_id)`. Preserve current per-kill reward configuration. Return a complete `PlayerArchive` snapshot.

- [ ] **Step 4: Register development settlement routes**

Development registers Login, Heartbeat, SaveArchive, LoadArchive, CombatResult, and GetPlayerStats. Update explicit route tests; keep rank/payment/GM/config mutation routes absent.

- [ ] **Step 5: Run GREEN stress and full gates**

Run settlement concurrency tests repeatedly, full Go tests, vet, build, protobuf drift, and diff checks.

- [ ] **Step 6: Review and commit locally**

Complete task review and commit `feat: settle combat runs exactly once` without pushing.

### Task 6: Client Battle Settlement Coordinator And Result UI

**Files:**
- Create: client `Assets/Scripts/Online/BattleSettlementService.cs`, `BattleSettlementCoordinator.cs`, `IBattleSettlementGateway.cs` and metas
- Modify: client `Assets/Scripts/Online/OnlineSessionHost.cs`, `Game.Online.asmdef`
- Modify: client `Assets/Scripts/Game/BattleRunController.cs`, `BattleSceneSetup.cs`
- Modify: client `Assets/Scripts/UI/BattleUI/GameOverUI.cs`
- Modify: client `Assets/Scripts/Managers/AchievementManager.cs`, talent integration call sites
- Remove active terminal reporting from client `DungeonManager.cs` and legacy `CombatManager` ownership where duplicated
- Test: client EditMode Online settlement tests and PlayMode battle completion tests

**Interfaces:**
- Produces: `Settle(BattleRunOutcome outcome, CombatResultData data, Action<BattleSettlementResult> completed)` with one active run.
- Produces: GameOverUI states `Pending`, `Saved`, and `Failed`, plus one retry command.

- [ ] **Step 1: Write failing coordinator state tests**

Cover one run ID, duplicate terminal suppression, matching response only, response then archive save, save failure retry without new combat request, reconnect resend with same run ID, duplicate response, dispose/late frame, and Offline immediate completion.

- [ ] **Step 2: Verify RED in focused EditMode**

Run the new settlement filters. Expected: missing service/coordinator/UI state APIs.

- [ ] **Step 3: Implement settlement services and host ownership**

Use generated protobuf messages, the A3 connection generation, and existing archive service. Do not create another heartbeat, reconnect loop, transport, or persistent manager.

- [ ] **Step 4: Connect the one terminal producer**

After `BattleRunStateMachine.TryComplete` wins, capture data once, report talent/achievement once for both outcomes, freeze combat, display pending result, settle, save, then unlock navigation. Remove the dormant duplicate terminal report.

- [ ] **Step 5: Implement result UI state and retry**

Pending disables Restart/Menu; Saved shows rewards and enables both; Failed shows the failure and enables only Retry. Repeated clicks cannot create a second transition or settlement.

- [ ] **Step 6: Run GREEN and visual checks**

Run focused EditMode, focused PlayMode, result UI screenshot probe at 960x540, full EditMode, full PlayMode, asset/Pester, and diff checks.

- [ ] **Step 7: Review and commit locally**

Complete task review and commit `feat: complete online battle settlement flow` without pushing.

### Task 7: Real Waves, Victory/Defeat, Persistence, And Full Regression

**Files:**
- Modify: client `Assets/Tests/PlayMode/BattleCombatLoopTests.cs`
- Create: client `Assets/Tests/PlayMode/OnlineBattleCompletionTests.cs` and `.meta`
- Modify: client `Assets/Tests/PlayMode/RealBackendOnlineFlowTests.cs`
- Modify: client `tools/integration/Invoke-A4BackendIntegration.ps1`
- Modify: client `CLAUDE.md`
- Modify: backend `README.md`, `AGENTS.md`
- Update: this plan's evidence and checkboxes

**Interfaces:**
- Consumes: completed protobuf/archive/settlement flows.
- Produces: authoritative real-process and Unity evidence.

- [ ] **Step 1: Add the real short-wave RED test**

Construct two deterministic waves with real enemies. Start the real spawner, damage each enemy through its health API, observe death/unregister, wait for the next wave, and reach Victory through `OnAllWavesComplete`. Direct event invocation or reflection terminal triggering is forbidden.

- [ ] **Step 2: Add terminal correctness tests**

Use real lethal player damage for Defeat. Assert one completion, one local progression report, one settlement request, result UI state ordering, retry behavior, and one scene transition.

- [ ] **Step 3: Add real backend victory persistence flow**

Run protobuf Online startup, short real waves, Victory, settlement, archive save, return to menu, reload, and assert rewards, total games/kills, highest clear, and the same server identity. Add a focused real Defeat settlement assertion.

- [ ] **Step 4: Verify default battle configuration**

Load the production BattleScene, assert ten waves, 181 configured enemies, final boss wave, valid player and UI, and no duplicate completion owner. Run a bounded accelerated smoke without replacing the production config asset.

- [ ] **Step 5: Run all authoritative gates**

Run protobuf generation drift checks, backend full tests/vet/build, client asset/Pester, full EditMode, full PlayMode, focused real backend runner, screenshots, `git diff --check`, and process/port/environment cleanup.

- [ ] **Step 6: Complete final reviews and evidence**

Run whole-branch specification and quality reviews across both repositories, fix every Critical/Important finding, rerun affected and full gates, and record exact totals, paths, SHAs, PIDs, and cleanup state.

- [ ] **Step 7: Commit locally**

Commit documentation/evidence in each affected repository without pushing.

### Task 8: One-Push Delivery

**Files:**
- No production file changes; delivery-only Git operations after clean verification.

**Interfaces:**
- Produces: remote feature and master refs at reviewed commits.

- [ ] **Step 1: Verify local branches and remote ancestry**

Require clean client/backend feature worktrees, clean primary masters, remote masters equal the recorded merge bases, all local commits reviewed, ports `8080/8081` free, no Unity/server processes, and empty integration environment.

- [ ] **Step 2: Push each repository exactly once**

Use one atomic push command per repository to update both refs:

```powershell
git push --atomic origin HEAD:refs/heads/feature/protobuf-battle-completion HEAD:refs/heads/master
```

This is one push for the client repository and one push for the backend repository.

- [ ] **Step 3: Verify remote exactness and align local masters**

Use `git ls-remote` to prove feature/master SHAs equal the reviewed heads. Fast-forward local primary masters to those commits without another push. Preserve worktrees until evidence paths have been copied or regenerated in primary roots.

- [ ] **Step 4: Final cleanup audit**

Require both primary statuses clean, all four remote refs exact, `8080/8081` free, relevant process count zero, and no integration environment value.

## Execution Handoff

The user selected Subagent-Driven execution and explicitly requested continuous work without confirmation. Execute Tasks 1-8 in order, review after each implementation task, and do not push until Task 8.
