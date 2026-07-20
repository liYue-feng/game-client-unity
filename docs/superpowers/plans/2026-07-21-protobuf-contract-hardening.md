# Protobuf Combat Duration Contract Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep `CombatResultReq.duration_ms` wire-compatible as protobuf `int64` and make protocol drift checks reliable on Windows and against the C# source Unity actually compiles.

**Architecture:** `game-server-go/proto/game/v1/messages.proto` remains the only schema source. The pinned backend generator produces Go plus both C# copies, while comparison normalizes line endings so Git's Windows checkout policy cannot create false drift. The client verifier independently rejects a missing or stale runtime copy under `Assets`.

**Tech Stack:** PowerShell 5.1, Pester 3.4, protoc 35.0, protoc-gen-go v1.36.11, Google.Protobuf 3.35.1, Go 1.24, Unity 2022.3 C#.

## Global Constraints

- Preserve `CombatResultReq` field 5 as `int64 duration_ms`; do not change the field number or protobuf scalar type.
- Preserve `CombatResultData.survivalTime` as integer seconds and its existing conversion to `DurationMs` with `* 1000L`.
- The backend schema remains the sole `.proto` source; the client must contain no `.proto` file.
- Generated-file checks must ignore CRLF versus LF only; every other byte-level text difference must fail.
- The backend generator must check or update both `tools/protobuf/generated/Messages.cs` and `Assets/Scripts/Protocol/Generated/Messages.cs` in the selected client root.
- Do not modify unrelated existing worktree changes under `.superpowers/sdd` or the backend files already reported modified.

---

### Task 1: Make Backend Generation Checks Line-Ending Stable and Runtime-Aware

**Files:**
- Modify: backend `tools/protobuf/Generate-Protocol.Tests.ps1`
- Modify: backend `tools/protobuf/Generate-Protocol.ps1`

**Interfaces:**
- Consumes: backend canonical schema `proto/game/v1/messages.proto` and the `-ClientRoot` argument.
- Produces: `Generate-Protocol.ps1 -Check` that accepts CRLF/LF-only differences and validates both committed C# destinations.

- [ ] **Step 1: Write the failing integration test**

Add a Pester case that invokes the real pinned generator against the sibling client checkout and expects `-Check` not to throw on a clean Windows checkout:

```powershell
It 'accepts checkout line endings and checks both client outputs' {
    $clientRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\game-client-unity\.worktrees\protobuf-battle-completion')).Path
    { & $scriptPath -ClientRoot $clientRoot -Check } | Should Not Throw
}
```

Resolve the client root using the existing workspace/worktree discovery pattern rather than depending on the literal sample path when implementing the test.

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
powershell.exe -NoProfile -Command "Invoke-Pester -Script tools/protobuf/Generate-Protocol.Tests.ps1 -EnableExit"
```

Expected: FAIL because the current hash comparison treats committed CRLF and generated LF as different.

- [ ] **Step 3: Implement byte-exact CRLF-only comparison and the runtime destination**

Add a byte comparison helper and use it only in `-Check` mode. It removes `0x0D` only when it immediately precedes `0x0A`, then returns a case-sensitive base64 fingerprint of every remaining byte:

```powershell
function Get-NormalizedGeneratedFingerprint {
    param([string]$Path)

    [byte[]]$source = [IO.File]::ReadAllBytes($Path)
    $normalized = New-Object 'System.Collections.Generic.List[byte]'
    for ($index = 0; $index -lt $source.Length; $index++) {
        if ($source[$index] -eq 0x0D -and $index + 1 -lt $source.Length -and $source[$index + 1] -eq 0x0A) {
            continue
        }
        $normalized.Add($source[$index])
    }

    return [Convert]::ToBase64String($normalized.ToArray())
}
```

In `Copy-OrCheckGeneratedFile`, compare normalized fingerprints with `-cne`. Define the Unity runtime destination as:

```powershell
$csharpRuntimeOutputPath = Join-Path $ClientRoot 'Assets\Scripts\Protocol\Generated\Messages.cs'
```

Call `Copy-OrCheckGeneratedFile` for both C# destinations using the same generated candidate.

- [ ] **Step 4: Run focused and full backend verification**

Run:

```powershell
powershell.exe -NoProfile -Command "Invoke-Pester -Script tools/protobuf/Generate-Protocol.Tests.ps1 -EnableExit"
powershell.exe -NoProfile -File tools/protobuf/Verify-Protocol.ps1 -ClientRoot E:\Own_project\game-client-unity\.worktrees\protobuf-battle-completion
go test ./...
```

Expected: all Pester cases pass, protocol drift check prints pinned versions and hashes, and all Go packages pass.

- [ ] **Step 5: Commit the backend task**

```powershell
git add tools/protobuf/Generate-Protocol.ps1 tools/protobuf/Generate-Protocol.Tests.ps1
git commit -m "fix: harden protobuf generation checks"
```

### Task 2: Reject a Stale Unity Runtime Generated Source

**Files:**
- Modify: client `tools/protobuf/GeneratedProtocol.Tests.ps1`
- Modify: client `tools/protobuf/Verify-GeneratedProtocol.ps1`

**Interfaces:**
- Consumes: staged `tools/protobuf/generated/Messages.cs` and runtime `Assets/Scripts/Protocol/Generated/Messages.cs`.
- Produces: a standalone verifier that rejects missing or content-divergent runtime generated code while accepting CRLF/LF-only differences.

- [ ] **Step 1: Write the failing fixture test**

Create an isolated temporary client fixture in Pester, copy the verifier into `tools/protobuf`, write a valid staged generated source, and write a different runtime source. Invoke the copied verifier and require a throw:

```powershell
It 'rejects a runtime generated source that differs from staging' {
    # Arrange fixture paths relative to a copied verifier so $PSScriptRoot resolves inside the fixture.
    { & $fixtureVerifier } | Should Throw
}
```

The fixture's staged source must include `namespace Game.Protocol`, `public sealed partial class LoginReq`, and `Google.Protobuf` so failure is specifically caused by runtime drift.

- [ ] **Step 2: Run the test and verify RED**

Run:

```powershell
powershell.exe -NoProfile -Command "Invoke-Pester -Script tools/protobuf/GeneratedProtocol.Tests.ps1 -EnableExit"
```

Expected: FAIL because the current verifier never reads the runtime `Assets` copy.

- [ ] **Step 3: Implement runtime generated-source verification**

Resolve the runtime path from `$projectRoot`, require it to exist, compare CRLF-normalized byte fingerprints for both generated files, and throw when the fingerprints differ:

```powershell
$runtimeGeneratedPath = Join-Path $projectRoot 'Assets\Scripts\Protocol\Generated\Messages.cs'
if (-not (Test-Path -LiteralPath $runtimeGeneratedPath -PathType Leaf)) {
    throw "Unity runtime generated C# protocol is missing: $runtimeGeneratedPath"
}

