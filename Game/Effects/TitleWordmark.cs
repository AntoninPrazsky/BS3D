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

        //Facets around each tube. The body is on show and its specular streak runs along it; the keyline is one
        //flat unlit tone whose only job is a silhouette, so it is swept coarser and nobody can tell.
        private const int BODY_SIDES = 16, OUTLINE_SIDES = 10;

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
        //wide-angle stretch a closer hang would give it. It also keeps the whole block outside the island
        //(radius 26) at the front end's orbit radius of 44, so nothing can ever intersect it.
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
        private const float YAW_CENTRE = -0.20f, YAW_SWAY = 0.07f, YAW_RATE = 0.34f;
        private const float PITCH_ANGLE = 0.055f, PITCH_RATE = 0.23f;

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

        //The pulse has TWO limbs on purpose, because either alone fails on half the game's thirteen scenes.
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
        //of every sweep" arriving through brightness instead of through hue. 0.22 holds the word plainly lit
        //against a black sky at the bottom of the beat and still leaves a swing of nearly three to one.
        //
        //AND THE CREST IS NOT PUSHED HIGHER THAN IT NEEDS TO BE, because the emissive term is FLAT: it is
        //added per pixel without regard to the normal, so every point of the glow is brightness the shading
        //gradient does not get to vary. That gradient is what makes a tube read as round, so a glow big enough
        //to swamp it turns the letters back into the flat stickers this whole approach exists to avoid. The
        //crest is as bright as it is only because it is momentary.
        private const float GLOW_REST = 0.22f, GLOW_PEAK = 0.60f;

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
        //the eighteen domes. The diffuse is therefore held at black and the specular stated small (rather than
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
        private readonly List<InstancedModelRenderer> _bodyRenderers = new();
        private readonly List<InstancedModelRenderer> _outlineRenderers = new();

        //Every letter of the title that is actually drawn, in reading order, with its place in the block
        //solved once at construction. Spaces are not slots: they moved the pen and that is all they do.
        private readonly Slot[] _slots;

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

        private readonly BasicEffectParams _bodyParams, _outlineParams;

        //The block's own size in cap heights, solved once. Both are the SWEPT box rather than the resting ink:
        //the width is the widest line's ink, the height the whole stack's ink plus the reach of the wave that
        //carries every letter above and below its slot. So a letter at the top of the wave is inside the box
        //rather than outside it, which is what the fit and the anchor below are solved against.
        private readonly float _blockWidth, _blockHeight;

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
        /// wants one controlled finish; a wordmark stands over all thirteen backdrops under all eighteen
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
            }
        }

        /// <summary>One drawn letter's place in the block, in cap heights, solved once at construction.</summary>
        private readonly struct Slot
        {
            public readonly int Glyph;          //index into the mesh/renderer lists
            public readonly float X, Baseline;  //the letter's own origin in block space (x left-negative, y down-negative)
            public readonly float Scale;        //its line's scale (1, or BADGE_SCALE on the last line)
            public readonly float Advance;      //the glyph's advance, kept so the draw can centre it
            public readonly float Phase;        //0..1 along the whole wordmark in reading order: the wave and the hue
            public readonly float Across;       //-1..+1 across the block's WIDTH: the depth bow

            public Slot(int glyph, float x, float baseline, float scale, float advance, float phase, float across)
            {
                Glyph = glyph;
                X = x;
                Baseline = baseline;
                Scale = scale;
                Advance = advance;
                Phase = phase;
                Across = across;
            }
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

            //=== The block, laid out once in cap heights ===
            //Block space: x runs LEFT from 0 (the right-hand ink edge of the widest line) and y runs DOWN
            //from 0 (the top ink edge of the first line), both negative. Right-aligned and top-anchored,
            //which is the corner the composition hangs from.
            float[] wordWidth = new float[words.Length];
            float widestInk = 0f;
            for (int w = 0; w < words.Length; w++)
            {
                wordWidth[w] = LetterShapes.WordWidth(words[w], tracking);

                float scale = Scale(w, words.Length);
                widestInk = MathF.Max(widestInk, (wordWidth[w] + 2f * TUBE_RADIUS) * scale);
            }

            List<Slot> slots = new();
            float inkTop = 0f;
            int totalLetters = 0;
            foreach (string word in words) totalLetters += word.Length;

            int letterOrdinal = 0;
            for (int w = 0; w < words.Length; w++)
            {
                float scale = Scale(w, words.Length);
                float lineInkHeight = (LetterShapes.CAP_HEIGHT + 2f * TUBE_RADIUS) * scale;

                //The line's own right ink edge sits at 0; its first letter's skeleton origin is its whole ink
                //width to the left of that, less the tube radius the ink stands outside the skeleton by.
                float pen = -(wordWidth[w] + TUBE_RADIUS) * scale;
                float baseline = inkTop - (LetterShapes.CAP_HEIGHT + TUBE_RADIUS) * scale;

                //Nothing here is a space: the split above removed them, and a word is one line.
                foreach (char c in words[w])
                {
                    float advance = LetterShapes.Advance(c);

                    //One phase for the wave and the hue both, so the colour and the motion travel together
                    //as one thing through the word rather than as two. Across is filled in below, once the
                    //block's own width is known.
                    float phase = totalLetters > 1 ? letterOrdinal / (float)totalLetters : 0f;
                    slots.Add(new Slot(GlyphIndex(device, instancingEffect, c), pen, baseline, scale, advance, phase, 0f));
                    letterOrdinal++;

                    pen += (advance + tracking) * scale;
                }

                inkTop -= lineInkHeight;
                if (w < words.Length - 1)
                    inkTop -= LINE_GAP * MathF.Max(scale, Scale(w + 1, words.Length));
            }

            _blockWidth = widestInk;
            _blockHeight = -inkTop + 2f * WAVE_DEPTH * Scale(words.Length - 1, words.Length);

            foreach (Slot slot in slots) _widestLetter = MathF.Max(_widestLetter, slot.Advance * slot.Scale);

            //THE DEPTH BOW'S PHASE IS THE LETTER'S PLACE ACROSS THE BLOCK, not its place in the reading order,
            //and that is the difference between one surface bulging towards the lens and three lines each
            //bulging on their own — which reads as three objects. Solved here because it needs the block's
            //width, which is not known until every line has been measured.
            _slots = new Slot[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                float centreX = slot.X + slot.Advance * 0.5f * slot.Scale;
                float across = _blockWidth > 1e-4f ? 1f + 2f * centreX / _blockWidth : 0f;

                _slots[i] = new Slot(slot.Glyph, slot.X, slot.Baseline, slot.Scale, slot.Advance, slot.Phase,
                    MathHelper.Clamp(across, -1f, 1f));
            }

            _letterWorld = new Matrix[_slots.Length];

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
        }

        /// <summary>The scale of one line: every word at 1 but the last, which is the badge.</summary>
        private static float Scale(int word, int words) => word == words - 1 && words > 1 ? BADGE_SCALE : 1f;

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

            LetterMesh body = new(device, c, TUBE_RADIUS, BODY_SIDES);
            LetterMesh outline = new(device, c, TUBE_RADIUS + OUTLINE_WIDTH, OUTLINE_SIDES);

            _bodyMeshes.Add(body);
            _outlineMeshes.Add(outline);

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

            return index;
        }

        /// <summary>
        /// Draws the wordmark, anchored to the frame. Called from the front end's own screen while the main
        /// menu is the page on top, so it is on screen exactly there and nowhere else — no page has to opt in
        /// and no page added later can forget to opt out.
        /// </summary>
        /// <param name="wallClock">
        /// The host's wall clock. A front-end effect has no session, so play time does not exist for it — and
        /// the drift, the wave and the beat all have to keep running while a settings page is open over the
        /// menu, which is the same argument the balls' heartbeat and the clouds' drift make.
        /// </param>
        /// <remarks>
        /// <b>The draw states are stated here and put back</b>, which is the contract <c>ArenaIsland</c>'s
        /// slices and <c>BallGlow</c> keep: the caller's next act is the frame's translucent glass, and it is
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
        public void Draw(ICamera camera, float wallClock)
        {
            if (_slots.Length == 0) return;

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
            float reach = 0.5f * _blockWidth * MathF.Sin(MathF.Abs(YAW_CENTRE) + YAW_SWAY)
                + 0.5f * _blockHeight * MathF.Sin(PITCH_ANGLE)
                + 0.5f * _widestLetter * MathF.Sin(LETTER_YAW);

            float availableHeight = BLOCK_HEIGHT_FRACTION * 2f * halfHeight / (1f + SCALE_BEAT);
            float availableWidth = BLOCK_WIDTH_FRACTION * 2f * halfWidth / (1f + SCALE_BEAT);

            float cap = MathF.Min(
                availableHeight * DISTANCE / (_blockHeight * DISTANCE + availableHeight * reach),
                availableWidth * DISTANCE / (_blockWidth * DISTANCE + availableWidth * reach));

            //THE INSET, one figure for both edges and both of them in world units off the frame's own extent —
            //the front end's own rule for its two corners ("so the name's distance from its edges and the
            //column's from its own are the same measurement rather than two that drifted apart").
            float inset = _insetFraction * 2f * halfHeight;

            //THE BEAT, and the size breathes with it before anything is placed, so the whole block grows and
            //shrinks about its anchored corner rather than about its middle.
            float beat = 0.5f + 0.5f * MathF.Sin(wallClock * BEAT_RATE * MathHelper.TwoPi);

            cap *= 1f + SCALE_BEAT * (beat * 2f - 1f);

            //THE ANCHOR, and it carries the same perspective term the fit above does — for the same reason and
            //with the same arithmetic. The corner has to land ON the inset when it is at its NEAREST, so the
            //frame's usable half-extent is taken at that depth rather than at the block's own plane: the
            //corner's screen place is (centre + half the block) scaled by M11/(D − z), so putting the centre at
            //(halfExtent − inset)·(D − z)/D − half the block makes that come out at exactly the inset. At rest
            //the block therefore sits a little further in than the inset, which is the margin the sway spends.
            float shrink = (DISTANCE - reach * cap) / DISTANCE;

            Vector3 blockCentre = camera.Position
                + forward * DISTANCE
                + right * ((halfWidth - inset) * shrink - _blockWidth * cap * 0.5f)
                + up * ((halfHeight - inset) * shrink - _blockHeight * cap * 0.5f);

            //Block space to world: x right, y up, z towards the lens — then the block's own two sways, applied
            //BEFORE the basis so they turn the word about its own axes rather than about the world's.
            Matrix blockToWorld =
                Matrix.CreateRotationY(YAW_CENTRE + YAW_SWAY * MathF.Sin(wallClock * YAW_RATE))
                * Matrix.CreateRotationX(PITCH_ANGLE * MathF.Sin(wallClock * PITCH_RATE))
                * new Matrix(
                    right.X, right.Y, right.Z, 0f,
                    up.X, up.Y, up.Z, 0f,
                    -forward.X, -forward.Y, -forward.Z, 0f,
                    blockCentre.X, blockCentre.Y, blockCentre.Z, 1f);

            //=== Every letter's matrix, once, then both passes read it ===
            Vector4 fullyOpen = new(0f, 0f, 0f, 1f);   //no occluder, no ambient occlusion: nothing shades a title

            for (int i = 0; i < _slots.Length; i++)
                _letterWorld[i] = LetterWorld(in _slots[i], cap, wallClock, in blockToWorld);

            //=== The keyline, one draw a letter, each rim its own colour off the same wheel ===
            _device.RasterizerState = RasterizerState.CullClockwise;

            for (int i = 0; i < _slots.Length; i++)
            {
                InstancedModelRenderer renderer = _outlineRenderers[_slots[i].Glyph];

                //Through EmissiveTint and in LINEAR radiance, for the reason on OUTLINE_MATERIAL: this pass
                //has no usable normals, so its colour cannot come from the light.
                renderer.EmissiveTint = ColorSpace.SrgbToLinear(
                    Hue(_slots[i].Phase + wallClock * HUE_FLOW + OUTLINE_HUE_SHIFT) * OUTLINE_VALUE);

                _oneInstance[0] = new ModelInstance(_letterWorld[i], fullyOpen);
                renderer.Draw(camera, _oneInstance, 1, _outlineParams);
            }

            //=== The letters, one draw each, because the colour is a per-draw uniform ===
            _device.RasterizerState = RasterizerState.CullCounterClockwise;

            float glowLevel = MathHelper.Lerp(GLOW_REST, GLOW_PEAK, beat);

            for (int i = 0; i < _slots.Length; i++)
            {
                Vector3 hue = Hue(_slots[i].Phase + wallClock * HUE_FLOW);

                InstancedModelRenderer renderer = _bodyRenderers[_slots[i].Glyph];
                renderer.EmissiveTint = Glow(hue, glowLevel);

                _oneInstance[0] = new ModelInstance(_letterWorld[i], fullyOpen);
                renderer.Draw(camera, _oneInstance, 1, _bodyParams, hue);
            }

            //Put back what BeginSceneDraw stated for the scene, so the glass that follows finds the frame as
            //it left it.
            _device.BlendState = BlendState.AlphaBlend;
            _device.DepthStencilState = DepthStencilState.Default;
            _device.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <summary>
        /// One letter's world matrix. It is centred on its own middle before anything turns it, or the letter
        /// would swing about its bottom-left corner like a flag on a pole rather than turning on the spot.
        /// </summary>
        private Matrix LetterWorld(in Slot slot, float cap, float wallClock, in Matrix blockToWorld)
        {
            //Exactly one cycle of the wave across the whole wordmark - see WAVE_DEPTH.
            float wavePhase = (slot.Phase - wallClock * WAVE_RATE) * MathHelper.TwoPi;
            float wave = MathF.Sin(wavePhase);

            //Where the letter's own centre sits in the block, in cap heights, measured from the block's CENTRE
            //- which is what blockToWorld turns about, so this has to be relative to it and not to the corner
            //the layout was written from.
            float x = slot.X + slot.Advance * 0.5f * slot.Scale + _blockWidth * 0.5f;
            float y = slot.Baseline + LetterShapes.CAP_HEIGHT * 0.5f * slot.Scale
                + wave * WAVE_DEPTH * slot.Scale + _blockHeight * 0.5f;

            //Bowed towards the lens by how far across the BLOCK it stands - see Slot.Across.
            float z = BOW_DEPTH * (1f - slot.Across * slot.Across);

            return
                Matrix.CreateTranslation(-slot.Advance * 0.5f, -LetterShapes.CAP_HEIGHT * 0.5f, 0f)
                * Matrix.CreateScale(cap * slot.Scale)
                * Matrix.CreateRotationY(LETTER_YAW * MathF.Cos(wavePhase))
                * Matrix.CreateTranslation(x * cap, y * cap, z * cap)
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
            foreach (LetterMesh mesh in _bodyMeshes) mesh?.Dispose();
            foreach (LetterMesh mesh in _outlineMeshes) mesh?.Dispose();
        }
    }
}
