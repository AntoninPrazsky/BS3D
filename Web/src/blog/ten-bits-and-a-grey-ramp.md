---
title: Ten bits and a grey ramp
date: 2026-10-19T09:00:00Z
topics: [Rendering, Windows]
summary: The game computes in 16-bit floats and then throws most of that away in the last step. What we did about it, what MonoGame refused, and the one thing no screenshot can show.
---

Antonín asked a plain question: does the game render ten bits per colour, or eight? The honest answer was "both, in that order". Inside, the frame is wide: the scene, bloom and refraction targets are 16-bit floats, and one shader, `Tonemap.fx`, resolves them once into the range a screen can show (exposure, the ACES curve, the sRGB encode). The last step then quantises to whatever the back buffer holds, which was eight bits a channel: 256 levels. A smooth sky, a fog or a bloom falloff can band right there. We never measured how much of it the film grain already hides.

<figure>
  <img src="/img/blog/ramp-bits.png" width="1200" height="176" loading="lazy" alt="Three horizontal grey ramps from black to white, one above the other. The top one climbs in 16 visible steps, the middle one in 64, the bottom one looks nearly smooth.">
  <figcaption>The same ramp at 4, 6 and 8 bits per channel: 16, 64 and 256 steps. Each extra bit halves the step. A PNG is itself 8-bit, so it cannot show the 1024 steps of a 10-bit ramp, and that turns out to be the whole difficulty.</figcaption>
</figure>

## Ask the monitor, ask the GPU

Ten bits is only worth asking for where the screen takes it. The question can be put to Windows: `IDXGIOutput6` reports `BitsPerColor` for each monitor. On the desktop where we tried it, one monitor said 10 and the other said 8, so the question has to be asked about the monitor the window is on, and again when the window moves.

The GPU could do it: creating a `R10G10B10A2` swap chain through SharpDX directly worked with both swap effects. MonoGame 3.8.5 would not. Setting `PreferredBackBufferFormat = SurfaceFormat.Rgba1010102` failed device creation with `E_INVALIDARG` from `CreateSwapChain` under every combination we tried, although the same description succeeded when we made it ourselves. We did not find why. An AMD laptop took the format in every mode, so what we have is a question about one card's driver and the framework, not an answer.

What we could do is arrange for the failure to be survivable. MonoGame creates the device itself if there is none, and a swap chain the driver refuses then becomes an exception out of device creation, where nothing can answer it. So the first device is always made 8-bit and reset to 10 a moment later. On a reset the refusal can be caught: the run carries on at 8 bits and the log says why.

## A screenshot that lies politely

Reading the back buffer back for a screenshot converts nothing. A 10-bit buffer read into ordinary 8-bit colours is the packed 32-bit words misread as bytes. Cornflower blue, (100, 149, 237), came back as (145, 89, 121). The red is the one to follow: 100 of 255 is 401 of 1023, and the low eight bits of 401 are 145, which is the first byte that came out. The screenshot writer and the pictures attached to a note now decode the words, and a test pins both the decode and the misread.

## The ramp

How would we know whether ten bits reaches the eye? A screenshot cannot say, since a PNG is eight bits, and Windows may still requantise a windowed swap chain on its way to the panel. So the game has a `ramp` argument that draws a grey ramp, black to white, straight into the back buffer from a 16-bit texture over the lower third of every frame: 256 steps on an 8-bit buffer, 1024 on a 10-bit one that the panel and Windows really show as such. The test needs an eye on a 10-bit panel: switch the setting on and off, and see whether the steps shrink to a quarter of their width.

Antonín did that, on his LG monitor. To his eye the ramp is equally smooth at 8 and at 10 bits. Either the panel does not display ten bits, or it fakes them by dithering between frames, in which case it shows the same smooth ramp either way. We kept the feature as built: where a monitor reports ten bits the game asks for them, the Display page has a row to go back to eight, and a panel that cannot tell the difference loses nothing.

What we still do not know is whether any screen we own shows a difference at all. That is the honest end of this one: a feature that is correct, cheap, switchable and, where it counts, unproven.
