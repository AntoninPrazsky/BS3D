using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Prazsky.Core.Tools
{
    /// <summary>
    /// Provides methods for calculating geometric objects (<see cref="BoundingBox"/>, <see cref="BoundingSphere"/>) based on a three-dimensional model (<see cref="Model"/>).
    /// </summary>
    public static class Geometry
    {
        /// <summary>
        /// Returns the axis-aligned bounding box (AABB) of the given model.
        /// </summary>
        /// <param name="model">The model to be used to calculate the bounding box.</param>
        /// <returns>The bounding box of the specified model.</returns>
        public static BoundingBox GetBoundingBox(Model model)
        {
            //The future minimum and maximum point is initialized with the inverse value for later decrease/increase
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (ModelMesh mesh in model.Meshes)
            {
                foreach (ModelMeshPart meshPart in mesh.MeshParts)
                {
                    //Sized to hold one vertex of the model part
                    int vertexStride = meshPart.VertexBuffer.VertexDeclaration.VertexStride;
                    //Total size required to store vertices
                    int vertexBufferSize = meshPart.NumVertices * vertexStride;

                    int vertexDataSize = vertexBufferSize / sizeof(float);
                    float[] vertexData = new float[vertexDataSize];
                    //Get all vertices of a model part
                    meshPart.VertexBuffer.GetData(vertexData);

                    for (int i = 0; i < vertexDataSize; i += vertexStride / sizeof(float))
                    {
                        //The X, Y, and Z coordinates of a single vertex
                        Vector3 vertex = new Vector3(vertexData[i], vertexData[i + 1], vertexData[i + 2]);
                        //Minimum and maximum point update
                        min = Vector3.Min(min, vertex);
                        max = Vector3.Max(max, vertex);
                    }
                }
            }
            return new BoundingBox(min, max);
        }

        /// <summary>
        /// Returns the sphere circumscribed by the circumscribed box of the model centered at the origin.
        /// </summary>
        /// <param name="model">The model to be used to calculate the circumscribed sphere.</param>
        /// <returns>A circumscribed sphere of the specified model centered at the origin.</returns>
        public static BoundingSphere GetBoundingSphere(Model model)
        {
            return new BoundingSphere(Vector3.Zero, Vector3.Distance(Vector3.Zero, GetBoundingBox(model).Max));
        }

        /// <summary>
        /// Returns a sphere centered at the origin and a radius equal to the distance from the origin to the specified point.
        /// </summary>
        /// <param name="pointOnSurface">A point in space to calculate the radius.</param>
        /// <returns>A sphere with the center at the origin and the calculated radius.</returns>
        public static BoundingSphere GetBoundingSphere(Vector3 pointOnSurface)
        {
            return new BoundingSphere(Vector3.Zero, Vector3.Distance(Vector3.Zero, pointOnSurface));
        }
    }
}