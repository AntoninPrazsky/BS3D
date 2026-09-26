using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The storm (the seventeenth scene, #219): a field of cumulus cells standing in open air around and below
    /// the arena, with lightning breaking through the gaps — billboard puffs and bolt channels in
    /// <c>StormClouds.fx</c>, no ground at all. Moved out of <see cref="SceneRenderer"/> whole in #580, the first
    /// backdrop with a scene event: the strike schedule, the flash, the lamp it throws and the cells the chapter
    /// intro frames. The renderer's public <c>StormFlash</c>, <c>TryGetStormFlash</c>, <c>StormCellCount</c> and
    /// <c>StormCell</c> forward here. See "The storm" in docs/scenes.md.
    /// </summary>
    internal sealed class StormBackdrop : Backdrop
    {
        private readonly GraphicsDevice _graphicsDevice;

        private StormSceneConfig _stormConfig = new();

        private readonly Effect _stormEffect;

        //The cloud field: one static buffer of billboard puffs, turned to face the camera in the vertex
        //shader. The sea's spray and the mountain's snow are drawn exactly this way, and #151 measured two
        //thousand of those at nothing at all.
        //Where the cells stand, kept so a strike can go off INSIDE one. A strike placed at a hashed radius
        //instead lands in clear air about as often as not, and a discharge with no cloud around it has
        //nothing for its glow to light - which is most of what the flash is.
        private Vector2[] _stormStrikeCells = System.Array.Empty<Vector2>();

        //Each cell's body beside it — (base height, radius, height) — for a lens that has to keep out of the
        //cells (the chapter intro's prologue, #559). Built with the field, read through StormCell.
        private Vector3[] _stormCellBodies = System.Array.Empty<Vector3>();

        private VertexBuffer _stormCloudVertexBuffer;
        private IndexBuffer _stormCloudIndexBuffer;
        private int _stormCloudPuffCount;

        //The visible discharge. Static too, and entirely procedural in the vertex shader off the strike's
        //own period index — so a bolt costs no CPU work per frame and every executable draws the same one
        //at the same second, which is the rule the whole flash schedule already follows.
        private VertexBuffer _stormBoltVertexBuffer;
        private IndexBuffer _stormBoltIndexBuffer;
        private int _stormBoltQuadCount;

        //Segments a bolt's channel is drawn in. Enough that the jagged path reads as a filament with kinks
        //in it rather than as a polyline; the width is a config dial and the glare pass does the rest.
        private const int STORM_BOLT_SEGMENTS = 26;

        //⚠ The buffers are 16-bit indexed, which caps the field at 16 383 quads. StormCloudsConfig's own
        //note says so; this is where it is enforced, because a silent overflow here draws a scene made of
        //garbage triangles rather than failing.
        private const int STORM_MAX_QUADS = 16000;

        //How far from the arena a strike may go off: a bolt beyond this is a bolt nobody sees. Tested against
        //where a cell stands NOW (StormCellPosition), not where it was built — the field moves (#532).
        private const float STORM_STRIKE_REACH = 420f;

        //Look/tuning parameters (the cloud field, the material, the flash, the air) live in
        //StormSceneConfig; this class reads them from _stormConfig.

        /// <summary>Loads the effect, builds the cloud field and the bolts and pushes the config at them.</summary>
        public StormBackdrop(BackdropServices services, ContentManager content) : base(services)
        {
            _graphicsDevice = services.GraphicsDevice;

            //--- Storm (#219): the seventeenth scene — broken cumulus standing in open air around and below
            //the arena, with lightning breaking through the gaps. It is the one scene with NO ground in it,
            //so it is not a terrain draw at all: StormClouds.fx's header has why a height field could not
            //carry it and why the shared sky cloud field could not either.
            _stormEffect = content.Load<Effect>("Shaders/StormClouds");
            BuildStormCloudBuffers();
            BuildStormBoltBuffers();

            ApplyStormParameters();
        }

        /// <inheritdoc/>
        public override SceneKind Kind => SceneKind.Storm;

        /// <inheritdoc/>
        public override SceneConfig Config => _stormConfig;

        /// <summary>Pushes the storm's static tuning into <c>StormClouds.fx</c>. The flash's own per-frame
        /// values are pushed by <see cref="Draw"/>, since they come off the wall clock.</summary>
        private void ApplyStormParameters()
        {
            StormCloudsConfig clouds = _stormConfig.Clouds;
            StormSurfaceConfig surface = _stormConfig.Surface;
            StormAirConfig air = _stormConfig.Air;

            _stormEffect.Parameters["TopColor"].SetValue(surface.TopColor.ToVector3());
            _stormEffect.Parameters["BaseColor"].SetValue(surface.BaseColor.ToVector3());
            _stormEffect.Parameters["SilverStrength"].SetValue(surface.SilverStrength);
            _stormEffect.Parameters["AmbientStrength"].SetValue(surface.AmbientStrength);

            _stormEffect.Parameters["PuffOpacity"].SetValue(clouds.PuffOpacity);
            _stormEffect.Parameters["EdgeSoftness"].SetValue(Math.Clamp(clouds.EdgeSoftness, 0.01f, 0.98f));
            _stormEffect.Parameters["UnderShade"].SetValue(clouds.UnderShade);
            _stormEffect.Parameters["MassNormalMix"].SetValue(Math.Clamp(clouds.MassNormalMix, 0f, 1f));
            _stormEffect.Parameters["LayerBottomY"].SetValue(clouds.LayerBottomY);
            _stormEffect.Parameters["LayerTopY"].SetValue(clouds.LayerTopY);

            _stormEffect.Parameters["FlashColor"].SetValue(_stormConfig.Flash.Color.ToVector3());
            _stormEffect.Parameters["FlashGlow"].SetValue(_stormConfig.Flash.CloudGlow);
            _stormEffect.Parameters["BoltColor"].SetValue(_stormConfig.Flash.BoltColor.ToVector3());
            _stormEffect.Parameters["BoltWidth"].SetValue(MathF.Max(_stormConfig.Flash.BoltWidth, 0.02f));

            //How far a strike's glow carries through the field. ⚠ It is its OWN dial and not a figure
            //derived from anything else, which is what the first build did (a lattice spacing x 0.8 = 136
            //units) — and 136 units around a strike standing 173 to 348 units out is a patch smaller than
            //the gap to it, so the glow landed almost entirely outside the frame and the flash read as not
            //working at all.
            _stormEffect.Parameters["FlashReach"].SetValue(MathF.Max(_stormConfig.Flash.GlowReach, 1f));

            _stormEffect.Parameters["HazeTint"].SetValue(air.HazeTint.ToVector3());
            _stormEffect.Parameters["HorizonHazeDistance"].SetValue(MathF.Max(air.HorizonHazeDistance, 1f));
            _stormEffect.Parameters["HazeStrength"].SetValue(air.HazeStrength);
            StormWindFrame(out Vector2 wind, out _);
            _stormEffect.Parameters["WindDirection"].SetValue(wind);
            _stormEffect.Parameters["DriftSpeed"].SetValue(air.DriftSpeed);
            _stormEffect.Parameters["FieldHalfLength"].SetValue(MathF.Max(clouds.OuterRadius, 1f));
            _stormEffect.Parameters["FieldClearance"].SetValue(StormCellClearance());
        }

        /// <summary>
        /// (Re)builds the storm's cloud field: cumulus cells scattered through a volume around and below the
        /// arena, each built from soft billboard puffs. Deterministic seed, so the sky is the same one in
        /// every executable and every session.
        /// <para>
        /// <b>The shape of a cell is what makes it cumulus.</b> Puffs are laid on a profile that is widest
        /// through the middle and tapers at both ends, biased low so the body is heavier than the crown, and
        /// their own radius shrinks with height — which is what gives the cauliflower top. Laying them in a
        /// plain ellipsoid gives a bun, and a bun is what the height field's turrets already were.
        /// </para>
        /// </summary>
        private void BuildStormCloudBuffers()
        {
            StormCloudsConfig c = _stormConfig.Clouds;

            int massCount = Math.Max(c.MassCount, 1);
            int perMass = Math.Max(c.PuffsPerMass, 4);
            if (massCount * perMass > STORM_MAX_QUADS) massCount = STORM_MAX_QUADS / perMass;

            _stormCloudPuffCount = massCount * perMass;

            CloudPuffVertex[] vertices = new CloudPuffVertex[_stormCloudPuffCount * 4];
            Random rng = new(90219 + Services.SeedOffset);

            StormWindFrame(out Vector2 along, out Vector2 across);
            float halfLength = MathF.Max(c.OuterRadius, 1f);
            float halfWidth = MathF.Max(c.BandHalfWidth, 1f);

            //Every cell's middle where it was BUILT: a strike picks one and asks StormCellPosition where it
            //stands now, so the list is the whole field and the reach test waits until then (#532).
            Vector2[] strikeCells = new Vector2[massCount];
            Vector3[] cellBodies = new Vector3[massCount];

            int puff = 0;
            for (int m = 0; m < massCount; m++)
            {
                //Uniform over a BAND aligned with the wind — OuterRadius up- and downwind, BandHalfWidth
                //across — and not over an annulus (#532). The field drifts along the band and wraps through
                //its far end, so a band is what stays evenly covered; the annulus, drifting with no wrap,
                //emptied its upwind half within minutes. The arena's clearance is not cut out of it here:
                //the drift steers every cell round the arena (StormClouds.fx's StormCellOffset), at launch
                //as much as after an hour, so the band is built without a hole.
                float a = ((float)rng.NextDouble() * 2f - 1f) * halfLength;
                float b = ((float)rng.NextDouble() * 2f - 1f) * halfWidth;

                Vector3 centre = new(along.X * a + across.X * b, Lerp(c.BaseYMin, c.BaseYMax, (float)rng.NextDouble()),
                    along.Y * a + across.Y * b);

                strikeCells[m] = new Vector2(centre.X, centre.Z);

                float massRadius = Lerp(c.MassRadiusMin, MathF.Max(c.MassRadiusMax, c.MassRadiusMin), (float)rng.NextDouble());
                float massHeight = massRadius * Lerp(c.HeightScaleMin, MathF.Max(c.HeightScaleMax, c.HeightScaleMin), (float)rng.NextDouble());
                cellBodies[m] = new Vector3(centre.Y, massRadius, massHeight);

                for (int k = 0; k < perMass; k++)
                {
                    //Biased towards the base, so a cell is heavier below than above.
                    float h = MathF.Pow((float)rng.NextDouble(), 0.80f);

                    //Widest through the middle, tapering at both ends — the profile of a developed cumulus
                    //rather than of a dome or a cone.
                    float taper = MathF.Sqrt(MathF.Max(1f - MathF.Pow(2f * h - 1f, 2f) * 0.82f, 0.05f));
                    float ring = massRadius * taper * MathF.Sqrt((float)rng.NextDouble());
                    float ringAngle = (float)rng.NextDouble() * MathHelper.TwoPi;

                    Vector3 position = centre + new Vector3(
                        MathF.Cos(ringAngle) * ring,
                        h * massHeight,
                        MathF.Sin(ringAngle) * ring);

                    //Smaller lobes towards the crown: that gradient IS the cauliflower.
                    //
                    //⚠ And the sizes within one cell span MANY SCALES, which is the other half of why a field
                    //of billboards reads as bubble wrap (#510). Every aircraft-window reference shows a cell
                    //built of big lobes with smaller lobes standing on them and smaller ones again on those,
                    //where this drew one uniform band (0.22–0.40 of the cell, a ±29 % spread): balls of one
                    //size tile, and the eye counts them. Squaring the roll over a much wider band keeps the
                    //mean about where it was — 0.12 + (0.58 − 0.12)/3 = 0.27 against the old 0.31, so a cell
                    //has the same body — while giving it a long tail of real lobes and a crowd of small
                    //billows to break their outlines with.
                    float sizeRoll = (float)rng.NextDouble();
                    float puffRadius = massRadius
                        * Lerp(c.PuffScaleMin, MathF.Max(c.PuffScaleMax, c.PuffScaleMin), sizeRoll * sizeRoll)
                        * Lerp(1f, 0.62f, h);

                    float seed = (float)rng.NextDouble();

                    //The cell's own middle, taken at half its height: a normal measured from its foot would
                    //point outwards and up everywhere and would light the whole cell as a dome.
                    Vector3 massMiddle = centre + new Vector3(0f, massHeight * 0.45f, 0f);

                    int v = puff * 4;
                    vertices[v] = new CloudPuffVertex(position, new Vector4(-1f, 1f, puffRadius, seed), massMiddle);
                    vertices[v + 1] = new CloudPuffVertex(position, new Vector4(1f, 1f, puffRadius, seed), massMiddle);
                    vertices[v + 2] = new CloudPuffVertex(position, new Vector4(-1f, -1f, puffRadius, seed), massMiddle);
                    vertices[v + 3] = new CloudPuffVertex(position, new Vector4(1f, -1f, puffRadius, seed), massMiddle);
                    puff++;
                }
            }

            _stormCloudVertexBuffer?.Dispose();
            _stormCloudIndexBuffer?.Dispose();

            _stormCloudVertexBuffer = new VertexBuffer(_graphicsDevice, CloudPuffVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            _stormCloudVertexBuffer.SetData(vertices);
            _stormCloudIndexBuffer = Services.BuildQuadIndexBuffer(_stormCloudPuffCount);
            _stormStrikeCells = strikeCells;
            _stormCellBodies = cellBodies;
        }

        /// <summary>
        /// (Re)builds the lightning channels. The buffer carries nothing but which bolt and how far along it
        /// each vertex is — the path itself is hashed in the vertex shader off the strike's own period
        /// index, so a bolt costs no per-frame CPU work and is the same one in every executable.
        /// </summary>
        private void BuildStormBoltBuffers()
        {
            int bolts = Math.Max(_stormConfig.Flash.BoltCount, 0);
            _stormBoltQuadCount = bolts * STORM_BOLT_SEGMENTS;
            if (_stormBoltQuadCount == 0) return;

            CloudPuffVertex[] vertices = new CloudPuffVertex[_stormBoltQuadCount * 4];

            int quad = 0;
            for (int b = 0; b < bolts; b++)
            {
                for (int s = 0; s < STORM_BOLT_SEGMENTS; s++)
                {
                    float t0 = s / (float)STORM_BOLT_SEGMENTS;
                    float t1 = (s + 1) / (float)STORM_BOLT_SEGMENTS;

                    int v = quad * 4;
                    vertices[v] = new CloudPuffVertex(new Vector3(b, t0, -1f), Vector4.Zero, Vector3.Zero);
                    vertices[v + 1] = new CloudPuffVertex(new Vector3(b, t0, 1f), Vector4.Zero, Vector3.Zero);
                    vertices[v + 2] = new CloudPuffVertex(new Vector3(b, t1, -1f), Vector4.Zero, Vector3.Zero);
                    vertices[v + 3] = new CloudPuffVertex(new Vector3(b, t1, 1f), Vector4.Zero, Vector3.Zero);
                    quad++;
                }
            }

            _stormBoltVertexBuffer?.Dispose();
            _stormBoltIndexBuffer?.Dispose();

            _stormBoltVertexBuffer = new VertexBuffer(_graphicsDevice, CloudPuffVertex.Declaration, vertices.Length, BufferUsage.WriteOnly);
            _stormBoltVertexBuffer.SetData(vertices);
            _stormBoltIndexBuffer = Services.BuildQuadIndexBuffer(_stormBoltQuadCount);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        //A billboard puff: where it stands in the world, and (corner x, corner y, radius, seed). The corner
        //is the unit quad's own -1..1 offset, which the pixel shader reads back as the puff's disc
        //coordinate — so one attribute carries both the billboard and the shading frame.
        private struct CloudPuffVertex : IVertexType
        {
            public Vector3 Position;
            public Vector4 Data;

            //The middle of the CELL this puff belongs to. It is what lets a mass shade as one body: without
            //it every puff shades as its own little sphere, complete with its own light-to-dark gradient
            //and its own circular edge, and a cell built of those reads as a heap of balls rather than as
            //cloud. Carried per vertex because there is nowhere cheaper to put it - a puff has no other way
            //of knowing what it is part of.
            public Vector3 MassCentre;

            public CloudPuffVertex(Vector3 position, Vector4 data, Vector3 massCentre)
            {
                Position = position;
                Data = data;
                MassCentre = massCentre;
            }

            public static readonly VertexDeclaration Declaration = new(
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
                new VertexElement(12, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 0),
                new VertexElement(28, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0));

            readonly VertexDeclaration IVertexType.VertexDeclaration => Declaration;
        }

        /// <summary>The lightning envelope at a wall-clock time; see <see cref="SceneRenderer.StormFlash"/>.</summary>
        public float Flash(float time)
        {
            StormFlashConfig flash = _stormConfig.Flash;

            float period = StormStrikeSchedule(time, out float index, out float start, out float length, out float size);
            float u = time / period;

            float p = (u - index - start) / length;
            if (p <= 0f || p >= 1f) return 0f;

            //A hard attack over the first 6 % and a fast power decay after it.
            float envelope = p < 0.06f ? p / 0.06f : MathF.Pow(1f - (p - 0.06f) / 0.94f, 2.6f);

            //The return strokes. Rectified so every flicker is a brightening rather than a sign change, and
            //floored well above zero so the channel never goes fully dark mid-strike (which reads as two
            //separate strikes rather than one stuttering one).
            float flicker = MathF.Max(flash.Flicker, 0f);
            if (flicker > 0f)
                envelope *= 0.55f + 0.45f * MathF.Abs(MathF.Cos(p * MathHelper.Pi * flicker));

            //Not every strike is the same size: a scene whose every event is identical stops having events.
            return envelope * size;
        }

        //The strike's SCHEDULE, in one place: which period it is, where in that period the light starts, how
        //long it lasts and how big it is. StormFlash draws its envelope from this and TryGetSceneEvent hands
        //the same four figures to the sound, so the flash and the thunder cannot disagree about which strike
        //went off or when it started. Returns the period, which both callers need as well.
        private float StormStrikeSchedule(float time, out float index, out float start, out float length, out float size)
        {
            StormFlashConfig flash = _stormConfig.Flash;

            float period = MathF.Max(flash.Period, 0.5f);

            index = MathF.Floor(time / period);
            start = 0.08f + 0.62f * SceneRenderer.Hash01(index);
            length = Math.Clamp(MathF.Max(flash.Length, 0.05f) / period, 0.01f, 0.7f);
            size = 0.5f + 0.5f * SceneRenderer.Hash01(index + 313f);

            return period;
        }

        /// <summary>The storm's wind as an orthonormal XZ frame — <paramref name="along"/> downwind and
        /// <paramref name="across"/> its perpendicular — the axes the cloud band is built on and drifts
        /// along. Normalised here, so a config wind that is not unit-length cannot silently mean a faster
        /// sky, and pushed to the shader in the same form.</summary>
        private void StormWindFrame(out Vector2 along, out Vector2 across)
        {
            along = _stormConfig.Air.Wind.ToVector2();
            along = along.LengthSquared() > 1e-6f ? Vector2.Normalize(along) : Vector2.UnitX;
            across = new Vector2(-along.Y, along.X);
        }

        /// <summary>
        /// Where a cloud cell stands at a wall-clock time, from where it was built: carried downwind, wrapped
        /// through the far end of the band back to the near one, and steered round the arena. <b>The host
        /// copy of <c>StormClouds.fx</c>'s <c>StormCellOffset</c>, kept in step by hand</b> — the shader
        /// moves the puffs and this places the strike inside them, and if the two disagreed the bolt would
        /// go off in clear air. Which is what happened until #532: the strike stood where the cell was
        /// built, inside it for the first half minute of a session and in the air it had left ever after.
        /// </summary>
        private Vector2 StormCellPosition(Vector2 built, float time)
        {
            StormCloudsConfig clouds = _stormConfig.Clouds;
            StormWindFrame(out Vector2 along, out Vector2 across);

            float halfLength = MathF.Max(clouds.OuterRadius, 1f);
            float clearance = StormCellClearance();
            float span = 2f * halfLength;

            float a = Vector2.Dot(built, along) + time * _stormConfig.Air.DriftSpeed;
            float b = Vector2.Dot(built, across);

            a -= span * MathF.Floor((a + halfLength) / span);

            float bump = MathF.Exp(-(a * a) / (2f * clearance * clearance));
            float side = b < 0f ? -1f : 1f;
            float abs = MathF.Abs(b);
            b = side * (abs + clearance * bump * MathF.Max(0f, 1f - abs / (3f * clearance)));

            return along * a + across * b;
        }

        /// <summary>
        /// How close to the arena a cell's MIDDLE may come: <see cref="StormCloudsConfig.InnerRadius"/> for
        /// its puffs, plus the largest cell's radius, since a puff stands up to that far from its middle.
        /// The first cut of #532 steered the middle to <c>InnerRadius</c> alone, and the Game's front end
        /// photographed its lens inside a puff at ten seconds — the whole frame milk.
        /// </summary>
        private float StormCellClearance()
        {
            StormCloudsConfig clouds = _stormConfig.Clouds;
            return MathF.Max(clouds.InnerRadius, 1f) + MathF.Max(clouds.MassRadiusMax, clouds.MassRadiusMin);
        }

        /// <summary>
        /// Where the current strike stands, in the XZ plane. Hashed off the same period index the envelope
        /// is, so the flash's glow and the light it throws cannot disagree about which cell went off — and
        /// held out past the clearing, because a strike inside the ring the island stands in would be a
        /// bolt in the play field rather than weather in the distance.
        /// </summary>
        private Vector2 StormFlashCenter(float time)
        {
            float period = MathF.Max(_stormConfig.Flash.Period, 0.5f);
            float index = MathF.Floor(time / period);

            //⚠ IN A CELL, not at a hashed radius. Placed by radius and bearing alone a strike lands in clear
            //air about as often as in cloud, and a discharge with nothing around it lights nothing - the
            //glow IS the flash from most cameras, since the channel itself is usually inside the cell it
            //went off in. The cells' own middles are kept when the field is built for exactly this.
            //
            //And in the cell where it stands NOW (#532): the hashed pick is walked on to the first cell
            //within reach of the arena this second, so it is still a pure function of the period index and
            //the clock. A cell drifts 0.7 units over one strike, so the bolt rides with it unnoticed.
            int count = _stormStrikeCells.Length;
            if (count > 0)
            {
                int first = Math.Clamp((int)(SceneRenderer.Hash01(index + 57f) * count), 0, count - 1);
                Vector2 nearest = default;
                float nearestDistance = float.MaxValue;

                for (int step = 0; step < count; step++)
                {
                    Vector2 at = StormCellPosition(_stormStrikeCells[(first + step) % count], time);
                    float distance = at.Length();
                    if (distance <= STORM_STRIKE_REACH) return at;
                    if (distance < nearestDistance) { nearestDistance = distance; nearest = at; }
                }

                return nearest;
            }

            //Nothing to strike (a field configured empty): fall back to a ring outside the arena.
            float inner = MathF.Max(_stormConfig.Clouds.InnerRadius, 1f) + 40f;
            float bearing = SceneRenderer.Hash01(index + 57f) * MathHelper.TwoPi;

            return new Vector2(MathF.Cos(bearing) * inner, MathF.Sin(bearing) * inner);
        }

        /// <summary>The flash as a scene point light; see <see cref="SceneRenderer.TryGetStormFlash"/>.</summary>
        public bool TryGetFlash(float time, out Vector3 position, out Vector3 color, out float range)
        {
            position = Vector3.Zero;
            color = Vector3.Zero;
            range = 0f;

            StormFlashConfig flash = _stormConfig.Flash;

            if (flash.LightStrength <= 0f) return false;

            float envelope = Flash(time);
            if (envelope <= 0f) return false;

            float distance = MathF.Max(flash.LightDistance, 1f);

            //Under the island and leaning towards the cell that actually went off, so the fill has a
            //direction rather than being a flat uplight — but overwhelmingly below, which is what makes it
            //read as the deck and not as a second sun.
            Vector2 at = StormFlashCenter(time);
            Vector3 towards = SceneRenderer.SafeNormal(new Vector3(at.X * 0.25f, -distance, at.Y * 0.25f), -Vector3.UnitY);

            position = towards * distance;

            //Normalized like the planetshine and the earthshine, so LightStrength alone says how bright the
            //fill is and the colour only says its hue.
            Vector3 tint = flash.Color.ToVector3();
            float peak = MathF.Max(MathF.Max(tint.X, tint.Y), MathF.Max(tint.Z, 1e-4f));

            color = tint / peak * (flash.LightStrength * envelope);

            range = distance * 3f;

            return true;
        }

        /// <summary>
        /// Draws the storm (#219): the field of cumulus cells, then the lightning channel over it.
        /// <para>
        /// <b>Alpha-blended, depth-read, depth-write off</b> — the sea's spray and the mountain's snow are
        /// drawn the same way and for the same reason: a soft-edged billboard that wrote depth would punch
        /// its own quad's silhouette out of everything behind it, which is the hard edge this whole scene
        /// exists to avoid. The field is unsorted, which is a real approximation and an acceptable one here:
        /// every puff is the same near-white medium, so getting two of them the wrong way round changes the
        /// blend weights and nothing the eye can name.
        /// </para>
        /// <para>
        /// <b>It deliberately does NOT invoke <see cref="SceneFrame.ApplyClouds"/></b>. The hook pushes the
        /// sky's own weather into a scene effect's <c>Cloud*</c> namespace, and this field neither wants a
        /// cloud shadow cast on it (it <i>is</i> the cloud) nor could survive one: <c>CloudSunlight</c>
        /// above the shared plane degenerates to the point's own column and returns about the shadow floor.
        /// <c>StormClouds.fx</c>'s header has the whole argument.
        /// </para>
        /// </summary>
        public override void Draw(in SceneFrame frame)
        {
            Matrix inverseView = Matrix.Invert(frame.Camera.View);
            float envelope = Flash(frame.Time);

            _stormEffect.Parameters["View"].SetValue(frame.Camera.View);
            _stormEffect.Parameters["Projection"].SetValue(frame.Camera.Projection);
            _stormEffect.Parameters["CameraPosition"].SetValue(frame.Camera.Position);
            _stormEffect.Parameters["CameraRight"].SetValue(inverseView.Right);
            _stormEffect.Parameters["CameraUp"].SetValue(inverseView.Up);
            _stormEffect.Parameters["SunDirection"].SetValue(frame.SunDirection);
            _stormEffect.Parameters["SunColor"].SetValue(frame.SunColor);
            _stormEffect.Parameters["ZenithColor"].SetValue(frame.ZenithLinear);
            _stormEffect.Parameters["HorizonColor"].SetValue(frame.HorizonLinear);
            _stormEffect.Parameters["CloudTime"].SetValue(frame.Time);

            //The strike, off the same clock and the same hashed period index the light rig's own lamp reads,
            //so the glow in the cloud, the channel drawn through it and the flash on the arena cannot
            //disagree about which cell went off.
            _stormEffect.Parameters["FlashEnvelope"].SetValue(envelope);
            _stormEffect.Parameters["FlashCenterXZ"].SetValue(StormFlashCenter(frame.Time));
            _stormEffect.Parameters["FlashStrikeIndex"].SetValue(
                MathF.Floor(frame.Time / MathF.Max(_stormConfig.Flash.Period, 0.5f)));

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;

            _graphicsDevice.SetVertexBuffer(_stormCloudVertexBuffer);
            _graphicsDevice.Indices = _stormCloudIndexBuffer;
            _stormEffect.CurrentTechnique = _stormEffect.Techniques["StormClouds"];
            _stormEffect.CurrentTechnique.Passes[0].Apply();
            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _stormCloudPuffCount * 2);

            //The channel, and only while one is running: between strikes this is the whole cost of the
            //lightning. Additive, because a discharge adds light to whatever is behind it and never hides
            //it — a channel drawn with alpha over cloud reads as a painted stripe.
            if (envelope > 0f && _stormBoltQuadCount > 0)
            {
                _graphicsDevice.BlendState = BlendState.Additive;

                _graphicsDevice.SetVertexBuffer(_stormBoltVertexBuffer);
                _graphicsDevice.Indices = _stormBoltIndexBuffer;
                _stormEffect.CurrentTechnique = _stormEffect.Techniques["StormBolts"];
                _stormEffect.CurrentTechnique.Passes[0].Apply();
                _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _stormBoltQuadCount * 2);
            }

            _graphicsDevice.BlendState = BlendState.AlphaBlend;
            _graphicsDevice.DepthStencilState = DepthStencilState.Default;
            _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        /// <inheritdoc/>
        public override bool TryGetViewpoint(float bearing, out SceneViewpoint viewpoint)
        {
            //The deck, which in this scene is BELOW: the arena floats over the cloud tops and the
            //flashes go off inside cells down there. Part-way out through the field's own inner and
            //outer radii, at the middle of the range its cells' bases are rolled from.
            StormCloudsConfig clouds = _stormConfig.Clouds;
            viewpoint = new SceneViewpoint(
                SceneRenderer.AtBearing(bearing, (clouds.InnerRadius + clouds.OuterRadius) * 0.35f,
                    (clouds.BaseYMin + clouds.BaseYMax) * 0.5f),
                2.2f, 24f, 145f, "the cloud deck");
            return true;
        }

        /// <inheritdoc/>
        public override bool TryGetSceneEvent(float time, out SceneEvent staged)
        {
            float period = StormStrikeSchedule(time, out float index, out float start, out float _, out float size);
            Vector2 at = StormFlashCenter(time);

            //Mid-deck: the cells' bases are rolled between these two, and a strike goes off inside a
            //cell. It is the DISTANCE this is wanted for, so the middle of the range is honest and
            //the exact height of one cell is not.
            StormCloudsConfig clouds = _stormConfig.Clouds;
            float deckY = (clouds.BaseYMin + clouds.BaseYMax) * 0.5f;

            staged = new SceneEvent((int)index, (index + start) * period, new Vector3(at.X, deckY, at.Y), size);
            return true;
        }

        /// <summary>How many cumulus cells the field was built with; see <see cref="SceneRenderer.StormCellCount"/>.</summary>
        public int CellCount => _stormStrikeCells.Length;

        /// <summary>Cell <paramref name="index"/> at a wall-clock time; see <see cref="SceneRenderer.StormCell"/>.</summary>
        public Vector3 Cell(int index, float time, out float radius, out float height)
        {
            Vector3 body = _stormCellBodies[index];
            Vector2 at = StormCellPosition(_stormStrikeCells[index], time);

            radius = body.Y;
            height = body.Z;
            return new Vector3(at.X, body.X, at.Y);
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            _stormCloudVertexBuffer?.Dispose();
            _stormCloudIndexBuffer?.Dispose();
            _stormBoltVertexBuffer?.Dispose();
            _stormBoltIndexBuffer?.Dispose();
        }
    }
}
