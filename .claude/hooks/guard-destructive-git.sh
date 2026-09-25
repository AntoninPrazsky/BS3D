#!/usr/bin/env bash
#
# PreToolUse guard: refuses `git clean` and `git reset --hard` anywhere in this repository.
#
# Why this exists. On 2026-08-04 an agent session tidied the working tree with
# `git reset --hard origin/main && git clean -fd`. The clean printed:
#
#     Removing Game/Levels/Three.json
#     Removing Game/Levels/Four.json
#     Removing Game/Levels/Five.json
#
# — three hand-built levels, gone. They were untracked, so no blob was ever written to the object
# database; `git clean` deletes straight through rather than to the Recycle Bin; and their contents
# had never been read into a transcript. Unrecoverable, and not noticed for four days.
#
# Untracked files in this repo are DATA, not litter: levels saved out of the MapEditor land in
# Game\Levels as `??`, and the editor's save dialog defaults to the working directory, so they land
# in bin folders too. Neither command has a use here that a branch or a stash cannot cover.
#
# Exit 0 always: the refusal is carried by the JSON on stdout (permissionDecision "deny"), and a
# guard that fails the tool call with a shell error instead would be indistinguishable from a broken
# hook. Printing nothing means "no opinion", which is the case for every other command.

payload=$(cat)

# There is no jq on this machine, so the hook's JSON payload is matched as raw text rather than
# parsed. The trade is not symmetric — a false positive costs one refused command, a false negative
# costs somebody's afternoon — so this leans towards refusing.
#
# But it matches the command POSITION, not the mere mention: the name must open the payload's command
# string (`"command":"git clean …`), follow a shell separator (`;`, `&&`, `||`, `|`, a subshell), or
# start a line (`\n`, which is how a newline is encoded inside the JSON string). Matching anywhere was
# the first version and it refused its OWN commit, whose message explains what the hook does — and it
# would equally have refused writing this comment, or any documentation naming the commands. What
# still trips it is a heredoc line that *begins* with one of them, which is a fair price: paraphrase,
# or run it yourself.
#
# `[^;&|"]*` keeps the reset match inside one command, so a distant `--hard` cannot be dragged in.
# The quote alternative is `[^\\]"` — a quote NOT escaped — so it matches the `:"` that opens
# `"command":"git clean …` but not the `\"` of a quoted argument. Without that distinction
# `rg "git clean" docs/` was refused, which is a search, not a deletion.
COMMAND_START='(^|[^\\]"|[|;&(){]|\\n)[[:space:]]*'

# A command handed to another shell as a string is in command position too (#575): `cmd /c git clean`,
# `pwsh -Command "git clean"`. Its opening quote arrives escaped (`\"`), which COMMAND_START deliberately
# does not treat as a boundary, so these are named on their own.
SHELL_START='(cmd(\.exe)?[[:space:]]+/[cCkK]|(pwsh|powershell)(\.exe)?([[:space:]]+-[A-Za-z]+)*)[[:space:]]+(\\"|'"'"')?'

# The executable by any of its spellings - `git`, `git.exe`, or a path ending in either - and then git's own
# GLOBAL options before the subcommand (#575): `git -C <repo> clean` is exactly how an agent told to prefer
# absolute paths would write it, and `-c key=value`, `--git-dir=...`, `--no-pager` sit in the same place.
GIT_NAME='([^[:space:]"]*[/\\])?git(\.exe)?'
GIT_GLOBALS='([[:space:]]+(-C|-c|--git-dir|--work-tree|--namespace)([[:space:]]+|=)[^[:space:]]+|[[:space:]]+--?[A-Za-z][-A-Za-z]*)*'
GIT_DESTRUCTIVE="${GIT_NAME}${GIT_GLOBALS}[[:space:]]+(clean|reset[^;&|\"]*--hard)"

if ! printf '%s' "$payload" | grep -Eq "(${COMMAND_START}|${SHELL_START})${GIT_DESTRUCTIVE}"; then
  exit 0
fi

reason="Refused by .claude/hooks/guard-destructive-git.sh. 'git clean' and 'git reset --hard' destroy \
untracked and uncommitted work in this repo, and untracked files here are data: levels saved from the \
MapEditor sit in Game/Levels as '??'. This exact pair once deleted Three/Four/Five.json permanently - no \
git blob, no Recycle Bin, noticed four days later. Run 'git status --porcelain' and deal with every '??' \
line first: 'git add' them (a staged blob survives even a hard reset and can be recovered with 'git fsck \
--unreachable'), or copy them aside. Prefer 'git stash -u' or a branch. If this really is what the user \
wants, say so and let them run it themselves."

# One line, no literal newlines or quotes in the reason, so this is valid JSON without an encoder
printf '{"hookSpecificOutput":{"hookEventName":"PreToolUse","permissionDecision":"deny","permissionDecisionReason":"%s"}}\n' "$reason"

exit 0
