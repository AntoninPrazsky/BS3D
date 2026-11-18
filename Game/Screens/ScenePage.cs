using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Prazsky.BS3D.Levels;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;
using HorizontalAlignment = Myra.Graphics2D.UI.HorizontalAlignment;
using Label = Myra.Graphics2D.UI.Label;

namespace BS3D.Screens
{
    /// <summary>
    /// Every setting the game is played in. What the player picks here is what they then play in — it is
    /// a real scene, not a menu backdrop — so the change takes effect at once and the screen behind the panel
    /// is the preview.
    /// <para>
    /// <b>In the campaign's order, each named with its chapter (#773, the owner's call):</b> the scenes the chapters
    /// play in come first, in the order the chapters come, as "Meadow (The Meadow)"; the scenes no chapter uses follow
    /// unlabelled, in <see cref="SceneKind"/> order, so the page also says which scenes are still free for a new
    /// chapter. Read off the level set (each chapter's first level's scene), so a chapter moved or added moves the list
    /// with no edit here.
    /// </para>
    /// <para>
    /// <b>On a phone the list is a grid</b> (#1153): one column of twenty-one stood a third of the screen wide and showed eight,
    /// so the scenes stand as tiles across the width, seven to a row and three rows deep (<see cref="WideMenuLayout"/>), each
    /// the scene's name over its chapter's and tall enough for a finger. The scene in use wears the inverted slab a chosen
    /// tab does, and the pad walks the tiles in the direction pressed. A monitor keeps the list.
    /// </para>
    /// </summary>
    internal sealed class ScenePage : MenuPage
    {
        //One label per SceneKind, indexed by the enum's own value — the count and the names are
        //SceneRenderer's since #75, so a new scene reaches this list without being added to it
        private readonly Label[] _sceneLabels = new Label[SceneRenderer.SceneCount];
        private readonly Button[] _sceneButtons = new Button[SceneRenderer.SceneCount];

        //A tile's chapter line, under its name (the phone's grid, #1153); null on the list, whose caption carries both
        private readonly Label[] _chapterLabels = new Label[SceneRenderer.SceneCount];

        //Between a tile's name and its chapter's, in design units
        private const int TILE_LINE_GAP = 6;

        //The line that says a tour can be skipped (#861), the chapter flythrough's own (PlayHud's), fading as it does there
        private readonly PlayHud _skipHint;

        //The list's order and each scene's chapters (#773), worked out once per level set (OrderByChapters): the level
        //files are read for their scene, fourteen of them, which is not a thing to do on every rebuild of the tree
        private SceneKind[] _order;
        private readonly string[] _captions = new string[SceneRenderer.SceneCount];
        private readonly string[] _chapters = new string[SceneRenderer.SceneCount];
        private LevelSet _orderedFor;

        //Whether the tree up is the phone's grid (#1153): the interface drawn larger, as on a phone held close, on a screen
        //wide enough for it (UsesTheWidth); and the grid's plate, whose width the page needs
        private bool AsGrid;
        private Panel _gridPlate;

        /// <summary>
        /// The grid's plate, laid out unbounded (#1153): seven tiles across are wider than a stacked page, and on a screen
        /// narrower than the X300 Ultra's 2.2:1 <c>FitPage</c> brings the page down to fit it rather than cut a column off.
        /// </summary>
        internal override int RequiredWidth => AsGrid && _gridPlate != null ? UnboundedWidth(_gridPlate) : 0;

        /// <summary>
        /// What the page needs around the list — the heading, the note under it, the Back button and the
        /// plate's padding — so the scroller only gives up what those actually take (the level picker's
        /// figure, for the same arrangement).
        /// </summary>
        private const int LIST_SURROUNDINGS = 620;

        public ScenePage(BS3DGame game) : base(game) => _skipHint = new PlayHud(game);

        protected override Widget BuildTree()
        {
            AsGrid = UsesTheWidth;

            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading("SCENE"));

