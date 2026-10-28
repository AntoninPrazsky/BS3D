using Microsoft.Xna.Framework;
using Prazsky.BS3D;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.GameStructure.DataBags;
using Prazsky.Core.Render;
using Prazsky.Core.Tools;
using System;

namespace BS3D.Screens
{
    /// <summary>What the front end does about its own gun, from the <c>menugun=</c> argument (#1040).</summary>
    internal enum MenuGunMode
    {
        /// <summary>A gun stands, and the camera attends to it now and then: what a player gets.</summary>
        Normal,

        /// <summary>No gun and no pass at it: the menu as it was before #1040, for a before/after pair.</summary>
        Off,

        /// <summary>Every cycle's excursion is the pass at the gun: the only way to photograph one on a schedule.</summary>
        Pass,
    }

    /// <summary>
    /// The gun the front end stands on the island when no session's does (#1040): where a session would stand it for the
    /// preview map, loaded as a session would load it, drifting slowly about its rest aim so it reads as alive. No physics and
    /// no firing — the gun is a prop here, and the cluster over it is the preview's data.
    /// <para>
    /// <b>The same gun, not a second drawing of it.</b> The pose is <see cref="Cannon"/>'s, the hardware
    /// <see cref="CannonRig"/>'s and the rounds in the bore <see cref="Magazine"/>'s, exactly as in play; the calls are
    /// those of <c>GameplayScreen.DrawGun</c>, and the collar breathes on the played gun's own formula
    /// (<see cref="GameplayScreen.MuzzleGlowAt"/>). The rest pose comes from the same fit a session solves
    /// (<see cref="GameCameraFit.Solve"/>) at the window's aspect, over the same field and frame
    /// (<see cref="GameplayScreen.FitClusterWorldOffset"/>, <see cref="GameplayScreen.FramedTopYFor"/>), so the menu shows the
    /// gun where the level will put it. The register is the host's (<see cref="BS3DGame.MenuGun"/>): the shadow pass and the
    /// frame ask one object, and a session's own gun (<see cref="BS3DGame.StandingGun"/>), kept for Continue or live, wins.
    /// </para>
    /// <para>
    /// <b>Loaded from the map's own colours.</b> A session deals only colours still hanging
    /// (<c>GameplayScreen.RandomBallType</c>), so the five rounds in the bore are colours the preview map has. No wildcard and
    /// no cutter: those are a level's rule (<c>WildcardEvery</c>) and the front end deals none.
    /// </para>
    /// </summary>
    internal sealed class MenuGun : IStandingGun
    {
        //The idle drift: about the rest aim, slow and slight. The traverse is the wide swing (the gun slews on its wheels,
        //which scrub as a slewed gun's do) and the elevation a breath of it, on periods that do not meet, so the pair never
        //repeats in a minute. A pure function of the wall clock, so it is the same on any machine and costs a sine apiece.
        private static readonly float TRAVERSE_SWING = MathHelper.ToRadians(16f);
        private const float TRAVERSE_PERIOD_SECONDS = 19f;
        private static readonly float ELEVATION_SWING = MathHelper.ToRadians(3f);
        private const float ELEVATION_PERIOD_SECONDS = 13f;

        //Which way round the island it stands: towards -X, and the same for every map so the picker's changing the preview never
        //moves it. A session parks its gun towards +Z (behind it is the play camera), and that is where the menu's lens stands
        //after a level, while at launch it stands at +X. -X is opposite the launch lens and a quarter turn from the other, so the
        //first frames of the wide turn - the ones a player meets the menu in - show the gun across the funnel and not under the
        //lens, where an ultrawide window crops it at the foot of the frame (photographed, 3840x1600: the carriage cut off with
        //the gun 70 degrees off the lens, whole at 135). On an axis, so the fit's rest radius is the one a session gets for it.
        private static readonly float BEARING = MathF.PI;

        private readonly BS3DGame _game;
        private readonly Random _random = new();

