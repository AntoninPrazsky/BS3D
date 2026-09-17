using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// The street level the city's towers stand on (#399): asphalt with lane lines and zebra crossings, sidewalks
    /// with rounded curbs, stone paving where a block has no tower, parked traffic, and in the neon city sodium
    /// lamps and the shops' spill. Until then the towers ran 420 units down to nothing, and the dome showed as
    /// bright slits between them from any view that looked down.
    /// <para>
    /// <b>One quad and one shader, <c>CityStreets.fx</c>.</b> The pattern is evaluated per pixel off the grid the
    /// city was built on (<see cref="City.BlockPitch"/>, <see cref="City.StreetWidth"/>), so a street cannot run
    /// through a tower. What the grid cannot say — whether a block carries a tower at all — is a texture of one
    /// texel per block from <see cref="City.BlockBuilt"/>, which the shader also blurs into how built-up a street's
    /// neighbourhood is: the stand-in for the canyon shadows this project does not draw.
    /// </para>
    /// <para>
    /// <b>The frame sequence stays the caller's</b>, as it does for <see cref="CityRooftops"/>: <see cref="Draw"/>
    /// gates on no scene, and the caller draws it after the towers and their roofs, so the depth test rejects every
    /// pixel of street a tower stands in front of before the shader runs. It sets the blend, rasterizer and depth
    /// state it needs and restores the scene's opaque state on the way out.
    /// </para>
    /// </summary>
    public sealed class CityStreets : IDisposable
    {
        /// <summary>
        /// How far past the city's last block the ground reaches. The shader fades it to its own average and then
        /// into the horizon's colour, so from the few views that see past the towers it reads as distance rather
        /// than as an edge.
        /// </summary>
        private const float OUTSKIRTS = 2500f;

        private readonly GraphicsDevice _device;
        private readonly Effect _effect;
        private readonly VertexBuffer _quad;
        private Texture2D _occupancy;

        private readonly EffectParameter _view, _projection, _cameraPosition, _sunDirection, _sunColor, _zenithColor, _horizonColor;
        private readonly EffectParameter _groundY, _blockPitch, _streetWidth, _radiusBlocks, _occupancyTexture;
        private readonly EffectParameter _sidewalkWidth, _cornerRadius, _asphaltColor, _sidewalkColor, _plazaColor, _curbColor, _markingColor;
        private readonly EffectParameter _ambientStrength, _canyonSkyView, _canyonSunView, _facadeBounce, _hazeDistance, _carChance, _treeColor, _treeChance;
        private readonly EffectParameter _neon, _neonAmbient, _lampColor, _lampSpacing, _neonSpill, _neonMagenta, _neonCyan, _neonHazeColor;

        /// <param name="device">The device the quad and the occupancy texture live on.</param>
        /// <param name="effect"><c>Shaders/CityStreets</c>, loaded by the caller's content manager.</param>
        /// <param name="city">The city whose grid the streets follow; rebuild with <see cref="Rebuild"/> when it changes.</param>
        public CityStreets(GraphicsDevice device, Effect effect, City city)
        {
            _device = device;
            _effect = effect;

            //A flat square the shader lifts to the city's ground level; only x and z are read. Wound for no cull
            //in particular: the draw states CullNone, since the MapEditor's free camera can stand under it.
            VertexPosition[] corners =
            {
                new(new Vector3(-OUTSKIRTS, 0f, -OUTSKIRTS)),
                new(new Vector3(OUTSKIRTS, 0f, -OUTSKIRTS)),
                new(new Vector3(-OUTSKIRTS, 0f, OUTSKIRTS)),
                new(new Vector3(OUTSKIRTS, 0f, OUTSKIRTS)),
            };
            _quad = new VertexBuffer(device, VertexPosition.VertexDeclaration, corners.Length, BufferUsage.WriteOnly);
            _quad.SetData(corners);

            EffectParameterCollection p = effect.Parameters;
            _view = p["View"];
            _projection = p["Projection"];
            _cameraPosition = p["CameraPosition"];
            _sunDirection = p["SunDirection"];
            _sunColor = p["SunColor"];
            _zenithColor = p["ZenithColor"];
            _horizonColor = p["HorizonColor"];
            _groundY = p["GroundY"];
            _blockPitch = p["BlockPitch"];
            _streetWidth = p["StreetWidth"];
            _radiusBlocks = p["RadiusBlocks"];
            _occupancyTexture = p["OccupancyTexture"];
            _sidewalkWidth = p["SidewalkWidth"];
            _cornerRadius = p["CornerRadius"];
            _asphaltColor = p["AsphaltColor"];
            _sidewalkColor = p["SidewalkColor"];
            _plazaColor = p["PlazaColor"];
            _curbColor = p["CurbColor"];
            _markingColor = p["MarkingColor"];
            _ambientStrength = p["AmbientStrength"];
            _canyonSkyView = p["CanyonSkyView"];
            _canyonSunView = p["CanyonSunView"];
            _facadeBounce = p["FacadeBounce"];
            _hazeDistance = p["HazeDistance"];
            _carChance = p["CarChance"];
            _treeColor = p["TreeColor"];
            _treeChance = p["TreeChance"];
            _neon = p["Neon"];
            _neonAmbient = p["NeonAmbient"];
            _lampColor = p["LampColor"];
            _lampSpacing = p["LampSpacing"];
            _neonSpill = p["NeonSpill"];
            _neonMagenta = p["NeonMagenta"];
            _neonCyan = p["NeonCyan"];
            _neonHazeColor = p["NeonHazeColor"];

            Rebuild(city);
        }

        /// <summary>
        /// Follows a rebuilt city: its grid, its ground level and which of its blocks carry a tower. Call it
        /// wherever the city itself is rebuilt (a quality step, the map editor's panel).
        /// </summary>
        public void Rebuild(City city)
        {
            int side = 2 * city.RadiusBlocks + 1;

            if (_occupancy == null || _occupancy.Width != side)
            {
                _occupancy?.Dispose();
                _occupancy = new Texture2D(_device, side, side, false, SurfaceFormat.Color);
            }

            Color[] texels = new Color[side * side];
            for (int i = 0; i < texels.Length; i++)
                texels[i] = city.BlockBuilt[i] ? Color.White : Color.Black;
            _occupancy.SetData(texels);

            _groundY.SetValue(city.GroundY);
            _blockPitch.SetValue(city.BlockPitch);
            _streetWidth.SetValue(city.StreetWidth);
            _radiusBlocks.SetValue((float)city.RadiusBlocks);
            _occupancyTexture.SetValue(_occupancy);
        }

        /// <summary>
        /// Draws the street level. After the towers and their roofs, in any render state: it states opaque blend,
        /// no culling and the default depth test itself, and leaves alpha blend and counter-clockwise culling behind,
        /// which is the scene state the callers draw everything else of the city in.
        /// </summary>
        /// <param name="frame">This frame's camera, sun, sky palette and cloud hook.</param>
        /// <param name="look">The look, read every draw so the map editor's panel edits it live.</param>
        /// <param name="neon">The neon night relight instead of the day.</param>
        /// <param name="neonLook">Where the neon's magenta and cyan come from, so the spill matches the lights.</param>
        public void Draw(in SceneFrame frame, CityStreetsConfig look, bool neon, NeonConfig neonLook)
        {
            _view.SetValue(frame.Camera.View);
            _projection.SetValue(frame.Camera.Projection);
            _cameraPosition.SetValue(frame.Camera.Position);
            _sunDirection.SetValue(frame.SunDirection);
            _sunColor.SetValue(frame.SunColor);
            _zenithColor.SetValue(frame.ZenithLinear);
            _horizonColor.SetValue(frame.HorizonLinear);

            _sidewalkWidth.SetValue(look.SidewalkWidth);
            _cornerRadius.SetValue(look.CornerRadius);
            _asphaltColor.SetValue(look.AsphaltColor.ToVector3());
            _sidewalkColor.SetValue(look.SidewalkColor.ToVector3());
            _plazaColor.SetValue(look.PlazaColor.ToVector3());
            _curbColor.SetValue(look.CurbColor.ToVector3());
            _markingColor.SetValue(look.MarkingColor.ToVector3());
            _ambientStrength.SetValue(look.AmbientStrength);
            _canyonSkyView.SetValue(look.CanyonSkyView);
            _canyonSunView.SetValue(look.CanyonSunView);
            _facadeBounce.SetValue(look.FacadeBounce);
            _hazeDistance.SetValue(look.HazeDistance);
            _carChance.SetValue(look.CarChance);
            _treeColor.SetValue(look.TreeColor.ToVector3());
            _treeChance.SetValue(look.TreeChance);
            _neon.SetValue(neon ? 1f : 0f);
            _neonAmbient.SetValue(look.NeonAmbient.ToVector3());
            _lampColor.SetValue(look.LampColor.ToVector3());
            _lampSpacing.SetValue(look.LampSpacing);
            _neonSpill.SetValue(look.NeonSpill);
            _neonMagenta.SetValue(neonLook.Magenta.ToVector3());
            _neonCyan.SetValue(neonLook.Cyan.ToVector3());
            _neonHazeColor.SetValue(look.NeonHazeColor.ToVector3());

            //The cloud shadow falls on the street as it does on the towers; null in the map editor, which draws no weather
            frame.ApplyClouds?.Invoke(_effect);

            _device.BlendState = BlendState.Opaque;
            _device.RasterizerState = RasterizerState.CullNone;
            _device.DepthStencilState = DepthStencilState.Default;

            _device.SetVertexBuffer(_quad);
            _effect.CurrentTechnique.Passes[0].Apply();
            _device.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);

            _device.BlendState = BlendState.AlphaBlend;
            _device.RasterizerState = RasterizerState.CullCounterClockwise;
        }

        public void Dispose()
        {
            _quad.Dispose();
            _occupancy?.Dispose();
        }
    }
}
