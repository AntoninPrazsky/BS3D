using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;
using System.Collections.Generic;
using System.Text;

namespace BS3D.Effects
{
    /// <summary>
    /// The game's name as a <b>3D object standing in the front end's scene</b> (#248): the title set in round
    /// tube letters (<see cref="LetterMesh"/>), one word to a line stacked into the frame's top-right corner
    /// with the last word blown up into a badge, each letter its own colour off a rainbow that flows through
    /// the word, each ringed by a dark keyline, the whole thing breathing and drifting.
    /// <para>
    /// It replaces a static Myra label that the owner's complaint called a whisper: "the title should be
    /// prominent, distinct, unmissable, the main thing, and it currently whispers". The issue offered two
    /// roads — an outlined, rainbow, pulsing <i>2D</i> treatment, or real 3D lettering that lives in the
    /// scene — and the owner chose the second: <i>"it will render as colourful / rainbow 3D objects"</i>. So
    /// the 2D road is not built at all, and the label is gone.
    /// </para>
    /// <para>
    /// <b>It is also what the game's 2D logo turns into (#454).</b> The game opens on a flat bitmap of the logo
    /// (<see cref="Screens.SplashPage"/>: up out of black, the scene cross-faded in behind it), and the picture
    /// then cross-fades into these letters standing in the picture's own layout in the middle of the frame —
    /// see the <c>LOGO_</c> constants, every one measured off the bitmap — before the block flies to the corner
    /// as the menu arrives. That hand-over is the <i>only</i> time the title is in the middle of the frame:
    /// it starts settled in its corner (<see cref="_morph"/>), and <see cref="BeginHandover"/> is what puts
    /// it in the picture's place. Until #454 the title opened centred on one line on its own account and moved
    /// to the corner when the splash handed over; the bitmap is the first impression now, and a second title
    /// arriving in the middle of the frame under it would have been two openings.
    /// </para>
    /// <para>
    /// <b>It is placed against the FRAME, not against the world</b>, for the reason
    /// <see cref="TrophyPodium"/> is: the front end turns the camera around the island once every ninety
    /// seconds, so anything left standing in the arena swings out of shot. The anchor is stated in normalised
    /// device coordinates and turned into a world offset through the camera's own <b>projection</b>
    /// (<c>d / M11</c>, <c>d / M22</c>), so the wordmark holds its corner at any field of view and any window
    /// shape — which matters more here than for the cup, because the front end runs a 60° lens where gameplay
    /// runs about 43° and the two share one camera object.
    /// </para>
    /// <para>
    /// <b>It is drawn in the HDR scene pass, not in the sharp foreground layer.</b> The cup and the confetti
    /// pay for that layer because the result screen defocuses the frame around them; the front end never
    /// defocuses and never dims (<c>MainMenuPage.DimsFrame</c> is false and no front-end page overrides
    /// <c>FrameBlur</c>), so there is nothing here for a sharp layer to be sharp against — and the layer costs
    /// a permanently allocated supersampled target, a bright pass over it and a full-screen composite on every
    /// frame of a screen the adaptive-quality probe is measuring. In the scene pass the wordmark gets the
    /// frame's real depth buffer, the same exposure, the same ACES curve and the same film grain as everything
    /// else, and its bright pass feeds the bloom pyramid for free.
    /// </para>
    /// <para>
    /// <b>A rainbow here is the second deliberate exception to the front end's greyscale rule</b>, and
    /// <c>docs/game-shell.md</c> is explicit that an exception has to argue for itself "or the next accent
    /// gets waved through on its example". Three things make this one admissible where #180's flat rainbow
    /// badge failed — that one was photographed over the meadow and its cyan stop went "very nearly
    /// invisible against the sky", so the badge blinked out for a third of every sweep:
    /// <list type="number">
    /// <item>It is <b>not chrome</b>. It is the game's own name, the one element on the screen whose job is to
    /// be looked at rather than read past, and the owner asked for it in as many words.</item>
    /// <item>It is a <b>lit solid</b>, not a flat glyph. A round tube carries a light-to-dark gradient across
    /// itself, a specular streak down its length and a Fresnel rim on its silhouette, so it reads by shading
    /// and by edge even where its hue matches what is behind it. That is exactly what a flat sprite cannot
    /// do, and it is why the same colour that failed on a 2D badge holds here.</item>
    /// <item>Every letter carries a <b>dark keyline of its own</b> (see <see cref="OUTLINE_WIDTH"/>), so most
    /// of the contrast that makes the word readable comes from something other than its colour.</item>
    /// </list>
    /// <b>That third leg is weaker than it was, and deliberately.</b> The keyline was a constant near-black —
    /// contrast no hue could take away — and the owner ruled it out on seeing it run: it read as ink from a
    /// different game and did not suit the rest of the game's style. It is now thinner and plays the rainbow
    /// itself, a third of a turn ahead of the letter it rings (<see cref="OUTLINE_HUE_SHIFT"/>), held dark
    /// enough (<see cref="OUTLINE_VALUE"/>) that it still separates the letter from what is behind it. What
    /// survives of the guarantee is that the rim is <i>dark</i> whatever it is playing, which holds against a
    /// bright scene; what is given up is the case of a dark scene, where a dark rim has nothing to be dark
    /// against and the glow's floor is what carries the word instead. That is the trade, made with the owner's
    /// eyes on both versions rather than inferred.
    /// </para>
    /// </summary>
    public sealed class TitleWordmark : IDisposable
    {
        //=== THE LETTERING ===

        //HALF THE STROKE WEIGHT, in cap heights. 0.13 (a stroke of 0.26) is a heavy mono-line: it gives the
        //word the mass a wordmark needs while leaving B's counter 0.24 of a cap height of daylight, which is
        //about twenty pixels at the size this stands on a 900p frame. Heavier closes the counters of B, O, D
        //and 3 and the word turns into a row of blobs; lighter and the tube stops reading as an inflated
        //object and starts reading as wire.
        private const float TUBE_RADIUS = 0.13f;

        //THE KEYLINE, in cap heights, drawn as a second fatter tube behind each letter. This is the "edges —
        //coloured lines around the letters, an outline" of the owner's brief. It is a CONSTANT fraction of the
        //cap height rather than a pixel width, so it holds its weight from 900p to 4K.
        //
        //It was 0.035 and NEAR-BLACK for one revision, and the owner ruled on both after seeing it running:
        //the black line read as ink from a different game — "it does not suit the rest of the game's style" —
        //and it was too heavy. So it is thinner, and it is COLOURED (see OUTLINE_HUE_SHIFT). What that costs is
        //stated where the rainbow argues its case in the class remarks: a keyline the same near-black behind
        //every letter was contrast the hue could not take away, and a coloured one is a weaker guarantee. It
        //is still the right trade — the owner has seen both — but it is a trade and not a free win.
        private const float OUTLINE_WIDTH = 0.022f;

        //WHERE THE KEYLINE'S OWN COLOUR COMES FROM: the letter's hue advanced a third of the way round the
        //same wheel, so the rims are themselves a rainbow, one step ahead of the letters. A third of a turn
        //rather than a half: the complementary of a colour at a low value is the muddiest thing on the wheel,
        //where a triadic step stays a colour at every stop. And rather than a small shift, which reads as a
        //shaded edge of the same letter instead of as a line playing its own colours.
        private const float OUTLINE_HUE_SHIFT = 1f / 3f;

        //How dark that colour is drawn, as an sRGB scale on the hue. Dark enough that the rim still separates
        //the letter from whatever is behind it - which is the job the black line did and the whole reason a
        //keyline is here at all - and no darker, or the colour it is now supposed to be playing is not
        //readable as a colour. sRGB, so the linear radiance it lands at is nearer a twentieth than a third.
        private const float OUTLINE_VALUE = 0.34f;

        //THE GAP LEFT BETWEEN TWO LETTERS' INK, in cap heights, on top of the two tube radii the tracking has
        //to clear first (see LetterShapes.WordWidth). It has to clear two KEYLINES as well before any daylight
        //is left, which is where the figure comes from: 0.10 less 2 x OUTLINE_WIDTH leaves about 0.056 of real
        //gap, so the letters read as separate without the word falling apart into eleven objects. It was set
        //against a keyline half again as thick and is deliberately not tightened now that one is thinner —
        //the extra daylight is what lets a COLOURED rim be read as a rim rather than as part of its neighbour.
        private const float DAYLIGHT = 0.10f;

        //⚠⚠ FACETS AROUND EVERY TUBE, AND IT IS ONE FIGURE FOR ALL THREE OF THEM BECAUSE THEIR SILHOUETTES
        //HAVE TO NEST. This is the single nastiest trap in this class and it bit twice, both times as a DASHED
        //contour along the letters and both times plainest on the badge — the largest thing on screen, and so
        //where a tessellation fault shows first.
        //
        //Each shell is a POLYGON of this many sides, not a circle, so its silhouette lies somewhere between
        //r·cos(pi/n) and r. Give two neighbouring shells different counts and those two bands interleave: their
        //boundaries cross back and forth, and in the crossings there is a sliver belonging to NEITHER of them.
        //What shows in a sliver is whatever is behind — the sky, the grass — so a one-pixel thread of backdrop
        //runs along inside the outline and breaks it up.
        //
        //It was the keyline against the body first (ten against sixteen), which equal counts fixed. Then it came
        //back between the KEYLINE and the AURA when the aura had to be narrowed to clear the fold (see
        //AURA_WIDTH): at the old width their radii were 0.063 apart and no facet error could bring them
        //together, at the new one they are 0.020 apart and a 12-gon inside a 16-gon crossed it easily. Measured
        //by magnifying the badge's edge to single pixels: body, then a purple keyline, then a ONE-PIXEL GREEN
        //THREAD OF GRASS, then the glow.
        //
        //So: one count, and the three radii (0.130, 0.152, 0.172) keep their cos(pi/16) bands clear of one
        //another — 0.1275-0.130, 0.1491-0.152, 0.1687-0.172, which do not touch. Sixteen is what the letter
        //itself wants (its specular streak runs along it and its silhouette is on show against the sky); the
        //other two would have been happy coarser and cannot be.
        private const int TUBE_SIDES = 16;

