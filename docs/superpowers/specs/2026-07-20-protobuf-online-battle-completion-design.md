# Protobuf Online Battle Completion Design

## Goal

Replace every WebSocket JSON payload with generated Protocol Buffers messages and complete a playable, persistent flow from online login through battle, victory or defeat, server settlement, archive save, result UI, return to menu, and reload.

## Delivery Boundary

This phase delivers:

- one canonical protobuf schema for all 32 routed message IDs;
- generated Go and C# message classes using official protobuf runtimes;
- the existing six-byte little-endian frame envelope with protobuf bytes as its body;
- typed player archive load/save without JSON inside the WebSocket protocol;
- exactly-once battle settlement using a client run ID and a server uniqueness boundary;
- development-backend combat settlement so the real Unity test does not require MySQL or Redis;
- one local completion path for both Victory and Defeat;
- result UI settlement states, retry, and navigation that cannot duplicate rewards;
- automated short-wave completion through real spawn, damage, death, and wave completion;
- a default ten-wave configuration smoke check and a real backend PlayMode flow.

This phase does not replace the WebSocket transport, the frame header, the A3 reconnect owner, or the client-authoritative combat simulation. It does not add old JSON compatibility because the project has no released protocol consumer. HTTP payment callbacks remain JSON because they are an external WeChat HTTP contract, not the game WebSocket protocol.

## Chosen Approach

Use standard generated types from `protoc` for both languages. The backend owns the canonical schema at `proto/game/v1/messages.proto`; generated Go code lives in `internal/protocolpb`, and generated C# code is copied to `Assets/Scripts/Protocol/Generated`. The Unity runtime vendors `Google.Protobuf.dll` under `Assets/Plugins/Google.Protobuf`.

The company project's useful pattern is preserved: schema, route IDs, payload codec, and transport envelope remain separate. Its Lua descriptor loader, XLua tables, large hand-maintained message map, Erlang serial/CRC envelope, and protobuf-net legacy generator are not copied.

## Toolchain

- Protobuf compiler: `protoc 35.0`.
- Go runtime: `google.golang.org/protobuf v1.36.11`.
- Go generator: `protoc-gen-go v1.36.11`.
- C# runtime: `Google.Protobuf 3.35.1`, using its .NET Framework 4.5 assembly for Unity 2022.3.
- Generated sources and the Unity runtime DLL are committed.
- `tools/protobuf/Generate-Protocol.ps1` generates both outputs from the backend schema and supports `-Check` to fail when committed output drifts.

## Wire Contract

The frame remains:

```text
uint32 little-endian total_length
uint16 little-endian message_id
protobuf payload bytes
```

`total_length` includes the six-byte header. The client continues to reject frames larger than 64 KiB. The backend WebSocket read limit remains 4 MiB as a transport-level defense, while `protocol.Decode` enforces the 64 KiB application frame limit.

The login golden vector is fixed on both sides:

```text
LoginReq { code: "abc" }
body  = 0A 03 61 62 63
frame = 0B 00 00 00 E9 03 0A 03 61 62 63
```

No codec guesses JSON versus protobuf from payload bytes. Client and server upgrade atomically.

## Schema

The schema uses:

```proto
syntax = "proto3";
package game.protocol.v1;
option go_package = "game-server/internal/protocolpb;protocolpb";
option csharp_namespace = "Game.Protocol";
```

`MessageId` preserves all existing numeric IDs: Login `1001-1004`, archive `2001-2004`, rank `3001-3004`, combat `4001-4014`, payment `5001-5003`, GM `6001-6002`, and Error `9999`. Existing `MsgID` compatibility constants remain so application call sites do not duplicate route numbers.

All existing request/response messages move into the schema. `PayResultNotify` becomes an explicit message with `order_no`, `status`, and `product_id`. `GMCommandReq.args_json` is `bytes`; it remains operator-supplied JSON data inside a typed protobuf field and is never used as the WebSocket envelope codec.

The archive contract is typed:

```proto
message PlayerArchive {
  int32 schema_version = 1;
  int32 gold = 2;
  int32 exp = 3;
  int64 best_score = 4;
  int64 total_kills = 5;
  int64 total_games = 6;
  int32 highest_cleared_dungeon = 7;
  int32 talent_points = 8;
  repeated int32 unlocked_styles = 9;
  int32 last_style_id = 10;
}

message SaveArchiveReq { PlayerArchive archive = 1; }
message SaveArchiveResp { bool success = 1; }
message LoadArchiveReq {}
message LoadArchiveResp {
  bool found = 1;
  PlayerArchive archive = 2;
}
```

Archive storage becomes bytes. Reads and writes copy byte slices, missing rows map to `found=false`, and malformed stored protobuf is an error rather than a new-player response.

Battle settlement uses:

```proto
enum BattleOutcome {
  BATTLE_OUTCOME_UNSPECIFIED = 0;
  BATTLE_OUTCOME_VICTORY = 1;
  BATTLE_OUTCOME_DEFEAT = 2;
}

message CombatResultReq {
  string run_id = 1;
  int32 dungeon_level = 2;
  int64 score = 3;
  int32 kills = 4;
  double survival_time = 5;
  int32 style_id = 6;
  BattleOutcome outcome = 7;
  int32 player_level = 8;
}

message CombatResultResp {
  bool success = 1;
  bool duplicate = 2;
  int32 reward_gold = 3;
  int32 reward_exp = 4;
  int64 best_score = 5;
  PlayerArchive archive = 6;
  string run_id = 7;
}
```