        //Handed to the magazine once, so dealing a queue allocates no delegate
        private readonly Func<BallType> _nextType;

        private Cannon _cannon;
        private Magazine _magazine;
        private BallType[] _colours = GameplayScreen.DEFAULT_BALL_TYPES;

        //What the fit was solved for, kept so a window that changes shape re-solves it without the map
        private float _halfX, _halfZ, _floorY, _topY, _fitAspect;

        public MenuGun(BS3DGame game)
        {
            _game = game;
            _nextType = DealType;
        }

        /// <summary>This gun if it stands, or null: what the host registers and the camera looks for.</summary>
        public IStandingGun Standing => _cannon == null ? null : this;

        /// <summary>Takes the gun away: there is no preview map to stand it for.</summary>
        public void Clear()
        {
            _cannon = null;
            _magazine = null;
        }

        /// <summary>
        /// Stands the gun for <paramref name="map"/>, centred and just hung as the preview: at the rest pose the fit solves for
        /// its field, loaded with five rounds in the colours the map has. Run when a map is hung, never per frame.
        /// </summary>
        public void Place(BallsMap map)
        {
            //The session's own derivations, through their one home each: the field's floor and the glass over it
            Vector3 offset = GameplayScreen.FitClusterWorldOffset(map, out float fieldTopY);
            float ceilingCentreY = CeilingPlate.CentreYAbove(fieldTopY);

            _halfX = CeilingPlate.FootprintFor(map.StageSizeX) * Constants.HALF;
            _halfZ = CeilingPlate.FootprintFor(map.StageSizeZ) * Constants.HALF;
            _floorY = offset.Y;
            _topY = GameplayScreen.FramedTopYFor(ceilingCentreY, offset.Y);

            _cannon = GameplayScreen.NewCannon();
            _cannon.OrbitToFace(new Vector3(MathF.Cos(BEARING), 0f, MathF.Sin(BEARING)));
            Fit();

            _colours = ColoursOn(map);
            _magazine = new Magazine(_nextType);

            Console.WriteLine($"[menu] gun placed: {_cannon.RestRadius:F1} out (walk {_cannon.AdvanceMin:F1}..{_cannon.AdvanceMax:F1}),"
                + $" trunnions y {_cannon.Position.Y:F1}, barrel reach {_game.CannonRig.BarrelReach:F2}"
                + $", loaded {Describe(_magazine)} from {_colours.Length} colours on the map");
        }

        /// <summary>
        /// The fit a session solves on a level load and a resize, for this field at the window's aspect: the rest radius and its
        /// walk. Which bound decides the radius flips with the window (the camera the gun is placed in front of stands further
        /// back in a narrow one), which is why <see cref="Update"/> runs it again when the aspect moves.
        /// </summary>
        private void Fit()
        {
            float aspect = _game.Camera.AspectRatio;

            CameraFit fit = GameCameraFit.Solve(_cannon, _game.CannonRig.BarrelReach, _halfX, _halfZ, _floorY, _topY,
                GameplayScreen.GAME_FOV, aspect);

            _cannon.OrbitRadius = fit.CannonOrbitRadius;
            _cannon.SetAdvanceRange(fit.CannonMinRadius, fit.CannonMaxRadius);
            _fitAspect = aspect;
        }

        /// <summary>
        /// The idle drift, once per frame while the front end updates: re-fits to a window that has changed shape and swings the
        /// aim about its rest. Stated, not integrated — <see cref="Cannon.AimTo"/> lands on the pose it is given — so a hitched
        /// frame cannot overshoot, and the same wall-clock second is the same pose on every machine.
        /// </summary>
        public void Update()
        {
            if (_cannon == null) return;

            if (MathF.Abs(_game.Camera.AspectRatio - _fitAspect) > 1e-3f) Fit();

            float t = _game.WallClock;

            _cannon.AimTo(
                _cannon.RestElevation + ELEVATION_SWING * MathF.Sin(MathHelper.TwoPi * t / ELEVATION_PERIOD_SECONDS + 1.3f),
                TRAVERSE_SWING * MathF.Sin(MathHelper.TwoPi * t / TRAVERSE_PERIOD_SECONDS));
        }