        //HOW MUCH BIGGER THE LAST WORD IS. The owner picked the three-line composition with the last word as
        //a big separate badge, out of three offered: one line, two lines, and this. "3D" is the half of the
        //name that says what the game IS, so it is the half that gets to be huge — and being two glyphs it can
        //afford to be, where blowing up a seven-letter word would run off the frame.
        private const float BADGE_SCALE = 1.9f;

        //The gap between two lines' ink, in cap heights, scaled by the TALLER of the two lines it separates.
        //A constant gap reads as tight under the badge and loose under the small lines.
        private const float LINE_GAP = 0.16f;

        //=== WHERE IT SITS IN THE FRAME ===

        //How far in front of the lens the wordmark hangs. It does not set the SIZE — that is solved from the
        //frame below — only the perspective: at this distance the block's far corners are about three quarters
        //of a unit further from the lens than its centre, so the word has a real vanishing point without the
        //wide-angle stretch a closer hang would give it.
        //
        //IT IS ALSO A DEPTH RELATION, and one that stopped being simple the day the fly-in arrived (#254).
        //The block hangs in the world with depth writes on, so anything nearer the lens than this is drawn
        //in front of the game's own name. The pass now comes in to within a couple of units of the balls
        //(#261), far inside this figure, and what keeps that honest is the title shrinking to a small
        //corner mark for the whole close pass (Draw's presence): a ball passing in front of a modest corner
        //mark is parallax, while a ball cutting through the frame-dominating name was the broken look the
        //clearance used to be spent avoiding.
        private const float DISTANCE = 7f;

        //HOW MUCH OF THE FRAME THE BLOCK FILLS. Height binds on every aspect anyone plays at (at 16:9 the
        //width solve comes out about a quarter larger, so it never bites); the width limit is what keeps an
        //unusually tall window — 4:3, a portrait desktop — from pushing the word off both sides. Both are of
        //the FULL frame, not the half.
        //
        //These are the ASKED share, not the delivered one: the perspective term in the fit below spends some
        //of it on the margin the sway needs, so 0.66 lands the resting block at a little over half the frame's
        //height. Photographed rather than reasoned about, at 16:9 and at both ends of the aspect range a
        //window can be dragged to (1104x861 and 1744x721, where the two limits swap over) — the flat 2D label
        //this replaces had its own size measured off a capture for the same reason, that type too big for its
        //frame is a fault nothing in the code can catch.
        private const float BLOCK_HEIGHT_FRACTION = 0.66f;
        private const float BLOCK_WIDTH_FRACTION = 0.62f;

        //=== THE OPENING COMPOSITION IS THE 2D LOGO'S LAYOUT (#454) ===
        //
        //The game opens on a flat bitmap of its logo (Images/logo/bs3d-logo-2048.png, drawn by SplashPage), and
        //this wordmark is what that picture CROSS-FADES INTO before flying to its corner — so the composition
        //the letters stand in while the picture thins has to be the picture's own, or the hand-over reads as
        //one title being swapped for another. The bitmap is "BUBBLE" over "SHOOTER", both lines centred and
        //nearly touching, with a small "3D" set in a round badge tucked under the second word. Every figure
        //below was MEASURED off that bitmap (alpha > 128, in the 2048 x 1267 file) and is stated in cap heights
        //of a word line, where one word line's ink (cap + two tube radii) is the 414 px the bitmap's two words
        //average: BUBBLE rows 31–441, SHOOTER rows 448–865, the "3D" glyphs rows 906–1117 and the badge disc's
        //bottom at 1242.
        //
        //It cannot be 1:1 and is not meant to be — the bitmap's letters are fat balloon lettering in a different
        //hand, and matching them would be a redesign of LetterMesh — but with the LINES in the same places the
        //picture and the geometry are the same object at the moment of the cut, and the disc, which this
        //alphabet has no counterpart for, simply dissolves with the picture. The owner's ruling was exactly
        //that: "it need not be perfect and 1:1, there will be a cross-fade, and it will still be striking".

        //The daylight between the two words' ink: six rows, all but touching (0.018 of a cap).
        private const float LOGO_LINE_GAP = 0.02f;

        //From the second word's ink down to the top of the "3D" glyphs — forty rows, the badge's rim.
        private const float LOGO_BADGE_GAP = 0.12f;

        //How big the "3D" is against a word line in the PICTURE: 212 rows of ink against 414, and 337 columns
        //against the 598 this alphabet's "3D" would take at full size — the two agree on about a half. It is
        //the opposite of the menu's BADGE_SCALE, and the move between the two compositions is where the badge
        //GROWS: the picture's small "3D" in its disc swells into the menu's big one as the block flies.
        private const float LOGO_BADGE_SCALE = 0.53f;

        //The disc runs on below the "3D" glyphs (rows 1117 to 1242), and the block's box carries that empty
        //depth so that its CENTRE is the picture's centre — the fit and the anchor place the box's centre on
        //the frame's, and a box that stopped at the glyphs would stand the whole word a few per cent high.
        private const float LOGO_DISC_MARGIN = 0.38f;

        //How much of the DRAWN bitmap its ink actually covers, width and height — the file is cropped to the
        //drawing with a few pixels of clear margin (35 columns left, 37 right, 31 rows above, 25 below), and
        //the splash hands over the rectangle it drew, not the ink. The open composition asks for the ink's
        //share of the frame, so the second word lands on the picture's second word rather than a shade wider.
        private const float LOGO_INK_WIDTH_SHARE = 1977f / 2048f;
        private const float LOGO_INK_HEIGHT_SHARE = 1211f / 1267f;

        //⚠ THE ONE FIGURE ABOVE THAT IS NOT A PIXEL MEASUREMENT, AND #475 IS WHAT EXPOSED THE GAP IT LEAVES.
        //Every LOGO_ constant above fixes a VERTICAL rhythm (the gaps, the badge's shrink, the disc's margin),
        //but nothing ties the block's WIDTH to the picture at all — the open composition's lines were laid out
        //at the menu's own tracking (2*TUBE_RADIUS + DAYLIGHT = 0.36), which is a figure about the MENU's
        //corner, not about this bitmap. Solved from the two ink measurements above and this alphabet's own
        //letter widths: the block comes out 6.66 cap-heights wide by 3.81 tall at the menu's tracking, W/H =
        //1.749, where the picture's own ink rectangle (1977 x 1211) is 1.633 - a block proportionally WIDER
        //than the picture by enough that Draw's fit (whichever of width/height binds) came out WIDTH-bound,
        //so the letters stood at 93% of the picture's own height. That is not "a shade narrower" (the class
        //remarks' own claim, from a single photographed frame): three lines compressed 7% short cascades into
        //a few pixels of drift by BUBBLE and enough by "3D" to double-expose rather than land on it, which is
        //what a WATCHED run shows and a single mid-fade photograph did not.
        //
        //Tightened for the open composition alone (the menu's own tracking is untouched - it was never the
        //complaint) to 0.32, which brings W/H to 1.686 - not exact, because exact (0.2862) undercuts the
        //keyline clearance two adjacent rims need: TRACKING - 2*TUBE_RADIUS is the gap between them, and it
        //has to clear 2*OUTLINE_WIDTH (0.044) before any of it is daylight rather than two rims touching. The
        //floor is 2*(TUBE_RADIUS + OUTLINE_WIDTH) = 0.304; this keeps a hair above it rather than reopening
        //the fold/touching-rim trap OUTLINE_WIDTH's own remarks warn about. What is left (1.686 against 1.633
        //- the letters realise 97% of the picture's own height instead of 93%, half the shortfall) is the
        //residual the owner's own ruling on #454 already accepted - a cross-fade, not a match - and closing
        //it further is exactly the LetterMesh redesign that issue ruled out of scope. (Verified with a script,
        //not carried over: the figures above are LetterShapes' actual per-glyph advances summed by hand, not
        //an estimate.)
        private const float LOGO_TRACKING = 0.32f;

        //What the open composition asks for when NOTHING has handed a picture over — a share no frame ever
        //shows, because the title stands settled in its corner from the first frame unless BeginHandover is
        //called (see _morph). Kept sane rather than zero so a stray draw could not divide by nothing.
        private const float OPEN_WIDTH_FRACTION = 0.53f;
        private const float OPEN_HEIGHT_FRACTION = 0.76f;

        //=== THE MOTION ===

        //THE BLOCK'S OWN DRIFT. Two sways about two axes at two unrelated rates, so they never come back into
        //step and the word never looks like it is on a turntable: it reads as an object hanging in the air.
        //The yaw is what shows the letters' round sides and moves the specular streak along them, which is the
        //whole reason the wordmark is geometry and not a picture. Kept well under the angle at which a letter
        //would start to foreshorten badly - a title that turns edge-on is a title that cannot be read.
        //
        //THE YAW IS BIASED TOWARDS THE FRAME'S CENTRE AND NEVER CROSSES BACK, and that is the owner's ruling
        //on the first revision, where it was a plain symmetric sway about facing straight out. A symmetric
        //sway spends half its time turned the OTHER way, and the wordmark hangs in the top-right CORNER, so
        //half the time it was angled away from everything - "as if it were looking out of the window, and
        //since it is at the edge that does not look good". Turned inwards it reads as a sign angled to face
        //the room rather than the wall behind it.
        //
        //THE SIGN IS THE WHOLE POINT AND IS EASY TO GET BACKWARDS. Block space has +x to screen right and +z
        //towards the lens, and CreateRotationY carries +z to (sin, 0, cos) - so a POSITIVE angle tilts the
        //face towards +x, screen right, off the frame, and a NEGATIVE one turns it towards the centre. Hence
        //the centre angle is negative and the sway is smaller than it, so their sum never reaches zero.
        //It is the SETTLED composition's turn. The opening one stands in the middle of the frame, where there
        //is no edge to face away from and facing straight out is right, so the bias arrives with the move.
        private const float YAW_CENTRE = -0.20f, YAW_SWAY = 0.07f, YAW_RATE = 0.34f;
        private const float PITCH_ANGLE = 0.055f, PITCH_RATE = 0.23f;

