using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Prazsky.Core.Tools;
using System;
using System.IO;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Provides methods for rendering the orthogonal projection of a three-dimensional model.
    /// The result can be (<see cref="Texture2D"/>) or export to a file in PNG format.
    /// </summary>
    public static class BitmapRenderer
    {
        /// <summary>
        /// The default ratio between the 3D model and its bitmap orthogonal projection. A value of 100 means that 1 unit of the 3D model corresponds to 100 pixels of the bitmap.
        /// </summary>
        public const int DEFAULT_BITMAP_SCALE = 100;

        private const int MAX_BITMAP_WIDTH = 4096;

        private const int MAX_BITMAP_HEIGHT = 4096;

        /// <summary>
        /// Returns the orthogonal projection of the model as a bitmap (<see cref="Texture2D"/>).
        /// </summary>
        /// <param name="graphicsDevice">The graphics device to be used to render the model.</param>
        /// <param name="model">Three-dimensional model to render.</param>
        /// <param name="bitmapScale">The ratio of the model to its bitmap representation. The default value of 100 means that 1 model unit corresponds to 100 bitmap pixels.</param>
        /// <returns>Returns the orthogonal projection of the three-dimensional model as a bitmap.</returns>
        /// <param name="orthographicModelSize">The size of the model to render. If this parameter is not specified, it is calculated automatically.</param>
        public static Texture2D RenderOrthographic(
                GraphicsDevice graphicsDevice,
                Model model,
                int bitmapScale = DEFAULT_BITMAP_SCALE,
                Vector2 orthographicModelSize = new Vector2())
        {
            SizeFloat modelSize;
            BoundingBox box = Geometry.GetBoundingBox(model);

            if (orthographicModelSize.X == 0f || orthographicModelSize.Y == 0f)
                modelSize = CalculateModelSize(box);
            else
            {
                modelSize.X = orthographicModelSize.X;
                modelSize.Y = orthographicModelSize.Y;
            }

            SizeInt renderSize = CalculateBitmapSize(modelSize, bitmapScale);

            RenderTarget2D renderTarget = new RenderTarget2D(
                    graphicsDevice,
                    renderSize.X,
                    renderSize.Y,
                    false,
                    graphicsDevice.PresentationParameters.BackBufferFormat,
                    DepthFormat.Depth16);

            Matrix world = Matrix.Identity;

            //The position of the camera on the Z axis corresponds to the nearest point of the model
            Vector3 cameraPosition = new Vector3(0f, 0f, Math.Abs(box.Min.Z));
            Matrix view = Matrix.CreateLookAt(cameraPosition, Vector3.Zero, Vector3.Up);

            //The near clipping plane is not offset from the camera, the far clipping plane corresponds to the farthest point of the model
            float nearPlane = 0f;
            float farPlane = box.Max.Z * 2;
            Matrix projection = Matrix.CreateOrthographic(modelSize.X, modelSize.Y, nearPlane, farPlane);

            graphicsDevice.SetRenderTarget(renderTarget);
            graphicsDevice.DepthStencilState = DepthStencilState.Default; //the framework's cached state, not a fresh native-backed instance per call

            graphicsDevice.Clear(Color.Transparent);
            model.Draw(world, view, projection);

            graphicsDevice.SetRenderTarget(null);

            return renderTarget;
        }

        /// <summary>
        /// Renders the orthogonal projection of the model using the <see cref="RenderOrthographic(GraphicsDevice, Model, int, Vector2)"/> method and writes it to a PNG file.
        /// </summary>
        /// <param name="graphicsDevice">The graphics device to be used to render the model.</param>
        /// <param name="model">Three-dimensional model to render.</param>
        /// <param name="filePath">The full path to the final file (for example "C:\image.png" for Windows).</param>
        /// <param name="bitmapScale">The ratio of the model to its bitmap representation. The default value of 100 means that 1 model unit corresponds to 100 bitmap pixels.</param>
        public static void RenderOrthographicAsPNG(
                GraphicsDevice graphicsDevice,
                Model model,
                string filePath,
                int bitmapScale = DEFAULT_BITMAP_SCALE)
        {
            Texture2D bitmap = RenderOrthographic(graphicsDevice, model, bitmapScale);
            Stream stream = File.Create(filePath);
            bitmap.SaveAsPng(stream, bitmap.Width, bitmap.Height);
            stream.Dispose();
            bitmap.Dispose();
        }

        private static SizeFloat CalculateModelSize(BoundingBox modelBoundingBox)
        {
            SizeFloat calculatedSize;

            calculatedSize.X = Math.Abs(modelBoundingBox.Max.X) + Math.Abs(modelBoundingBox.Min.X);
            calculatedSize.Y = Math.Abs(modelBoundingBox.Max.Y) + Math.Abs(modelBoundingBox.Min.Y);

            if (calculatedSize.X <= 0 || calculatedSize.Y <= 0)
                throw new ArgumentException("Error calculating model size. Check the model (both height and width must be greater than 0).", nameof(modelBoundingBox));

            return calculatedSize;
        }

        private static SizeInt CalculateBitmapSize(SizeFloat modelSize, int bitmapScale)
        {
            SizeInt calculatedSize;

            calculatedSize.X = (int)(modelSize.X * bitmapScale);
            calculatedSize.Y = (int)(modelSize.Y * bitmapScale);

            if (calculatedSize.X > MAX_BITMAP_WIDTH)
                throw new ArgumentException(
                        string.Format("The width of the resulting bitmap background ({0}) exceeds the maximum allowed value ({1}). Use a smaller model or reduce the value of parameter {2} ({3}).",
                        calculatedSize.X,
                        MAX_BITMAP_WIDTH,
                        nameof(bitmapScale),
                        bitmapScale));

            if (calculatedSize.Y > MAX_BITMAP_HEIGHT)
                throw new ArgumentException(
                        string.Format("The height of the resulting bitmap background ({0}) exceeds the maximum allowed value ({1}). Use a smaller model or reduce the value of parameter {2} ({3}).",
                        calculatedSize.Y,
                        MAX_BITMAP_HEIGHT,
                        nameof(bitmapScale),
                        bitmapScale));

            return calculatedSize;
        }

        private struct SizeFloat
        {
            public float X;
            public float Y;
        }

        private struct SizeInt
        {
            public int X;
            public int Y;
        }
    }
}