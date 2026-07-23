![BS3D — Bubble Shooter 3D](/Screenshots/bs3d_logo_8.png)

# BS3D

- Project needs references to (both are installed from NuGet):
  - https://github.com/bepu/bepuphysics2 (BepuPhysics, BepuUtilities)
  - https://www.nuget.org/packages/MonoGame.Framework.DesktopGL (MonoGame)

- Content.mgcb needs to be built with MonoGame Pipeline Tool
  - http://www.monogame.org/downloads/
  
- Test maps for Testbed project are located in `\Testbed\Maps`

- The HUD text is rendered in **Segoe UI** (the Windows system UI font, shipped with every Windows since Vista), covering ASCII plus Latin-1 Supplement and Latin Extended-A so Czech and other European diacritics render. The game-mode indicator is the gamepad glyph (U+E7FC "Game") from **Segoe MDL2 Assets**, a Windows icon font present on Windows 10 and 11. MonoGame bakes the glyphs into a texture at build time, so only the build machine needs the fonts installed — end users do not.

## Screenshot

![Screenshot](/Screenshots/screenshot1.png)
