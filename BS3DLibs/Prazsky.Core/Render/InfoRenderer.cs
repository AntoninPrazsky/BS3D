using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Prazsky.Core.Render
{
    /// <summary>
    /// Draws FPS and other text information.
    /// </summary>
    public class InfoRenderer : DrawableGameComponent
    {
        private ContentManager _content;
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;

        private int _frameRate = 0;
        private int _frameCounter = 0;
        private TimeSpan _elapsedTime = TimeSpan.Zero;

        private Vector2 _fpsPosition;
        private Vector2 _hintTextPosition;
        private Vector2 _customTextPosition;
        private string _fps;
        private readonly string _fontAssetName;
        private readonly string _iconFontAssetName;
        private SpriteFont _iconFont;

        private const float SCALE_DIVISOR = 2160f;
        private const float SHADOW_OFFSET = 2.5f;
        //On-screen icon height authored for a 2160p viewport (scaled down with everything else)
        private const float ICON_HEIGHT = 150f;
        private float _scale = 1f;

        public string CustomText { get; set; }
        public string HintText { get; set; }
        public bool ShowIcon { get; set; }

        //Glyph drawn as the game-mode indicator, taken from the icon font (e.g. Segoe MDL2 Assets "Game")
        public string IconGlyph { get; set; }

        public int CurrentFPS { get => _frameRate; }

        /// <summary>
        /// Constructor of the Info class for rendering FPS and other text information.
        /// </summary>
        /// <param name="game">Game in which information is to be rendered.</param>
        /// <param name="fontAssetName">Text font asset name (e.g. "Content/Fonts/segoeui").</param>
        /// <param name="iconFontAssetName">Optional icon font asset holding the game-mode glyph (e.g. "Content/Fonts/icons", Segoe MDL2 Assets).</param>
        public InfoRenderer(Game game, string fontAssetName, string iconFontAssetName = null) : base(game)
        {
            if (string.IsNullOrEmpty(fontAssetName))
                throw new ArgumentNullException(nameof(fontAssetName), "Font asset name needs to be provided in order to load font");

            _content = new ContentManager(game.Services);
            _fontAssetName = fontAssetName;
            _iconFontAssetName = iconFontAssetName;
        }

        public void RecomputeScale()
        {
            _scale = Game.GraphicsDevice.Viewport.Height / SCALE_DIVISOR;

            _fpsPosition = new Vector2(30f, 20f) * _scale;
            _hintTextPosition = new Vector2(30f, 150f) * _scale;
            _customTextPosition = new Vector2(450f, 20f) * _scale;
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _font = _content.Load<SpriteFont>(_fontAssetName);

            //Render characters missing from the font as '?' instead of DrawString throwing for the whole frame
            _font.DefaultCharacter ??= '?';

            if (_iconFontAssetName != null) _iconFont = _content.Load<SpriteFont>(_iconFontAssetName);

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
            if (!string.IsNullOrEmpty(CustomText)) RenderTextScale(CustomText, new Vector2(_customTextPosition.X, _customTextPosition.Y), 0.6f, Color.DarkGoldenrod);
            if (!string.IsNullOrEmpty(HintText)) RenderTextScale(HintText, new Vector2(_hintTextPosition.X, _hintTextPosition.Y), 0.7f, Color.Azure);

            if (_iconFont != null && ShowIcon && !string.IsNullOrEmpty(IconGlyph)) RenderIcon();

            _spriteBatch.End();
        }

        //Draws the game-mode glyph in the top-right corner, sized to ICON_HEIGHT and scaled to the viewport
        private void RenderIcon()
        {
            var glyphSize = _iconFont.MeasureString(IconGlyph);
            var iconScale = ICON_HEIGHT * _scale / glyphSize.Y;

            var x = GraphicsDevice.Viewport.Width - (glyphSize.X * iconScale) - (30f * _scale);
            var y = 30f * _scale;

            _spriteBatch.DrawString(_iconFont, IconGlyph, new Vector2(x + (SHADOW_OFFSET * _scale), y + (SHADOW_OFFSET * _scale)), Color.Black, 0f, Vector2.Zero, iconScale, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_iconFont, IconGlyph, new Vector2(x, y), Color.White, 0f, Vector2.Zero, iconScale, SpriteEffects.None, 0f);
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
