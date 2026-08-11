using System;

namespace Testbed.Diagnostics
{
    /// <summary>
    /// The <c>switchmap=&lt;path&gt;</c> harness: after a delay, load a second map on top of the running one.
    /// It exists because that path — tear the constrained structure down, retire every shot and falling ball,
    /// refit the ceiling, resolve the camera again, rebuild — is the one the F2 dialog and a file dropped on the
    /// window take, and it is the one no still frame can check. It fires <b>once</b>; what happens after the
    /// swap is what is being looked at.
    /// </summary>
    public sealed class SwitchMapDriver
    {
        /// <summary>
        /// Long enough that the first map has settled and been looked at before the swap, so a fault after it is
        /// obviously the swap's. <c>.claude/skills/verify</c> documents the wait.
        /// </summary>
        public const float DELAY_SECONDS = 10f;

        private readonly string _path;

        //Captured once, per BestPractices.md §3 — this is asked for every frame until it fires
        private readonly Action<string> _load;

        private float _elapsed;
        private bool _done;

        public SwitchMapDriver(string path, Action<string> load)
        {
            _path = path;
            _load = load;
        }

        /// <summary>Counts down and loads once. A no-op for the rest of the run.</summary>
        public void Update(float elapsedSeconds)
        {
            if (_done) return;

            _elapsed += elapsedSeconds;
            if (_elapsed < DELAY_SECONDS) return;

            _done = true;

            //A CLI surface (.claude/skills/verify greps for it), so the wording is a contract
            Console.WriteLine($"[switchmap] Loading {_path}");

            _load(_path);
        }
    }
}
