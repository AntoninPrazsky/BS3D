using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Draws FPS and other text information.
    /// </summary>
    public class TextInfoRenderer : DrawableGameComponent
    {
        private ContentManager _content;
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;

        private int _frameRate = 0;
        private int _frameCounter = 0;
        private TimeSpan _elapsedTime = TimeSpan.Zero;

        private Vector2 _fpsPosition = new(20f, 10f);
        private Vector2 _customTextPosition = new(450f, 10f);
        private Vector2 _hintTextPosition = new(20f, 150f);
        private string _fps;
        private readonly string _fontAssetName;

        private readonly float SCALE_DIVISOR = 3840f;
        private readonly float SHADOW_OFFSET = 2.5f;
        private float _scale = 1f;

        public string CustomText { get; set; }
        public string HintText { get; set; }

        public int CurrentFPS { get => _frameRate; }

        /// <summary>
        /// Constructor of the Info class for rendering FPS and other text information.
        /// </summary>
        /// <param name="game">Game in which information is to be rendered.</param>
        /// <param name="fontAssetName">Font asset name (e.g. "Content/Fonts/cascadia").</param>
        public TextInfoRenderer(Game game, string fontAssetName) : base(game)
        {
            if (string.IsNullOrEmpty(fontAssetName))
                throw new ArgumentNullException(nameof(fontAssetName), "Font asset name needs to be provided in order to load font");

            _content = new ContentManager(game.Services);
            _fontAssetName = fontAssetName;
        }

        public void RecomputeScale()
        {
            _scale = Game.GraphicsDevice.Viewport.Width / SCALE_DIVISOR;
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _font = _content.Load<SpriteFont>(_fontAssetName);

            RecomputeScale();
        }

        protected override void UnloadContent()
        {
            _content.Unload();
        }

        /// <summary>
        /// Update of FPS.
        /// </summary>
        /// <param name="gameTime">Game time.</param>
        public override void Update(GameTime gameTime)
        {
            _elapsedTime += gameTime.ElapsedGameTime;

            if (_elapsedTime > TimeSpan.FromSeconds(1))
            {
                _elapsedTime -= TimeSpan.FromSeconds(1);
                _frameRate = _frameCounter;
                _frameCounter = 0;
            }

            _fps = "FPS: " + _frameRate.ToString();
        }

        /// <summary>
        /// Renders FPS and other information.
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Draw(GameTime gameTime)
        {
            _frameCounter++;

            _spriteBatch.Begin();

            RenderText(_fps, _fpsPosition);
            if (!string.IsNullOrEmpty(CustomText)) RenderTextScale(CustomText, new Vector2(_customTextPosition.X * _scale, _customTextPosition.Y), 0.6f, Color.DarkGoldenrod);
            if (!string.IsNullOrEmpty(HintText)) RenderTextScale(HintText, new Vector2(_hintTextPosition.X, _hintTextPosition.Y * _scale), 0.7f, Color.Azure);

            _spriteBatch.End();
        }

        private void RenderText(string text, Vector2 position)
        {
            _spriteBatch.DrawString(_font, text, new Vector2(position.X + (SHADOW_OFFSET * _scale), position.Y + (SHADOW_OFFSET * _scale)), Color.Black, 0f, Vector2.Zero, _scale, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, text, position, Color.DarkGray, 0f, Vector2.Zero, _scale, SpriteEffects.None, 0);
        }

        private void RenderTextScale(string text, Vector2 position, float scale, Color color)
        {
            var customScale = _scale * scale;

            _spriteBatch.DrawString(_font, text, new Vector2(position.X + (SHADOW_OFFSET * _scale), position.Y + (SHADOW_OFFSET * _scale)), Color.Black, 0f, Vector2.Zero, customScale, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, text, position, color, 0f, Vector2.Zero, customScale, SpriteEffects.None, 0f);
        }
    }
}
