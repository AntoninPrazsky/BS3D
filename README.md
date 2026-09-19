# BS3D

A 3D take on the Puzzle-Bobble/bubble-shooter formula: a cluster of coloured balls hangs above a stone
island, jiggling under real physics, and you fire balls up at it from a cannon to match three-or-more of
the same colour and bring pieces of it down. It's a solo hobby project, built mostly to learn and try
things out — nothing here is trying to be groundbreaking, it's just a small game being built in the open.

## Screenshot

![Screenshot](/Images/screenshot2.jpg)

*A level of the neon city chapter, captured in game at 1920×1080.*

## Download and play

Windows 10 or 11, 64-bit, and **nothing to install** — the .NET runtime, MonoGame and everything else the
game needs travel inside the zip.

Take the newest [release](https://github.com/AntoninPrazsky/BS3D/releases/latest), right-click the zip →
Properties → tick **Unblock** (Windows marks everything it downloads), extract it and run `BS3D.exe`. The
exe is not code-signed, so SmartScreen says "Windows protected your PC" the first time: **More info → Run
anyway**. Progress and settings live in `%LOCALAPPDATA%\BS3D` rather than in the game's folder, so that
folder can be replaced or deleted without losing a save.

Everything below is about building it yourself instead.

## What's in the repo

- **`Game`** — the shipping game (`BS3D.exe`): a menu-driven front end (main menu, settings, a level
  picker organized into chapters), 120 hand-built levels grouped into 12 chapters, a star rating and score
  per level, and 20 different backdrops (sea, desert, mountains, a neon city, a volcano, deep space, and
  more) the levels play against.
- **`Testbed`** — where every system actually gets built and tuned before it reaches the game: shooting,
  the physics simulation, the camera, the HUD. Loads test maps from `Testbed/Maps`.
- **`MapEditor`** — a visual editor for the JSON map files the levels are made of, rendering them with the
  same shaders and lighting the game uses, so a map looks in the editor the way it will look in play.

Underneath all three sit three shared libraries (`BS3DLibs/`): general 3D/rendering plumbing
(`Prazsky.Core`), the game's own logic — the ball grid, map/level formats, scoring
(`Prazsky.BS3D`) — and the [BepuPhysics](https://github.com/bepu/bepuphysics2)-backed simulation that
turns a grid of balls into a physical structure that can be shot at, break apart and fall
(`Prazsky.BS3D.Physics`).

A few smaller command-line tools (`Tools/`) support the above: one generates and validates the game's
levels, one plays every shipped level through the real scoring code to make sure the star rating actually
tracks skill, and one renders the procedurally-composed score to `.wav` for a quick listen and bakes the
music the game ships with.

Very little in the game is a hand-made asset — the ball meshes, the cannon, the island, the skies, the
scenery in every backdrop and every sound effect are generated in code rather than imported from a
modelling or audio tool. The music is the one thing that is not, any more: the levels play recordings
now, and the original procedurally-composed score is kept on a small player on the About page.

## Building and running

Built with [MonoGame](https://www.monogame.net/) (WindowsDX / DirectX 11) and BepuPhysics 2, targeting
.NET 10, Windows only. There are no external asset files to fetch — everything needed is either in the
repo or restored via NuGet.

```powershell
# Build everything
dotnet build BS3DLibs.sln     # libraries only
dotnet build Testbed.sln
dotnet build MapEditor.sln
dotnet build Game.sln

# Run
dotnet run --project Testbed\Testbed.csproj
dotnet run --project MapEditor\MapEditor.csproj
dotnet run --project Game\Game.csproj
```

Each of the three executables carries its own `dotnet tool` manifest for the content pipeline builder, so
run this once inside `Testbed/`, `MapEditor/` and `Game/` before building:

```powershell
dotnet tool restore
```

There are no automated tests in the usual sense; instead `Tools/LevelGen` and `Tools/ScoreSim` check that
every generated level is actually playable and that the scoring rates them sensibly, and both exit
non-zero on a failure.

## More detail

`CLAUDE.md` at the repo root is a fuller overview of the architecture, and `docs/` goes into each
subsystem — rendering, the backdrops, the game's menus, its simulation and levels, the HUD/audio/effects,
the Testbed's gun and camera, and the map/level file formats — in more depth than fits here.
