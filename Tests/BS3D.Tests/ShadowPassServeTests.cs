using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// <see cref="SceneRenderer.DrawShadowMaps"/> hands its backdrop <see cref="Backdrop.BeforeShadowMaps"/> every frame, and
    /// the backdrop it no longer serves gives back what it holds only while drawn (<see cref="Backdrop.Leave"/>, #1100: Mars's
    /// baked maps, 171 MiB): when the host turns to another scene, and when it stops drawing the scene's scenery. Held over
    /// <see cref="SceneRenderer.ServeShadowPass"/>, the one place the renderer decides it.
    /// </summary>
    public class ShadowPassServeTests
    {
        private sealed class CountingBackdrop : Backdrop
        {
            public int Left;

            public CountingBackdrop() : base(null) { }

            public override SceneKind Kind => SceneKind.Mars;

            public override SceneConfig Config => null;

            public override void Draw(in SceneFrame frame) { }

            public override void Leave() => Left++;
        }

        [Fact]
        public void TheSceneTheHostTurnsFromGivesBackWhatItHolds()
        {
            var mars = new CountingBackdrop();
            var sea = new CountingBackdrop();
            Backdrop served = null;

            served = SceneRenderer.ServeShadowPass(served, mars);
            served = SceneRenderer.ServeShadowPass(served, mars);
            Assert.True(mars.Left == 0, $"Mars, served two frames running, was told {mars.Left} time(s) that the pass had left it");

            served = SceneRenderer.ServeShadowPass(served, sea);
            Assert.True(mars.Left == 1, $"the host turned from Mars to the sea and Mars was told {mars.Left} time(s)");
            Assert.True(sea.Left == 0, $"the sea, the scene now served, was told {sea.Left} time(s)");

            served = SceneRenderer.ServeShadowPass(served, sea);
            Assert.True(mars.Left == 1 && sea.Left == 0, $"a second frame on the sea told Mars {mars.Left} and the sea {sea.Left} time(s)");
            Assert.Same(sea, served);
        }

        [Fact]
        public void TheSceneWhoseSceneryIsNotDrawnGivesBackWhatItHolds()
        {
            var mars = new CountingBackdrop();
            Backdrop served = SceneRenderer.ServeShadowPass(null, mars);

            //DrawShadowMaps serves no backdrop on a frame the host draws no scenery (sceneryCasts false)
            served = SceneRenderer.ServeShadowPass(served, null);
            Assert.True(mars.Left == 1, $"Mars's scenery went undrawn and it was told {mars.Left} time(s) that the pass had left it");

            served = SceneRenderer.ServeShadowPass(served, null);
            served = SceneRenderer.ServeShadowPass(served, mars);
            Assert.True(mars.Left == 1, $"frames without scenery and the scenery back told Mars {mars.Left} time(s) in all");
            Assert.Same(mars, served);
        }
    }
}