            OrderByChapters();

            if (AsGrid) column.Widgets.Add(BuildGrid());
            else
            {
                //THE LIST SCROLLS, because this page grows by one row every time a scene is added and nothing was
                //bounding it: the entries plus the heading, the note and Back ran past the bottom of a short
                //window with no way to reach what fell off. The heading, note and Back stay outside the scroller,
                //so the way back is never the thing that scrolls away.
                VerticalStackPanel list = MenuColumn();

                //Added in the list's order, which is also the order the pad walks them in; the arrays stay indexed by kind
                foreach (SceneKind scene in _order)
                {
                    int i = (int)scene;
                    _sceneButtons[i] = MenuButton(_captions[i], () => Choose(scene), out _sceneLabels[i]);
                    _chapterLabels[i] = null;
                    list.Widgets.Add(_sceneButtons[i]);
                }

                column.Widgets.Add(MenuScroll(list, LIST_SURROUNDINGS));
            }

            column.Widgets.Add(new Label
            {
                Text = "Applies at once — the menu and the game both play in it.",
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 29, 0, 29),
            });
            column.Widgets.Add(BackButton());

            Panel plate = Plate(column);
            _gridPlate = AsGrid ? plate : null;
            return ScreenRoot(plate);
        }

        /// <summary>
        /// The phone's grid (#1153): a tile a scene, in the list's order across and then down, <see cref="WideMenuLayout.SCENE_COLUMNS"/>
        /// to a row. In a scroller that must show it whole, as the level picker's chapter is (#1052): a screen too short for
        /// it lays the page out smaller rather than cut a row, and the pad's walk scrolls it all the same.
        /// </summary>
        private Widget BuildGrid()
        {
            int columns = Math.Min(WideMenuLayout.SCENE_COLUMNS, _order.Length);

            Grid grid = new()
            {
                ColumnSpacing = Scaled(WideMenuLayout.TILE_GAP),
                RowSpacing = Scaled(WideMenuLayout.TILE_GAP),
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            for (int c = 0; c < columns; c++) grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            for (int r = 0; r < WideMenuLayout.Rows(_order.Length, columns); r++) grid.RowsProportions.Add(new Proportion(ProportionType.Auto));

            //Added in the list's order, which is also the order the arrow keys walk them in (the pad walks the grid)
            for (int at = 0; at < _order.Length; at++)
            {
                Button tile = BuildTile(_order[at]);
                (int column, int row) = WideMenuLayout.RowMajor(at, columns);
                Grid.SetColumn(tile, column);
                Grid.SetRow(tile, row);
                grid.Widgets.Add(tile);
            }

            return MenuScroll(grid, LIST_SURROUNDINGS, mustFit: true);
        }

        /// <summary>
        /// One scene's tile: its name in the display face over its chapter's in the small one, both centred, the chapter's cut
        /// with an ellipsis to the tile rather than widening it (a scene two chapters share names both). A scene no chapter
        /// plays has the name alone, which is how the page says it is free for a new chapter.
        /// </summary>
        private Button BuildTile(SceneKind scene)
        {
            int i = (int)scene;

            VerticalStackPanel content = new()
            {
                Spacing = Scaled(TILE_LINE_GAP),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            _sceneLabels[i] = new Label
            {
                Text = SceneRenderer.SceneName(scene),
                Font = FontBody,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            content.Widgets.Add(_sceneLabels[i]);

            _chapterLabels[i] = new Label
            {
                Text = _chapters[i] ?? string.Empty,
                Font = FontSmall,
                TextAlign = FontStashSharp.RichText.TextHorizontalAlignment.Center,
                AutoEllipsisMethod = FontStashSharp.RichText.AutoEllipsisMethod.Character,
                AutoEllipsisString = "…",
                HorizontalAlignment = HorizontalAlignment.Center,
                Visible = _chapters[i] != null,
            };
            content.Widgets.Add(_chapterLabels[i]);

            Button tile = Game.MenuTile(content, () => Choose(scene), WideMenuLayout.SCENE_TILE_WIDTH, WideMenuLayout.SCENE_TILE_HEIGHT);

            //The chapter's line as wide as the tile inside its padding, so a long one is cut rather than widening the tile
            _chapterLabels[i].Width = tile.Width.GetValueOrDefault() - tile.Padding.Left - tile.Padding.Right;

            _sceneButtons[i] = tile;
            return tile;
        }

        /// <summary>
        /// The list's order and its captions (#773): each chapter's scene (its first level's) in campaign order, named
        /// "Scene (Chapter)" — a scene two chapters share names both — and then every scene no chapter plays in, bare, in
        /// <see cref="SceneKind"/> order. Once per level set; a level file that cannot be read leaves its chapter out
        /// rather than the page.
        /// </summary>
        private void OrderByChapters()
        {
            LevelSet set = Game.LevelSet;
            if (_order != null && ReferenceEquals(set, _orderedFor)) return;
            _orderedFor = set;

            var chapters = new List<string>[SceneRenderer.SceneCount];
            var order = new List<SceneKind>(SceneRenderer.SceneCount);

            if (set != null && set.HasBlocks)
            {
                for (int index = 0; index < set.Count; index++)
                {
                    set.BlockRange(index, out int first, out _);
                    if (first != index || !TryReadScene(set, index, out SceneKind scene)) continue;

                    (chapters[(int)scene] ??= new List<string>()).Add(set.BlockName(index));
                    if (!order.Contains(scene)) order.Add(scene);
                }
            }

            for (int i = 0; i < SceneRenderer.SceneCount; i++)
            {
                SceneKind scene = (SceneKind)i;
                if (!order.Contains(scene)) order.Add(scene);

                string name = SceneRenderer.SceneName(scene);
                _chapters[i] = chapters[i] == null ? null : string.Join(", ", chapters[i]);
                _captions[i] = chapters[i] == null ? name : $"{name} ({_chapters[i]})";
            }

            _order = order.ToArray();
        }

        private static bool TryReadScene(LevelSet set, int index, out SceneKind scene)
        {
            scene = default;

            try
            {
                string path = set.ResolvePath(index);
                if (!Level.IsLevelFile(path) || Level.Load(path).Scene is not SceneKind named) return false;

                scene = named;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void Choose(SceneKind scene)
        {
            Game.SetScene(scene);
            Refresh();

            //The inverted slab moved to the tile chosen: the host paints the slabs (EntryRestBrushFor), the cursor staying put
            if (AsGrid && IsActive) Game.RefreshNavEntries(_focused);

            //And show it (#406). The tour is the scene's own establishing flight — the very ChapterIntro a
            //chapter's opening level runs, not a second flight built to look like it — so what is reviewed
            //here is what will be shown in play. Picking a scene IS the request: the owner's ask was to be
            //able to review all of them by switching through the list, and a separate button would mean two
            //presses per scene to do what one already implies.
            //Only over the front end. Opened from the pause, a game stands between this page and the backdrop, and
            //the stack does not update a screen under a session (UpdatesUnderlying) - so a tour started there never
            //advanced, never ended, and the plate below stayed hidden for as long as it was "engaged": the owner was
            //left on the pause's blurred frame with no page and no way back into the level (#636). There the pick
            //only changes the scene, which is what it is for in the middle of a level.
            if (!InLevel) Manager?.Find<BackdropScreen>()?.PlayTour();
        }

        /// <summary>
        /// The pad's and the arrow keys' cursor arrives on the scene in use (#740), the one entry this page already marks,
        /// rather than on the top of a list of twenty-one: the walk starts where the player is.
        /// </summary>
        internal override Button NavArrival => _sceneButtons[(int)Game.Scene];

        /// <summary>The phone's grid is walked by the pad in the direction pressed (#1153), as the level picker's tiles are.</summary>
        internal override bool PadWalksGrid => AsGrid;

        //The entry the pad's cursor stands on, so a pick leaves it there
        private Button _focused;

        internal override void NavFocusChanged(Button focused) => _focused = focused;

        /// <summary>
        /// On the phone's grid the scene in use rests in the inverted slab a chosen tab and High Scores' period wear (#1153),
        /// so a choice looks the same wherever the player makes one; handed to the host, whose focus highlight repaints every
        /// entry's slab on each pass. The list keeps the shared slab and marks the scene by its type's brightness.
        /// </summary>
        internal override IBrush EntryRestBrushFor(Button entry) =>
            AsGrid && entry != null && entry == _sceneButtons[(int)Game.Scene] ? SELECTED_BRUSH : base.EntryRestBrushFor(entry);

        //Whether this page stands over a game in progress (reached from the pause) rather than over the front end
        private bool InLevel => Manager != null && Manager.Contains<GameplayScreen>();

        /// <summary>
        /// The page gets out of the way while a tour is flying (#406) and comes back when it lands. A tour
        /// watched from behind the very list that asked for it is the fault #472 fixed on the level picker,
        /// one page over — and here there is nothing to read while it runs, so the page can simply go.
        /// <para>
        /// <b>All of it, and its input with it (#861).</b> Hiding the plate alone left the focus prompt the host draws beside
        /// the cursor's entry standing over the flight, and left the page taking the walk and Enter: a second pick chose a
        /// new scene under the running tour and broke it. Standing aside, the host draws and feeds none of the page, and any
        /// skip key cuts the tour short, as a click or Space cuts the chapter's own in play — with the same hint saying so.
        /// </para>
        /// </summary>
        internal override bool StandsAside => !InLevel && Manager?.Find<BackdropScreen>()?.TourEngaged == true;

        internal override void SkipPressed()
        {
            //Said in the run log, one line a skip, as the session's skips are
            if (Manager?.Find<BackdropScreen>()?.TrySkipTour() == true) Console.WriteLine("[skip] scene tour");
        }

        internal override void DrawAside() => _skipHint.DrawSkipHintAlone(Game.PromptsForPad);

        public override void Update(Microsoft.Xna.Framework.GameTime gameTime)
        {
            base.Update(gameTime);

            //The hint's fade, on the tour's own clock and the chapter flythrough's timings (PlayHud.UpdateSkipHint)
            BackdropScreen backdrop = Manager?.Find<BackdropScreen>();
            bool running = !InLevel && backdrop != null && backdrop.TourRunning;
            _skipHint.UpdateSkipHint((float)gameTime.ElapsedGameTime.TotalSeconds, running, chapterIntro: true,
                running ? backdrop.TourElapsed : 0f, running ? backdrop.TourRemaining : 0f);
        }

        /// <summary>
        /// Marks the scene in use, so the screen says where you are as well as where you can go. Brightness,
        /// not colour: the one in use is stated white and the rest step back to grey, which reads the same over
        /// a neon city and over a snowfield.
        /// </summary>
        internal override void Refresh()
        {
            if (_sceneLabels[0] == null) return;

            for (int i = 0; i < SceneRenderer.SceneCount; i++)
            {
                bool inUse = (SceneKind)i == Game.Scene;

                //A tile (#1153): the scene in use on the inverted slab (EntryRestBrushFor) in its dark type, every other name
                //in the loud white over its chapter's in the body grey, readable on a slab over a bright sky
                if (_chapterLabels[i] != null)
                {
                    _sceneLabels[i].TextColor = inUse ? SELECTED_TEXT : BS3DGame.MENU_TEXT;
                    _chapterLabels[i].TextColor = inUse ? SELECTED_TEXT : BS3DGame.MENU_TEXT_BODY;
                }
                else _sceneLabels[i].TextColor = inUse ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;
            }
        }
    }
}
