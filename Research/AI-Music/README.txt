Locally generated music (ACE-Step 1.5 through acestep.cpp, on the desktop's AMD
RX 6900 XT via Vulkan). Each .wav has a .json sidecar with the exact prompt, what
the model's LM rewrote it into, the loop cut and what was measured.

Reference renders for the proposal to replace the procedural music (see the
GitHub issue that links here). BS3D's shipped music is still entirely procedural
(Game/Audio/ProceduralMusic.cs) and nothing here is wired into a content pipeline.

All loops, 48 kHz, 32-bit float, stereo.

  menu-loop-v2.wav    59.2s  A minor  100  front end's loop (BakeMenu's brief)
  theme-pulse.wav     63.8s  A minor  128  MusicTheme.Pulse
  theme-bohemia.wav   41.7s  D minor* 116  MusicTheme.Bohemia   loop below the
                                           track's own timekeeping -- listen
  theme-nocturne.wav  80.0s  --       96   MusicTheme.Nocturne
  theme-mural.wav     88.0s  G major  120  MusicTheme.Mural
  theme-ember.wav     57.4s  E minor  134  MusicTheme.Ember
  theme-ember-punk-01.wav  56.8s  E minor   84  Ember as brooding alt/punk rock
  theme-ember-punk-02.wav  45.4s  E minor  168  Ember as fast pop-punk (came back
                                                as metal -- see its sidecar)
  theme-ember-punk-03.wav  46.6s  E minor  134  Ember as a punk ballad (came back
                                                as blues-rock solos)
  game-track-01.wav   61.4s  C major  124  a new piece, not a counterpart

  * D Dorian was asked for in the caption; the key field has no modes.

The theme loops and game-track-01 are whole-bar loops cut out of the body of a
longer render, aligned to the millisecond and crossfaded for about two beats.
menu-loop-v2 predates that and is the render's tail folded into its head.
