<#
.SYNOPSIS
Makes the key that signs BS3D's Android releases, once, and hands it to GitHub (#1053).

.DESCRIPTION
An Android app's signature is its identity: a phone refuses an update signed with another key, so there is one key
for the life of the app, it belongs to the owner, and it is never made a second time. This script

  1. makes it (RSA 4096, alias "bs3d") in %USERPROFILE%\BS3D-signing\bs3d-release.keystore, under a password
     it asks for twice,
  2. gives release.yml what its release-android job needs, as two repository secrets
     (ANDROID_KEYSTORE_BASE64, ANDROID_KEYSTORE_PASSWORD),
  3. writes the certificate's SHA-256 to GameAndroid\release-certificate.sha256, which is to be committed: from
     then on a release whose APK is signed by any other key fails in CI, and so does a release with no key at all.

The keystore file and its password are the only copies. Lose either and no installed game can ever be updated:
keep both somewhere that is not this PC.

It refuses to run when the certificate file or the keystore is already there.

.PARAMETER Rehearse
Tries the whole thing without leaving anything: the key is made in a temp folder under a fixed password and deleted,
no secret is set and nothing is written into the repository.
#>
param([switch]$Rehearse)

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$pinPath = Join-Path $repo 'GameAndroid\release-certificate.sha256'
$folder = if ($Rehearse) { Join-Path $env:TEMP "bs3d-signing-rehearsal-$PID" } else { Join-Path $env:USERPROFILE 'BS3D-signing' }
$store = Join-Path $folder 'bs3d-release.keystore'

if (-not $Rehearse) {
    if (Test-Path $pinPath) { throw "$pinPath exists: BS3D already has its signing key, and an app's key is never made twice." }
    if (Test-Path $store) { throw "$store exists already. If it is BS3D's key, it is the one to keep; this script makes a key only where there is none." }
}

# keytool comes with the JDK the Android build already uses
$keytool = @("$env:LOCALAPPDATA\Android\jdk\bin\keytool.exe", "$env:JAVA_HOME\bin\keytool.exe") |
    Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $keytool) { throw 'keytool.exe was not found under %LOCALAPPDATA%\Android\jdk\bin or %JAVA_HOME%\bin (the JDK 17 that builds GameAndroid).' }

if (-not $Rehearse) {
    gh auth status | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'gh is not signed in (gh auth login): the secrets could not be set.' }
}

function Read-Plain([string]$prompt) {
    $secure = Read-Host $prompt -AsSecureString
    $pointer = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

if ($Rehearse) {
    $password = 'rehearsal-password'
}
else {
    Write-Host 'This makes the one key that signs every Android release of BS3D.'
    Write-Host 'Write the password down before typing it: it cannot be recovered or changed back in.'
    $password = Read-Plain 'A password for the key (12 or more plain characters, no spaces)'
    if ($password.Length -lt 12) { throw 'The password is shorter than 12 characters.' }
    # Plain ASCII only: it travels through a pipe to gh and through an environment variable to the build
    if ($password -notmatch '^[\x21-\x7E]+$') { throw 'The password may hold only plain letters, digits and punctuation (no spaces, no accents).' }
    if ((Read-Plain 'The same password again') -cne $password) { throw 'The two passwords differ.' }
}

New-Item -ItemType Directory -Force $folder | Out-Null

# The password reaches keytool through the environment, never on a command line
$env:BS3D_SIGNING_PASSWORD = $password
try {
    & $keytool -genkeypair -keystore $store -storetype PKCS12 -alias bs3d -keyalg RSA -keysize 4096 -validity 18250 `
        -storepass:env BS3D_SIGNING_PASSWORD -dname 'CN=BS3D, O=Antonin Prazsky'
    if ($LASTEXITCODE -ne 0) { throw "keytool could not make the key (exit $LASTEXITCODE)." }

    $listing = & $keytool -list -v -keystore $store -alias bs3d -storepass:env BS3D_SIGNING_PASSWORD
    if ($LASTEXITCODE -ne 0) { throw "keytool could not read the key back (exit $LASTEXITCODE)." }
}
finally {
    Remove-Item Env:\BS3D_SIGNING_PASSWORD -ErrorAction SilentlyContinue
}

$match = $listing | Select-String 'SHA256:\s*([0-9A-Fa-f:]{95})'
if (-not $match) { throw 'keytool printed no SHA256 fingerprint for the key.' }
# The form apksigner prints and release.yml compares: 64 lower-case hex digits
$digest = ($match.Matches[0].Groups[1].Value -replace ':', '').ToLowerInvariant()

if ($Rehearse) {
    Remove-Item -Recurse -Force $folder
    Write-Host "Rehearsal: a key was made, read back and deleted. Its certificate was $digest."
    Write-Host 'Nothing was written to the repository and no secret was set.'
    return
}

# One line, LF, no BOM: the repository's convention, and what release.yml trims and compares
[System.IO.File]::WriteAllText($pinPath, $digest + "`n", (New-Object System.Text.UTF8Encoding($false)))

[System.Convert]::ToBase64String([System.IO.File]::ReadAllBytes($store)) | gh secret set ANDROID_KEYSTORE_BASE64
if ($LASTEXITCODE -ne 0) { throw 'gh could not set ANDROID_KEYSTORE_BASE64.' }
$password | gh secret set ANDROID_KEYSTORE_PASSWORD
if ($LASTEXITCODE -ne 0) { throw 'gh could not set ANDROID_KEYSTORE_PASSWORD.' }

Write-Host ''
Write-Host "The key is in $store"
Write-Host "Its certificate: $digest"
Write-Host ''
Write-Host 'Three things are left, and the first cannot wait:'
Write-Host "  1. Copy $store and the password somewhere that is not this PC. They are the only copies."
Write-Host '  2. Commit GameAndroid\release-certificate.sha256 (an agent can do it: it is one line, already written).'
Write-Host '  3. Run the Release workflow by hand once (Actions > Release > Run workflow): its release-android job'
Write-Host '     must end with "Signed by <the certificate above> (owner)". Nothing is published by a manual run.'
