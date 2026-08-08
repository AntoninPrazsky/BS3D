#!/usr/bin/env bash
#
# Exercises guard-destructive-git.sh in both directions: `bash .claude/hooks/guard-destructive-git.test.sh`
#
# It is a file rather than a one-liner because the payloads below contain the very commands the guard
# refuses — typed into a shell they would be caught in command position and the test would refuse
# itself, which is exactly what happened the first time. Run this way the guard only ever sees
# `bash <this file>`.
#
# Both halves matter. The guard was tightened twice while it was being written: matching the command
# name anywhere refused its own commit message, and then treating every quote as a command boundary
# refused `rg "git clean" docs/`. A guard nobody can work around is worth having; a guard that
# refuses reading and writing about itself is one somebody will switch off.

GUARD="$(dirname "$0")/guard-destructive-git.sh"
fail=0

expect() {
  want=$1; label=$2; payload=$3
  if printf '%s' "$payload" | bash "$GUARD" | grep -q '"deny"'; then got=DENY; else got=allow; fi
  if [ "$got" = "$want" ]; then printf '  ok    %-18s %s\n' "$got" "$label"
  else printf '  WRONG %-18s %s (wanted %s)\n' "$got" "$label" "$want"; fail=1; fi
}

echo "must DENY:"
expect DENY "bare clean"       '{"tool_name":"Bash","tool_input":{"command":"git clean -fd -e .zcode"}}'
expect DENY "bare hard reset"  '{"tool_name":"Bash","tool_input":{"command":"git reset --hard origin/main"}}'
expect DENY "compound"         '{"tool_name":"Bash","tool_input":{"command":"git fetch && git reset --hard origin/main && git clean -fd"}}'
expect DENY "semicolon"        '{"tool_name":"Bash","tool_input":{"command":"cd foo; git clean -xdf"}}'
expect DENY "subshell"         '{"tool_name":"Bash","tool_input":{"command":"(git clean -fd)"}}'
expect DENY "newline in cmd"   '{"tool_name":"Bash","tool_input":{"command":"cd repo\ngit clean -fd"}}'
expect DENY "powershell"       '{"tool_name":"PowerShell","tool_input":{"command":"git clean -xdf"}}'
expect DENY "flag before"      '{"tool_name":"Bash","tool_input":{"command":"git reset -q --hard HEAD"}}'
expect DENY "pipe"             '{"tool_name":"Bash","tool_input":{"command":"echo y | git clean -fdi"}}'

echo "must ALLOW:"
expect allow "status"          '{"tool_name":"Bash","tool_input":{"command":"git status --porcelain"}}'
expect allow "soft reset"      '{"tool_name":"Bash","tool_input":{"command":"git reset --soft HEAD~1"}}'
expect allow "reset a path"    '{"tool_name":"Bash","tool_input":{"command":"git reset Game/Levels/Three.json"}}'
expect allow "prose in msg"    '{"tool_name":"Bash","tool_input":{"command":"git commit -m \"Refuse git clean and git reset --hard in this repo\""}}'
expect allow "stash -u"        '{"tool_name":"Bash","tool_input":{"command":"git stash -u"}}'
expect allow "dotnet clean"    '{"tool_name":"Bash","tool_input":{"command":"dotnet clean Game.sln"}}'
expect allow "search for it"   '{"tool_name":"Bash","tool_input":{"command":"rg \"git clean\" docs/"}}'

if [ "$fail" = 0 ]; then echo "ALL PASS"; else echo "SOME FAILED"; fi
exit $fail
