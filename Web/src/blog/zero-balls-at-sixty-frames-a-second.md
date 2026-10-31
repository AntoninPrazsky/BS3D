---
title: Zero balls at sixty frames a second
date: 2026-10-31T11:00:00Z
topics: [Raspberry Pi, Measurement]
summary: "The benchmark's finale grows a storm of balls until the frame drops under 60 FPS. On a Raspberry Pi 5 it held none. How the Pi got a figure, and how a Pi that is never idle was made quiet enough to find one millisecond."
thumb: /img/blog/thumbs/zero-balls-at-sixty-frames-a-second.jpg
---

Since version 0.5.0 the game has a benchmark, under Extras. Antonín asked for it because nobody knows what the game will be run on: *"We develop on AMD software … and we still found the bug with the 125% desktop. What other surprises can there be?"* He also asked it to cover the Pi: *"there is no telling whether a Pi 6 with more powerful graphics comes out tomorrow. But our benchmark must be able to recognise that."*

A run takes about two and a half minutes:
- it flies tours over four of the dearest scenes;
- it plays the city's biggest level, with the gun aiming and firing by itself;
- it ends with **the swarm**, and then times the physics.

The swarm is a storm of the game's own balls: a funnel winding round the island in six coloured arms, in Space. It starts at 1,024 balls and grows by 1.3 times every 1.2 seconds, for as long as the median frame stays within 60 frames a second. A count that breaks the budget is measured once more, so that a single hitch does not end the run. The figure is the last count that held. On our desktop's RX 6900 XT at High it is 88,728 balls. The budget is 60 FPS on every machine, so two machines compare by the figure, and a machine nobody here owns can be told apart without its name.

On the Raspberry Pi 5, on 29 October, the figure was zero.

## Zero

```
[bench] swarm in Space: from 1024 balls, x1.3 every 1.2 s while the median frame holds 16.67 ms
[bench] swarm: held 0 balls (median 0.00 ms), broke at 1024 (24.67 ms) after 2 steps
```

The Pi draws through [Potato](/blog/what-a-raspberry-pi-5-can-afford/), its own plainer renderer. With the swarm's first 1,024 balls its frame took 24.67 ms against a budget of 16.67, broke again on the second measurement, and the run was over. The result page said *"Swarm: fewer than 1,024 balls at 60 FPS"*.

That was wrong twice. The comment on the starting count promised that it was "low enough that a Pi's Potato path can hold it", and it was not. Worse, a zero cannot do the one thing Antonín asked for: a Pi 6 that held 800 balls would read the same as this one.

## Down before up

There were three ways out: halve the count until one holds, start everyone lower, or measure the Pi at the size it plays at too. The fix that went in on 30 October is the first, because it leaves a strong machine's run as it was:
- **While nothing has held**, a count that broke twice is halved, down to 32 balls at the least.
- **From the first count that holds**, the storm grows by 1.3 times again, until the next count would be one that already broke.

On 31 October we ran it on the Pi twice, at native 1920x1080, full screen, on the Potato path:

| Step | Balls | Median, run 1 | Median, run 2 | |
|---|---|---|---|---|
| 1 | 1,024 | 24.39 ms | 24.39 ms | broke |
| 2 | 1,024 | 24.41 | 24.42 | broke again, so down |
| 3 | 512 | 18.37 | 18.45 | broke |
| 4 | 512 | 18.12 | 18.18 | broke again, so down |
| 5 | 256 | 14.97 | 14.98 | held |
| 6 | 333 | 15.98 | 16.00 | **held** |
| 7 | 433 | 17.14 | 17.28 | broke |
| 8 | 433 | 17.14 | 17.17 | broke again, so done |

Both runs took the same eight steps, with medians within 0.15 ms of each other. **The Pi 5 holds 333 balls at 60 FPS**, and that is now a figure the next Pi can be told apart by. The way down costs the Pi six more steps than a strong machine's run, about seven seconds.

<figure>
  <picture>
    <source media="(max-width: 600px)" srcset="/img/blog/zero-balls-at-sixty-frames-a-second/swarm-phone-400.png 400w, /img/blog/zero-balls-at-sixty-frames-a-second/swarm-phone-800.png 800w" sizes="calc(100vw - 32px)" width="400" height="520">
    <img src="/img/blog/zero-balls-at-sixty-frames-a-second/swarm-800.png" srcset="/img/blog/zero-balls-at-sixty-frames-a-second/swarm-800.png 800w, /img/blog/zero-balls-at-sixty-frames-a-second/swarm-1600.png 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="470" loading="lazy" alt="A chart of the median frame in milliseconds against the balls in the storm. Ringed orange points mark counts that broke: 1,024 at 24.4 ms, 512 at about 18.3 and 433 at 17.1. Filled blue points mark counts that held: 256 at 15.0 ms and 333 at 16.0. A dashed line marks the budget at 16.67 ms, with the held points under it and the broken ones over it. A straight line runs through all the points, from 11.9 ms at zero balls.">
  </picture>
  <figcaption>The swarm's eight steps in the first of the two runs. The second run's medians are within 0.15 ms of these. Each count that broke was measured twice, which is why some points are doubled. Drawn by a script from the runs' logs.</figcaption>
