$ErrorActionPreference = "Stop"

$pluginPath = Join-Path $PSScriptRoot "../BossMod/Framework/Plugin.cs"
$observerPath = Join-Path $PSScriptRoot "../BossMod/Framework/PassiveActionObserver.cs"
$manifestPath = Join-Path $PSScriptRoot "../BossMod/BossModRebornRadar.json"

$plugin = Get-Content $pluginPath -Raw
$observer = Get-Content $observerPath -Raw
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

$forbiddenBootstrapPatterns = @(
    "IPCProvider",
    "ActionManagerEx",
    "MovementOverride",
    "RotationModuleManager",
    "AIManager",
    "ExecuteHints",
    "UseAction(",
    "InteractWithObject",
    "LevelSync("
)

foreach ($pattern in $forbiddenBootstrapPatterns) {
    if ($plugin.Contains($pattern)) {
        throw "Radar boundary violation in Plugin.cs: $pattern"
    }
}

if ($observer.Contains("ActionRequest") -or $observer.Contains("UseAction")) {
    throw "PassiveActionObserver must not intercept or execute action requests."
}

$hookCount = ([regex]::Matches($observer, "HookAddress<")).Count
if ($hookCount -ne 1 -or -not $observer.Contains("ActionEffectHandler.Delegates.Receive")) {
    throw "PassiveActionObserver may only hook ActionEffectHandler.Receive."
}

if ($manifest.InternalName -ne "BossModRebornRadar") {
    throw "Unexpected plugin InternalName: $($manifest.InternalName)"
}

Write-Host "Radar-only boundary verified."
