$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$reportPath = Join-Path $projectRoot 'docs/combat-resource-gap-report.md'
$scriptsRoot = Join-Path $projectRoot 'Assets/Scripts'

function Get-GapRows {
    return @(Get-Content -Encoding UTF8 $reportPath |
        Where-Object { $_ -match '^\| (SPR|AUD|ART|FONT)-' } |
        ForEach-Object {
            $columns = $_ -split '\|'
            [pscustomobject]@{
                Id = $columns[1].Trim()
                Status = $columns[2].Trim()
                Command = $columns[8].Trim()
                Raw = $_
            }
        })
}

Describe 'Combat resource gap report handoff' {
    It 'keeps the exact generated and licensed inventory counts' {
        $rows = @(Get-GapRows)

        $rows.Count | Should Be 59
        @($rows.Id | Sort-Object -Unique).Count | Should Be 59
        @($rows | Where-Object { $_.Status -eq 'placeholder-generated' }).Count | Should Be 25
        @($rows | Where-Object { $_.Status -eq 'source-needed-final' }).Count | Should Be 25
    }

    It 'gives every source-needed row a literal generator and graphical test command' {
        $rows = @(Get-GapRows | Where-Object { $_.Status -eq 'source-needed' })

        $rows.Count | Should Be 7
        foreach ($row in $rows) {
            $row.Command | Should Match '-executeMethod Game\.Editor\.CombatAssetGenerator\.GenerateAll'
            $row.Command | Should Match '-runTests'
            $row.Command | Should Match 'Start-Process'
        }
    }

    It 'states the current animator gap and an executable future wiring contract' {
        $report = Get-Content -Raw -Encoding UTF8 $reportPath

        $report | Should Match 'Current runtime creates `SpriteRenderer` only; no `Animator` is attached\.'
        $report | Should Match 'Assets/Scripts/Game/BattleSceneSetup\.cs:225.*CreatePlayer'
        $report | Should Match 'Assets/Scripts/Game/Dungeon/WaveSpawner\.cs:96.*CreateEnemy'
        $report | Should Match 'serialized `RuntimeAnimatorController`'
        $report | Should Match 'Add serialized `RuntimeAnimatorController` fields to `WaveSpawner`'
        foreach ($field in @('gruntAnimatorController', 'archerAnimatorController', 'eliteAnimatorController', 'bossAnimatorController')) {
            $report | Should Match ([regex]::Escape($field))
        }
        $report | Should Match 'AddComponent<Animator>\(\)'
        $report | Should Match 'Assets/Scenes/BattleScene\.unity'
        $report | Should Match 'Assets/Tests/PlayMode/CombatAnimatorResourceTests\.cs'
        foreach ($state in @('Idle', 'Run', 'Attack', 'Hurt', 'Death')) {
            $report | Should Match ([regex]::Escape($state))
        }
    }

    It 'states the LegacyRuntime reality and the future shared font provider contract' {
        $report = Get-Content -Raw -Encoding UTF8 $reportPath
        $scriptFiles = @(Get-ChildItem $scriptsRoot -Recurse -Filter '*.cs' -File)
        $legacyMatches = @(Select-String -Path $scriptFiles.FullName `
            -Pattern 'Resources\.GetBuiltinResource<Font>\("LegacyRuntime\.ttf"\)' `
            -AllMatches)
        $directCallCount = ($legacyMatches | ForEach-Object { $_.Matches.Count } |
            Measure-Object -Sum).Sum
        $uniqueFileCount = @($legacyMatches.Path | Sort-Object -Unique).Count
        $derivedInventoryPhrase =
            "$directCallCount direct call sites across $uniqueFileCount files"

        $report.Contains($derivedInventoryPhrase) | Should Be $true
        $report | Should Match 'Resources\.GetBuiltinResource<Font>\("LegacyRuntime\.ttf"\)'
        $report | Should Match 'Assets/Scripts/UI/Common/CombatUiFontProvider\.cs'
        $report | Should Match 'Resources\.Load<Font>\("Fonts/ZhetianUIFont"\)'
        $report | Should Match 'Assets/Scripts/Game/Visual/DamageNumber\.cs'
        $report | Should Match 'Assets/Scripts/UI/BattleUI/'
        $report | Should Match 'Assets/Scripts/UI/Common/'
        $report | Should Match 'Assets/Scripts/UI/Menu/'
        $report | Should Match "rg -n 'LegacyRuntime\\\.ttf' Assets/Scripts -g '\*\.cs'"
        $report | Should Match 'Assets/Tests/EditMode/CombatUiFontProviderTests\.cs'
        $report | Should Match 'Assets/Tests/PlayMode/CombatUiFontAssignmentTests\.cs'
        $victoryGlyph = [char]0x80dc
        $report.Contains("HasCharacter('$victoryGlyph')") | Should Be $true
    }
}