        //=== THE MOVE FROM ONE COMPOSITION TO THE OTHER ===

        //How long the title takes to leave the middle of the frame and settle into its corner. Long enough to
        //be watched rather than glimpsed, and short enough that a player who came to press Play is not made to
        //wait for it - and it is a smoothstep, so it leaves and arrives at rest and only the middle is quick.
        //It runs once per launch, when the splash hands the front end over after the 2D logo has cross-faded
        //into these letters (#454); a launch that skipped the splash never sees it (see _morph).
        private const float MORPH_SECONDS = 1.15f;

        //The arrival — and since #454 it happens UNDER THE FADING PICTURE rather than into an empty frame.
        //BeginHandover starts it on the frame the splash begins thinning the bitmap, so the letters swell the
        //last few per cent into place while the flat picture over them goes, and the flare below lands as the
        //last of it leaves: the picture inflates into geometry rather than being replaced by it. The swell is
        //small on purpose — it was 0.58 when the title arrived alone at boot, and a word growing by that much
        //under a picture that stays put reads as two things, not one. It starts at a size rather than at
        //nothing because the letters are opaque geometry: there is no alpha to fade here.
        private const float REVEAL_SECONDS = 0.7f, REVEAL_FROM = 0.86f;

        //Below this the whole draw is skipped — a presence that has all but reached zero is a block of
        //degenerate sub-pixel matrices nobody can see. A guard on the caller's scalar rather than a state
        //the front end reaches: since the owner's ruling on #261 the flight floors the title at a small
        //corner size rather than nothing, so nothing on the front end asks for zero to-day. The reveal's
        //own rule, the crosshair's own threshold — an eased scalar that has settled at either end is not
        //a draw.
        private const float MIN_PRESENCE = 0.01f;

        //And the glow's own kick as it lands, on top of the beat. This is where the reveal's overshoot lives,
        //because scale cannot have one: the fit above solves the block to the frame, so a block that overshot
        //its own size would cross the inset it was just fitted inside. Light has no such budget.
        private const float REVEAL_FLARE = 0.30f;

        //THE WAVE THROUGH THE LETTERS, and the wavelength is not a taste: it is EXACTLY ONE CYCLE across the
        //whole wordmark. At any other figure the letters read as shimmering independently rather than as one
        //word undulating, and per-letter vertical motion is the one motion that touches the alignment a word
        //is read from. Slow, for the same reason.
        private const float WAVE_DEPTH = 0.05f, WAVE_RATE = 0.55f;    //cap heights, cycles a second

        //And each letter's own small turn, on the same one-cycle phase. It is not for the movement — at this
        //angle it is barely visible as movement — it is so the chamfer highlight and the keyline's leading
        //edge crawl continuously along the word. A bevel that never moves relative to the key light is a
        //bevel nobody notices.
        private const float LETTER_YAW = 0.13f;

        //THE DEPTH BOW: the middle of each line stands nearer the lens than its ends, in cap heights. One
        //number, and it is what turns the block's yaw from foreshortening a flat plane into real parallax with
        //real occlusion between letters - the cheapest "this is really three-dimensional" signal there is, and
        //it costs nothing because the scene pass has a depth buffer. Small: it also changes the middle letters'
        //apparent size, and a bow deep enough to see as a bow reads as a fisheye lens.
        private const float BOW_DEPTH = 0.45f;

        //=== THE PULSE ===

        //A breath every two and a bit seconds - slower than a heartbeat, which is deliberate: the balls have
        //the heartbeat (BallRenderSet's own pulse) and #252 was the owner asking for one more thing to STOP
        //sharing it. A wordmark is not a ball.
        private const float BEAT_RATE = 0.45f;                        //cycles a second

        //The pulse has TWO limbs on purpose, because either alone fails on half the game's scenes.
        //The scale breath is visible over any backdrop, bright or dark, since it is motion rather than light.
        //The glow is what makes the word look lit from inside, and it is what carries the pulse over a DARK
        //scene where a 3 % size change on a small object is nearly nothing.
        private const float SCALE_BEAT = 0.030f;

        //THE GLOW, as a Rec. 709 luminance in LINEAR RADIANCE, at the trough and the crest of the beat. The
        //glare pass blooms anything over 0.55, so the crest is over the threshold and the trough is well under
        //it: the word breathes in and out of its own halo, which is the pulse the issue asked for. It is added
        //to every letter at the SAME luminance rather than at the same radiance (see Glow below), so a green
        //letter and a blue one bloom alike - Rec. 709 makes green about ten times brighter than blue at equal
        //radiance, and without the normalisation the word would flare in six different ways as the ramp turned.
        //
        //THE TROUGH IS A FLOOR AND NOT A ZERO, and it was 0.08 for one pass and photographed over the MOON,
        //where that failed outright. A black sky gives the light rig almost nothing to work with, so over the
        //moon and in space the glow is very nearly the only thing lighting the letters - at 0.08 the word went
        //dark and muddy for half of every 2.2-second beat, which is #180's "the badge blinked out for a third
        //of every sweep" arriving through brightness instead of through hue.
        //
        //THE CREST IS NOT PUSHED HIGHER THAN IT HAS TO BE, because the emissive term is FLAT: it is added per
        //pixel without regard to the normal, so every point of the glow is brightness the shading gradient does
        //not get to vary. That gradient is what makes a tube read as round, so a glow big enough to swamp it
        //turns the letters back into the flat stickers this whole approach exists to avoid.
        //
        //⚠ AND RAISING IT IS NOT HOW THE HALOES GET BIGGER — that was tried, on the owner's ask for more of
        //them, and 0.34/1.15 came back PALE: an emissive bright enough to swell the halo dominates the diffuse,
        //and the tonemap's own shoulder desaturates what it is given, so the letters went chalky at the crest of
        //every beat and only their haloes kept the colour. The halo is a shell of its own now (see AURA_WIDTH).
        //
        //WHICH IS ALSO WHY THE LETTERS' OWN SWING IS SMALL. It was 0.22 to 0.60 while this term was the only
        //thing pulsing; the aura carries the beat now, and a letter that breathed as hard as its halo does went
        //pale at every crest for a second time — the bloom is a full-frame pass, so a halo that swells lays its
        //own blurred light back over the letter it came from, and the letter's emissive was adding to that.
        //A quiet floor with a gentle lift over it keeps the hue rich through the whole beat.
        private const float GLOW_REST = 0.20f, GLOW_PEAK = 0.38f;

        //=== THE GHOSTS ===

        //THE AURA: a third tube, fatter again than the keyline, drawn ADDITIVELY behind the letter so what shows
        //of it is a band of coloured light just outside the rim — which the glare pass then blows into the soft
        //coloured halo the owner saw in the verification captures and asked to have more of.
        //
        //IT IS A SEPARATE SHELL RATHER THAN A BRIGHTER LETTER, and that is the whole point of it. The halo can
        //only be fed by pushing something over the bright pass's 0.55 threshold, and pushing the LETTER over it
        //costs the letter its colour and its roundness both (see GLOW_REST above). Pushing a band OUTSIDE the
        //letter over it costs neither: the letters keep the light they were tuned to and the ghosts get as much
        //as they want. Width in cap heights, from the letter's own surface — so the band that shows is this less
        //OUTLINE_WIDTH, and the keyline still separates the letter from its own glow.
        //
        //⚠⚠ AND ITS CEILING IS GEOMETRY, NOT TASTE. It was 0.085, which put the shell's radius at 0.215 against
        //this alphabet's tightest bend of LetterShapes.MIN_BEND_RADIUS = 0.2016 — so the shell FOLDED THROUGH
        //ITSELF on the inside of that bend, and the fold showed as flat coloured patches filling the counters of
        //B, O, D and 3. The owner reported it as looking like a hole or a gap, which is exactly what an
        //inside-out surface looks like. LetterMesh now REFUSES a tube that fat, so this cannot come back
        //silently; the figure below is 85 % of the bend radius less the letter's own, which leaves the counters
        //of B a clear 0.16 of a cap height of daylight (their bars are 0.50 apart, less two shell radii).
        private const float AURA_WIDTH = 0.042f;

