---
title: Music made at home, chosen by measurement
date: 2026-10-19T10:00:00Z
topics: [Music, Measurement]
summary: A local model writes the game's loops on a graphics card at home. Choosing between its renders turned into a script that knows what "choppy" means, and a lesson about instruments that cannot fail.
thumb: /img/blog/thumbs/music-made-at-home-chosen-by-measurement.jpg
---

Every chapter of Bubble Shooter 3D has about ten loops of its own, generated at home. The model is ACE-Step 1.5, run through `acestep.cpp` on an AMD RX 6900 XT over Vulkan, at 48 kHz. Each render is prompted from a written description of the piece it is for, and a loop is cut from the body of the render at whole bars. Never from its ends: a generated piece *ends*, so folding its tail onto its head gave an ending followed by a beginning, and the level fell from 1.0 to 0.04 of the median across the join.

The model makes a lot of music. The hard part is choosing, and "by ear" does not scale to three candidates for each of a hundred and forty slots.

<figure>
  <img src="/img/shots/minaret-800.jpg" srcset="/img/shots/minaret-800.jpg 800w, /img/shots/minaret-1600.jpg 1600w" sizes="(max-width: 1088px) calc(100vw - 32px), 1056px" width="800" height="450" loading="lazy" alt="A cluster hangs under glass over a desert island, with vultures circling in a pale sky above sand dunes.">
  <figcaption>Minaret, in The Coil, the desert chapter. Its music, the family called <code>ember</code>, is the one the others are measured against.</figcaption>
</figure>

## A yardstick

We chose one family, `ember`, as the reference, and `measure-music.py` measures any loop against its range: the share of energy per band, how tonal the middle is, how often a held note changes, tempo wander, clipped runs, pitch wobble. Every figure outside ember's range is printed in bold.

The first thing it taught us was what the problem was not. One family was described as sounding "drunk". We measured its pitch wobble and detune and they were no worse than ember's, while a synthetic wow of 25 cents moved both three to seven times. Whatever Antonín heard was timbre or playing, which only an ear can name.

It also showed what band shares miss. One candidate was inside ember's range in every band and jumped 15.4 dB at its seam. Another spent 24 of its 43 seconds at 9 to 16 dB under the loop's level, a hole. Band shares alone miss both.

## "Choppy"

Then Antonín listened to every loop in the game's Jukebox and wrote of one track that it sounded "as if the track were cut up and completely silent gaps remained in it, which are short and unpleasant". None of our checks could see a gap of a fraction of a second.

So we built one. Cut a loop into 20 ms frames, take each frame's loudness against the median of the two seconds around it, and call a gap a run of 30 to 500 ms that sits more than 20 dB under it. On the shipped loops, ember has between 0 and 27 gaps a minute. That track had 97.4, 51 dB deep: about one a beat.

A check is evidence only once it has been seen to fire on real data. On a loop with no gaps of its own, four mutes of 40, 80, 150 and 300 ms read as four gaps, and ten mutes of 80 ms as ten. A single 700 ms silence read as none: runs over 500 ms are deliberately not counted, and a silence of half a second to two seconds is, as far as we know, named by no check at all. That is the instrument's blind spot, written down where the next person will see it.

The same pass measured another of his notes, that a track opens with the same melody many times over. The Main theme's opening pattern repeats for 15 bars, 24 seconds of 38. Ember's opening repeats for one to three bars.

## Music that will not behave

Some styles keep their habits whatever the brief says. Ska keeps its full-band rests: eight renders of three briefs read 26.7 to 101.3 gaps a minute against ember's 27 at most, and in the gaps the bass and the mids fall 24 to 36 dB together. The whole band is resting. Cutting the best render again from other stretches gave 29.3 to 30.7 gaps a minute: the rests are in the render, not in the cut. We ship the render at 29.3, with the one column still outside ember's range named in its file, because Antonín decided it is better than the recording it replaced, which had 99.2 gaps a minute and 21 clipped runs.

So the script does not choose, it argues. Its own header says it: a tune proxy measures harmonic content and pitch movement, not whether a melody pleases, and nothing there replaces the ear. The regeneration of the choppy track is still open, and these columns are the gates its candidates have to pass before anyone is asked to listen.
