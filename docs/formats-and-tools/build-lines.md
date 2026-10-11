# What a run says it is: the `[build]` lines, and `width=`/`height=`

Part of [Formats and tools](../formats-and-tools.md).

## The `[build]` lines (#372)

All three executables open with three stdout lines from `Prazsky.Core.Tools.BuildStamp`:

```
[build] Testbed.dll 2026-09-07 21:37:16 50145c15
[build] libraries 3 set 08f67881, newest Prazsky.BS3D.Physics 2026-09-23 04:10:46
[build] shaders 28 set 54512ed6, newest Glare 2026-09-07 21:38:53, oldest Sky 2026-09-02 16:17:17
```

They answer the question every capture and measurement rests on: **which code and which shaders are you running?** ⚠ **MGCB skips an `.fx` whose `.xnb` is newer and copies nothing**: `dotnet build` prints `Skipping …\InstancedModel.fx` and reports success, and deleting `bin` is not enough; only MGCB's own intermediate (`BS3DLibs\Prazsky.Shaders\Content\bin`) forces the rebuild and copy.

- **Line 1 is the managed assembly**, not the apphost `.exe` (the code is in the `.dll`; its write time and hash say whether a C# change reached the run). On the release's single-file exe (#714) it names `BS3D.exe` and hashes the whole file, and line 2 says the libraries are inside it.
- ⚠ **Line 1 cannot see most of the code** (the executables are thin shells over the three libraries): a library-only change leaves it and the shader `set` identical, which is why **line 2** hashes the libraries (`set`, `newest`).
- **Line 3's `set` is one SHA-256 over every `.xnb` beside the exe, each file's name as well as its bytes**: the authority on content, timestamps are not. **`newest` is the shader most recently compiled**: when it names something else, MGCB skipped yours. Since #618 every executable gets the same effects, so one tree's builds print the same `set`; a difference means one is stale.
- **A Windows run started with `potato` prints a fourth line (#808)**, `[build] potato shaders 9 set …`, for the effects under `Content\Potato\Shaders` that line 3 does not look into (`BuildStamp.ReportShaderOverlay`).
- It reports and never judges, and swallows every failure (a diagnostic must never stop a run starting).

## `width=` and `height=` reach the Game too

⚠ They were the Testbed's alone and the Game **ignored them in silence**: a run asking for 3840×1600 came back at 1600×900, plausibly. They pin the **windowed** back buffer only; fullscreen stays the display's. **`shot=` does not cap out**: its clock is the game's own and starts at the first Update, seconds after launch, so a schedule that looks truncated was killed early.
