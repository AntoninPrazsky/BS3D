using Prazsky.Core.Camera;
using Prazsky.Core.Render;
using System;

namespace BS3D.Audio
{
    /// <summary>
    /// Everything the game hears, and the pad it shakes, as one owned object (#583): the effects, the music, the
    /// About page's player, the scene's bed and its one-shots, the rumble mixer — and the policy that used to be
    /// spread through <c>BS3DGame.Update</c> and <c>BS3DGame.Settings.cs</c>: which music the moment wants, the
    /// fireworks giving way to the fanfare, the music stepping aside for the About page's player, and the one
    /// place the player's gains reach the audio.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>It outlives every session</b>, as each of its parts did on the host: a track that restarted from the top
    /// on every retry would be exhausting, the scene's sound runs pause included, and a celebration goes on over
    /// a frozen gameplay screen. Built once in <c>LoadContent</c> with the scene already picked, disposed in
    /// <c>UnloadContent</c>.
    /// </para>
    /// <para>
    /// <b>The host still says when, and the order within a frame is the host's</b>, exactly as it was: the
    /// listener is posed before anything can make a noise (<see cref="UpdateListener"/>, above the fireworks),
    /// the music, the beds and the hand-over run after the celebrations have advanced (<see cref="Update"/>), and
    /// the pad decays after the stack (<see cref="UpdateRumble"/>). Three calls rather than one because the
    /// fireworks, which make sound through <see cref="Sfx"/>, sit between the first two.
    /// </para>
    /// <para>
    /// What the session and the pages play is still asked of the parts directly (<see cref="Sfx"/>,
    /// <see cref="Music"/>, <see cref="Jukebox"/>, <see cref="Rumble"/>): a shot's sound, a theme's lifecycle and a
    /// fanfare are events the session owns, and routing each through here would be forwarding, not policy.
    /// </para>
    /// </remarks>
    internal sealed class AudioDirector : IDisposable
    {
        //Procedurally synthesized SFX (shot, landing). Shared by the gameplay screen and the menu — same
        //pattern as the camera.
        private readonly ProceduralAudio _audio;

        //The pad's own answer to a shot (#378). Built alongside the audio, for the same reason: the per-event
        //paths only ever call Kick, never touch GamePad themselves.
        private readonly GamepadRumble _rumble;

        //The music — the level themes, the menu loop and the fanfares. Outlives any one session, because a
        //track that restarted from the top on every retry would be exhausting.
        private readonly GameMusic _music;

        //The About page's player of the original procedural score (#443). Here rather than on the page, like
        //the music: it has to be walked every frame and let go of cleanly when the game closes.
        private readonly ProceduralJukebox _jukebox;

        //Whether the front end's music is on — the edge detector for the stack question in Update (#46).
        private bool _menuMusicOn;

        //The scenes' ambient beds (#46): one looping texture per backdrop, crossfaded by SetScene. The scene
        //is the host's, and its sound runs pause included.
        private readonly ProceduralAmbience _ambience;

        //The scene's own one-shots — the storm's thunder and the volcano's boom (#219, #223). Beside the bed
        //and NOT inside it: a bed is a sealed loop and an event baked into one is a metronome.
        private readonly SceneEventSounds _sceneEvents;

        //The player's rows, read through to the settings file (#583) — what ApplyVolumes turns into gains
        private readonly EffectiveSettings _settings;

        /// <summary>
        /// Builds every part. The SFX are synthesized from raw PCM here, once, so the per-event paths only ever
        /// play a buffer — no asset files, no pipeline step. The music's constructor only starts reading the
        /// tracks off disk, on background threads, while the player is still looking at the splash (see
        /// <see cref="GameMusic"/>); the About page's player renders nothing until it is asked to.
        /// </summary>
        /// <param name="scene">
        /// The scene already picked. It was picked before the audio existed (<c>SetScene</c> runs early in
        /// <c>LoadContent</c>), so the pick is handed over here; every later change reaches the bed through
        /// <see cref="SetScene"/> like everything else scenic.
        /// </param>
        /// <param name="settings">The player's rows, applied at once — a muted start has to reach the freshly made parts.</param>
        internal AudioDirector(SceneKind scene, EffectiveSettings settings)
        {
            _settings = settings;

            _audio = new ProceduralAudio();

            //The pad's own mixer (#378) — no device or content dependency, built here purely to sit beside the
            //audio it answers alongside
            _rumble = new GamepadRumble();

            _music = new GameMusic();
            _jukebox = new ProceduralJukebox();

            _ambience = new ProceduralAmbience();
            _sceneEvents = new SceneEventSounds(_audio);
            _ambience.SetScene(scene);

            //A muted start (the mute argument) has to reach the freshly made subsystems; every later change
            //comes through the settings rows
            ApplyVolumes();
        }