        /// <summary>
        /// The colours a map has, in the order of the type enumeration: every matchable ball's, the census a session deals from
        /// (<c>GameplayScreen.RecountBallTypes</c>). The default four on a map with no ball in it at all.
        /// </summary>
        private static BallType[] ColoursOn(BallsMap map)
        {
            Span<bool> present = stackalloc bool[BallRenderSet.TYPE_COUNT];

            StaticBall[,,] balls = map.GetStaticBallsArray();
            XZLevel size = XZLevel.FromArray(balls);
            int count = 0;

            for (int level = 0; level < size.Level; level++)
                for (int x = 0; x < size.X; x++)
                    for (int z = 0; z < size.Z; z++)
                    {
                        StaticBall ball = balls[x, z, level];
                        if (ball == null || !BallKinds.Matchable(ball.Kind)) continue;

                        int index = (int)ball.Type - 1;
                        if (index < 0 || index >= present.Length || present[index]) continue;

                        present[index] = true;
                        count++;
                    }

            if (count == 0) return GameplayScreen.DEFAULT_BALL_TYPES;

            BallType[] colours = new BallType[count];
            int next = 0;

            for (int i = 0; i < present.Length; i++)
                if (present[i]) colours[next++] = (BallType)(i + 1);

            return colours;
        }

        private BallType DealType() => _colours[_random.Next(_colours.Length)];

        private static string Describe(Magazine magazine)
        {
            string loaded = string.Empty;

            for (int i = 0; i < Magazine.SIZE; i++) loaded += (i > 0 ? "," : "") + magazine.Peek(i);

            return loaded;
        }

        #region IStandingGun

        public Vector3 Position => _cannon.Position;

        public void CastShadow(Matrix shadowViewProjection) =>
            _game.CannonRig.DrawShadow(shadowViewProjection, _cannon.BarrelWorld(), _cannon.CarriageWorld(),
                _cannon.WheelTravel, _cannon.SlideTravel);

        public void CollectBalls(in BallDrawFrame frame)
        {
            BorePose pose = _magazine.Pose(_cannon, _game.CannonRig.PivotToFrontBall);

            for (int i = 0; i < Magazine.SIZE; i++)
            {
                Matrix world = pose.SlotWorld(i, out Vector3 position);

                //Still and unoccluded, as the played gun's rounds are (#236, #252): they do not breathe with the cluster, and
                //nothing is packed round a ball in a bore
                frame.Add(_magazine.Slot(i).Type, position, world, BallRenderSet.UNOCCLUDED, 0f, 0f, true);
            }
        }

        public void Draw()
        {
            CannonRig rig = _game.CannonRig;
            Matrix barrel = _cannon.BarrelWorld();

            rig.Draw(_game.Camera, barrel, _game.SceneEffectParams);

            //The next round's colour on the muzzle collar (#425), breathing as the played gun's does
            rig.DrawMuzzleCollar(_game.Camera, barrel, _game.SceneEffectParams,
                BasicEffectParamsProvider.GetDiffuseTintByType(_magazine.Peek()), GameplayScreen.MuzzleGlowAt(0f, _game.WallClock));

            rig.DrawCarriage(_game.Camera, _cannon.CarriageWorld(), _cannon.WheelTravel, _cannon.SlideTravel, _game.SceneEffectParams);

            //On the game's birthday the gun wears its hat here too (#230)
            _game.PartyHat?.Draw(_game.Camera, barrel, _game.SceneEffectParams);
        }

        public void DrawGlass() =>
            _game.CannonRig.DrawGlass(_game.Camera, _cannon.BarrelWorld(), _game.SceneEffectParams);

        #endregion
    }
}
