---
title: The JIT we had ruled out
date: 2026-11-11T10:00:00Z
topics: [Raspberry Pi, .NET, Measurement]
summary: "On a Raspberry Pi the first level after launch ran in slow motion for two seconds, just as the cluster was let go. Precompiling the game did not change it, so we wrote down that the JIT was not to blame. It was, and the fix was one line in the project file."
thumb: /img/blog/thumbs/the-jit-we-had-ruled-out.jpg
---

On the evening of 10 November we measured the Raspberry Pi build's first minute. A player who installs the game to try it judges it by that minute, and in a .NET program the first minute is mostly code running for the first time.

That matters because of how .NET runs code. The game ships as intermediate code, and the JIT compiles each method to machine code the first time it is called. By default that first compile is quick and barely optimised, which .NET calls **tier 0**. A method that turns out to be called often is compiled again later, optimised, in the background, and its calls move over to the new code. On a desktop this is the right trade: a program starts sooner, and its hot code catches up within seconds. On a Pi 5, the seconds before it catches up are easy to see.

## ReadyToRun: a second and a half sooner

The usual answer is **ReadyToRun**: the assemblies are compiled for the target when the game is published, so most methods have machine code before the game starts. The JIT handles only what was left out, and precompiled code that turns out to be hot is still recompiled, optimised, like everything else. The cost is size, because each assembly carries its machine code beside its intermediate code.

We measured both builds on a Pi 5 with 8 GB under Raspberry Pi OS Trixie:
- **The same commit, published twice** with the release's own arguments, once with ReadyToRun and once without.
- **Every run on a fresh profile**, with no network and no splash screen, off-screen at 1920x1080.
- **The two builds took turns** in the order A B B A, after one warm-up run of each that we threw away.

There were 92 runs and no exception in any of them.

- **Launch to the main menu:** 4.63 s without ReadyToRun, 3.61 s with it (medians).
- **Launch to the first frame of each scene's first level:** sooner in all fourteen scenes, by 0.87 s in the Volcano up to 2.08 s on the Moon, and 1.5 s on average.
- **The size:** the packed download grew from 413.7 to 417.8 MB, and the unpacked game from 507 to 517 MB.

The first second of each level, when much of the game's code runs for the first time, halved or better in eleven of the fourteen scenes:

<figure>
  <picture>
    <source media="(max-width: 600px)" srcset="/img/blog/the-jit-we-had-ruled-out/ready-to-run-phone-400.png 400w, /img/blog/the-jit-we-had-ruled-out/ready-to-run-phone-800.png 800w" sizes="calc(100vw - 32px)" width="400" height="514">
    <img src="/img/blog/the-jit-we-had-ruled-out/ready-to-run-800.png" srcset="/img/blog/the-jit-we-had-ruled-out/ready-to-run-800.png 800w, /img/blog/the-jit-we-had-ruled-out/ready-to-run-1600.png 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="488" loading="lazy" alt="A chart of the mean frame in the first second of each scene's first level on a Raspberry Pi 5, without and with ReadyToRun, one row for each of fourteen scenes. Every row falls. The City falls from 145.5 to 32.4 ms and the Aurora from 144.5 to 25.7 ms. The others fall from between 55.7 and 101.2 ms to between 19.0 and 47.7 ms.">
  </picture>
  <figcaption>The mean frame in each scene's first second of play, without and with ReadyToRun. Each figure is the first one-second window of one run. Drawn by a script from the measurements.</figcaption>
</figure>

The steady frame, twenty seconds into each level, was no slower with ReadyToRun beyond the variation between runs: where it read higher, it was by 0.18 ms at most. Two scenes read much lower: the Mountains 16.74 → 12.61 ms and the Cavern 23.55 → 17.19 ms. Our reading is that twenty seconds in, the build without ReadyToRun was still replacing tier-0 code in those two scenes.

So ReadyToRun went in. One thing did not change at all.

## The stutter that stayed

On every scene's first level, at the end of the opening, the game stumbled. A level opens with the camera flying in to the cluster, which hangs perfectly still. When the camera arrives, the cluster is let go and swings into its seat. In the same second the red laser net that marks the losing line under the cluster is shown for two seconds: its four emitters turn to aim, and the net lights up. In that second the Pi held one frame for at least half a second. For the next two seconds it ran at about 9 frames a second, and then it settled. In the Cavern, from the log:

```
[fps] 55.7 (17.95 ms)
[intro] the first opening seen whole: every later one can be skipped
[fps] 10.2 (98.01 ms)   [pace] … 17.5-500.0
[net] reveal begins at 11.31 s: the housings turn over 0.6 s, the net is lit until 2.00 s
[fps] 8.6 (116.12 ms)   [pace] … 96.1-162.1
[net] reveal ends at 13.18 s
[fps] 26.7 (37.50 ms)
```

The `[pace]` line gives the shortest and longest frame of each window. Its 500 ms is a ceiling, not a measurement: the framework hands the game at most half a second per frame, so the real hold could have been longer.

It was not only off-screen. On the Pi's own display, the same second of the Mountains' first level read 65 and then 48 ms under Wayland, and 109 and then 61 ms under X11, against 17 and 27 ms around it. The older Bookworm userland, with an older Mesa, showed it too.

ReadyToRun did not touch it. In the City those windows read 55 and 56 ms without it and 51 and 46 ms with it. In the Cavern they read 119 and 54 ms without it and 95 and 83 ms with it. On that evidence we wrote on the issue: *"The JIT is ruled out, since precompiled code changes nothing here."*

## Not the net

The suspect was the laser net, because it starts in that very second. We built temporary switches that leave out one part of it at a time, and measured the Mountains' first level with each:

