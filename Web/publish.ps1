<#
.SYNOPSIS
    Builds the website and uploads it to the hosting (bs3d.winphonew.eu, #815).

.DESCRIPTION
    Builds Web/ with Eleventy into a clean _site/ and uploads every file in it to the subdomain's document root on the
    Webglobe hosting, over FTPS (curl) or SFTP (Windows' own OpenSSH sftp). Nothing is deleted on the server: a file
    the site no longer has stays there until it is removed by hand.

    The credentials come from the environment and never from a file, since this repository is public:
      BS3D_WEB_HOST      the server, e.g. the hosting's FTP or SSH host name
      BS3D_WEB_USER      the WEB account's login (never the off-site backup's account: see docs/website.md)
      BS3D_WEB_ROOT      the subdomain's document root on the server, e.g. /www/bs3d (no trailing slash)
      BS3D_WEB_PASSWORD  FTPS only; SFTP asks for the password itself, or uses your SSH key

    See docs/website.md for the whole route: the build, the DNS at Cloudflare, the hosting and this script.

.PARAMETER Protocol
    ftps or sftp, whichever the hosting plan gives.

.PARAMETER DryRun
    Build and list what would be uploaded, and upload nothing.

.EXAMPLE
    $env:BS3D_WEB_HOST = '...'; $env:BS3D_WEB_USER = '...'; $env:BS3D_WEB_ROOT = '/www/bs3d'
    .\Web\publish.ps1 -Protocol sftp
#>
param(
    [Parameter(Mandatory)][ValidateSet('ftps', 'sftp')][string]$Protocol,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$web = $PSScriptRoot
$site = Join-Path $web '_site'

# A clean build: Eleventy writes over _site but never removes what a page no longer makes
if (Test-Path $site) { Remove-Item -Recurse -Force $site }
Push-Location $web
try {
    if (-not (Test-Path (Join-Path $web 'node_modules'))) { npm ci --no-audit --no-fund; if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' } }
    npx eleventy
    if ($LASTEXITCODE -ne 0) { throw "eleventy failed (exit $LASTEXITCODE)" }
}
finally {
    Pop-Location
}

$files = Get-ChildItem -Recurse -File $site
$relative = $files | ForEach-Object { $_.FullName.Substring($site.Length + 1).Replace('\', '/') }
"{0} files, {1:N1} MB" -f $files.Count, (($files | Measure-Object Length -Sum).Sum / 1MB) | Write-Host

if ($DryRun) {
    $relative | ForEach-Object { Write-Host "  $_" }
    Write-Host 'Dry run: nothing uploaded.'
    return
}

foreach ($name in 'BS3D_WEB_HOST', 'BS3D_WEB_USER', 'BS3D_WEB_ROOT') {
    if (-not [Environment]::GetEnvironmentVariable($name)) { throw "$name is not set (see the help: Get-Help .\Web\publish.ps1)" }
}
$hostName = $env:BS3D_WEB_HOST
$root = $env:BS3D_WEB_ROOT.TrimEnd('/')

if ($Protocol -eq 'ftps') {
    if (-not $env:BS3D_WEB_PASSWORD) { throw 'BS3D_WEB_PASSWORD is not set (FTPS)' }
    # curl.exe, not PowerShell's alias of the same name; explicit TLS from the first byte, folders made as needed
    for ($i = 0; $i -lt $files.Count; $i++) {
        $url = "ftp://$hostName$root/$($relative[$i])"
        curl.exe --silent --show-error --ssl-reqd --ftp-create-dirs --user "$($env:BS3D_WEB_USER):$($env:BS3D_WEB_PASSWORD)" -T $files[$i].FullName $url
        if ($LASTEXITCODE -ne 0) { throw "upload of $($relative[$i]) failed (curl exit $LASTEXITCODE)" }
        Write-Host "  $($relative[$i])"
    }
}
else {
    # One sftp session from a batch file: make every folder (a "-" prefix ignores "already exists"), then put each file
    $batch = New-TemporaryFile
    try {
        $folders = $relative | ForEach-Object { if ($_.Contains('/')) { $_.Substring(0, $_.LastIndexOf('/')) } } | Sort-Object -Unique
        $lines = @()
        foreach ($folder in $folders) {
            $parts = $folder.Split('/')
            for ($depth = 1; $depth -le $parts.Count; $depth++) { $lines += "-mkdir `"$root/$(($parts[0..($depth - 1)]) -join '/')`"" }
        }
        for ($i = 0; $i -lt $files.Count; $i++) { $lines += "put `"$($files[$i].FullName)`" `"$root/$($relative[$i])`"" }
        $lines | Select-Object -Unique | Set-Content -Encoding ascii $batch
        sftp -b $batch "$($env:BS3D_WEB_USER)@$hostName"
        if ($LASTEXITCODE -ne 0) { throw "sftp failed (exit $LASTEXITCODE)" }
    }
    finally {
        Remove-Item $batch
    }
}

Write-Host "Published to $hostName$root. Check https://bs3d.winphonew.eu/ (a hard reload: Cloudflare may cache the old files)."