        /// <summary>Procedurally generated SFX, shared by the gameplay screen and the menu.</summary>
        internal ProceduralAudio Sfx => _audio;

        /// <summary>The music: generated loops for the levels and the menu (#443), and the procedural fanfares.</summary>
        internal GameMusic Music => _music;

        /// <summary>The About page's player of the original procedural score (#443).</summary>
        internal ProceduralJukebox Jukebox => _jukebox;

        /// <summary>The pad's two body motors (#378), fed a <c>Kick</c> from wherever a violent moment already is.</summary>
        internal GamepadRumble Rumble => _rumble;

        /// <summary>
        /// The ears, before anything can make a noise this frame. Called at the top of the host's frame and
        /// unconditionally: sound is made with no session standing — a whole celebration of it over a frozen
        /// gameplay screen, and the menu's own clicks over an orbiting backdrop — so the listener cannot belong
        /// to the session either. There is one camera for the whole process, so there is one listener, valid
        /// across every level rebuild.
        /// <para>
        /// It is posed from the pose the previous frame was DRAWN from (the stack poses the camera further
        /// down), which is exactly the staleness the panning it replaces already had: half a unit at sixty
        /// frames a second. Nothing is gained by moving it, and correctness would be lost.
        /// </para>
        /// </summary>
        internal void UpdateListener(ICamera camera) => _audio.UpdateListener(camera);

        /// <summary>
        /// The music, the beds and the hand-over, once a frame after the celebrations have advanced and before
        /// the stack updates — see the remarks on the class for why the frame is cut here.
        /// </summary>
        /// <param name="elapsed">The frame's own time: the fades and the crossfades move on it.</param>
        /// <param name="scene">The scene on screen, for its one-shots.</param>
        /// <param name="scenes">The renderer that stages the scene's events (the lightning the thunder answers).</param>
        /// <param name="wallClock">The clock the scene draws from.</param>
        /// <param name="onFrontEnd">True while no gameplay screen is on the stack.</param>
        /// <param name="sessionBuilt">Whether a level is standing, which is when returning to it re-wants its theme.</param>
        internal void Update(float elapsed, SceneKind scene, SceneRenderer scenes, float wallClock, bool onFrontEnd, bool sessionBuilt)
        {
            //The music's feed: the sounding loop is queued again before the current pass ends, so the repeat is
            //seamless (see GameMusic.Update). Up with the fireworks and for the same reason — it has to keep
            //running whatever is on the stack. It takes the frame's own time since #211: the fades move on it.
            _music.Update(elapsed);

            //And the About page's player, whose held piece is what the game's own music steps aside for — asked
            //every frame rather than told on a click, so a render landing, a pause or the page closing all reach
            //the music without anyone having to remember to say so (#443).
            _jukebox.Update(elapsed);
            _music.Yielding = _jukebox.HoldsPiece;

            //The scene's bed and its crossfade, on the wall clock's frame like the clouds: the scene is on
            //screen whether or not a session stands, so its sound is too, pause included.
            _ambience.Update(elapsed);

            //Right after the bed, on the same wall clock, and for the same reason it is: the scene stages its
            //events whether or not a session stands, so a strike seen from the pause menu is heard from it.
            //The clock handed over is the one the SCENE draws from (BuildSceneFrame), which is what lets the
            //flash and its thunder be one event rather than two schedules that drift.
            _sceneEvents.Update(scene, scenes, wallClock, elapsed);

            //Which music the moment wants is the stack question (#46): the front end's loop plays exactly
            //while no session screen is on it. The theme's own lifecycle stays the session's — BuildLevel
            //starts it, TearDown and the level's endings stop it — this only closes the one gap that had no
            //owner: leaving to the main menu keeps the session but must not keep its music ("it plays while a
            //level is being played", docs/game-feedback.md), and Continue re-wants the theme because it comes
            //back WITHOUT a BuildLevel. A fresh build's own Play a moment later is the "already sounding"
            //no-op, so the two writers cannot fight. Both directions are a REPLACEMENT, so both take the
            //fading stop (#211): the theme leaves under the loop's held pads, the loop under the theme's
            //prelude — the level endings' dead stop stays the endings' own, where the silence is the message.
            if (onFrontEnd != _menuMusicOn)
            {
                _menuMusicOn = onFrontEnd;

                if (onFrontEnd)
                {
                    _music.FadeOut();
                    _music.PlayMenu();
                }
                else
                {
                    _music.StopMenu();
                    if (sessionBuilt) _music.Play();
                }
            }

            //The fireworks give way to the fanfare. Both arrive on the frame a level ends, and a report is
            //broadband and loud enough to bury a tune under it — the bang is an event, the fanfare is the
            //point. Read per frame rather than latched, so the ducking lifts by itself when the piece ends.
            _audio.FireworkDuck = _music.IsFanfarePlaying ? ProceduralAudio.FIREWORK_DUCKED : 1f;
        }