        //Its own linear luminance at the trough and the crest of the same beat, and both are well over the 0.55
        //the glare pass blooms at: the band itself is only a few pixels of a 900p frame, so what the eye reads
        //is almost entirely what the bloom pyramid makes of it, and a band that only just crossed the threshold
        //would smear into nothing. The swing is what makes the ghosts breathe with the word.
        //
        //THESE TWO ARE TIED TO AURA_WIDTH BY THEIR PRODUCT, and were doubled when the width was halved to clear
        //the fold above. What the bloom integrates is the LIGHT in the band, which is its width times its
        //radiance — so a band half as wide at twice the radiance makes the same halo, and the halo's SIZE was
        //never the band's anyway, it is the pyramid's. Their old pair (0.50 and 0.85 at a width of 0.085) is
        //the same product as this one, which is why the photographs either side of the fix match.
        //
        //⚠ THE CREST IS BOUNDED BY WHAT THE HALO DOES TO THE LETTERS, not by the halo itself, and this is the
        //third figure that lesson has moved. The bloom is a FULL-FRAME pass: a halo that swells lays its own
        //blurred light back over the letter it came from, that brightening climbs the ACES shoulder, and the
        //shoulder desaturates what it compresses — so the brighter the ghosts, the paler the word inside them.
        //At 1.75 the letters were flooded and their counters filled in; 1.05 kept the counters but still went
        //chalky at every crest. At 0.85 the beat reads as the word breathing between SOLID AND VIVID at the
        //trough and SOFT AND GLOWING at the crest, which is a better pulse than brightness alone would be —
        //but it is a ceiling found by photograph, and anything above it is paid for in the letters' colour.
        private const float AURA_REST = 1.01f, AURA_PEAK = 1.72f;

        //=== THE COLOUR ===

        //A FULL WHEEL ACROSS THE WORDMARK, which is what "rainbow" was asked for, and it TRAVELS - the hue at
        //a given letter walks on, so the word is never twice the same and the eye is caught by the change
        //rather than by the colour. One turn of the wheel every fourteen seconds: slow enough that a glance
        //reads a still image, fast enough that a second glance reads a different one.
        private const float HUE_FLOW = 0.07f;                         //turns a second

        //Saturation is held just off full and every hue is then lerped a sixth of the way to WHITE, which lifts
        //the blue side of the wheel off black: a fully saturated blue is a DARK colour (Rec. 709 gives it 0.07
        //against green's 0.72), and a near-black letter in a rainbow word reads as a hole in the word rather
        //than as a letter. It was a fifth of the way to white for one pass and photographed over the sea, the
        //brightest backdrop in the game, where the whole word came out PASTEL — chalky against a bright sky
        //rather than vivid over it. So the whitening is only as much as the dark side of the wheel needs, and
        //the contrast the word is read by comes mostly from the keyline instead, which is dark against a
        //bright scene whatever hue it happens to be playing.
        private const float SATURATION = 0.94f, WHITEN = 0.16f;

        //The material diffuse every letter mesh is built with. 0.8 rather than 1 because Draw's diffuseTint
        //path multiplies the tint by the material's own luminance and boosts by 1.25 to compensate for
        //"the brightest material being 0.8" - so at exactly 0.8 a tint passes through unchanged, and at 1 it
        //would come out a quarter too bright and clip the hue towards white.
        private static readonly Vector3 BODY_MATERIAL = Vector3.One * 0.8f;

        //THE KEYLINE'S MATERIAL IS BLACK, AND ITS COLOUR ARRIVES AS EMISSIVE INSTEAD. That is not a
        //flourish, it is the only way to get a STABLE colour onto this pass. The keyline is drawn with front
        //faces culled (see Draw), so every pixel of it has a normal pointing away from the lens - which says
        //nothing about where the three directional lights are, so a LIT rim would brighten and darken as the
        //menu's orbit carried the lights round behind it, and a rim that is meant to be one line of one colour
        //would breathe on its own. EmissiveTint is added flat, per pixel, ungoverned by any normal, so a rim
        //authored through it is exactly the colour it was asked for from every bearing and under every one of
        //every dome. The diffuse is therefore held at black and the specular stated small (rather than
        //left zero, which falls back to the renderer's white default) so nothing else can reach it - and the
        //sky reflection is turned off entirely, for the reason in GlyphIndex.
        private static readonly Vector3 OUTLINE_MATERIAL = new(0.008f, 0.008f, 0.010f);

        private readonly GraphicsDevice _device;

        //One mesh and one renderer per DISTINCT letter, twice over: the body and its keyline. A letter that
        //appears three times (B, in this title) is one mesh drawn three times, and the two O's of "SHOOTER"
        //likewise - which is why the meshes are keyed by character rather than by slot.
        private readonly Dictionary<char, int> _glyphIndex = new();
        private readonly List<LetterMesh> _bodyMeshes = new();
        private readonly List<LetterMesh> _outlineMeshes = new();
        private readonly List<LetterMesh> _auraMeshes = new();
        private readonly List<InstancedModelRenderer> _bodyRenderers = new();
        private readonly List<InstancedModelRenderer> _outlineRenderers = new();
        private readonly List<InstancedModelRenderer> _auraRenderers = new();

        //Every letter of the title that is actually drawn, in reading order — and nothing about WHERE it sits,
        //because that depends on which composition is standing. Spaces are not letters: they move the pen and
        //that is all they do.
        private readonly Letter[] _letters;

        //THE TWO COMPOSITIONS, both solved once at construction, and every frame is somewhere between them.
        //_open is the 2D logo's layout (#454): the same three lines centred in the middle of the frame, the
        //badge small, in the rectangle the splash drew the bitmap in — see the LOGO_ constants. _settled is the
        //menu's: one word to a line, right-aligned into the corner, the last word blown up into a badge. The
        //open block is the one field here that is not readonly, because its share of the frame is the
        //picture's and the picture is placed in pixels: BeginHandover restates it per launch.
        private readonly Placement[] _open, _settled;
        private Composition _openBlock;
        private readonly Composition _settledBlock;

        //Where between them this frame is, 0 open and 1 settled, and the wall clock it was last advanced
        //against. The morph is driven off the WALL CLOCK rather than an elapsed value because this class is
        //only ever reached from a draw: a frame that is not drawn is a frame in which nothing here should have
        //moved. A gap in the drawing — a level played, then Main Menu — comes back as one huge step, which
        //saturates the morph and is exactly right, because the title belongs in its corner by then.
        //
        //BOTH START AT ONE, SETTLED AND ARRIVED (#454): the title stands in its menu corner from the first
        //frame it is ever drawn and never occupies the middle of the frame on its own account. The only thing
        //that puts it there is the splash handing the 2D logo over (BeginHandover), which is the one launch
        //path that has a picture for it to stand in — a `play` boot, or a skip before the hand-over began,
        //finds the title already in its corner rather than watching it fly there over a frame it never opened
        //in the middle of.
        private float _morph = 1f, _reveal = 1f;
        private float _lastClock = -1f;

        //THE KEYLINE PASS IS ONE DRAW A LETTER, like the body pass, and it was eleven INSTANCED draws until
        //the rim took a colour of its own: a colour is a per-DRAW uniform here, so the moment every letter's
        //rim differs there is nothing left to batch. Fifteen draws rather than eleven, on a pass whose whole
        //cost measured under this machine's noise.

        //The one instance the body pass hands over per letter: a body draw is one letter, because its colour
        //is a per-DRAW uniform (InstancedModelRenderer.Draw's diffuseTint) and every letter's is different.
        private readonly ModelInstance[] _oneInstance = new ModelInstance[1];

        //Every letter's world matrix, solved ONCE a frame and read by both passes. The keyline has to sit on
        //exactly the matrix its letter sits on: re-deriving it for the second pass would leave the ring
        //thicker on one side by however much the two computations differ.
        private readonly Matrix[] _letterWorld;

        private readonly BasicEffectParams _bodyParams, _outlineParams, _auraParams;

        //The largest single letter's own span in cap heights, badge scale included: the letter that reaches
        //furthest towards the lens when the per-letter turn is at its extreme.
        private readonly float _widestLetter;

        //How far the composition is held off the frame's edges, as a fraction of the frame's HEIGHT — one
        //figure for both edges. Handed in rather than read off the menu, so this class carries no knowledge of
        //Myra's design units; see the call site for the derivation.
        private readonly float _insetFraction;

        /// <summary>
        /// Every renderer the wordmark owns, for the host's sky-lighting enrolment.
        /// <para>
        /// The letters are <b>lit by the scene like everything else in it</b>, which is not the choice
        /// <see cref="TrophyPodium"/> made and is worth stating. A cup is presented for a few seconds and
        /// wants one controlled finish; a wordmark stands over every backdrop under every
        /// domes for as long as the game is not being played, and enrolment is what makes it come out right
        /// on both ends of that range — over the sea at noon the rig is bright and so are the letters, over
        /// space and the Moon the background is black and dim letters read perfectly against it. The glow and
        /// the keyline are the two things that do <i>not</i> follow the dome, and between them they are what
        /// stops a dark dome from taking the word away.
        /// </para>
        /// </summary>
        public IEnumerable<InstancedModelRenderer> Renderers
        {
            get
            {
                foreach (InstancedModelRenderer renderer in _bodyRenderers) yield return renderer;
                foreach (InstancedModelRenderer renderer in _outlineRenderers) yield return renderer;
                foreach (InstancedModelRenderer renderer in _auraRenderers) yield return renderer;
            }
        }

        /// <summary>
        /// One drawn letter, and only what does <b>not</b> depend on which composition is standing — so the
        /// hue and the wave travel through the word in reading order whether the title is on one line or three,
        /// and neither jumps while it is moving between them.
        /// </summary>
        private readonly struct Letter
        {
            public readonly int Glyph;      //index into the mesh/renderer lists
            public readonly float Advance;  //the glyph's own advance, kept so the draw can centre it
            public readonly float Phase;    //0..1 along the whole wordmark in reading order

            public Letter(int glyph, float advance, float phase)
            {
                Glyph = glyph;
                Advance = advance;
                Phase = phase;
            }
        }

