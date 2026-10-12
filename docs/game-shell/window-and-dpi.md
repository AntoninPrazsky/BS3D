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
- **The run log says which it is**: `[display] 3840x1600 at 75 Hz, scale 125 % (120 DPI), DPI-aware per monitor` after the first `SetGraphics`, or `DPI-UNAWARE: …` (then the mode is the divided one, `3072x1280` on that display at 125 %, and so is the Resolution row). A note's context carries `"scale"` and `"dpiAware"` (`DisplayRefresh.TryGetScale`; GamePi answers nothing). A scale change while the game runs adds `[display] the window's scale changed: now 100 % (96 DPI)`.
- On a scaled display the frame is the panel's own pixels (3840×1600 back buffer in a 3840×1600 client at 125 %, so nothing is stretched) and costs more (1.56× at 125 %), and mouse aim is faster by the scale (Aim sensitivity carries a feel across).
- **Sizes are pixels, not the scale.** The 1600×900 default window stays 1600×900 pixels at 125 % (smaller beside the desktop's other windows), and the menus and the pointer are sized off the window's and the panel's height, so the scale does not move them either. Kept: a window scaled by 1.25 (2000×1125) would not fit the 1920×1200 laptop the issue came from.
- **`WM_DPICHANGED`** (the scale of the window's monitor changed under it, or the window was dragged onto a monitor of another scale) **keeps the window's size in pixels**: windowed, only the frame's thickness changes, so the client and the back buffer move by a few pixels through the ordinary resize (1600×900 becomes 1602×908 going from 125 % to 100 %); borderless fullscreen stays the panel's size. ⚠ **WinForms re-publishes the window icon on every one** (`Form.OnDpiChanged` calls its `UpdateWindowIcon`), putting the single large frame into the title bar's small slot. `DisplayRefresh.WatchScale` calls back once WinForms is done, and the callback republishes the authored frames of the new scale (see [the window icon](../formats-and-tools/application-icon.md)).
- **To check it without a scaled panel**, run the exe with `__COMPAT_LAYER=DPIUNAWARE` in its environment: the line says `DPI-UNAWARE`. A scale can be changed for a check without signing out through `DisplayConfigSetDeviceInfo` (the undocumented DPI-scale packet the open-source SetDPI tool uses); restore it and read it back.