function Get-NormalizedGeneratedFingerprint {
    param([string]$Path)

    [byte[]]$source = [IO.File]::ReadAllBytes($Path)
    $normalized = New-Object 'System.Collections.Generic.List[byte]'
    for ($index = 0; $index -lt $source.Length; $index++) {
        if ($source[$index] -eq 0x0D -and $index + 1 -lt $source.Length -and $source[$index + 1] -eq 0x0A) {
            continue
        }
        $normalized.Add($source[$index])
    }

    return [Convert]::ToBase64String($normalized.ToArray())
}

if ((Get-NormalizedGeneratedFingerprint -Path $generatedPath) -cne (Get-NormalizedGeneratedFingerprint -Path $runtimeGeneratedPath)) {
    throw "Unity runtime generated C# protocol differs from staging: $runtimeGeneratedPath"
}
```

- [ ] **Step 4: Run focused client verification**

Run:

```powershell
powershell.exe -NoProfile -Command "Invoke-Pester -Script tools/protobuf/GeneratedProtocol.Tests.ps1 -EnableExit"
powershell.exe -NoProfile -File tools/protobuf/Verify-GeneratedProtocol.ps1
```

Expected: all Pester cases pass and the verifier prints matching output hashes.

- [ ] **Step 5: Commit the client task and plan**

```powershell
git add docs/superpowers/plans/2026-07-21-protobuf-contract-hardening.md tools/protobuf/GeneratedProtocol.Tests.ps1 tools/protobuf/Verify-GeneratedProtocol.ps1
git commit -m "fix: verify Unity protobuf runtime source"
```

### Task 3: Verify the End-to-End Combat Contract

**Files:**
- Verify only: backend `proto/game/v1/messages.proto`
- Verify only: backend `internal/protocolpb/messages.pb.go`
- Verify only: client `Assets/Scripts/Protocol/Generated/Messages.cs`
- Verify only: client `Assets/Scripts/Online/BattleSettlementService.cs`

**Interfaces:**
- Consumes: the hardened generator/verifiers from Tasks 1 and 2.
- Produces: fresh evidence that field 5 is `int64`/`long`, generated outputs are synchronized, and combat settlement tests remain green.

- [ ] **Step 1: Run both protocol verifiers**

```powershell
powershell.exe -NoProfile -File E:\Own_project\game-server-go\.worktrees\protobuf-battle-completion\tools\protobuf\Verify-Protocol.ps1 -ClientRoot E:\Own_project\game-client-unity\.worktrees\protobuf-battle-completion
powershell.exe -NoProfile -File E:\Own_project\game-client-unity\.worktrees\protobuf-battle-completion\tools\protobuf\Verify-GeneratedProtocol.ps1
```

Expected: both commands pass with no generated drift.

- [ ] **Step 2: Run backend combat tests**

```powershell
go test ./internal/combat ./internal/store ./internal/protocol ./internal/protocolpb
```

Expected: all selected packages pass.

- [ ] **Step 3: Inspect the compiled C# request contract**

```powershell
rg -n "DurationMsFieldNumber = 5|public long DurationMs|DurationMs = Math.Max" Assets/Scripts/Protocol/Generated/Messages.cs Assets/Scripts/Online/BattleSettlementService.cs
```

Expected: field number `5`, generated C# type `long`, and integer-seconds-to-milliseconds conversion using `1000L`.

- [ ] **Step 4: Confirm no unintended schema or generated-code changes**

```powershell
git status --short
git diff --check
```

Expected: only planned script, test, and plan files are changed or committed; generated outputs and schema remain unchanged.

## Self-Review

- Spec coverage: the plan covers the current wire-type concern, generator false drift, runtime C# drift, and combat regression verification.
- Placeholder scan: no deferred implementation or unspecified test remains.
- Type consistency: `.proto int64` maps to Go `int64` and C# `long`; `CombatResultData.survivalTime` remains integer seconds and is converted once to milliseconds.
