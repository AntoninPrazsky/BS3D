# The victory fireworks

Part of [The game: feedback](../game-feedback.md).

Clearing a level sets off a display: shells rise from a ring around the island, whistle, burst over the arena and rain down, each report arriving as its shell goes off. `Game/Effects/Fireworks.cs` owns it and `Prazsky.Shaders/Content/Shaders/Fireworks.fx` draws it; the three sounds are procedural bakes ("The sound" in [game-feedback](../game-feedback.md)). How a finished block or the campaign grows it: [Block and campaign endings](milestone-endings.md).

## Where it lives and when it runs

- **It lives on the host, not on the gameplay screen.** A cleared level pushes the result page over the session, and the session runs only its world under that page (#241), so a celebration owned by the session would freeze exactly when the player is meant to watch it and die with the session on "Main Menu". Owned by the frame it keeps running whatever the stack holds: `BS3DGame.Update` advances it above `_screens.Update`.
- **It is drawn from `DrawTranslucentsBehindGlass`** (the call every screen makes right after `BeginSceneDraw`), still inside the HDR pass so it blooms. Drawn last of the scene, after the copy the ceiling's glass bends (#541), a burst behind the plate would be painted over the glass sharp instead of bent through it (#689).
- **It starts in `CheckLevelCleared`**, not on the result screen, so the first shells climb while the last of the cluster is still falling. `BuildLevel` and `TearDown` stop it, or the next level (or the front end) would open under the last one's bursts.
- **Schedule.** The display hangs back `CELEBRATION_DELAY` (2.2 s) so the victory fanfare has its opening statement to itself, runs `CELEBRATION_SECONDS` (60 s; at nine seconds it ran out while the result page was still being read), launches every `INTERVAL_OPENING` (0.07 s) for the first `OPENING_SECONDS`, then every `INTERVAL_STEADY` (0.17 s), and past `RELAXED_AFTER` (16 s) eases to `INTERVAL_RELAXED` (0.52 s), because a minute at barrage density is exhausting. Shells burst between `BURST_Y_MIN` and `BURST_Y_MAX` up, spread right across the sky: about twenty shells in the air at once. The opening's length is a per-display value (a finished block takes a longer one).
- **The party popper fires when the display starts**, not when the celebration is asked for (it is the display's own opening crack, the fanfare having announced the win). It is flagged rather than hung off the delay reaching zero, because at a delay of nothing that branch never runs and the `celebrate` argument asks for exactly that case.

## The mechanism

- **One static vertex buffer and one draw call** for the whole display (the idiom of `Snow.fx` and `Spray.fx`). Every shell and spark is animated in the vertex shader from per-shell uniforms (`ShellOrigin`, `ShellBurst`, `ShellColor`, `ShellShape`; `MAX_SHELLS` = 32); C# owns only what the shader cannot know: when a shell goes up, where it bursts, its colour, when to make a noise. A dead slot is `ShellColor.a == 0`, which collapses its quads: an idle display costs the buffer's vertex work and not one pixel.
- **The same buffer is the rocket and the burst.** While the age is negative the sparks are strung along the flight path by trail rank (the rising comet and its tail); at zero they are released. A spark's whole path is a pure function of its age, which is what lets the buffer be static, and it is why burst directions are **baked at build time**, not hashed in the shader: a direction that changed between frames would make every spark jitter.
- ⚠ **`MAX_SHELLS × SPARKS_PER_SHELL × 4` vertices must stay under 65 536** (the 16-bit index buffer). 32 × 400 is 51 200; 512 sparks a shell is the most it allows. Raising either unchecked makes far triangles quietly reference the wrong vertices.
- **Cost: not measurable.** One draw call of vertex work is not what the frame is made of (City, 3840x1600, `quality=high`: 7.66 ms with no display, 7.63 with the 32-shell one). Twice the vertices of the first design, 400 thin trails a shell since #612, still read 216.2 FPS against 216.3 on the neon city at 1920x1080.

## ⚠ The ceiling's glass must not write depth (#299)

The scene pass runs under `DepthStencilState.Default`, and the ceiling plate inherited it and wrote depth like a wall. The display is drawn afterwards under `DepthStencilState.DepthRead`, so every shell beyond the glass was depth-tested away, which from the play camera (low behind the gun, the plate filling the top of the frame) is most of the sky the shells use. The bursts appeared only once the released post-win camera swung out to a vantage with no glass in the way. `BS3DGame.DrawCeilingGlass` is now the one place the state is stated, for the session's plate and the front end's preview plate; it still reads depth, so the glass goes behind the island, cluster and gun. **The drain's glass and the gun's window pane still write depth** (the same latent shape, left because neither is ever between the lens and a burst).

The `celebrate` and `confetti` launch arguments are consumed from `Update` after the startup level (`StartupScript.StartCelebrations`), because `BuildLevel` stops both displays on purpose and would swallow a request made at the end of `LoadContent`.

## The look: what has to be true

- **A burst has to be big and dense.** A burst subtends about 19 degrees from the play camera, so a hundred-odd sparks land hundreds of pixels apart and read as lonely glints. Spark count and spark size are in world units (a burst is 40-90 units up) and **both must scale with the burst radius**, or a bigger shell looks emptier than a small one.
- **A spark is a trail, not a dot** (#612). It is drawn from where it was `TRAIL_SECONDS` (0.6, each spark 0.7-1.3 of that) ago to where it is, the same closed-form path asked twice and never before the burst, as a camera-facing capsule along that segment's screen direction. On the flash the trails reach back to the centre (a ball of radial lines); as drag stalls the stars they shorten; as gravity takes over they hang and fall, which draws the willow with no code of its own. A streak is what says "something blows apart"; a cloud of round dots only reads as a swarm.
  - **Thin, with a one-pixel floor.** `TRAIL_WIDTH` is a half-width in world units; under a pixel a quad is sampled rather than drawn and crawls as a dotted line, so it is never drawn narrower than one (`PixelAngle`) and its light is scaled by how much it was widened (`Snow.fx`'s floor, #654).
  - **Brighter per pixel (`TRAIL_GAIN`)**: a line a couple of pixels wide covers a tenth of the pixels the old spark did; at the old radiance the display read as a faint dusting of scratches. Thin lines that bright are what the glare pass turns into glowing strokes.
  - **White among the colours**: `WHITE_SHARE` of a shell's sparks burn white, and every trail's head burns `HEAD_WHITE` of the way to white all its life (the white-hot star on a coloured line in every reference).
- ⚠ **`INITIAL_SPREAD` must stay tiny** (0.015 of the radius). At 0.12 it pre-arranged the shell into a formed sphere that inflated rigidly (a bottle brush on a wire). At the old, near-zero spread every spark is coincident on the burst frame, but moving fastest, so the trails spread the flash's energy instead of stacking one blown-out sample that the glare drew as a six-armed star.
- ⚠ **A Fibonacci sphere is too regular.** Expanded uniformly the lattice keeps its pattern and the eye picks the spiral out as a rigid structure inflating: a quarter-radian of scatter per direction keeps the coverage and destroys the lattice, and the speed spread is wide (`0.34 + 0.66*rand^0.6`), because sparks that all reach the same distance leave a clean hollow surface.
- **Two colours per shell, split per spark, across the wheel** (`ShellColorB`): a hard split, not a blend, so two are seen. The second colour is `PAIR_STEP_MIN`..`PAIR_STEP_MIN + PAIR_STEP_SPREAD - 1` steps round the eight-hue wheel (the complement or a neighbour of it); "any different one" gave red with orange and gold with white, halves nobody could tell apart.

### Colour (#612)

The display came out near-white on every scene. Rules that fix it:

- ⚠ **Every hue is two channels and a zero.** The ACES fit in `Tonemap.fx` works per channel, and a spark driven up its shoulder (peak ~4 before exposure) squeezes the dominant channel under 1 while every minor channel catches up: a red of `(1, 0.22, 0.18)` comes out pink-white whatever else is done. A channel at zero stays zero through any curve, and the small second channel is the one lifted most (which is why a spark drifts warmer as it cools). White left the palette; magenta joined it.
- **Each hue is brought to `SPARK_LUMINANCE` (1.5)**, not one boost for all, within `BOOST_MIN`..`BOOST_MAX` (x2..x10). Blue and violet stop at x10, where they still bloom without their second channel turning them cyan or pink.
- **The hot core is a flash, not the spark's whole life**: `HOT_CORE` 0.25 of the way to white, arriving between brightness 0.6 and 0.9 (`HOT_CORE_FROM`/`HOT_CORE_WIDTH`). At 70 % white from a quarter brightness up (most of a spark's life) it was a white spark with a coloured edge.
- **Added light cannot colour a bright sky, and the result page's defocus averages a thin streak into the sky around it.** Over the meadow's noon blue a red spark lifts only the red channel, so the best purely additive spark is pale pink. The blend is therefore **premultiplied** (`Fireworks.SparkBlend`): a spark still adds exactly the light it did, and in alpha also **covers** what is behind it by its weight squared times `SKY_COVER` (12). Over a dark sky it is the additive display it was; over a bright one it lets a red spark be red. Much past 12 the colour a dim spark tends to (`radiance / SKY_COVER`) goes dark enough to read as soot.
- **Result** (mean HSV saturation of the display's own pixels, measured with a temporary flat-background instrument): dark sky 0.25 to 0.67, meadow noon sky 0.31 to 0.40, cloud 0.01 to 0.22; brightness over the dark sky unchanged (the colour did not come back by dimming it). Not fixed: over a bright sky a late spark is dimmer than the sky and fades into it pale, and a burst in the sky's own hue contrasts little.

### Under the sea (#761)

The sea writes no depth, so from below nothing hid the shells and they showed through the murk as if the water were glass (a drop cinematic dives under the island while the celebration has begun). `Fireworks.Draw` takes a visibility, `1 - SceneRenderer.LensSubmergedAmount`, set as the shader's `Visibility` on the premultiplied output (light and cover fade together); nothing is drawn once the lens is under. A "very blurred, refracted" look is not built, and the burst sounds are not muffled under water either.

## Sound level (#552)

The reports used to pound over the music (measured on the BS.1770 meter `MusicBake` carries: **-4.8 LUFS short-term, the mix peaking +6.6 dBFS** and clipping, against the themes' -19.7). Three causes, three fixes:

- **The drive.** The recording took the bake's 0.30 RMS (measured over a 4 s file whose sound is under two), a drive of x6.8 into the `tanh`. `BURST_TARGET_RMS` is 0.10 now (x2.6, no sample reaches the knee).
- **The knock and the click.** `SoftenReport` runs before the level is set: a 300 Hz fourth-order low-pass (two Butterworth sections) and an 8 ms raised-cosine fade on the head. The bake behind a missing file takes both.
- **The rate.** A voice per shell made loudness the launch rate (thirteen booms a second in the barrage). `Fireworks` keeps a count of recent reports decaying over `REPORT_CROWD_SECONDS` (1.2 s) and `ProceduralAudio.BurstCrowding` turns each report down by 1/sqrt(1 + count): the stream stays near one report's power however dense the sky is, while a lone shell after a quiet spell lands at full level.

After: -20.6 LUFS short-term once the fanfare ended, -21.0 in the relaxed tail, a lone report 5 LU under the shot, the mix peaking -7.5 dBFS. ⚠ K-weighting discounts bass (its high-pass is at 38 Hz) and this sound is mostly under 200 Hz, so these figures say "level with a theme on the meter", not that a subwoofer agrees; the owner listens on monitor speakers.

## Checking it

`celebrate` on the command line fires the display on the front end (clearing a level is the only thing that normally starts it and cannot be scripted); `level=<any> result celebrate` shows it over the result page. Judge it over a dark scene and a bright one: the play camera sees little of any display, because the shells burst above the frame it holds on the cluster. The display is random, so compare by contact sheet, never by one frame.