</figure>

## A straight line through the steps

The points lie close to a straight line. A least-squares fit through the sixteen step medians of both runs gives **11.9 ms for the frame without the storm**, plus **12.2 µs a ball**. Without the storm, the frame is Space, the 1,204-ball cluster the front end shows and the rest of the scene. The line crosses the budget at about 390 balls, between the 333 that held and the 433 that broke. On the Pi, then, the rest of the frame leaves the storm about 4.7 ms. A machine with a cheaper frame or cheaper balls holds more, and that is what the figure is for.

## The millisecond nobody asked for

The run of 29 October showed something else as well. All four tours were 1.4 to 1.7 ms slower than in the first run on the Pi the day before, while the city's play was only 0.6 to 0.9 ms slower. Nineteen commits had landed between the two runs. One of them stood out: the menu's loaded gun, now standing on the island that the tours fly over. But one run against one run proves nothing, so we set up an A/B.

This Pi is not a quiet lab machine. It runs the score server and [its front panel](/blog/seventeen-greys-on-a-screen-that-has-two/), and the game is built and tested on it too. Our first chain of runs had a compiler server and a build busy under it, and we threw it away. For the second:
- **Before each run, the script waited for a quiet Pi**: under 20 % busy, read from `/proc/stat`, twice in a row.
- **During each run it logged how busy the rest of the Pi was.** It was 5 to 9 % of the four cores, mostly the panel and other work going on.
- **Three builds took turns**: A was the build of 28 October, B the build of 29 October, and C was B with the gun turned off (`menugun=off`). They ran in the order A B C C B A, so a slow drift of the machine, such as heat or a job in the background, falls on both sides of every comparison.
- **Each run used a fresh profile**, and nothing was sent.

| Median (ms) | A1 | B1 | C1 | C2 | B2 | A2 | B−A | B−C |
|---|---|---|---|---|---|---|---|---|
| Cavern | 11.05 | 12.01 | 10.72 | 10.76 | 12.17 | 10.78 | **+1.18** | **+1.35** |
| Neon City | 12.48 | 13.50 | 12.46 | 12.49 | 13.48 | 12.44 | **+1.03** | **+1.01** |
| Forest | 10.67 | 11.87 | 10.61 | 10.81 | 11.80 | 10.54 | **+1.23** | **+1.12** |
| Meadow | 11.71 | 12.78 | 11.62 | 11.69 | 12.71 | 11.62 | **+1.08** | **+1.09** |
| City intro | 18.69 | 18.61 | 18.68 | 18.74 | 18.47 | 18.65 | −0.13 | −0.17 |
| City in action | 17.53 | 17.63 | 17.55 | 17.74 | 17.41 | 17.50 | 0.00 | −0.12 |

The two runs of each build agree to within 0.2 ms. C is A to within 0.1 ms, so turning the gun off takes back all of the difference: **the gun costs the tours about 1.1 ms a frame on the Pi**, and nothing else among the nineteen commits cost them anything. The city's play did not change at all. The 0.6 to 0.9 ms seen that morning was the variation between one run and the next.

## Antonín kept the gun

The fix of 30 October first took the gun out of the whole benchmark, so that the tours would compare with version 0.5.0's. On 31 October Antonín ruled the other way: *"I would not leave the gun out."* Nobody plays the game on a Pi now or sends benchmark results from one, he added, so it does not matter. The gun is back in the tours, as it stands in the menu. Only the swarm leaves it out, so that a measurement of the swarm stays one of the swarm.

We checked that change in the same way, in the order G M M G: the tours were 1.07 to 1.39 ms slower with the gun, the city's play changed by 0.00 and 0.03 ms, and the swarm held 333 balls in all four runs. Versions 0.5.0 and 0.5.1 drew no gun in their tours, so on a Pi their tours read about a millisecond cheaper than the next release's will.

## What we do not know

- **The Pi was measured at native 1080p, but it plays at 720p.** The benchmark holds the resolution step off, on purpose. There is no swarm figure for the Pi at 720p.
- **We have not measured how the 11.9 ms splits** between Space's sky and the rest of the frame.
- **These are two runs a build on one Pi**, with 5 to 9 % of its cores busy elsewhere. They are good for comparing builds side by side, not for promising anyone a frame rate.
- **The tours are the desktop's.** Potato has backdrops for the fourteen scenes the levels are set in, and the Forest is not one of them, so on the Pi the Forest tour flies over the island under a bare sky. Segments of the Pi's own are still to come.
