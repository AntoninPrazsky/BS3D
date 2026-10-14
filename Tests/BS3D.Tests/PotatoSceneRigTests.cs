using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>The sky-replacing scenes' own rigs and voids for a host with no scene renderer (#829).</summary>
    public class PotatoSceneRigTests
    {
        [Fact]
        public void TheGridAnswersItsConfigsRigAndVoidAndTheSavannaDoesNot()
        {
            var config = new GridSceneConfig();

            Assert.True(PotatoSceneRig.TryGetLightRig(SceneKind.Grid, 0f, out SceneLightRig rig));
            Assert.Equal(config.Lighting.SkyAmbient.ToVector3(), rig.SkyAmbient);
            Assert.Equal(config.Lighting.GroundAmbient.ToVector3(), rig.GroundAmbient);
            Assert.Equal(config.Lighting.KeyTint.ToVector3(), rig.KeyTint);
            Assert.Equal(config.Lighting.BackTint.ToVector3(), rig.BackTint);

            Assert.True(PotatoSceneRig.TryGetVoid(SceneKind.Grid, out Vector3 void_));
            Assert.Equal(config.VoidColor.ToVector3(), void_);

            Assert.False(PotatoSceneRig.TryGetLightRig(SceneKind.Savanna, 0f, out _));
            Assert.False(PotatoSceneRig.TryGetVoid(SceneKind.Savanna, out _));
            Assert.False(PotatoSceneRig.TryGetSunDirection(SceneKind.Savanna, out _));
        }

        [Fact]
        public void EverySceneThatReplacesTheSkyHasARigAndAVoid()
        {
            foreach (SceneKind scene in System.Enum.GetValues<SceneKind>())
            {
                bool replaces = SceneRenderer.ReplacesSky(scene);
                Assert.Equal(replaces, PotatoSceneRig.TryGetLightRig(scene, 0f, out _));
                Assert.Equal(replaces, PotatoSceneRig.TryGetVoid(scene, out _));
            }
        }

        [Fact]
        public void TheMoonsSunIsLowAndUp()
        {
            Assert.True(PotatoSceneRig.TryGetSunDirection(SceneKind.Moon, out Vector3 sun));
            Assert.Equal(1f, sun.Length(), 3);
            Assert.InRange(sun.Y, 0.2f, 0.35f); //16 degrees
        }

        [Fact]
        public void ARigWithNoRendererTakesTheSceneLookup()
        {
            var rig = new SkyLightRig { SceneRig = PotatoSceneRig.TryGetLightRig, SceneSun = PotatoSceneRig.TryGetSunDirection };
            Assert.NotNull(rig.SceneRig);
            Assert.True(rig.SceneRig(SceneKind.Cavern, 0f, out SceneLightRig cavern));
            Assert.Equal(new CavernSceneConfig().Lighting.SkyAmbient.ToVector3(), cavern.SkyAmbient);
        }
    }
}