        /// <summary>
        /// Where one letter sits in one composition, in cap heights, measured from that composition's block
        /// <b>centre</b> — which is what the block turns about, so this is the frame the two compositions can
        /// be interpolated in without knowing each other's size.
        /// </summary>
        private readonly struct Placement
        {
            public readonly float X, Y;      //the letter's own centre
            public readonly float Scale;     //its line's scale (1, or BADGE_SCALE on the badge line)
            public readonly float Across;    //-1..+1 across the block's WIDTH: the depth bow

            public Placement(float x, float y, float scale, float across)
            {
                X = x;
                Y = y;
                Scale = scale;
                Across = across;
            }

            public static Placement Lerp(in Placement a, in Placement b, float t) =>
                new(MathHelper.Lerp(a.X, b.X, t), MathHelper.Lerp(a.Y, b.Y, t),
                    MathHelper.Lerp(a.Scale, b.Scale, t), MathHelper.Lerp(a.Across, b.Across, t));
        }

        /// <summary>
        /// One whole composition: how big the block is, how much of the frame it asks for, which corner it is
        /// pinned to and how far it stands turned.
        /// <para>
        /// <b>Both sizes are the SWEPT box rather than the resting ink</b> — the width is the widest line's ink,
        /// the height the whole stack's ink plus the reach of the wave that carries every letter above and below
        /// its line. So a letter at the top of the wave is inside the box rather than outside it, which is what
        /// makes the fit and the anchor honest.
        /// </para>
        /// <para>
        /// <b>The anchor is two numbers rather than a mode</b>, and that is what makes the move between the two
        /// compositions a plain interpolation: at 0 the block is centred in the frame on that axis, at 1 it is
        /// pinned to the far edge with the inset, and everything in between is where it is on its way. The rest
        /// of the anchor arithmetic is shared, so lerping these two lerps the whole anchor.
        /// </para>
        /// </summary>
        private readonly struct Composition
        {
            public readonly float Width, Height;                    //cap heights
            public readonly float WidthFraction, HeightFraction;    //of the whole frame
            public readonly float EdgeX, EdgeY;                     //0 centred, 1 pinned
            public readonly float Yaw;                              //the standing turn, radians

            public Composition(float width, float height, float widthFraction, float heightFraction,
                float edgeX, float edgeY, float yaw)
            {
                Width = width;
                Height = height;
                WidthFraction = widthFraction;
                HeightFraction = heightFraction;
                EdgeX = edgeX;
                EdgeY = edgeY;
                Yaw = yaw;
            }

            public static Composition Lerp(in Composition a, in Composition b, float t) =>
                new(MathHelper.Lerp(a.Width, b.Width, t), MathHelper.Lerp(a.Height, b.Height, t),
                    MathHelper.Lerp(a.WidthFraction, b.WidthFraction, t),
                    MathHelper.Lerp(a.HeightFraction, b.HeightFraction, t),
                    MathHelper.Lerp(a.EdgeX, b.EdgeX, t), MathHelper.Lerp(a.EdgeY, b.EdgeY, t),
                    MathHelper.Lerp(a.Yaw, b.Yaw, t));
        }

        /// <param name="title">
        /// The game's name. Split on spaces into one word a line, so the composition follows the string rather
        /// than a hardcoded layout: if the title ever gains or loses a word, the stack does too, and the last
        /// word is the badge whatever it is. Set in capitals whatever case it is written in — a wordmark is
        /// louder in caps, and it is the only case this alphabet draws.
        /// </param>
        /// <param name="ambientIntensity">
        /// The scene's flat ambient fill, the figure the rest of the setting is drawn with. Handed in rather
        /// than assumed, so the letters sit in the same light as the island turning behind them.
        /// </param>
        /// <param name="insetFraction">
        /// How far the block is held off the frame's top and right edges, as a fraction of the frame's
        /// <b>height</b> — one figure for both, which is the front end's own rule for its two corners.
        /// </param>
        public TitleWordmark(GraphicsDevice device, Effect instancingEffect, string title,
            float ambientIntensity, float insetFraction)
        {
            _device = device;
            _insetFraction = insetFraction;

            string[] words = (title ?? string.Empty).ToUpperInvariant()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            //Anything this alphabet cannot set is dropped rather than throwing, in the manner of the level
            //loader and the shot schedule: a wordmark is not a typesetter, and a title that gained a comma
            //should lose the comma, not the front end.
            for (int w = 0; w < words.Length; w++) words[w] = Drawable(words[w]);

            float tracking = 2f * TUBE_RADIUS + DAYLIGHT;

            //=== The letters, in reading order, once. The hue and the wave ride on this order alone, so they
            //do not care which composition is standing and cannot jump while the title is moving. ===
            int totalLetters = 0;
            foreach (string word in words) totalLetters += word.Length;

            List<Letter> letters = new();
            foreach (string word in words)
                foreach (char c in word)
                    letters.Add(new Letter(
                        GlyphIndex(device, instancingEffect, c), LetterShapes.Advance(c),
                        totalLetters > 1 ? letters.Count / (float)totalLetters : 0f));

            _letters = letters.ToArray();
            _letterWorld = new Matrix[_letters.Length];

            //=== And the two compositions the same letters are laid out in ===
            //The open composition gets its OWN tracking (see LOGO_TRACKING) - the menu's is a figure about
            //the menu's corner and was never asked to agree with the picture's own width.
            _settledBlock = Place(words, settled: true, tracking, out _settled);
            _openBlock = Place(words, settled: false, LOGO_TRACKING, out _open);

            for (int i = 0; i < _letters.Length; i++)
                _widestLetter = MathF.Max(_widestLetter,
                    _letters[i].Advance * MathF.Max(_open[i].Scale, _settled[i].Scale));

            Vector3 ambient = Vector3.One * ambientIntensity;

            //THE LETTERS' FINISH. A hard, glossy dielectric — a blown plastic or a boiled sweet, which is what
            //a bubble shooter's own name should be made of. Metalness stays at zero: a metal's colour IS its
            //reflectance, so a metal letter would mirror the sky rather than show its hue, and the hue is the
            //whole point here. The highlight is white and TIGHT (a high power), because a tight highlight on a
            //round surface is a streak rather than a wash, and a streak running down a tube is the single
            //strongest cue that the letter is round.
            _bodyParams = new BasicEffectParams(ambient, new Vector3(1f, 1f, 1f), 120f, Vector3.Zero);

            //And the keyline's, which wants no highlight at all: a specular glint on the dark ring would read
            //as a crack in the letter.
            _outlineParams = new BasicEffectParams(ambient, new Vector3(0.02f, 0.02f, 0.02f), 8f, Vector3.Zero);

            //The aura's, which wants no LIGHT at all: no ambient either, unlike the two above. It is a band of
            //emitted colour and nothing else, and every term the scene could add to it would be light the
            //frame adds twice.
            _auraParams = new BasicEffectParams(Vector3.Zero, new Vector3(0.001f, 0.001f, 0.001f), 4f, Vector3.Zero);
        }

        /// <summary>
        /// Lays the whole title out in one composition and hands back both the block it came to and where every
        /// letter sits in it.
        /// <para>
        /// Both compositions are one word to a line, and they differ in exactly three ways, which is all this
        /// method knows about which it is building: the <b>alignment</b> (right against the block when settled,
        /// centred in it when not), the <b>badge</b> (the last line blown up to <see cref="BADGE_SCALE"/> when
        /// settled, shrunk to the picture's <see cref="LOGO_BADGE_SCALE"/> when not) and the <b>spacing</b>
        /// (the menu's <see cref="LINE_GAP"/> everywhere when settled; the bitmap's own measured gaps and the
        /// empty depth of its badge disc when not — see the <c>LOGO_</c> constants).
        /// </para>
        /// <para>
        /// It works in block space: <c>x</c> runs LEFT from 0, the right-hand ink edge of the widest line, and
        /// <c>y</c> runs DOWN from 0, the top ink edge of the first line — both negative. The placements handed
        /// back are converted out of it, to the block's own centre, at the end.
        /// </para>
        /// </summary>
        private Composition Place(string[] words, bool settled, float tracking, out Placement[] placements)
        {
            string[] lines = words;

            float[] lineWidth = new float[lines.Length];
            float widestInk = 0f;
            for (int l = 0; l < lines.Length; l++)
            {
                lineWidth[l] = LetterShapes.WordWidth(lines[l], tracking);
                widestInk = MathF.Max(widestInk, (lineWidth[l] + 2f * TUBE_RADIUS) * LineScale(l, lines.Length, settled));
            }

            //Laid out into block space first, then rebased onto the block's centre once its height is known.
            List<Placement> raw = new();
            float inkTop = 0f;
            float tallestLine = 1f;

            for (int l = 0; l < lines.Length; l++)
            {
                float scale = LineScale(l, lines.Length, settled);
                tallestLine = MathF.Max(tallestLine, scale);

                float lineInkWidth = (lineWidth[l] + 2f * TUBE_RADIUS) * scale;

                //Where this line's right ink edge sits: against the block when the composition is
                //right-aligned, or half its slack in from it when the line is centred.
                float right = settled ? 0f : -(widestInk - lineInkWidth) * 0.5f;

                float pen = right - (lineWidth[l] + TUBE_RADIUS) * scale;
                float baseline = inkTop - (LetterShapes.CAP_HEIGHT + TUBE_RADIUS) * scale;

                foreach (char c in lines[l])
                {
                    float advance = LetterShapes.Advance(c);

                    //A space moves the pen and is not a letter. No word carries one to-day — the title is split
                    //on them — but the alphabet supports it, and a title that gained one should lay out.
                    if (c != ' ')
                        raw.Add(new Placement(
                            pen + advance * 0.5f * scale,
                            baseline + LetterShapes.CAP_HEIGHT * 0.5f * scale, scale, 0f));

                    pen += (advance + tracking) * scale;
                }

                inkTop -= (LetterShapes.CAP_HEIGHT + 2f * TUBE_RADIUS) * scale;

                //The gap to the next line. The menu's is one figure scaled by the taller of the two lines it
                //separates; the picture's are its own two measured gaps, in word-line cap heights, and the
                //last is the badge's rim rather than daylight (see LOGO_BADGE_GAP).
                if (l < lines.Length - 1)
                    inkTop -= settled
                        ? LINE_GAP * MathF.Max(scale, LineScale(l + 1, lines.Length, settled))
                        : l == lines.Length - 2 ? LOGO_BADGE_GAP : LOGO_LINE_GAP;
            }

            //The picture's badge disc runs on below its glyphs, and the box carries that depth so its centre is
            //the picture's centre — see LOGO_DISC_MARGIN.
            if (!settled) inkTop -= LOGO_DISC_MARGIN;

            float width = widestInk;
            float height = -inkTop + 2f * WAVE_DEPTH * tallestLine;

            //THE DEPTH BOW'S PHASE IS THE LETTER'S PLACE ACROSS THE BLOCK, not its place in the reading order,
            //and that is the difference between one surface bulging towards the lens and every line bulging on
            //its own — which reads as as many objects as there are lines. Solved here because it needs the
            //block's width, which is not known until every line has been measured.
            placements = new Placement[raw.Count];
            for (int i = 0; i < raw.Count; i++)
            {
                Placement p = raw[i];
                float across = width > 1e-4f ? 1f + 2f * p.X / width : 0f;

                placements[i] = new Placement(p.X + width * 0.5f, p.Y + height * 0.5f, p.Scale,
                    MathHelper.Clamp(across, -1f, 1f));
            }

            return settled
                ? new Composition(width, height, BLOCK_WIDTH_FRACTION, BLOCK_HEIGHT_FRACTION, 1f, 1f, YAW_CENTRE)
                : new Composition(width, height, OPEN_WIDTH_FRACTION, OPEN_HEIGHT_FRACTION, 0f, 0f, 0f);
        }

