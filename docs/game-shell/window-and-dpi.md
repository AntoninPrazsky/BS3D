# The window: size, borderless fullscreen, DPI

Part of [The game: shell](../game-shell.md).

## The windowed size is the player's (#137)

`_fullscreen` is F11's own switch; the OS maximize and an edge drag (`AllowUserResizing` is on) leave it `false`. A `SetGraphics` that set `PreferredBackBuffer` to `WINDOW_WIDTH`×`WINDOW_HEIGHT` whenever `!_fullscreen` shrank the swap chain under a window the OS did not shrink (maximized, it went 3840×1529 to 1600×900 and the picture was upscaled).

It restores `_windowedSize` instead, recorded in `OnClientSizeChanged` **only while windowed**, with a zero guard for the minimize. ⚠ The pass that goes fullscreen raises that event at the display's size; recording it would bring the game out of fullscreen into a window the size of the screen.

## Fullscreen is borderless (#157)

`SetGraphics` sets `HardwareModeSwitch = false`: minimizing exclusive fullscreen tears down the DXGI fullscreen state and, with no `Activated`/`Deactivated` handling, the window never restored. The back buffer is sized to the display either way. The Testbed and MapEditor have the same line (#400): a scripted `at=…:F11` on a window without focus crashed their hardware mode switch in `SwapChain.SetFullscreenState`.

## DPI awareness (#825)

The Game is **per-monitor DPI aware** (`Game/app.manifest`, `PerMonitorV2`). Unaware, Windows divides every size a process reads by the display scale: a 1920×1200 laptop at 125 % read `CurrentDisplayMode` as 1536×960, so the back buffer and the 3D were that size and blurred up, and mouse aim (which counts pixels) turned 1/1.25 as far.

- A manifest and not `Application.SetHighDpiMode`, because `Program.Main` is GamePi's too and has no WinForms there (`WFO0003` is suppressed in `Game.csproj`).
- **The run log says which it is**: `[display] 3840x1600 at 75 Hz, scale 100 % (96 DPI), DPI-aware per monitor` after the first `SetGraphics`, or `DPI-UNAWARE: …`. A note's context carries `"scale"` and `"dpiAware"` (`DisplayRefresh.TryGetScale`; GamePi answers nothing).
- On a scaled display the frame is the panel's own pixels and costs more (1.56× at 125 %), mouse aim is faster by the scale (Aim sensitivity carries a feel across), and the 1600×900 window is smaller. Not verified on a scaled display, nor on `WM_DPICHANGED` between monitors.
