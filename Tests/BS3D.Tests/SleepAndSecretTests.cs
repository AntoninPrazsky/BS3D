using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameObjects;
using Prazsky.BS3D.Input;
using Prazsky.BS3D.Scoring;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// #230's two easter eggs, on the terms that make them free: the sleeping gun's sag moves what is DRAWN and
    /// never where a shot goes, and the secret shot code fires on its pattern and on nothing else.
    /// </summary>
    public class SleepAndSecretTests
    {
        /// <summary>
        /// <c>Cannon.Droop</c> lowers the drawn bore and the drawn muzzle, and leaves the aim and the true muzzle —
        /// everything a shot is fired along — exactly where they were. At zero the drawn bore IS the aim.
        /// </summary>
        [Fact]
        public void DroopMovesTheDrawnBarrelAndNeverTheAim()
        {
            Cannon cannon = new(new Vector3(0f, 5f, 0f));
            cannon.AimTarget = cannon.Position + new Vector3(0f, 6f, -10f);

            Vector3 aim = cannon.AimDirection;
            Vector3 muzzle = cannon.MuzzlePosition(2f);

            //Awake: the barrel's forward (the mesh's muzzle is local -Z, CreateWorld's forward) is the aim
            Assert.True(Vector3.Distance(cannon.BarrelWorld().Forward, aim) < 1e-5f);
            Assert.True(Vector3.Distance(cannon.DrawnMuzzlePosition(2f), muzzle) < 1e-5f);

            cannon.Droop = 0.3f;

            //The shot's line has not moved
            Assert.Equal(aim, cannon.AimDirection);
            Assert.Equal(muzzle, cannon.MuzzlePosition(2f));

            //The drawn one has, downwards, by the droop, and in the aim's own vertical plane
            Vector3 drawn = cannon.BarrelWorld().Forward;
            Assert.True(drawn.Y < aim.Y);
            Assert.Equal(0.3f, System.MathF.Acos(MathHelper.Clamp(Vector3.Dot(drawn, aim), -1f, 1f)), 3);
            Assert.True(System.MathF.Abs(Vector3.Dot(drawn, Vector3.Normalize(Vector3.Cross(aim, Vector3.Up)))) < 1e-5f);
            Assert.True(cannon.DrawnMuzzlePosition(2f).Y < muzzle.Y);
        }

        /// <summary>Three misses, two landings, three misses — and only that, and only once per completion.</summary>
        [Fact]
        public void TheShotCodeFiresOnItsPatternAlone()
        {
            SecretShotCode code = new();
            bool[] pattern = { false, false, false, true, true, false, false, false };

            for (int i = 0; i < pattern.Length - 1; i++) Assert.False(code.Record(pattern[i]));
            Assert.True(code.Record(pattern[^1]));

            //Right after firing it starts again from nothing: one more miss completes no second code
            Assert.False(code.Record(false));

            //A near miss of the pattern (two landings where one is wanted) never fires
            code.Reset();
            bool[] wrong = { false, false, false, true, false, false, false, false };
            foreach (bool shot in wrong) Assert.False(code.Record(shot));

            //And the pattern completes on the last eight shots whatever came before them
            code.Reset();
            foreach (bool shot in new[] { true, true, false }) code.Record(shot);
            for (int i = 0; i < pattern.Length - 1; i++) Assert.False(code.Record(pattern[i]));
            Assert.True(code.Record(pattern[^1]));
        }

        /// <summary>
        /// The main menu's Konami code completes on its final A and nowhere earlier, survives a menu walk before it,
        /// and does not complete with one press missing — the same window the shot code runs on (<c>SecretCode</c>).
        /// </summary>
        [Fact]
        public void TheKonamiCodeFiresOnItsSequenceAlone()
        {
            KonamiKey[] konami =
            {
                KonamiKey.Up, KonamiKey.Up, KonamiKey.Down, KonamiKey.Down,
                KonamiKey.Left, KonamiKey.Right, KonamiKey.Left, KonamiKey.Right, KonamiKey.B, KonamiKey.A,
            };

            KonamiCode code = new();

            //A player walking the menu first, then the code: only its last press completes it
            foreach (KonamiKey key in new[] { KonamiKey.Down, KonamiKey.Down, KonamiKey.A }) code.Record(key);
            for (int i = 0; i < konami.Length - 1; i++) Assert.False(code.Record(konami[i]));
            Assert.True(code.Record(konami[^1]));

            //Once found it forgets: one more A completes nothing
            Assert.False(code.Record(KonamiKey.A));

            //One right short never fires
            code.Reset();
            foreach (KonamiKey key in new[]
            {
                KonamiKey.Up, KonamiKey.Up, KonamiKey.Down, KonamiKey.Down,
                KonamiKey.Left, KonamiKey.Right, KonamiKey.Left, KonamiKey.B, KonamiKey.A,
            })
                Assert.False(code.Record(key));
        }
    }
}
