---
title: Seventeen greys on a screen that has two
date: 2026-10-31T10:00:00Z
topics: [Raspberry Pi, Hardware]
summary: "The score server's front panel is a 128x64 LCD whose pixels are either on or off. How it shows seventeen greys, fades without a backlight, sounds a siren, and admits it when it has stopped."
thumb: /img/blog/thumbs/seventeen-greys-on-a-screen-that-has-two.jpg
---

Every score the game sends lands on a Raspberry Pi 5, which keeps the leaderboards. Since October that Pi has a face: a 2.4-inch ST7565 panel of 128 by 64 pixels. It is an LCD of the old kind, yellow-green with blue-black pixels and no backlight, and each of its pixels is either on or off. It greets the room with the game's animated logo. Then it shows whether the server is online and how the playing is going, and it sounds a siren when something fails.

Its code is public as [BS3D-LCD](https://github.com/AntoninPrazsky/BS3D-LCD), and most of it would work for any ST7565 panel on a Pi. This post is about the parts that took some thought:
- greys on a panel that has none;
- fades on a panel with no light to dim;
- a status display that can never pass for a working one once it has stopped.

<figure class="grid grid-3">
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/photo-logo.jpg" width="1000" height="850" loading="lazy" alt="A small red circuit board holding a yellow-green LCD, on which the words BUBBLE SHOOTER and a round 3D badge are drawn in dark blue pixels, with shades of grey inside the letters."><span class="cell-label">The logo</span></div>
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/photo-planet.jpg" width="1000" height="850" loading="lazy" alt="The same LCD showing a striped planet with a ring round it and a small moon to its right."><span class="cell-label">A planet</span></div>
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/photo-bubbles.jpg" width="1000" height="850" loading="lazy" alt="The same LCD with a few round glass bubbles scattered over it, darker at their rims."><span class="cell-label">Bubbles</span></div>
<figcaption>Phone photos of the real panel. The camera's short exposure catches some of the flicker that the eye averages away.</figcaption>
</figure>

## A driver first

Nothing could simply be installed. The usual Python library for small displays, luma.lcd, has no ST7565 class, and its nearest relative, the ST7567, hard-codes the column offset. RPi.GPIO, which such libraries use to drive the data/command and reset lines, does not work on the Pi 5. So BS3D-LCD brings its own ST7565 device for luma.core and drives those two lines through libgpiod. Anything that draws on a luma device can draw on it.

## Greys made of time

An ST7565 pixel has no third state. But the liquid crystal is slow, and so is the eye. If a pixel is on in half the frames and the frames come fast enough, both see a half grey. The trick is called frame rate control.

Done naively, it flickers: if every pixel of a grey turns on in the same frames, the whole area blinks together. So each frame is ordered dithering with a 4x4 Bayer matrix, whose 16 thresholds are spread so that neighbours differ. From frame to frame the thresholds shift by a bit-reversed sequence (0, 8, 4, 12, 2, …). Over 16 frames every pixel meets every threshold once, so it is on for exactly its share of them, and its neighbours take their turns at other moments. Sixteen frames give **17 greys**: a pixel can be on in none of them, in one, and so on up to all sixteen.

<figure class="grid grid-3">
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/sphere-frame-710.png" srcset="/img/blog/seventeen-greys-on-a-screen-that-has-two/sphere-frame-710.png 710w, /img/blog/seventeen-greys-on-a-screen-that-has-two/sphere-frame.png 1420w" sizes="(max-width: 640px) calc(100vw - 32px), (max-width: 1088px) calc((100vw - 56px) / 3), 344px" width="1420" height="908" loading="lazy" alt="Black dots on white: a ball and a background of horizontal bands, every grey made only of dots that are fully on or off."><span class="cell-label">One frame, as sent</span></div>
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/sphere-gray-710.png" srcset="/img/blog/seventeen-greys-on-a-screen-that-has-two/sphere-gray-710.png 710w, /img/blog/seventeen-greys-on-a-screen-that-has-two/sphere-gray.png 1420w" sizes="(max-width: 640px) calc(100vw - 32px), (max-width: 1088px) calc((100vw - 56px) / 3), 344px" width="1420" height="908" loading="lazy" alt="The same picture in smooth greys: a shaded ball above bands that grow darker downwards, and the ball's shadow below it."><span class="cell-label">What sixteen frames average to</span></div>
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/sphere-710.png" srcset="/img/blog/seventeen-greys-on-a-screen-that-has-two/sphere-710.png 710w, /img/blog/seventeen-greys-on-a-screen-that-has-two/sphere.png 1420w" sizes="(max-width: 640px) calc(100vw - 32px), (max-width: 1088px) calc((100vw - 56px) / 3), 344px" width="1420" height="908" loading="lazy" alt="The same picture on a simulated yellow-green LCD, the greys as shades of teal."><span class="cell-label">On the LCD (simulated)</span></div>
<figcaption>The <code>sphere</code> scene. On the left is one 1-bit frame as it goes over SPI, in the middle the grey the eye takes from sixteen of them, and on the right the tool's simulation of the panel.</figcaption>
</figure>

The rate had to be found on the panel. A frame is 1 KB, which takes about 2.1 ms to send at the SPI bus's 4 MHz, and the bus manages about 380 frames a second. Rates below 200 beat against the LCD's own refresh, showing bars that rolled up the screen or pixels that looked blocky, so the panel gets **200 frames a second**. A frame that comes late shows as a flicker of the greys, so the rate also has to be even. The panel shows mid-tones lighter than their share of on-time suggests, so every grey is darkened by a gamma of 0.65 before it is dithered. Both values were picked by eye, from labelled steps the tool puts on the panel (`bs3d-lcd compare fps`, `compare gamma`).

The `gradient` scene shows what the time buys. It draws one ramp three ways: static Bayer dithering, static Floyd–Steinberg error diffusion, and the temporal dithering. Only the last is a smooth ramp.

<figure class="grid grid-2">
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/photo-gradient.jpg" width="1000" height="850" loading="lazy" alt="The LCD showing three horizontal ramps from light to dark: the top one a regular grid of dots, the middle one an irregular scatter, the bottom one a smooth gradient."><span class="cell-label">On the panel</span></div>
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/gradient.png" width="1420" height="908" loading="lazy" alt="The same three ramps on the simulated LCD."><span class="cell-label">Simulated</span></div>
<figcaption>Bayer on top, Floyd–Steinberg in the middle, temporal dithering at the bottom.</figcaption>
</figure>

## Tall pixels

The 128 by 64 pixels cover 43 by 26 millimetres, so each pixel is about a fifth taller than it is wide. A circle drawn for square pixels would come out as an egg standing on its end. So everything round is drawn 0.83 times as tall as it is wide.

Drawing at 128 by 64 has a second problem: a shape's edge falls on whole pixels, so a ball that moves by half a pixel jumps. The greyscale canvas therefore draws at four times the panel's resolution and averages down. Its edges come out soft, a shape can sit between two pixels, and the greys carry the fraction.

<figure class="grid grid-2">
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/photo-circles.jpg" width="1000" height="850" loading="lazy" alt="The LCD showing two rows of round shapes: rings of different weights, shaded balls, a target, a soft dot, a pie, a dotted ring and a crescent."><span class="cell-label">On the panel</span></div>
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/circles.png" width="1420" height="908" loading="lazy" alt="The same ten shapes on the simulated LCD."><span class="cell-label">Simulated</span></div>
<figcaption>Ten circle styles, each drawn 0.83 as tall as it is wide, so that it is round on the panel.</figcaption>
</figure>

## Light without a backlight

There is no backlight to dim, but there is a voltage. The ST7565 drives the liquid crystal at a voltage, V0, which two commands set: a resistor ratio with 8 values and an "electronic volume" with 64. The 64 values on their own are coarse steps. Sorted by the V0 they produce, the **512 pairs** make a fine ladder, and stepping along it fades the whole panel smoothly, from nothing to full and back. Every scene fades in and out over 0.4 s. A short step to 1.1 times the level and back makes the panel blink.

The logo makes its own entrance. A bubble is shot up and pops, its ring reveals the title, the "3D" badge springs in and the panel blinks. From then on a glint sweeps over it every few seconds.

<figure>
<video class="clip" width="710" height="454" muted loop playsinline controls preload="none" poster="/video/blog/seventeen-greys-on-a-screen-that-has-two/logo-poster.png" aria-label="The startup logo on the simulated panel: a bubble rises and pops, the title appears, the 3D badge springs in, the panel blinks and a glint sweeps across.">
<source src="/video/blog/seventeen-greys-on-a-screen-that-has-two/logo.webm" type="video/webm">
<source src="/video/blog/seventeen-greys-on-a-screen-that-has-two/logo.mp4" type="video/mp4">
</video>
<figcaption>The startup logo in BS3D-LCD's simulation of the panel. The colours, the gaps between the pixels and the soft shadow each pixel casts on the reflector behind the liquid crystal are taken from the photos.</figcaption>
</figure>

## A status display

The panel's job is to tell anyone who passes whether the server is well. A strip along the top always shows whether the server is online and what time it is. Under it, pages take turns:
- the server;
- today's play;
- the latest scores and the latest players;
- activity by the hour;
- the top players;
- the all-time totals;
- the Pi itself.

A page with nothing to say is left out.

Each page is pushed out by the next with a slide that starts slowly and brakes at the end. The curve is smootherstep, 6u⁵ − 15u⁴ + 10u³, whose speed and acceleration are both zero at either end, so the page neither jolts as it starts nor as it stops. Antonín picked it, over 1.0 s, from the variants the tool showed on the panel. At 200 frames a second, the fastest frame of that slide moves the page by about 1.2 pixels, so it can move in whole pixels and still look smooth.

<figure>
<video class="clip" width="710" height="454" muted loop playsinline controls preload="none" poster="/video/blog/seventeen-greys-on-a-screen-that-has-two/status-push-poster.png" aria-label="The server page slides out to the left while the activity page, a bar chart of the last 24 hours, slides in from the right under the status strip.">
<source src="/video/blog/seventeen-greys-on-a-screen-that-has-two/status-push.webm" type="video/webm">
<source src="/video/blog/seventeen-greys-on-a-screen-that-has-two/status-push.mp4" type="video/mp4">
</video>
<figcaption>One page pushing out another under the status strip, simulated with made-up data.</figcaption>
</figure>

The top players roll like the credits of a film, at 8 pixels a second, Antonín's pick again, because anything faster blurs on the slow liquid crystal. The nicknames are why the fonts are what they are. Pillow's bitmap fonts stop at 256 characters, and a player may well write an Š or a ů. So the panel draws the public-domain X11 misc-fixed fonts, read from their BDF files, which carry every Latin letter up to U+017F. Text stays 1-bit and sits on whole pixels, because greys blur small letters.

<figure class="grid grid-2">
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/status-today.png" width="1420" height="908" loading="lazy" alt="A simulated LCD page headed TODAY with the day's clears, unfinished attempts, players and time played."><span class="cell-label">Today</span></div>
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/status-latest.png" width="1420" height="908" loading="lazy" alt="A simulated LCD page listing three of the latest scores, each with a player, a level, its stars and how long ago it came."><span class="cell-label">Latest scores</span></div>
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/status-top.png" width="1420" height="908" loading="lazy" alt="A simulated LCD page headed TOP PLAYERS with ranked nicknames and their totals."><span class="cell-label">Top players</span></div>
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/status-box.png" width="1420" height="908" loading="lazy" alt="A simulated LCD page about the Pi: its temperature and fan speed, load and memory, free space and the last backup."><span class="cell-label">The Pi</span></div>
<figcaption>Four of the pages, simulated with the made-up data of the tool's screenshots. On Latest scores, a grey 0 in place of stars marks an unfinished attempt.</figcaption>
</figure>

## When something fails

Anything that makes the server unreachable sounds the siren:
- the service down for 20 s;
- its health check failing three times in a row;
- the tunnel down for a minute;
- the same check made from outside, through Cloudflare, failing three times.

So do a failed backup, an error in the service's log, a nearly full disk, under-voltage and a CPU over 85 °C. The alert page then takes over and says what failed and since when. The whole panel pulses through V0, 0.15 either side of its level every 1.5 s, with a quick rise and a slow fall. Antonín picked the depth and the period on the panel, as he did the slide.

A new note from a player is not an error, so it has a signal of its own: two short dips of the voltage to 0.9 of its level, every 4 s. The knock reaches across a room without passing for the siren. There are no quiet hours, because a panel without a backlight shows nothing in a dark room anyway.

The only control is a button on the Pi's header. A short press shows the next page. A hold of 1.5 s switches the alarm off and acknowledges the notes, and a bar along the bottom fills as it is held.

<figure class="grid grid-2">
<div class="cell"><video class="clip" width="710" height="454" muted loop playsinline controls preload="none" poster="/video/blog/seventeen-greys-on-a-screen-that-has-two/status-siren-poster.png" aria-label="An alert page reading OFFLINE, ERROR, API DOWN, with the reason and since when; the whole panel fades lighter and darker like a siren."><source src="/video/blog/seventeen-greys-on-a-screen-that-has-two/status-siren.webm" type="video/webm"><source src="/video/blog/seventeen-greys-on-a-screen-that-has-two/status-siren.mp4" type="video/mp4"></video><span class="cell-label">The siren</span></div>
<div class="cell"><video class="clip" width="710" height="454" muted loop playsinline controls preload="none" poster="/video/blog/seventeen-greys-on-a-screen-that-has-two/status-note-poster.png" aria-label="The server page gives way to a NEW NOTE page naming the sender and when the note came; the panel dips twice."><source src="/video/blog/seventeen-greys-on-a-screen-that-has-two/status-note.webm" type="video/webm"><source src="/video/blog/seventeen-greys-on-a-screen-that-has-two/status-note.mp4" type="video/mp4"></video><span class="cell-label">The knock for a new note</span></div>
<figcaption>Both signals, simulated. The error and the sender's name are made up, and the panel never shows a note's text.</figcaption>
</figure>

## A panel that cannot lie

An LCD has a nasty habit for a status display: when nothing drives it any more, it keeps its last picture. A crashed program would leave ONLINE and the evening's numbers on the panel for as long as anybody cared to believe them. Three things stop that:
- **The clock's colon blinks**, so a frozen picture is visibly frozen.
- **The render loop reports to systemd's watchdog.** If the loop stalls for 10 s, systemd restarts the service, which sets the panel up afresh. We tried it by stopping the process with SIGSTOP, and it came back within the 10 s.
- **A clean stop**, for an update or a shutdown, leaves STOPPED with the date and the time over the last picture. Once the program has ended nothing can blink or show a grey, so the picture's strokes are widened by a pixel and thinned out to every other pixel. It still reads as what it was, but it reads as old. The banner blinks three times while the service is still stopping, and then stays.

<figure class="grid grid-2">
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/status-server.png" width="1420" height="908" loading="lazy" alt="A simulated LCD page under the status strip: ONLINE in large letters, the version, and lines for uptime, the tunnel and the last score."><span class="cell-label">Running</span></div>
<div class="cell"><img src="/img/blog/seventeen-greys-on-a-screen-that-has-two/status-stopped.png" width="1420" height="908" loading="lazy" alt="The same page with its text thinned to a checkerboard of dots, under a framed banner reading STOPPED with a date and a time."><span class="cell-label">Stopped</span></div>
<figcaption>The server page while the display runs, and what a stop leaves over it: the last picture, thinned out, and when the display stopped.</figcaption>
</figure>

The display reads the server from the outside, each source in its own thread with a timeout. It reads the service's unit and health check, the tunnel, the journal, the backups, and the Pi's temperature, fan, load and free space. It never opens the scores database. The play statistics come from a snapshot that the score service writes every 15 seconds, which never holds a hash, a hidden player or the text of a note.

## What it costs, and what we do not know

On the Pi 5 the panel holds 200 frames a second on about 12 % of one core. The readings slowed it at first, to five late frames in ten seconds. One TLS context shared by all the checks, and a 1 ms switch interval for Python's global lock, brought that down to about one late frame a minute. While the same Pi builds and tests the game, the panel reports hundreds of late frames in ten minutes (539 on one occasion). That is the machine's load, not the panel.

What we do not know:
- **The pictures here are of two kinds, and neither is the panel as the eye sees it.** The photos come from a phone, whose short exposure catches part of the flicker that the eye averages away. The simulation was matched to those photos by eye, not with any instrument.
- **Every value was found on one module**, an OPEN-SMART 2.4-inch panel: the contrast, the 200 frames a second, the 16-frame cycle, the gamma of 0.65 and the pixels' 0.83. Another ST7565 may want other values. The tool's `sweep` and `compare` screens are there to find them.

The code is under the MIT licence. The game's logo, which the panel shows every time it starts, is not covered by it.
