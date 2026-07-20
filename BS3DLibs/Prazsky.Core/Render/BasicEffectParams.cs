using Microsoft.Xna.Framework;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Represents the lighting effects parameters for the <see cref="Microsoft.Xna.Framework.Graphics.BasicEffect"/> class.
    /// </summary>
    public class BasicEffectParams
	{
        /// <summary>
        /// Light effect parameters.
        /// </summary>
        /// <param name="ambientLightColor">Ambient light color.</param>
        /// <param name="specularColor">Specular color.</param>
        /// <param name="specularPower">Specular power.</param>
        /// <param name="emmisiveColor">The color of the emitted light.</param>
        /// <param name="directionalLight0">Parameters of the first directional light.</param>
        /// <param name="directionalLight1">Parameters of the second directional light.</param>
        /// <param name="directionalLight2">Parameters of the third directional light.</param>
        /// <param name="fog">Fog effect parameters.</param>
        public BasicEffectParams(
				Vector3 ambientLightColor,
				Vector3 specularColor,
				float specularPower,
				Vector3 emmisiveColor,
				DirectionalLightParams directionalLight0 = null,
				DirectionalLightParams directionalLight1 = null,
				DirectionalLightParams directionalLight2 = null,
				FogParams fog = null)
		{
			AmbientLightColor = ambientLightColor;
			SpecularColor = specularColor;
			SpecularPower = specularPower;
			EmmisiveColor = emmisiveColor;
			DirectionalLight0 = directionalLight0;
			DirectionalLight1 = directionalLight1;
			DirectionalLight2 = directionalLight2;
			Fog = fog;
		}

        /// <summary>
        /// Ambient light color.
        /// </summary>
        public Vector3 AmbientLightColor { get; set; }

        /// <summary>
        /// Specular color.
        /// </summary>
        public Vector3 SpecularColor { get; set; }

        /// <summary>
        /// Specular power.
        /// </summary>
        public float SpecularPower { get; set; }

        /// <summary>
        /// The color of the emitted light.
        /// </summary>
        public Vector3 EmmisiveColor { get; set; }

        /// <summary>
        /// Parameters of the first directional light.
        /// </summary>
        public DirectionalLightParams DirectionalLight0 { get; set; }

        /// <summary>
        /// Parameters of the second directional light.
        /// </summary>
        public DirectionalLightParams DirectionalLight1 { get; set; }

        /// <summary>
        /// Parameters of the third directional light.
        /// </summary>
        public DirectionalLightParams DirectionalLight2 { get; set; }

        /// <summary>
        /// Fog effect parameters.
        /// </summary>
        public FogParams Fog { get; set; }
	}

    /// <summary>
    /// Represents directional light parameters.
    /// </summary>
    public class DirectionalLightParams
	{
        /// <summary>
        /// Directional light parameters.
        /// </summary>
        /// <param name="direction">Light direction.</param>
        /// <param name="diffuseColor">The color of the basic (diffuse) light component.</param>
        /// <param name="specularColor">The color of the light reflection.</param>
        public DirectionalLightParams(Vector3 direction, Vector3 diffuseColor, Vector3 specularColor)
		{
			Direction = direction;
			DiffuseColor = diffuseColor;
			SpecularColor = specularColor;
		}

        /// <summary>
        /// Light direction.
        /// </summary>
        public Vector3 Direction { get; set; }

        /// <summary>
        /// The color of the basic (diffuse) light component.
        /// </summary>
        public Vector3 DiffuseColor { get; set; }

        /// <summary>
        /// The color of the light reflection.
        /// </summary>
        public Vector3 SpecularColor { get; set; }
	}

    /// <summary>
    /// Represents the fog effect parameters.
    /// </summary>
    public class FogParams
	{
        /// <summary>
        /// Fog effect parameters.
        /// </summary>
        /// <param name="fogColor">The color of the fog.</param>
        /// <param name="fogStart">The beginning of fog in a 3D world as the distance from the camera (<see cref="Simulation.Camera.ICamera"/>).
        /// Objects in front of this distance are fully visible.</param>
        /// <param name="fogEnd">The end of the fog in the 3D world as the distance from the camera (<see cref="Simulation.Camera.ICamera"/>).
        /// Objects beyond this distance are completely invisible.</param>
        public FogParams(Vector3 fogColor, float fogStart, float fogEnd)
		{
			FogColor = fogColor;
			FogStart = fogStart;
			FogEnd = fogEnd;
		}

        /// <summary>
        /// The color of the fog.
        /// </summary>
        public Vector3 FogColor { get; set; }

        /// <summary>
        /// The beginning of fog in a 3D world as the distance from the camera (<see cref="Simulation.Camera.ICamera"/>).
        /// Objects in front of this distance are fully visible.
        /// </summary>
        public float FogStart { get; set; }

        /// <summary>
        /// The end of the fog in the 3D world as the distance from the camera (<see cref="Simulation.Camera.ICamera"/>).
        /// Objects beyond this distance are completely invisible.
        /// </summary>
        public float FogEnd { get; set; }
	}
}