`SubmitScoreReq` uses a `ScoreMetadata` message instead of a JSON metadata string. `CombatResultReq` removes the JSON combat log; the current server validates bounded typed counters and does not consume event logs.

## Backend Architecture

`protocol.Encode` and the kernel use `proto.Marshal` and `proto.Unmarshal`. Kernel registration accepts generated pointer messages and rejects handlers whose request or response does not implement `proto.Message`. HTTP callback JSON stays inside `internal/payment`.

`CombatSettlementService` validates the request and delegates exactly-once persistence to a narrow repository. Production persistence uses one MySQL transaction and a `combat_settlements` unique index on `(player_id, run_id)`. The transaction creates or locks player stats, applies rewards and totals once, updates best score, stores a score record, and stores the response snapshot. A duplicate returns the stored response with `duplicate=true` and does not change stats.

`MemoryDevelopmentStore` implements the same settlement contract under one mutex. Development runtime registers Login, Heartbeat, SaveArchive, LoadArchive, CombatResult, and GetPlayerStats. It still does not create MySQL, Redis, payment, GM, rank, or the other production combat configuration handlers.

The settlement reward formula remains the current formula: kills multiplied by configured gold and experience per kill. Outcome changes completion progress: Victory advances `highest_cleared_dungeon`; Defeat does not. No unreviewed balance bonus is introduced.

## Client Architecture

`NetworkClient` sends `IMessage` and parses inbound bytes with an explicit `ProtocolMessageRegistry` keyed by MsgID. Registration checks that the requested generic type matches the generated parser. Raw byte subscriptions remain internal for protocol diagnostics only.

`OnlineSessionHost` owns a `BattleSettlementCoordinator` beside login and one shared `ArchiveSessionService`. It accepts one terminal result per `run_id`, sends `CombatResultReq`, merges the returned `PlayerArchive`, serializes every load/save through that single archive operation owner, and reports completion only after the owning `SaveArchiveResp.success`. A busy operation rejects the later caller without failing or replacing the active owner; the session coordinator handles only startup, reload, and main-save responses it initiated, while battle settlement handles only its pending save. If the connection generation changes while settlement is pending, the coordinator resends the same `run_id`; the backend idempotency record prevents duplicate rewards.

Loaded archives hydrate a `PlayerProgressState` owned by the online host. Offline mode uses a scene-local settlement gateway that completes immediately while retaining local `PlayerPrefs` compatibility. The dormant `DungeonManager -> CombatManager` terminal reporting path is removed from active ownership so there is one completion producer.

`BattleRunController` emits one terminal completion containing outcome and captured result data after winning the existing state-machine race. Both outcomes report achievements and talent progress exactly once. It freezes gameplay before settlement begins.

`GameOverUI` shows the result immediately with a settlement status:

- Offline: navigation buttons enabled immediately.
- Online pending: navigation buttons disabled and status shows settlement in progress.
- Online saved: reward totals appear and navigation buttons enable.
- Online failed: retry enables; restart and menu remain disabled until the same run is settled or the player explicitly returns through the existing safe scene teardown after a successful retry.

Repeated terminal events, repeated response frames, double button clicks, reconnect resends, and object destruction are idempotent.

## Battle Acceptance Flow

The deterministic short-flow PlayMode test constructs the real `WaveSpawner`, real enemies, real damage/death path, and real `BattleRunController`. It configures two short waves, kills spawned enemies through their health API, and waits for `OnAllWavesComplete`; it never invokes the terminal callback directly.

The real backend PlayMode flow performs:

```text
Online startup -> MenuScene -> BattleScene
short real waves -> Victory -> protobuf CombatResult
reward response -> protobuf SaveArchive -> result UI saved
return MenuScene -> ReloadArchive -> persisted progress assertions
```

A second focused test covers Defeat through real lethal player damage and proves one settlement. Additional tests cover duplicate completion, duplicate server request, reconnect resend, settlement failure/retry, and default ten-wave configuration containing the expected 181 enemies and terminal boss wave.

## Error Handling

- Unknown MsgID, malformed protobuf, wrong registered type, oversized frame, missing `run_id`, unspecified outcome, invalid counters, and malformed stored archive all fail closed.
- Error responses are protobuf `ErrorResp` messages.
- Settlement failures do not unlock navigation or mutate the client archive.
- Archive save failure retains the settled response and retries save without resubmitting a new run ID.
- Shutdown disposes subscriptions and pending callbacks; late frames are ignored by generation/run identity.

## Verification

- Go generated-code check, golden frame tests, protocol/kernel tests, package tests, `go test ./...`, `go vet ./...`, and `go build ./...`.
- Unity generation drift check, asset integrity, Pester `5/5`, protobuf codec/registry EditMode tests, full EditMode, focused real-wave PlayMode, full PlayMode, and canvas screenshots when UI state changes.
- Real backend runner verifies protobuf login, typed archive load/save, combat settlement, reload, exact log evidence, environment restoration, captured PID cleanup, and ports `8080/8081` free.
- Final task reviews and whole-branch reviews must have no open Critical or Important findings.

## Delivery

Work is committed incrementally on isolated client and backend feature branches. Per the user's request, neither repository is pushed during implementation; each repository is pushed once after final review and verification, then its clean `master` is fast-forwarded locally and the already-pushed feature commit is used to update remote `master` without a second object upload.
