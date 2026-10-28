# The backlit keyboard

Part of [The game: feedback](../game-feedback.md).

A backlit keyboard shows the game (#987):
- while playing, the round about to fire and the balls after it;
- at a clear, a rainbow and then the cup's colour; at a loss, red;
- under a tutorial card, the keys the card asks for, blinking white.

It goes through **Windows' own lighting API, `Windows.Devices.Lights.LampArray`**: the HID LampArray standard behind Windows 11's Dynamic Lighting. There is no vendor SDK and no DLL, so any keyboard Dynamic Lighting lists works the same.

The owner's Logitech G513 reports itself as a LampArray keyboard, measured on 2026-10-28 (`[lights] Keyboard 'G513' (046D:C33C)…` in the run log):
- 104 lamps;
- every key addressable by its virtual key (E is lamp 4, Space 40, F1 53);
- 33 ms between updates.

Logitech's own LED SDK was the first idea. It needs a DLL shipped under its EULA, and the G513 is not in its manual's device list.

## What it shows

`Game/Lighting/KeyLightModel.cs` is a pure function from the game's state to a colour a lamp. `KeyLightModelTests` hold it.

| When | The board |
|---|---|
| Playing | Every key in the loaded round's colour at `PLAY_BASE` (0.4). **Space**, which fires, at full. **F1 to F4**: the magazine's next four balls at full, F1 next, so the queue reads left to right as the strip on screen does. The colours are `GameplayScreen.LoadedTint`'s, the ones the muzzle collar and the aim beam take, so a wildcard's keys cycle with it and a cutter's are the Cut's accent. The queue stops where the strip stops, at the shots the budget has left. |
| A tutorial card is asking | Its keys blink white at `BLINK_HERTZ` (2) over whatever else is drawn: Fire's card blinks Space ("Space fires too"), Traverse A and D, Walk W and S, the lean-and-walk card A and D at its second step, Swap E, Brake Q, Cut R. Only while the card is arriving or shown, not while it praises or leaves, and only while the player's hand is on the keyboard and mouse (`Tutorial.AskingLesson`). |
| Won (the result page over a clear, and the campaign's finale in front of it) | A full-brightness rainbow sweeps across the board for `RAINBOW_SECONDS` (3). It then hands over in `CUP_FADE_SECONDS` to the cup's colour, breathing between 0.7 and 1. Bronze is orange, Silver a cool white, Gold gold, Diamond an icy blue with a tenth of the keys twinkling white. They are not the podium's metals: an LED has no specular, so a silver and a diamond would be one white. |
| Lost | Red, pulsing between 0.35 and full every 1.25 s, whether the cluster reached the line or the balls ran out. |
| The menus, a pause, the note page | A dim rainbow (value 0.22) drifting slowly across the board. |

The ending is the result page's own figures (`ResultPage.Result`), so the keyboard and the page cannot disagree, and a page the `result` argument stages lights it too. Before the page, the record a level that has ended keeps (`GameplayScreen.KeyLightEnding`) stands in: the campaign's finale is in front of the page.

## How it reaches the keyboard

- **Windows decides who drives the lamps:** the app in the foreground, unless the player has changed that under Settings > Personalization > Dynamic lighting.
  - The game composes and sends only while its window is active (`BS3DGame.UpdateKeyboardLights`). The player's own effect comes back when it is not, with nothing to do here.
  - Windows drops writes made while another app has the board, so the board is sent whole the frame the window comes back, and again every 0.5 s although nothing changed.
- **Nothing of it runs on the frame:**
  - `Game/Platform/KeyboardLights.cs` finds the keyboard with a `DeviceWatcher`, which also handles one plugged in later or pulled out. The first LampArray of kind Keyboard is the one lit, and another one Windows listed takes over when it is unplugged; a chassis lamp (the desktop's ASUS board has one) or a mouse is left alone.
  - The frame composes into its own array without allocating, and `Submit` copies it under a lock.
  - A worker thread sends it at the device's `MinUpdateInterval`, and only when a lamp changed. One `SetColorsForIndices` allocates 96 bytes, on the worker, measured.
- **The Settings row**, "Keyboard lights" on CONTROLS (`keyboardLighting` in `Settings.json`, on by default):
  - Off closes the device, which hands it back to Windows. A `LampArray` is not IClosable on the 19041 SDK, so its COM reference is released through `IWinRTObject.NativeObject`.
  - A player who turns it off before the keyboard is ever wanted never has it opened.
  - Once Windows has listed its lamps and none is a keyboard, the row reads "On (none found)". It never says so over a keyboard that is merely closed (the switch off).
- A capture run (`capture=`) never touches the keyboard.
- **GamePi** compiles a namesake that finds no keyboard (`GamePi/Platform/KeyboardLights.cs`).

## Seeing it

The game has to be the window in front, which a scripted run started without the focus is not: run these by hand.

```powershell
BS3D.exe play level=Pennant tutorial      # playing, the queue on F1 to F4, and the first cards' keys blinking (tutorial offers every card)
BS3D.exe result stars=4                   # a clear's rainbow, then the Diamond's icy blue twinkling
BS3D.exe result lost                      # the loss's red
```

The run log says which keyboard was found (`[lights] Keyboard 'G513' …`), or that none was.

## Traps

- **`LampArray.IsEnabled = false` does not hand the keyboard back.** It keeps the colours inside the API and stops sending them. Releasing the object is what gives the board to Windows.
- **`IsAvailable` and `AvailabilityChanged` are Windows 11 SDK members** (22000). The Game's TFM carries 19041, so they are not used; the foreground rule above stands in for them.
- **Background (ambient) control needs a packaged app** with the `com.microsoft.windows.lighting` extension. The game does not drive the keyboard while it is not in front, by design.
- **Black balls light nothing:** an LED cannot show black, so a black round's Space and the board around it go dark.
