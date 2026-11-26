# Other drivers: WARP and the D3D11 debug layer (#1252)

Part of [Rendering](../rendering.md).

The game has been drawn on an RX 6900 XT, an AMD APU, a GTX 1060 and the Pi's V3D, and never on an Intel GPU, which is what many a player's laptop has. No Intel machine is on the desk, so the Game carries two levers that put its Direct3D 11 device somewhere stricter than the Radeon's driver, and a script that collects what the stricter place says.

## The two levers

Both are launch arguments, set on MonoGame's statics before the device is made (`BS3DGame.AskForStrictDevice`), and both say so on a `[gpu]` line. The OpenGL builds (GamePi, GameAndroid) ignore them.

- **`warp`** makes the device on **WARP**, Microsoft's software rasteriser (`GraphicsAdapter.DriverType.FastSoftware`): a second, complete Direct3D 11 implementation, written to the specification rather than to one vendor's hardware, on any Windows machine. A frame takes the CPU's time: the City's menu at Low in 960x540 draws at about 17 FPS on the desktop's Ryzen 9 5900X, where the Radeon holds the display's 75 Hz cap.
- **`d3ddebug`** asks for the **D3D11 debug layer** (`GraphicsAdapter.UseDebugLayers`, WindowsDX's alone, so it is set by name and the OpenGL builds still compile). The layer checks every call against the specification and reports what it finds through `OutputDebugString`: input layouts that do not match the vertex shader's inputs, a texture bound for reading while it is a render target, a format a sampler or a blend cannot use, a resource leaked at exit. Those are the places where a lenient driver draws anyway and a strict one draws garbage, or ends the device.
  - ⚠ **The layer is Windows' optional feature "Graphics Tools"** (`d3d11_3SDKLayers.dll` on Windows 11; older systems carried `d3d11_1sdklayers.dll`, so the game looks for any `d3d11*sdklayers.dll`). Without it Direct3D refuses a debug device outright, so the run would end before its first frame: the game looks for the library first and, missing, says `[gpu] d3ddebug refused` and makes a plain device. Installing it takes an administrator: `Add-WindowsCapability -Online -Name Tools.Graphics.DirectX~~~~0.0.1.0`.
  - The two combine: `warp d3ddebug` is the layer over the software device.

## Collecting what the layer says

`Tools/d3d-debug-capture.ps1 -OutFile <report>` listens to `OutputDebugString` the way DebugView does, through the session's DBWIN buffer, with no debugger and no administrator. It counts every line one process (`BS3D` by default) writes and reports each distinct line once, with how many times and when it first came, errors first: a layer that objects to one draw objects every frame, and thousands of one line would bury the one that matters.

- ⚠ **Start it before the game**, or the messages of the device's creation are lost. It waits for the process, and stops when the process ends.
- ⚠ **One listener at a time**: close DebugView. A program started under a debugger sends its output there instead.

A run that photographs and listens (the scene and tier pinned, a scratch profile):

```powershell
Start-Process powershell -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','Tools\d3d-debug-capture.ps1','-OutFile','C:\temp\city-ultra.txt'
Tools\quiet-capture.ps1 -Exe Game\bin\net10.0-windows\BS3D.exe -Arguments 'warp d3ddebug nosplash windowed width=960 height=540 quality=ultra scene=City sceneseed=1 mute logfps userdata=C:/temp/ud shot=25' -Shots 1 -Timeout 120 -OutDir C:\temp -Tag city-ultra
```

## What the check found (2026-11-26, v0.7.0's code)

The sweep: every one of the twenty-one scenes behind the main menu (`scene=<key>`, `sceneseed=1`; Neon City's key is `neon`), at Low and at Ultra, one frame each; the Potato path on the City and the Aurora; and `benchmarkplay` at Low and Ultra, the city's biggest level played by the benchmark's gun (shots, cuts, drops, the swarm), three frames each. 960x540 windowed, a scratch profile, through `quiet-capture.ps1`, on the desktop (Ryzen 9 5900X, RX 6900 XT).

- **On WARP every run drew and nothing threw**: 46 runs, every frame written, no exception or failure in any run log, and every frame a plausible picture of its scene (looked at on contact sheets). A WARP frame took 47 to 210 ms at Low and 90 to 465 ms at Ultra in that window (the last `[fps]` line of each run's menu orbit; the Aurora the dearest at Low, the Forest at Ultra), and the Potato path about 14.5 ms.
- **The noise hash rounds differently.** The `[hash]` probe (`HashRoundingProbe`, ["Terrain grids and mirrors"](../scenes/terrain-grids-and-mirrors.md)) reads `fused` on the Radeon and `unfused` on WARP (3104 of 4096 samples agree fused). The terrain's mirrors follow the probe, so the two draw the same land: the kind of difference the probe exists for, seen on a second implementation.
- **Under the debug layer, on the Radeon, the same 46 runs: no D3D11 error in any.** One warning in each, and it is MonoGame's, not the game's: `DXGI WARNING: IDXGIFactory::CreateSwapChain: Blt-model swap effects ... are legacy`. MonoGame 3.8.5's WindowsDX makes its swap chain with a blt-model swap effect, which Windows 10 and 11 still support; flip-model presentation would be MonoGame's change to make, not this repository's.
  - The warning is also the proof the check can speak: a message from the layer, out of the game's own process, through the listener, on real data. A layer error would come the same way, as a line the report puts first.
- **What the layer calls INFO, and why it is no finding.** At Low (and in a few scenes at Ultra) thousands of lines a second read *"The Pixel Shader unit expects a Shader Resource View at Slot N, but none is bound. This is OK, as reads of an unbound Shader Resource View are defined to return 0."* A shader declares a texture a tier or a scene does not bind (the sun's shadow map with shadows off, a scene's own maps) and reads it behind a branch that is not taken. Direct3D 11 defines the read as 0 on every implementation, so a strict driver draws it the same. The report counts INFO lines by second and kind, and in the Savanna at Low, the run read that way, no object was made or destroyed in the steady seconds after the loading but three textures, once, about when `shot=` saved its frame: the per-frame hygiene of `BestPractices.md`, holding under the layer.
- **The effects the menu does not draw, under the layer: no error either.** Eleven more runs, each looked at: the victory fireworks (`celebrate`, Ultra and Potato), the floor's laser net in play (`level=21 lasers`), the result page and its trophy cup (`result`), the campaign's finale (`campaigndone`), Help's ODD BALLS with its live balls (`help=4`, Ultra and Potato), the desert's chapter intro into play with the HUD (`level=21`, Ultra), a city level's intro into play at Low (`level=111`), and Help's SCORING tab. The one warning in each is the swap chain's.

## What the check cannot say

- **It is not an Intel GPU.** The layer checks what the game asks of Direct3D, and WARP draws it a second way, but neither runs an Intel driver's own shader compiler, where a vendor's bugs live. A run on a real Intel machine stays the only proof of that, and `d3ddebug` there adds nothing the Radeon's did not: the layer's checks are the runtime's, not the driver's.
- **OpenGL is not covered.** GamePi and GameAndroid draw through MojoShader's GLSL, checked by their own builds ([The shared effects on OpenGL ES](opengl-es.md)), not by this.
- **A frame was looked at, not compared.** The sweep's frames were judged as plausible pictures of their scenes on contact sheets, not diffed against the Radeon's.
