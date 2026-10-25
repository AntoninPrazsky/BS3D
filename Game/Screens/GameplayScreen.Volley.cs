using BS3D.Effects;
using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure;
using Prazsky.BS3D.Levels;
using Prazsky.BS3D.Physics;
using Prazsky.Core.Tools;
using System;

namespace BS3D.Screens
{
    /// <summary>
    /// The unused balls fired off and counted (#881, <see cref="SpareVolley"/>): the cleared beat's answer to "what were the balls
    /// I did not fire worth". Armed by <c>CheckLevelCleared</c> with what the keeper paid for them, begun once the drop
    /// cinematic has let the lens go, and finished when the last ball has left or the player skips; the result page waits for
    /// it because it is a camera takeover, and the beat after it is cut to what the bonus's throw needs.
    /// </summary>
    internal sealed partial class GameplayScreen
    {
        private readonly SpareVolley _spareVolley = new();

        //Armed at the clear and begun when the lens is free; and the count under way, which is what a skip and the last ball both end
        private bool _volleyPending, _volleyCounting;
        private int _volleyBalls, _volleyBonus;

        //How fast a spare ball leaves, against a shot's SHOOT_SPEED: slower, so the stream is seen arcing into the field rather
        //than gone in a frame; and the recoil a ball gives the lens, a fraction of a shot's
        private const float VOLLEY_SPEED = 90f;
        private const float VOLLEY_KICK = 0.3f;

        //What is left of the cleared beat once the count is done: the bonus read at its whole figure, thrown and counted into
        //the score (PlayHud.BONUS_AFTER_COUNT, the throw's 0.55 s, AwardZoom.BONUS_COUNT_IN), and a breath
        private const float AFTER_VOLLEY_BEAT = 1.6f;

        //The gun's buzz under each ball, a fraction of a shot's
        private const float VOLLEY_RUMBLE = 0.18f, VOLLEY_RUMBLE_SECONDS = 0.05f;

        //How long after the clear the volley waits when no drop cinematic has the lens (the Drop camera off): the released
        //group is still on its way down, and the first balls would fly through it
        private const float VOLLEY_SETTLE = 0.8f;

        private void StepSpareVolley(float elapsed)
        {
            //Once the lens is free and the world runs at game time: not under a cut's slow motion, whose hit-stop and fall
            //would slow the balls under a count running at full speed; and not in the clear's first moments without a cinematic
            if (_volleyPending && _phase == LevelPhase.ClearedBeat && !_cinematic.Engaged && !_cutStaging.Slowed
                && _clearedElapsed >= VOLLEY_SETTLE)
                BeginSpareVolley();
            if (!_spareVolley.Engaged) return;

            bool wasRunning = _spareVolley.Running;
            int due = _spareVolley.Update(elapsed);

            //A frame that owes more than one (a hitch, a slow machine, the floor's cadence at 60 Hz) puts the earlier ones where
            //they would be by now, along their flight, rather than stacking them at the muzzle; one tick for the lot
            for (int i = 0; i < due; i++) FireSpareBall((due - 1 - i) * _spareVolley.CurrentInterval);
            if (due > 0)
            {
                Game.Audio.PlayVolleyTick(_spareVolley.Balls <= 1 ? 1f : (_spareVolley.Fired - 1f) / (_spareVolley.Balls - 1f));
                _hud.CountBonus(_spareVolley.Counted);
            }
            if (wasRunning && !_spareVolley.Running) FinishSpareVolley(skipped: false);
        }

        private void BeginSpareVolley()
        {
            _volleyPending = false;
            _volleyCounting = true;

            Vector3 direction = _cannon.FireDirection;
            Vector3 muzzle = _cannon.FireMuzzle(Game.CannonRig.PivotToFrontBall);
            _spareVolley.Begin(_volleyBalls, _volleyBonus, _cannon.Position, muzzle, direction, Camera.Position, Camera.Target, GAME_FOV);

            //The figure is born now and counts from nothing as the balls leave; read for the count's length and then thrown
            _hud.BeginBonusCount(SpareVolley.Duration(_volleyBalls) - SpareVolley.TAIL);
            Console.WriteLine($"[volley] {_spareVolley.Describe()}");
        }

        //One spare ball out of the barrel: the colour at the muzzle, a ball with no listener (it lands on nothing that counts)
        //kept with the released balls, so it is drawn and culled as they are; and the shot's smear, recoil, sound and buzz,
        //smaller, with the count's tick climbing
        private void FireSpareBall(float ago)
        {
            Vector3 direction = _cannon.FireDirection;
            Vector3 muzzle = _cannon.FireMuzzle(Game.CannonRig.PivotToFrontBall);
            Vector3 velocity = direction * VOLLEY_SPEED;

            //Fired ago seconds back: where its flight has carried it since, and as fast as it falls by now
            Vector3 at = muzzle + velocity * ago + new Vector3(0f, 0.5f * Constants.EARTH_GRAVITY * ago * ago, 0f);
            velocity.Y += Constants.EARTH_GRAVITY * ago;

            _fallingBalls.Add(new PhysicsBall
            {
                BallReference = _world.AddLooseBall(at.ToNumerics(), velocity.ToNumerics()),
                Type = LoadedColour(0),
                Kind = BallKind.Normal,
            });

            _smears.Add(muzzle, direction, LoadedTint(0));
            _magazine.Advance();
            _cannon.KickRecoil();
            Camera.Shake.Kick(VOLLEY_KICK);
            Game.Rumble.Kick(VOLLEY_RUMBLE, 0f, VOLLEY_RUMBLE_SECONDS);
            Game.Audio.PlayShoot(muzzle);
        }

        /// <summary>The count done, by its last ball or a skip: the whole figure up and on its way, and the beat cut to the throw.</summary>
        private void FinishSpareVolley(bool skipped)
        {
            if (!_volleyCounting) return;
            _volleyCounting = false;

            if (skipped) _hud.CutBonusCount(_volleyBonus);
            else _hud.CountBonus(_volleyBonus);

            _clearedCountdown = MathF.Min(_clearedCountdown, AFTER_VOLLEY_BEAT);
            Console.WriteLine($"[volley] {(skipped ? "skipped" : "done")}: {_spareVolley.Launched} of {_volleyBalls} fired, counted {_volleyBonus}");
        }
    }
}
