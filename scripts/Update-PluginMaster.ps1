param(
    [Parameter(Mandatory = $true)]
    [string] $Version
)

$ErrorActionPreference = "Stop"
if ($Version -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "Version must contain four numeric components."
}

$catalogPath = Join-Path $PSScriptRoot "../pluginmaster.json"
$catalog = @(Get-Content $catalogPath -Raw | ConvertFrom-Json)
$plugin = $catalog[0]
$download = "https://github.com/Surlako/BossModRebornRadar/releases/download/$Version/latest.zip"

$plugin.AssemblyVersion = $Version
$plugin.DownloadLinkInstall = $download
$plugin.DownloadLinkUpdate = $download
$plugin.DownloadLinkTesting = $download
$plugin.Changelog = "Radar-only companion release $Version, synchronized with current BossModReborn encounter modules."

ConvertTo-Json -InputObject $catalog -Depth 10 | Set-Content $catalogPath -Encoding utf8NoBOM