        /// <summary>
        /// The scale of one line: level everywhere except the last, which the settled composition blows up
        /// into the menu's badge and the opening one shrinks to the picture's small "3D" in its disc. A
        /// one-word title has no badge line in either.
        /// </summary>
        private static float LineScale(int line, int lines, bool settled) =>
            lines > 1 && line == lines - 1 ? (settled ? BADGE_SCALE : LOGO_BADGE_SCALE) : 1f;

        /// <summary>Keeps only the characters this alphabet can set, so an unsettable title degrades rather than throws.</summary>
        private static string Drawable(string word)
        {
            StringBuilder kept = new(word.Length);
            foreach (char c in word) if (LetterShapes.Supports(c)) kept.Append(c);

            return kept.ToString();
        }

        /// <summary>
        /// The mesh pair and renderer pair for one character, made on first use so a letter appearing three
        /// times is one mesh. Both meshes come off the same skeleton at two radii, which is what makes the
        /// keyline follow the letter instead of being a second drawing of it.
        /// </summary>
        private int GlyphIndex(GraphicsDevice device, Effect effect, char c)
        {
            if (_glyphIndex.TryGetValue(c, out int existing)) return existing;

            int index = _bodyMeshes.Count;
            _glyphIndex[c] = index;

            LetterMesh body = new(device, c, TUBE_RADIUS, TUBE_SIDES);
            LetterMesh outline = new(device, c, TUBE_RADIUS + OUTLINE_WIDTH, TUBE_SIDES);
            LetterMesh aura = new(device, c, TUBE_RADIUS + AURA_WIDTH, TUBE_SIDES);

            _bodyMeshes.Add(body);
            _outlineMeshes.Add(outline);
            _auraMeshes.Add(aura);

            _bodyRenderers.Add(new InstancedModelRenderer(device, body, BODY_MATERIAL, effect)
            {
                //The sky reflected off a glossy letter, at a little under full strength: at full the Fresnel
                //rim on every tube's silhouette washes the hue out at exactly the place the eye reads the
                //letter's shape from.
                SpecularAmbientStrength = 0.55f,
                LinearLightRig = true
            });

            _outlineRenderers.Add(new InstancedModelRenderer(device, outline, OUTLINE_MATERIAL, effect)
            {
                //NO sky reflection on the keyline, and this is not a taste: the keyline is drawn with FRONT
                //faces culled (see Draw), so the normal of every pixel of it points AWAY from the lens, and
                //the shader's Fresnel term reads that as a grazing angle and returns full reflectance. At any
                //strength above zero the ring that is supposed to be near-black would mirror the whole sky and
                //come out as the brightest thing in the frame.
                SpecularAmbientStrength = 0f,
                LinearLightRig = true
            });

            _auraRenderers.Add(new InstancedModelRenderer(device, aura, Vector3.Zero, effect)
            {
                //Nothing lit reaches the aura: its whole colour arrives as EmissiveTint, for the keyline's
                //reason and one more of its own — it is drawn ADDITIVELY, so every term the light rig could
                //give it would be light added to the frame on top of the light it is already adding.
                SpecularAmbientStrength = 0f,
                LinearLightRig = true
            });

            return index;
        }