        /// <summary>
        /// The pad's own decay, and the one <c>SetVibration</c> call a frame (#378). Read fresh rather than
        /// latched: <paramref name="allowed"/> false — paused, unfocused or off the gameplay screen — silences it
        /// on the spot regardless of what a covered session's own Update is still feeding into it underneath (see
        /// <see cref="GamepadRumble.Update"/>).
        /// </summary>
        internal void UpdateRumble(float elapsed, bool allowed) => _rumble.Update(elapsed, allowed);

        /// <summary>
        /// The scene's own sound follows the scene, on the one writer's rule (<c>BS3DGame.SetScene</c>) — and so
        /// does anything the OLD scene had in flight: the sound of a strike over a storm that is no longer on
        /// screen arriving over a meadow is worse than a strike going unheard (#219).
        /// </summary>
        internal void SetScene(SceneKind scene)
        {
            _ambience.SetScene(scene);
            _sceneEvents.Reset();
        }

        /// <summary>Which family is sounding, or null for Auto — read straight off the music (#279).</summary>
        internal string SoundingTrack => _music.SoundingTrack;

        /// <summary>
        /// Steps the music picker (#279) — see <c>BS3DGame.CycleMusicTrack</c>, the verb the settings row calls,
        /// for what the row is and is not. Auto leads to the first composition, and the last leads back to Auto
        /// so a stray click in the menus is one wrap from the loop it interrupted. In a level, where Auto has no
        /// loop to mean, the wrap goes straight round to the first piece again.
        /// </summary>
        internal void CycleMusicTrack()
        {
            string next = NextMusicTrack(_music.SoundingTrack);

            if (next == null)
            {
                //Exactly the handover the front end's own edge takes (see the music block in Update): the
                //theme leaves under the loop's held pads rather than being cut.
                _music.FadeOut();
                _music.PlayMenu();
            }
            else
            {
                _music.StopMenu();
                _music.SetTheme(next);

                //Needed even when SetTheme found the piece already selected: on the front end the theme's
                //chain was retired when the menus took over, so nothing is sounding for it to keep.
                _music.Play();
            }
        }

        private string NextMusicTrack(string current)
        {
            string[] families = _music.Families;
            if (families.Length == 0) return null;

            if (current == null) return families[0];

            int next = Array.IndexOf(families, current) + 1;
            if (next > 0 && next < families.Length) return families[next];

            return _menuMusicOn ? null : families[0];
        }

        /// <summary>
        /// The one place the player's gains reach the audio: effects and music each take master times their
        /// own row, so the two subsystems cannot disagree about what the master row means. The pad's row
        /// (#378) rides along here too — it answers to no master row of its own (there is nothing else it is
        /// a fraction OF), but every cycle that calls this is a click on one of these rows, so folding it in
        /// is what keeps a change taking effect the instant it is clicked, like the four beside it.
        /// </summary>
        internal void ApplyVolumes()
        {
            float master = _settings.MasterVolume;

            _audio.Gain = master * _settings.SfxVolume;
            _music.Gain = master * _settings.MusicVolume;

            //The About page's player is music too, and takes the music row
            _jukebox.Gain = master * _settings.MusicVolume;

            //The beds have a row of their own: how much atmosphere sits under the music is a taste, and
            //chaining it to the effects would turn the shot down with it.
            _ambience.Gain = master * _settings.AmbienceVolume;

            //The weather's one-shots ride the bed's row rather than the effects one (#219): thunder answers
            //nothing the player did, so a player who turned the atmosphere down has already said what they
            //think of it. See ProceduralAudio.WeatherGain.
            _audio.WeatherGain = master * _settings.AmbienceVolume;

            _rumble.Strength = _settings.RumbleStrength;
        }

        /// <summary>
        /// Lets go of every buffer and voice. The pad is not stopped here: vibration is a device state and the
        /// host says its last word to the device directly, first thing in <c>UnloadContent</c>.
        /// </summary>
        public void Dispose()
        {
            _audio.Dispose();
            _ambience.Dispose();
            _jukebox.Dispose();
            _music.Dispose();
        }
    }
}
