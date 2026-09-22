Locally generated music (ACE-Step 1.5 through acestep.cpp, on the desktop's AMD
RX 6900 XT via Vulkan). Each .wav has a .json sidecar with the exact prompt, what
the model's LM rewrote it into, the loop cut and what was measured.

The MASTERS of the game's music since #443. Tools/MusicBake --tracks writes
Game/Music/<track>.ogg from every file here named theme-<track>.wav, and
menu.ogg from menu-loop-v2.wav (#486; a table of eleven rows until then -- so
game-track-01, a reference, is left alone by its name), brought to the themes'
loudness, peaks held under full scale, as the Ogg Vorbis the game decodes when
a family is first asked for (#444; 16-bit .wav until then). Edit a master here
and rerun the tool; never edit Game/Music by hand. The procedural score these
replaced is on the game's About page.

The game groups Game/Music/*.ogg into FAMILIES by the name before the first
dash (ember.ogg and ember-punk-03.ogg are both ember's); a level's "music"
names a family, which rotates through its recordings one per level opening, or
one recording by its stem, which pins it. The generated families of #486 keep
their float masters OUTSIDE the repository (a hundred 20 MB files are not for a
public git history): MusicBake --tracks --masters <dir> reads them from there,
and their .json sidecars are committed here beside the .ogg the game plays.

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
  theme-ember-punk-04.wav  45.2s  E minor   84  Ember as a brooding punk anthem **
  theme-ember-punk-05.wav  52.7s  E minor  ~96  Ember as fast punk, felt at ~192 **
  game-track-01.wav   61.4s  C major  124  a new piece, not a counterpart

  * D Dorian was asked for in the caption; the key field has no modes.
 ** Their prompts name a real band and song as the style reference, and the LM
    was bypassed (use_cot_caption false). The owner listened and judged neither
    recognisable as that band or song, and cleared them for the game.

The theme loops and game-track-01 are whole-bar loops cut out of the body of a
longer render, aligned to the millisecond and crossfaded for about two beats.
menu-loop-v2 predates that and is the render's tail folded into its head.
