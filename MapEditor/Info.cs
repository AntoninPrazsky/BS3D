using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace MapEditor
{
    /// <summary>
    /// It renders the frame rate of the graphics and other information in the form of text.
    /// </summary>
    public class Info : DrawableGameComponent
    {
        private ContentManager _content;
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;

        private int _frameRate = 0;
        private int _frameCounter = 0;
        private TimeSpan _elapsedTime = TimeSpan.Zero;

        private Vector2 _fpsPosition = new Vector2(20f, 10f);
        private Vector2 _customTextPosition = new Vector2(20f, 50f);
        private string _fps;

        public string CustomText { get; set; }

        /// <summary>
        /// Constructor of the Info class to render the display's frame rate and other information.
        /// </summary>
        /// <param name="game">A game in which information is to be rendered.</param>
        public Info(Game game) : base(game)
        {
            _content = new ContentManager(game.Services);
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _font = _content.Load<SpriteFont>("Content/Fonts/Aileron");
        }

        protected override void UnloadContent()
        {
            _content.Unload();
        }

        /// <summary>
        /// Updates current frame rate.
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
        /// Renders frame rate and other information.
        /// </summary>
        /// <param name="gameTime"></param>
        public override void Draw(GameTime gameTime)
        {
            _frameCounter++;

            _spriteBatch.Begin();
            RenderText(_fps, _fpsPosition);

            if (!string.IsNullOrEmpty(CustomText))
            {
                RenderTextScale(CustomText, _customTextPosition, 0.7f);
            }

            _spriteBatch.End();
        }

        private void RenderText(string text, Vector2 position)
        {
            _spriteBatch.DrawString(_font, text, new Vector2(position.X + 2, position.Y + 2), Color.Black);
            _spriteBatch.DrawString(_font, text, position, Color.White);
        }

        private void RenderTextScale(string text, Vector2 position, float scale)
        {
            _spriteBatch.DrawString(
                _font,
                text,
                new Vector2(position.X + (2 * scale), position.Y + (2 * scale)),
                Color.Black,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f);

            _spriteBatch.DrawString(
                _font,
                text,
                position,
                Color.White,
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f);
        }
    }
}
