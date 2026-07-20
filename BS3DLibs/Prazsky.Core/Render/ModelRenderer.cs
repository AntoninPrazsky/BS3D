using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Camera;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Used to render a three-dimensional model.
    /// </summary>
    public static class ModelRenderer
    {
        /// <summary>
        /// Renders a three-dimensional model based on the provided parameters.
        /// </summary>
        /// <param name="model">Three-dimensional model to render.</param>
        /// <param name="transformations">Model transformation.</param>
        /// <param name="camera">A camera that looks at the resulting rendering of the model.</param>
        /// <param name="world">The world matrix to use for rendering.</param>
        /// <param name="basicEffectParams">Parameters for the <see cref="BasicEffect"/> class.</param>
        /// <param name="enableDefaultLighting">Enable three-point lighting, which is defined by the MonoGame framework.</param>
        /// <param name="preferPerPixelLighting">Enable calculation of illumination for each pixel, if supported by the graphics hardware.</param>
        public static void Render(
                Model model,
                Matrix[] transformations,
                ref ICamera camera,
                Matrix world,
                BasicEffectParams basicEffectParams,
                bool enableDefaultLighting,
                bool preferPerPixelLighting)
        {
            foreach (ModelMesh mesh in model.Meshes)
            {
                foreach (BasicEffect effect in mesh.Effects)
                {
                    effect.PreferPerPixelLighting = preferPerPixelLighting;

                    if (enableDefaultLighting) effect.EnableDefaultLighting();

                    if (basicEffectParams != null)
                    {
                        effect.LightingEnabled = true;

                        #region Apply light parameters (if set)

                        if (basicEffectParams.AmbientLightColor != Vector3.Zero) effect.AmbientLightColor = basicEffectParams.AmbientLightColor;
                        if (basicEffectParams.SpecularColor != Vector3.Zero)
                        {
                            effect.SpecularColor = basicEffectParams.SpecularColor;
                            effect.SpecularPower = basicEffectParams.SpecularPower;
                        }
                        if (basicEffectParams.EmmisiveColor != Vector3.Zero) effect.EmissiveColor = basicEffectParams.EmmisiveColor;

                        if (basicEffectParams.DirectionalLight0 != null)
                        {
                            effect.DirectionalLight0.Direction = basicEffectParams.DirectionalLight0.Direction;
                            effect.DirectionalLight0.DiffuseColor = basicEffectParams.DirectionalLight0.DiffuseColor;
                            effect.DirectionalLight0.SpecularColor = basicEffectParams.DirectionalLight0.SpecularColor;
                            effect.DirectionalLight0.Enabled = true;
                        }

                        if (basicEffectParams.DirectionalLight1 != null)
                        {
                            effect.DirectionalLight1.Direction = basicEffectParams.DirectionalLight1.Direction;
                            effect.DirectionalLight1.DiffuseColor = basicEffectParams.DirectionalLight1.DiffuseColor;
                            effect.DirectionalLight1.SpecularColor = basicEffectParams.DirectionalLight1.SpecularColor;
                            effect.DirectionalLight1.Enabled = true;
                        }

                        if (basicEffectParams.DirectionalLight2 != null)
                        {
                            effect.DirectionalLight2.Direction = basicEffectParams.DirectionalLight2.Direction;
                            effect.DirectionalLight2.DiffuseColor = basicEffectParams.DirectionalLight2.DiffuseColor;
                            effect.DirectionalLight2.SpecularColor = basicEffectParams.DirectionalLight2.SpecularColor;
                            effect.DirectionalLight2.Enabled = true;
                        }

                        #endregion Apply light parameters (if set)

                        #region Apply fog effect (if set)

                        if (basicEffectParams.Fog != null)
                        {
                            effect.FogColor = basicEffectParams.Fog.FogColor;
                            effect.FogStart = basicEffectParams.Fog.FogStart;
                            effect.FogEnd = basicEffectParams.Fog.FogEnd;
                            effect.FogEnabled = true;
                        }

                        #endregion Apply fog effect (if set)
                    }

                    effect.World = transformations[mesh.ParentBone.Index] * world;
                    effect.View = camera.View;
                    effect.Projection = camera.Projection;
                }
                mesh.Draw();
            }
        }
    }
}