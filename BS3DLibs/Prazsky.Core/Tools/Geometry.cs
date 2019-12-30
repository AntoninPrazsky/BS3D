using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Prazsky.Tools
{
	/// <summary>
	/// Poskytuje metody pro výpočet geometrických objektů (<see cref="BoundingBox"/>, <see cref="BoundingSphere"/>) na
	/// základě trojrozměrného modelu (<see cref="Model"/>).
	/// </summary>
	public static class Geometry
	{
		/// <summary>
		/// Vrátí opsaný kvádr typu AABB (axis-aligned bounding box) daného modelu.
		/// </summary>
		/// <param name="model">Model, který má být použit pro výpočet opsaného kvádru.</param>
		/// <returns>Opsaný kvádr zadaného modelu.</returns>
		public static BoundingBox GetBoundingBox(Model model)
		{
			//Budoucí minimální a maximální bod je inicializován s inverzní hodnotou pro pozdější snižování/zvyšování
			Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
			Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

			foreach (ModelMesh mesh in model.Meshes)
			{
				foreach (ModelMeshPart meshPart in mesh.MeshParts)
				{
					//Velikost pro uložení jednoho vrcholu části modelu
					int vertexStride = meshPart.VertexBuffer.VertexDeclaration.VertexStride;
					//Celková velikost nutná pro uložení vrcholů
					int vertexBufferSize = meshPart.NumVertices * vertexStride;

					int vertexDataSize = vertexBufferSize / sizeof(float);
					float[] vertexData = new float[vertexDataSize];
					//Získání všech vrcholů části modelu
					meshPart.VertexBuffer.GetData(vertexData);

					for (int i = 0; i < vertexDataSize; i += vertexStride / sizeof(float))
					{
						//Souřadnice X, Y a Z jednoho vrcholu
						Vector3 vertex = new Vector3(vertexData[i], vertexData[i + 1], vertexData[i + 2]);
						//Aktualizace minimálního a maximálního bodu
						min = Vector3.Min(min, vertex);
						max = Vector3.Max(max, vertex);
					}
				}
			}
			return new BoundingBox(min, max);
		}

		/// <summary>
		/// Vrátí sféru opsanou opsanému kvádru modelu se středem v počátku.
		/// </summary>
		/// <param name="model">Model, který má být použit pro výpočet opsané sféry.</param>
		/// <returns>Opsaná sféra zadaného modelu se středem v počátku.</returns>
		public static BoundingSphere GetBoundingSphere(Model model)
		{
			return new BoundingSphere(Vector3.Zero, Vector3.Distance(Vector3.Zero, GetBoundingBox(model).Max));
		}

		/// <summary>
		/// Vrátí sféru se středem v počátku a poloměrem odpovídajícím vzdálenosti od počátku k zadanému bodu.
		/// </summary>
		/// <param name="pointOnSurface">Bod v prostoru pro výpočet poloměru.</param>
		/// <returns>Sféra se středem v počátku a vypočteným poloměrem.</returns>
		public static BoundingSphere GetBoundingSphere(Vector3 pointOnSurface)
		{
			return new BoundingSphere(Vector3.Zero, Vector3.Distance(Vector3.Zero, pointOnSurface));
		}
	}
}