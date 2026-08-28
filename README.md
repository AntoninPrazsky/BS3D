![BS3D — Bubble Shooter 3D](/Images/bs3d_logo_8.png)

# BS3D

A 3D take on the Puzzle-Bobble/bubble-shooter formula: a cluster of coloured balls hangs above a stone
island, jiggling under real physics, and you fire balls up at it from a cannon to match three-or-more of
the same colour and bring pieces of it down. It's a solo hobby project, built mostly to learn and try
things out — nothing here is trying to be groundbreaking, it's just a small game being built in the open.

## Screenshot

![Screenshot](/Images/screenshot1.jpg)

## What's in the repo

- **`Game`** — the shipping game (`BS3D.exe`): a menu-driven front end (main menu, settings, a level
  picker organized into chapters), 90 hand-built levels grouped into 9 chapters, a star rating and score
  per level, and 17 different backdrops (sea, desert, mountains, a neon city, a volcano, deep space, and
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
tracks skill, and one renders the game's procedurally-composed music to `.wav` for a quick listen.

Very little in the game is a hand-made asset — the ball meshes, the cannon, the island, the skies, the
scenery in every backdrop, and even the music are generated in code rather than imported from a modelling
or audio tool.

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
