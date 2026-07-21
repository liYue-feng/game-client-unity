$supportPath = Join-Path $PSScriptRoot 'BackendIntegrationSupport.ps1'

Describe 'Backend integration support behavior' {
    BeforeAll { . $supportPath }

    It 'rejects occupied integration ports without stopping their owner' {
        Mock Get-IntegrationPortListeners { @([pscustomobject]@{ Port = 8080 }) }
        { Assert-IntegrationPortsFree } | Should Throw
        Assert-MockCalled Get-IntegrationPortListeners 1
    }

    It 'requires only the owned backend listener while health is ready' {
        Mock Get-IntegrationPortListeners { @([pscustomobject]@{ Port = 8080 }) }
        { Assert-OwnedIntegrationPortsReady } | Should Not Throw

        Mock Get-IntegrationPortListeners { @([pscustomobject]@{ Port = 8080 }, [pscustomobject]@{ Port = 8081 }) }
        { Assert-OwnedIntegrationPortsReady } | Should Throw

        Mock Get-IntegrationPortListeners { @() }
        { Assert-OwnedIntegrationPortsReady } | Should Throw
    }

    It 'restores PATH and GAME_BACKEND_INTEGRATION' {
        $savedPath = $env:PATH
        $savedIntegration = [Environment]::GetEnvironmentVariable('GAME_BACKEND_INTEGRATION', 'Process')
        try {
            $env:PATH = 'changed-path'
            [Environment]::SetEnvironmentVariable('GAME_BACKEND_INTEGRATION', 'changed', 'Process')
            Restore-IntegrationEnvironment -OriginalPath $savedPath -OriginalIntegrationEnvironment $savedIntegration
            $env:PATH | Should Be $savedPath
            [Environment]::GetEnvironmentVariable('GAME_BACKEND_INTEGRATION', 'Process') | Should Be $savedIntegration
        }
        finally {
            $env:PATH = $savedPath
            [Environment]::SetEnvironmentVariable('GAME_BACKEND_INTEGRATION', $savedIntegration, 'Process')
        }
    }

    It 'reads a closed Unity XML result and rejects an incomplete document' {
        $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("unity xml with spaces-{0}" -f [guid]::NewGuid())
        $complete = Join-Path $fixtureRoot 'complete.xml'
        $incomplete = Join-Path $fixtureRoot 'incomplete.xml'
        try {
            New-Item -ItemType Directory -Force -Path $fixtureRoot | Out-Null
            Set-Content -LiteralPath $complete -Value '<test-run total="3" passed="3" failed="0" skipped="0"></test-run>'
            Set-Content -LiteralPath $incomplete -Value '<test-run total="3">'
            $result = Read-UnityTestResult -Path $complete
            $result.Total | Should Be 3
            $result.Passed | Should Be 3
            { Read-UnityTestResult -Path $incomplete } | Should Throw
        }
        finally { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force -ErrorAction SilentlyContinue }
    }
}
