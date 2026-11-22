using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MapEditor.GUI
{
    /// <summary>
    /// The map editor's key help on screen (#1209): <see cref="EditorKeyHelp"/>'s lines in groups on a dark panel
    /// at the top left, laid out by <see cref="KeyHelpLayout"/> so it fits the window at any size. Folded
    /// (<see cref="Expanded"/> false, the H key) it keeps the file keys and the H line alone.
    /// <para>
    /// The editor's own, beside the shared <c>InfoRenderer</c>, which still draws the FPS line and the status line:
    /// the Testbed's and the Game's overlay is left exactly as it was. The layout is recomputed only when the
    /// window's size or the fold changes, so a frame draws cached strings at cached positions and allocates nothing.
    /// </para>
    /// </summary>
    public sealed class KeyHelpOverlay : DrawableGameComponent
    {
        private static readonly Color HEADING_COLOR = new(255, 196, 72);
        private static readonly Color KEY_COLOR = Color.White;
        private static readonly Color TEXT_COLOR = new(198, 216, 232);

        //The panel under the text: dark and see-through, so the scene behind still reads and the text reads over a
        //bright sky or a lit facade, where the old block's shadow alone did not
        private static readonly Color PANEL_COLOR = Color.Black * 0.55f;

        //The text's drop shadow, in line heights (InfoRenderer's 2.5 design units against the font's line spacing)
        private const float SHADOW_OFFSET = 0.03f;

        private readonly string _fontAssetName;
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;
        private Texture2D _pixel;

        private KeyHelpLayout _layout = KeyHelpLayout.Empty;
        private int _laidOutWidth, _laidOutHeight;
        private bool _expanded = true;
        private bool _stale = true;

        /// <param name="game">The editor.</param>
        /// <param name="fontAssetName">The text font, through the game's content manager (e.g. "Fonts/segoeui").</param>
        public KeyHelpOverlay(Game game, string fontAssetName) : base(game)
        {
            _fontAssetName = fontAssetName;
        }

        /// <summary>All the keys (true, how the editor opens) or only the file keys and H (false).</summary>
        public bool Expanded
        {
            get => _expanded;
            set
            {
                if (_expanded == value) return;
                _expanded = value;
                _stale = true;
            }
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _font = Game.Content.Load<SpriteFont>(_fontAssetName);
            _font.DefaultCharacter ??= '?';

            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        protected override void UnloadContent()
        {
            _pixel?.Dispose();
            _spriteBatch?.Dispose();
        }

        public override void Draw(GameTime gameTime)
        {
            Viewport viewport = GraphicsDevice.Viewport;
            if (_stale || viewport.Width != _laidOutWidth || viewport.Height != _laidOutHeight)
            {
                _layout = KeyHelpLayout.Compute(EditorKeyHelp.Visible(_expanded), MeasureWidth, _font.LineSpacing,
                    viewport.Width, viewport.Height);
                _laidOutWidth = viewport.Width;
                _laidOutHeight = viewport.Height;
                _stale = false;
            }

            if (_layout.Items.Length == 0) return;

            float scale = _layout.Scale;
            Vector2 shadow = new(SHADOW_OFFSET * _font.LineSpacing * scale);

            _spriteBatch.Begin();

            _spriteBatch.Draw(_pixel, _layout.PanelPosition, null, PANEL_COLOR, 0f, Vector2.Zero, _layout.PanelSize,
                SpriteEffects.None, 0f);

            foreach (KeyHelpItem item in _layout.Items)
            {
                Color color = item.Piece switch
                {
                    KeyHelpPiece.Heading => HEADING_COLOR,
                    KeyHelpPiece.Key => KEY_COLOR,
                    _ => TEXT_COLOR,
                };

                _spriteBatch.DrawString(_font, item.Text, item.Position + shadow, Color.Black, 0f, Vector2.Zero, scale,
                    SpriteEffects.None, 0f);
                _spriteBatch.DrawString(_font, item.Text, item.Position, color, 0f, Vector2.Zero, scale,
                    SpriteEffects.None, 0f);
            }

            _spriteBatch.End();
        }

        private float MeasureWidth(string text) => _font.MeasureString(text).X;
    }
}
