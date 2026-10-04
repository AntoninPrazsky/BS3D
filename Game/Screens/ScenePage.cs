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
    /// </summary>
    internal sealed class ScenePage : MenuPage
    {
        //One label per SceneKind, indexed by the enum's own value — the count and the names are
        //SceneRenderer's since #75, so a new scene reaches this list without being added to it
        private readonly Label[] _sceneLabels = new Label[SceneRenderer.SceneCount];
        private readonly Button[] _sceneButtons = new Button[SceneRenderer.SceneCount];

        //The page's own plate, kept so it can be hidden while a tour flies (#406).
        private Panel _plate;

        //The list's order and each scene's chapters (#773), worked out once per level set (OrderByChapters): the level
        //files are read for their scene, fourteen of them, which is not a thing to do on every rebuild of the tree
        private SceneKind[] _order;
        private readonly string[] _captions = new string[SceneRenderer.SceneCount];
        private LevelSet _orderedFor;

        /// <summary>
        /// What the page needs around the list — the heading, the note under it, the Back button and the
        /// plate's padding — so the scroller only gives up what those actually take (the level picker's
        /// figure, for the same arrangement).
        /// </summary>
        private const int LIST_SURROUNDINGS = 620;

        public ScenePage(BS3DGame game) : base(game) { }

        protected override Widget BuildTree()
        {
            VerticalStackPanel column = MenuColumn();
            column.Widgets.Add(ScreenHeading("SCENE"));

            //THE LIST SCROLLS, because this page grows by one row every time a scene is added and nothing was
            //bounding it: the entries plus the heading, the note and Back ran past the bottom of a short
            //window with no way to reach what fell off. The heading, note and Back stay outside the scroller,
            //so the way back is never the thing that scrolls away.
            VerticalStackPanel list = MenuColumn();

            OrderByChapters();

            //Added in the list's order, which is also the order the pad walks them in; the arrays stay indexed by kind
            foreach (SceneKind scene in _order)
            {
                int i = (int)scene;
                _sceneButtons[i] = MenuButton(_captions[i], () => Choose(scene), out _sceneLabels[i]);
                list.Widgets.Add(_sceneButtons[i]);
            }

            column.Widgets.Add(MenuScroll(list, LIST_SURROUNDINGS));

            column.Widgets.Add(new Label
            {
                Text = "Applies at once — the menu and the game both play in it.",
                Font = FontSmall,
                TextColor = BS3DGame.MENU_TEXT_DIM,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = ScaledThickness(0, 29, 0, 29),
            });
            column.Widgets.Add(MenuButton("Back", GoBack));

            _plate = Plate(column);
            return ScreenRoot(_plate);
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
                _captions[i] = chapters[i] == null ? name : $"{name} ({string.Join(", ", chapters[i])})";
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

        //Whether this page stands over a game in progress (reached from the pause) rather than over the front end
        private bool InLevel => Manager != null && Manager.Contains<GameplayScreen>();

        /// <summary>
        /// The page gets out of the way while a tour is flying (#406) and comes back when it lands. A tour
        /// watched from behind the very list that asked for it is the fault #472 fixed on the level picker,
        /// one page over — and here there is nothing to read while it runs, so the page can simply go.
        /// </summary>
        public override void Update(Microsoft.Xna.Framework.GameTime gameTime)
        {
            base.Update(gameTime);

            BackdropScreen backdrop = Manager?.Find<BackdropScreen>();
            if (_plate != null && backdrop != null) _plate.Visible = InLevel || !backdrop.TourEngaged;
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
                _sceneLabels[i].TextColor = (SceneKind)i == Game.Scene ? BS3DGame.MENU_TEXT : BS3DGame.MENU_TEXT_DIM;
        }
    }
}
