# Compiles GamePi's OpenGL effects (#789) into GamePi/Shaders/Compiled, the folder GamePi.csproj copies into its
# Content/Shaders. The compiled files are COMMITTED, because the machine GamePi is for cannot make them: mgfxc compiles
# an OpenGL effect by running the HLSL through the Windows D3D compiler (to Shader Model 3.0) and MojoShader, and the
# Pi has neither (nor Wine for ARM64). So this runs on Windows - a desktop, or .github/workflows/gamepi-shaders.yml,
# which runs it on every push that touches GamePi/Shaders, fails when the result differs from what is committed, and
# leaves the result as the run's artifact (fetch.sh brings it to a Linux checkout). The compile is deterministic:
# two separate compiles of the same source were byte-identical (#789's toolchain spike).
#
#   pwsh GamePi/Shaders/compile.ps1
$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$gamePi = Split-Path $here -Parent

# The content tool is the one GamePi's manifest pins (the official dotnet-mgcb 3.8.5), restored from GamePi's folder
# because the manifest search walks up from the working directory
Push-Location $gamePi
try {
    dotnet tool restore | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed in GamePi (exit $LASTEXITCODE)" }

    # Absolute paths, so nothing here depends on what mgcb resolves a relative one against
    dotnet mgcb /@:"$here/Shaders.mgcb" /workingDir:"$here" /outputDir:"$here/Compiled" /intermediateDir:"$here/obj" /platform:DesktopGL
    if ($LASTEXITCODE -ne 0) { throw "mgcb failed to compile GamePi's GL effects (exit $LASTEXITCODE)" }
}
finally {
    Pop-Location
}