| Left out | The windows after the release (ms) |
|---|---|
| nothing | 77, 66, 32 |
| the net | 92, 56, 50 |
| the lenses | 63, 60, 23 |
| the housings | 72, 56, 29 |
| the whole reveal | 79, 59, 38 |
| the net, the lenses and the housings | 61, 53, 28 |

Every row still had its half-second frame. Whatever the net costs is lost in the variation between runs.

## The world's first step

Something else starts in that second: the physics. The world is not stepped at all while the camera flies in, so the cluster hangs as it was built and the player sees the whole swing when it is let go. Letting go is the first time the physics engine, Bepu, runs at all.

A temporary timer around the world's step showed what happened then:
- the first step took 219 ms;
- for about two and a half seconds after it, every step took 15 to 30 ms;
- then the steps fell to 5 to 8 ms, and stayed there.

The game steps its world in fixed steps of 1/120 of a second, as many as the frame needs, but **at most four a frame**. Without a ceiling, a frame that falls behind would try to catch up, take longer doing it and fall further behind on the next. With one, the rest of the time is dropped and the world runs slow for that frame. At 15 to 30 ms a step, every frame used all four steps, so frames took 60 to 115 ms and each one moved the world on by 33 ms: only 29 to 56 % of the time that had passed. The swing that the opening is staged to show played in slow motion, at a few frames a second.

On 2 November, Antonín had ruled on slow motion in the game: *"The game should always run in real time."* Here it was anyway, at the start of the first level a new player sees.

Step times that start high and fall over a few seconds look like tiering, and .NET can test that without a new build. With the environment variable `DOTNET_TieredCompilation=0`, steps took 4 to 8 ms from the second one on.

So it was the JIT after all, in code that ReadyToRun had not precompiled. Bepu is written as generic code, and the game instantiates its solver over callback structs from one of our own libraries. As far as we can tell, ReadyToRun leaves that combination to the JIT, because it belongs wholly to neither assembly. Our experiment had changed which code was precompiled, and the guilty code was not among it. It could not have shown the JIT guilty, so it could not clear it either.

## One line

The fix is one property in the Pi build's project file:

```xml
<TieredCompilationQuickJit>false</TieredCompilationQuickJit>
```

A method with no precompiled code is then compiled optimised the first time it runs, instead of at tier 0 first. Precompiled code still starts precompiled and is still recompiled when it turns out to be hot.

<figure>
  <picture>
    <source media="(max-width: 600px)" srcset="/img/blog/the-jit-we-had-ruled-out/release-phone-400.png 400w, /img/blog/the-jit-we-had-ruled-out/release-phone-800.png 800w" sizes="calc(100vw - 32px)" width="400" height="772">
    <img src="/img/blog/the-jit-we-had-ruled-out/release-800.png" srcset="/img/blog/the-jit-we-had-ruled-out/release-800.png 800w, /img/blog/the-jit-we-had-ruled-out/release-1600.png 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="400" loading="lazy" alt="Three small charts, one each for the first level of the Mountains, the Cavern and the City, showing the mean frame in five windows of about a second from the moment the cluster is let go. Before the fix the frame climbs from about 26 ms to a peak and comes back: 54 ms in the Mountains, 101 ms in the Cavern and 58 ms in the City. After the fix it stays between 16 and 27 ms, close to the 16.7 ms line of 60 FPS: 18, 25 and 17 ms in the window where it peaked before.">
  </picture>
  <figcaption>The windows of about a second after the cluster is let go, on the build with ReadyToRun, before and after the fix. Measured off-screen on the Pi 5. Drawn by a script from the measurements.</figcaption>
</figure>

We also measured the default against two variants, in the order A B C C B A on the Mountains' first level:

| | Launch to the first frame (median) | The world's first step | Steady frame |
|---|---|---|---|
| The default | 4.75 s | 140–165 ms | 12.01–12.05 ms |
| QuickJit off | 4.44 s | 285–340 ms | 12.05–12.12 ms |
| Tiered compilation off | 4.23 s | 320–330 ms | 12.23 ms |

Turning tiered compilation off altogether would also remove the slow seconds. But then precompiled code would never be recompiled when it is hot, and the steady frame would be dearer for good: 12.23 ms against 12.05. QuickJit off keeps the steady frame where it was.

The world's first step did get longer, 285 to 340 ms against 140 to 165, because an optimised compile takes longer than a quick one, and now it is paid all at once. Version 0.6.1, released on 10 November, carries ReadyToRun and the fix.

## What is left

**One frame of 0.3 to 0.5 s, once a launch**, when the first level's cluster is let go: the solver being compiled, optimised, in one go. The plan is to step a throwaway world once while the first level is being built, so that the compile happens behind the loading, where nobody is watching anything move. That is not done yet.

## What we do not know

- **The fix was measured off-screen only.** The stutter was seen on the Pi's own display before it, but the display has not been measured since.
- **The longest frame is a floor, not a figure.** The half-second ceiling hides how long the worst frame really was, before and after.
- **The Cavern's first frames still hold.** With ReadyToRun, the longest frame of every other scene's first second fell to 214–439 ms, but the Cavern's still reaches the half-second ceiling. Since precompiling did not move it, our reading is that it is the scenery being built, Mesa compiling the shaders, or both. We have not measured which, and after this post we know what "precompiling did not move it" is worth.
- **We did not list which methods ReadyToRun left out.** The timer around the world's step, and the steps' fall over a few seconds, are the evidence that Bepu's solver was one of them.
- **One Pi.** Everything here was measured on one Pi 5 with 8 GB. The setting is the Pi build's alone, and the Windows and Android builds were not measured for it.