        /// <summary>
        /// Draws the wordmark, anchored to the frame. Called from the front end's own screen while either the
        /// title card or the main menu is the page on top, so it is on screen exactly there and nowhere else —
        /// no page has to opt in and no page added later can forget to opt out.
        /// </summary>
        /// <param name="wallClock">
        /// The host's wall clock. A front-end effect has no session, so play time does not exist for it — and
        /// the drift, the wave and the beat all have to keep running while a settings page is open over the
        /// menu, which is the same argument the balls' heartbeat and the clouds' drift make.
        /// </param>
        /// <param name="settled">
        /// Which composition to move towards: <c>false</c> is the 2D logo's layout, the three lines centred in
        /// the middle of the frame where the splash drew the picture; <c>true</c> is the menu's, one word to a
        /// line in the corner with the last blown up. The caller states the <i>target</i> and never the
        /// progress — the move itself is this class's, so a page cannot leave the title half way across the
        /// frame. The title starts settled, so <c>false</c> only means anything after <see cref="BeginHandover"/>.
        /// </param>
        /// <param name="presence">
        /// How present the title is, 1 fully and 0 not at all — the front end's fly-in passes its closeness
        /// eased down to a floor, so the word shrinks to a small corner mark in among the balls and comes
        /// back up as the lens leaves (#261; the floor rather than zero is the owner's ruling — the title
        /// stays, small). What scales is the block's SIZE, the reveal's own idiom, because the letters are
        /// opaque geometry with no alpha to fade; at or below <see cref="MIN_PRESENCE"/> nothing is drawn at
        /// all. The caller supplies the easing — this class holds no clock of the flight's.
        /// </param>
        /// <param name="stillness">
        /// How much of the block's own idle motion to hold back, 1 fully and 0 not at all (#475). Nothing
        /// asked for this while the wordmark only ever stood alone; it exists because the splash's hand-over
        /// stands these letters under a flat, motionless PICTURE for the width of the cross-fade, and every
        /// term below that moves on the wall clock — the block's yaw and pitch sway, the per-letter wave and
        /// turn, the beat's scale pulse — kept moving under it regardless. A static picture and a swaying,
        /// waving object never read as the same thing, whatever their silhouettes agree on; the reveal's own
        /// swell was already cut for exactly this reason (see <see cref="REVEAL_FROM"/>), and this finishes
        /// the job for the motion nothing had touched. The caller passes the picture's own fading opacity —
        /// full while it still covers the letters, easing to nothing as it goes — so the letters stand as
        /// still as the thing they are replacing for as long as that thing is still up, and wake into their
        /// ordinary drift only once they are the only object left. <see cref="BOW_DEPTH"/> is untouched: it is
        /// a fixed shape, not a motion, and does not read as the letters moving at all.
        /// </param>
        /// <remarks>
        /// <b>The draw states are stated here and put back</b>, which is the contract <c>ArenaIsland</c>'s
        /// slices keep: the caller's next act is the frame's translucent glass, and it is
        /// entitled to find the states <c>BeginSceneDraw</c> left for the scene. Nothing is inherited either —
        /// what ran last before this is the ball draw, and what a frame starts with depends on which pass
        /// finished the one before it.
        /// <para>
        /// <b>The keyline is drawn with FRONT faces culled</b>, and the whole outline trick turns on that.
        /// Both tubes share an axis and the keyline's is the fatter, so its near surface is <i>nearer the lens
        /// than the letter's</i> and drawing it normally would hide the letter inside it. Culled the other way
        /// round, what is drawn is the keyline's FAR surface, which lies behind the letter — so the letter
        /// paints over it and the dark ring survives only where the letter does not cover it, which is exactly
        /// the ring. It is also why the keyline goes first: with the depth buffer on, either order works, but
        /// the far surface writing depth first means the letters never test against a surface nearer than
        /// themselves.
        /// </para>
        /// </remarks>
        public void Draw(ICamera camera, float wallClock, bool settled, float presence = 1f, float stillness = 0f)
        {
            if (_letters.Length == 0) return;

            //The step aside (#261), BEFORE the clocks: a skipped frame arrives later as one huge step, which
            //saturates the morph and the reveal — the behaviour a level played and returned from already has,
            //and the one a twenty-second pass deserves too. Eased by the caller; this class only scales.
            if (presence <= MIN_PRESENCE) return;

            //THE ONE FACTOR EVERY IDLE-MOTION TERM BELOW IS SCALED BY (#475) — see stillness's own remarks.
            //Not clamped: the caller's own value (the splash's LogoOpacity) is already a SmoothStep output and
            //never leaves 0..1, and a class that clamped its caller's contract quietly would hide the day that
            //contract breaks instead of showing a wrong picture that gets noticed.
            float motion = 1f - stillness;

            //THE MOVE AND THE ARRIVAL, both stepped off the wall clock rather than off an elapsed value handed
            //in, because this class is only ever reached from a draw: a frame that was not drawn is a frame in
            //which nothing here should have moved. The step is deliberately NOT clamped — a gap in the drawing
            //(a level played, then Main Menu) arrives as one huge step, which saturates both and is exactly
            //right, since the title belongs settled in its corner by then and its arrival is long over.
            float step = _lastClock < 0f ? 0f : MathF.Max(0f, wallClock - _lastClock);
            _lastClock = wallClock;

            _morph = MathHelper.Clamp(_morph + (settled ? step : -step) / MORPH_SECONDS, 0f, 1f);
            _reveal = MathF.Min(1f, _reveal + step / REVEAL_SECONDS);

            //Eased at both ends, so the title leaves the middle of the frame and arrives in its corner at rest
            //and only the middle of the move is quick.
            float morph = MathHelper.SmoothStep(0f, 1f, _morph);
            float reveal = MathHelper.SmoothStep(0f, 1f, _reveal);

            Composition block = Composition.Lerp(in _openBlock, in _settledBlock, morph);

            _device.BlendState = BlendState.Opaque;
            _device.DepthStencilState = DepthStencilState.Default;

            Matrix view = camera.View;
            Vector3 right = new(view.M11, view.M21, view.M31);
            Vector3 up = new(view.M12, view.M22, view.M32);
            Vector3 forward = -new Vector3(view.M13, view.M23, view.M33);

            Matrix projection = camera.Projection;

            //The frame's own half-extents at the wordmark's distance, out of the projection rather than out of
            //a viewport or a remembered aspect: at a distance d the half-height is d / M22 and the half-width
            //d / M11, whatever the field of view and whatever the window shape. TrophyPodium's trick, and the
            //front end needs it more than the cup does — this lens is 60 degrees where gameplay's is 43, and
            //they are the same camera object.
            float halfHeight = DISTANCE / projection.M22;
            float halfWidth = DISTANCE / projection.M11;

            //THE SIZE, solved from the frame and not stated: the cap height that makes the block fill its
            //share of the frame, taking whichever of the two limits binds. Height binds at every aspect
            //anyone plays at; the width limit is what stops a tall window pushing the word off both sides.
            //
            //SOLVED AT THE SWAY'S EXTREME, WHICH IS NOT THE SAME AS SOLVED FLAT, and the first version was not
            //and it showed: the sway and the bow between them carry a corner letter up to REACH cap heights
            //nearer the lens than the block's own plane, and a nearer letter projects BIGGER — so a block that
            //fitted its share of the frame at rest put its last letter on the frame's edge a few seconds later
            //(photographed on the meadow, "SHOOTER"'s R and the badge's D both). A perspective divide is
            //linear in the near distance, so the fit has a closed form rather than needing to be iterated:
            //w·cap·D / (D − REACH·cap) ≤ available gives cap ≤ available·D / (w·D + available·REACH).
            //The breath is divided out with it, for the same reason — it is another few per cent of size that
            //arrives after the solve.
            //
            //THE BOW IS DELIBERATELY NOT IN THE REACH, and leaving it in was costing a third of the margin for
            //nothing: what the fit has to survive is the letter nearest the frame's EDGE, and the bow is zero
            //there by construction (it is a parabola across the block, at its full depth in the middle and at
            //nothing at both ends). A bowed middle letter does project outwards a little, but it starts well
            //inside the frame and 0.45 of a cap height out of seven units moves it by six per cent of the way
            //it still has to go.
            //Every term of it off the INTERPOLATED composition, so the fit is honest at every point of the
            //move and not only at its two ends. The interpolated box is a sound bound on the interpolated
            //letters, too: lerping the corners of two boxes contains the lerp of anything inside them.
            float reach = 0.5f * block.Width * MathF.Sin(MathF.Abs(block.Yaw) + YAW_SWAY)
                + 0.5f * block.Height * MathF.Sin(PITCH_ANGLE)
                + 0.5f * _widestLetter * MathF.Sin(LETTER_YAW);

            float availableHeight = block.HeightFraction * 2f * halfHeight / (1f + SCALE_BEAT);
            float availableWidth = block.WidthFraction * 2f * halfWidth / (1f + SCALE_BEAT);

            float cap = MathF.Min(
                availableHeight * DISTANCE / (block.Height * DISTANCE + availableHeight * reach),
                availableWidth * DISTANCE / (block.Width * DISTANCE + availableWidth * reach));

            //THE INSET, one figure for both edges and both of them in world units off the frame's own extent —
            //the front end's own rule for its two corners ("so the name's distance from its edges and the
            //column's from its own are the same measurement rather than two that drifted apart").
            float inset = _insetFraction * 2f * halfHeight;

            //THE BEAT, and the size breathes with it before anything is placed, so the whole block grows and
            //shrinks about its anchored corner rather than about its middle.
            float beat = 0.5f + 0.5f * MathF.Sin(wallClock * BEAT_RATE * MathHelper.TwoPi);

            //THE STEP ASIDE rides the same line: the anchor below and every placement are in cap heights, so
            //scaling the cap shrinks the whole block about its anchored corner — letters, keylines and auras
            //together — with no second scale to keep in step. The reveal is the same trick arriving, and for
            //the same reason: opaque tubes cannot fade, they can only be small.
            //
            //THE BEAT'S OWN SWING IS WHAT STILLNESS HOLDS BACK HERE (#475) — the reveal's swell is not: that
            //growth IS the letters arriving, the one motion the picture's own thinning is supposed to read
            //as, where the beat is idle breathing that has nothing to do with the hand-over at all.
            cap *= (1f + motion * SCALE_BEAT * (beat * 2f - 1f)) * MathHelper.Lerp(REVEAL_FROM, 1f, reveal) * presence;

            //THE ANCHOR, and it carries the same perspective term the fit above does — for the same reason and
            //with the same arithmetic. The corner has to land ON the inset when it is at its NEAREST, so the
            //frame's usable half-extent is taken at that depth rather than at the block's own plane: the
            //corner's screen place is (centre + half the block) scaled by M11/(D − z), so putting the centre at
            //(halfExtent − inset)·(D − z)/D − half the block makes that come out at exactly the inset. At rest
            //the block therefore sits a little further in than the inset, which is the margin the sway spends.
            float shrink = (DISTANCE - reach * cap) / DISTANCE;

            //EdgeX and EdgeY are what carry the block from the middle of the frame to its corner: at 0 the
            //offset is nothing and the block is centred, at 1 it is the whole anchored corner, and the move
            //between the two compositions is that pair being lerped like everything else.
            Vector3 blockCentre = camera.Position
                + forward * DISTANCE
                + right * (block.EdgeX * ((halfWidth - inset) * shrink - block.Width * cap * 0.5f))
                + up * (block.EdgeY * ((halfHeight - inset) * shrink - block.Height * cap * 0.5f));

            //Block space to world: x right, y up, z towards the lens — then the block's own two sways, applied
            //BEFORE the basis so they turn the word about its own axes rather than about the world's. Both
            //sways are stillness's to hold back (#475): block.Yaw itself is untouched, so the open composition
            //(whose own Yaw is 0) still turns dead level under a picture that has no perspective of its own.
            Matrix blockToWorld =
                Matrix.CreateRotationY(block.Yaw + motion * YAW_SWAY * MathF.Sin(wallClock * YAW_RATE))
                * Matrix.CreateRotationX(motion * PITCH_ANGLE * MathF.Sin(wallClock * PITCH_RATE))
                * new Matrix(
                    right.X, right.Y, right.Z, 0f,
                    up.X, up.Y, up.Z, 0f,
                    -forward.X, -forward.Y, -forward.Z, 0f,
                    blockCentre.X, blockCentre.Y, blockCentre.Z, 1f);

            //=== Every letter's matrix, once, then both passes read it ===
            Vector4 fullyOpen = new(0f, 0f, 0f, 1f);   //no occluder, no ambient occlusion: nothing shades a title

            for (int i = 0; i < _letters.Length; i++)
                _letterWorld[i] = LetterWorld(in _letters[i], Placement.Lerp(in _open[i], in _settled[i], morph),
                    cap, wallClock, motion, in blockToWorld);

            //=== The letters first, one draw each, because the colour is a per-draw uniform ===
            //
            //⚠⚠ THE ORDER OF THE THREE SHELLS IS LOAD-BEARING, AND IT IS THIS: letter, glow, keyline. The
            //letter goes first because it is the only one that WRITES depth, and everything after it is then
            //rejected by early-Z wherever the letter already covers — which is what keeps the two unlit shells
            //to the few pixels of them that show (see the aura pass below for the 1 ms that is worth).
            //
            //THE KEYLINE GOES LAST AND OVER THE GLOW, WHICH IS WHAT REMOVED A ONE-PIXEL SEAM. While the keyline
            //was drawn before the glow and wrote depth, the glow's inner edge was defined BY the keyline's
            //silhouette — two shells having to meet exactly along a curve, which they cannot: magnifying the
            //badge's edge to single pixels showed a thread of BACKDROP between them, grass at (120,225,100)
            //where the keyline's purple ended and the glow's cyan began, belonging to neither. Drawn this way
            //round the glow runs continuously from the letter's own edge outwards and the keyline is painted on
            //top of it, so there is no boundary two rasterisations have to agree about. Neither unlit shell
            //writes depth, for the same reason and for one more: their bands overlap wherever the word is
            //tight, and one of them writing depth would punch a hole in the other.
            _device.RasterizerState = RasterizerState.CullCounterClockwise;

            //The beat, plus the arrival's own flare — which is where the reveal's overshoot lives, scale having
            //no room for one (see REVEAL_FLARE). It decays as the reveal completes, so it is a landing and not
            //a second rhythm.
            float glowLevel = MathHelper.Lerp(GLOW_REST, GLOW_PEAK, beat)
                + REVEAL_FLARE * reveal * (1f - reveal) * 4f;

            for (int i = 0; i < _letters.Length; i++)
            {
                Vector3 hue = Hue(_letters[i].Phase + wallClock * HUE_FLOW);

                InstancedModelRenderer renderer = _bodyRenderers[_letters[i].Glyph];
                renderer.EmissiveTint = Glow(hue, glowLevel);

                _oneInstance[0] = new ModelInstance(_letterWorld[i], fullyOpen);
                renderer.Draw(camera, _oneInstance, 1, _bodyParams, hue);
            }

            //=== Then the ghosts: an additive band of the letter's own colour, running from the letter's own
            //edge outwards, which the glare pass turns into the halo. FRONT faces culled so what is drawn is
            //the shell's FAR surface, behind the letter, and only what lies outside the letter survives. ===
            //
            //⚠ IT WAS DRAWN FIRST OF THE THREE, AND THAT COST 2.6 ms OF A 26 ms FRAME — measured, on the pinned
            //rig, as the only version of this whose cost came out ABOVE the machine's noise. Drawn first there
            //is nothing in the depth buffer yet, so every pixel of every shell is shaded and then thrown away
            //by the letters painted over it; drawn after them, early-Z rejects all of that before the shader
            //runs and only the visible band is paid for. The band is a fraction of the shell, and the shader it
            //pays for is the frame's full lit material — cloud shadow, hemisphere ambient, Fresnel, sky
            //radiance — every term of which is multiplied by a black diffuse and thrown away. The picture is
            //identical either way: additive blending is order-independent, and the only pixels that differ are
            //the ones the depth test removes in both orders.
            _device.BlendState = BlendState.Additive;
            _device.DepthStencilState = DepthStencilState.DepthRead;
            _device.RasterizerState = RasterizerState.CullClockwise;

            float auraLevel = MathHelper.Lerp(AURA_REST, AURA_PEAK, beat) * reveal;

            for (int i = 0; i < _letters.Length; i++)
            {
                InstancedModelRenderer renderer = _auraRenderers[_letters[i].Glyph];
                renderer.EmissiveTint = Glow(Hue(_letters[i].Phase + wallClock * HUE_FLOW), auraLevel);

                _oneInstance[0] = new ModelInstance(_letterWorld[i], fullyOpen);
                renderer.Draw(camera, _oneInstance, 1, _auraParams);
            }

            //=== And the keyline over it, one draw a letter, each rim its own colour off the same wheel ===
            _device.BlendState = BlendState.Opaque;

            for (int i = 0; i < _letters.Length; i++)
            {
                InstancedModelRenderer renderer = _outlineRenderers[_letters[i].Glyph];

                //Through EmissiveTint and in LINEAR radiance, for the reason on OUTLINE_MATERIAL: this pass
                //has no usable normals, so its colour cannot come from the light.
                renderer.EmissiveTint = ColorSpace.SrgbToLinear(
                    Hue(_letters[i].Phase + wallClock * HUE_FLOW + OUTLINE_HUE_SHIFT) * OUTLINE_VALUE);

                _oneInstance[0] = new ModelInstance(_letterWorld[i], fullyOpen);
                renderer.Draw(camera, _oneInstance, 1, _outlineParams);
            }

            //Put back what BeginSceneDraw stated for the scene, so the glass that follows finds the frame as
            //it left it.
            _device.BlendState = BlendState.AlphaBlend;
            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// The splash handing the 2D logo over (#454): from the next draw the title stands in the <b>opening</b>
        /// composition — the picture's own layout, fitted to the rectangle the picture was drawn in — and its
        /// arrival swell starts over, so the letters inflate into place under the bitmap as the splash thins
        /// it. The move to the corner then begins when a caller first asks for <c>settled: true</c>, which is
        /// the main menu arriving.
        /// </summary>
        /// <param name="logoWidthFraction">
        /// The drawn bitmap's width as a fraction of the frame's — the whole file's, margins and all; the
        /// ink's share of it is this class's own figure (<see cref="LOGO_INK_WIDTH_SHARE"/>).
        /// </param>
        /// <param name="logoHeightFraction">Its height, likewise.</param>
        /// <remarks>
        /// The picture is placed in <i>pixels</i> (one source pixel to one display pixel across the owner's
        /// resolutions, scaled below them), so its share of the frame is a fact of this launch rather than a
        /// constant, and it is restated here rather than assumed. The open composition's other figures — the
        /// lines, the gaps, the small badge — were measured off the bitmap once and do not move.
        /// <para>
        /// The clock is reset with it: the first draw after this call steps by nothing, so the swell starts at
        /// exactly <see cref="REVEAL_FROM"/> on the frame the fade begins rather than a frame's worth in.
        /// </para>
        /// </remarks>
        public void BeginHandover(float logoWidthFraction, float logoHeightFraction)
        {
            _openBlock = new Composition(_openBlock.Width, _openBlock.Height,
                logoWidthFraction * LOGO_INK_WIDTH_SHARE, logoHeightFraction * LOGO_INK_HEIGHT_SHARE,
                0f, 0f, 0f);

            _morph = 0f;
            _reveal = 0f;
            _lastClock = -1f;
        }

        /// <summary>
        /// One letter's world matrix. It is centred on its own middle before anything turns it, or the letter
        /// would swing about its bottom-left corner like a flag on a pole rather than turning on the spot.
        /// </summary>
        /// <param name="motion">
        /// Stillness's complement (#475), scaling the wave and the letter's own turn the same way <c>Draw</c>
        /// scales the block's sway — the per-letter motion is the one most likely to read as swimming under a
        /// static picture, since it moves each letter off the line the picture drew it on individually rather
        /// than turning the whole block as one rigid thing.
        /// </param>
        private static Matrix LetterWorld(in Letter letter, Placement at, float cap, float wallClock, float motion,
            in Matrix blockToWorld)
        {
            //Exactly one cycle of the wave across the whole wordmark - see WAVE_DEPTH.
            float wavePhase = (letter.Phase - wallClock * WAVE_RATE) * MathHelper.TwoPi;
            float wave = MathF.Sin(wavePhase);

            //Bowed towards the lens by how far across the BLOCK it stands - see Placement.Across. Not scaled
            //by motion: a fixed shape, not a motion - see stillness's own remarks on Draw.
            float z = BOW_DEPTH * (1f - at.Across * at.Across);

            return
                Matrix.CreateTranslation(-letter.Advance * 0.5f, -LetterShapes.CAP_HEIGHT * 0.5f, 0f)
                * Matrix.CreateScale(cap * at.Scale)
                * Matrix.CreateRotationY(motion * LETTER_YAW * MathF.Cos(wavePhase))
                * Matrix.CreateTranslation(at.X * cap, (at.Y + motion * wave * WAVE_DEPTH * at.Scale) * cap, z * cap)
                * blockToWorld;
        }

        /// <summary>
        /// One stop of the ramp as an <b>sRGB</b> colour, which is the space <c>diffuseTint</c> is read in —
        /// the shader decodes it (<c>SrgbToLinear(DiffuseColor.rgb)</c>), so handing it linear radiance would
        /// be a double decode and every letter would come out darker than it was authored.
        /// <para>
        /// A plain HSV wheel at full saturation, lerped a fifth of the way to white: see
        /// <see cref="WHITEN"/> for why the blue side of the wheel cannot be left where it is.
        /// </para>
        /// </summary>
        private static Vector3 Hue(float turns)
        {
            float h = turns - MathF.Floor(turns);
            float sector = h * 6f;
            int i = (int)sector % 6;
            float f = sector - MathF.Floor(sector);

            float p = 1f - SATURATION;
            float q = 1f - SATURATION * f;
            float t = 1f - SATURATION * (1f - f);

            Vector3 pure = i switch
            {
                0 => new Vector3(1f, t, p),
                1 => new Vector3(q, 1f, p),
                2 => new Vector3(p, 1f, t),
                3 => new Vector3(p, q, 1f),
                4 => new Vector3(t, p, 1f),
                _ => new Vector3(1f, p, q),
            };

            return Vector3.Lerp(pure, Vector3.One, WHITEN);
        }

        /// <summary>
        /// The glow for one letter: its own hue, decoded to <b>linear radiance</b> (which is the space
        /// <c>EmissiveTint</c> is added in — it is the one term on this material the shader neither decodes nor
        /// premultiplies), then scaled so its Rec. 709 luminance is exactly <paramref name="level"/>.
        /// <para>
        /// The normalisation is the point of this function. Without it one radiance means six different
        /// brightnesses round the wheel — Rec. 709 weights green at 0.72 and blue at 0.07 — so the word would
        /// cross the glare threshold in six places at six different times and read as flickering rather than
        /// as breathing.
        /// </para>
        /// </summary>
        private static Vector3 Glow(Vector3 srgbHue, float level)
        {
            Vector3 linear = ColorSpace.SrgbToLinear(srgbHue);

            return linear * (level / MathF.Max(ColorSpace.Luminance(linear), 1e-3f));
        }

        public void Dispose()
        {
            foreach (InstancedModelRenderer renderer in _bodyRenderers) renderer?.Dispose();
            foreach (InstancedModelRenderer renderer in _outlineRenderers) renderer?.Dispose();
            foreach (InstancedModelRenderer renderer in _auraRenderers) renderer?.Dispose();
            foreach (LetterMesh mesh in _bodyMeshes) mesh?.Dispose();
            foreach (LetterMesh mesh in _outlineMeshes) mesh?.Dispose();
            foreach (LetterMesh mesh in _auraMeshes) mesh?.Dispose();
        }
    }
}
