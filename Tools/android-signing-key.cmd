@echo off
rem Runs android-signing-key.ps1 for this one run with the execution policy lifted (a stock Windows refuses to load any
rem .ps1), as play-latest.cmd does. It makes the key that signs the Android releases, once: see the script's own header.
rem android-signing-key.cmd -Rehearse tries it without leaving anything.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0android-signing-key.ps1" %*
pause
