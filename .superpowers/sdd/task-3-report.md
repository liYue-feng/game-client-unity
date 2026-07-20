# Task 3 Report: Protobuf Network Client

## RED / GREEN

- RED specification was written first in `Assets/Tests/EditMode/Protocol/ProtobufGoldenFrameTests.cs`.
- The first Unity invocation did not compile or emit XML because its licensing client failed before
  script compilation. This is excluded as TDD execution evidence.
- GREEN evidence: focused protobuf XML 6/6 passed; focused `NetworkClientTests` XML 9/9
  passed; full EditMode XML 213/213 passed.

## Runtime

- Google.Protobuf 3.35.1 was downloaded from the official NuGet flat container and checked
  against the official NuGet catalog SHA-512. Package SHA-256:
  `6BA51589915E3640E1FFDD384863DD0D73F0CA6A8AAC591EC81C42C6A3EE55CE`.
- Vendored `net45` Google.Protobuf.dll SHA-256:
  `452BCD1AE7A4BA8245702B3B07E0A3A15120090E2C74295DAC3E9D1199C7F45D`.
- Unity 2022.3 project API compatibility level is `6` (.NET Standard 2.1). Unity supplies
  `System.Memory`; the actual loader additionally required vendored
  System.Runtime.CompilerServices.Unsafe 4.5.2. Its NuGet package was catalog-verified and
  its DLL SHA-256 is `1AD2DD7225D5162A0FD3A3B337A1949448520E3130A4BC8E010EC02F76097500`.
- No duplicate assembly or unresolved reference remained in the successful Unity runs.

## Verification

- `tools/protobuf/Verify-GeneratedProtocol.ps1`: passed, staged C# SHA-256
  `50B20EF609A0718D72E4740F910904181F5461941F00235828F5B1B43ACEFC29`.
- Pester generated/staging checks: 3/3 passed. Unity asset integrity Pester checks: 5/5
  passed. The final generated/staging gate is 4/4 and proves both vendored DLL paths are
  tracked by Git. `Test-UnityAssetIntegrity.ps1` passed.
- Full PlayMode XML: 98/99 passed, 0 failed, 1 ignored because the real-backend flow is
  opt-in. The `-nographics` attempt hit an existing Camera.Render native crash in the visual
  evidence test; the graphics-enabled retry produced the passing XML.

## Durable PlayMode Evidence

- HEAD: `956daead3e1f1986f62e2c81d490983cc99be133`.
- Current run timestamp: XML `start-time=2026-07-20 15:13:16Z`,
  `end-time=2026-07-20 15:14:33Z`, duration `76.6910544` seconds.
- Result: total `99`, passed `98`, failed `0`, ignored `1` (the opt-in real backend flow).
- Exact command:
  `D:\Unity_Soft\2022\Editor\Unity.exe -batchmode -projectPath E:\Own_project\game-client-unity\.worktrees\protobuf-battle-completion -runTests -testPlatform PlayMode -testResults E:\Own_project\game-client-unity\.worktrees\protobuf-battle-completion\Logs\task3-playmode-final.xml -logFile E:\Own_project\game-client-unity\.worktrees\protobuf-battle-completion\Logs\task3-playmode-final.log`
- Evidence paths: `Logs/task3-playmode-final.xml` and `Logs/task3-playmode-final.log`
  (absolute: `E:\Own_project\game-client-unity\.worktrees\protobuf-battle-completion\Logs\task3-playmode-final.xml`
  and `E:\Own_project\game-client-unity\.worktrees\protobuf-battle-completion\Logs\task3-playmode-final.log`).
- `git diff --check` passed.
- Static scans found no JsonUtility protocol bridge, handwritten protocol class, public raw
  string subscription/send API, or lowercase generated property access.

## Commit

`956daead3e1f1986f62e2c81d490983cc99be133 refactor: use protobuf network client`

## Concerns

- Unity logs a licensing-token refresh warning in batch mode, but actual compiler output and
  all requested XML test results were produced successfully.
- Archive hydration remains intentionally deferred: absent archives use `new PlayerArchive()`.
