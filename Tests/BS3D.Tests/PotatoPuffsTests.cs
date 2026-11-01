using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Render;
using System.Collections.Generic;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>The Potato path's puffs (#1044), with no device: four corners a puff at its emitter, the phases spread over a life.</summary>
    public class PotatoPuffsTests
    {
        [Fact]
        public void EveryPuffIsFourCornersAtItsEmitterItsPhasesSpread()
        {
            var emitters = new List<(Vector3, float)> { (new Vector3(1f, 2f, 3f), 1f), (new Vector3(-5f, 0f, 9f), 0.5f) };
            VertexPositionColorTexture[] quads = PotatoPuffs.Build(emitters, 10, 7);

            Assert.Equal(2 * 10 * 4, quads.Length);
            var phases = new List<float>();
            for (int i = 0; i < quads.Length; i += 4)
            {
                Vector3 at = i < 40 ? new Vector3(1f, 2f, 3f) : new Vector3(-5f, 0f, 9f);
                for (int k = 0; k < 4; k++) Assert.Equal(at, quads[i + k].Position);
                Assert.Equal(new Vector2(-1f, -1f), quads[i].TextureCoordinate);
                Assert.Equal(new Vector2(1f, 1f), quads[i + 2].TextureCoordinate);
                if (i < 40) phases.Add(quads[i].Color.R / 255f);
            }

            //The first emitter's ten phases cover its life: one in each tenth
            phases.Sort();
            for (int k = 1; k < phases.Count; k++) Assert.InRange(phases[k] - phases[k - 1], 0f, 0.2f);
            Assert.InRange(quads[40].Color.A, 127, 128);
        }
    }
}
