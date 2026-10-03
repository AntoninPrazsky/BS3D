@echo off
rem Runs play-latest.ps1 for this one run with the execution policy lifted, because a stock Windows refuses to load any
rem .ps1 ("running scripts is disabled on this system") and nothing about the machine should have to change to play.
rem Everything after the name goes on to the script: play-latest.cmd -Ref origin/<branch> -NoRun -NoFetch
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0play-latest.ps1" %*
if errorlevel 1 pause
