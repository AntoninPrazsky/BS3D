using Prazsky.Core.Tools;
using System;

namespace BS3D
{
    public static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            //The run's log and its last-chance handlers (#570), before the first line is printed - see RunLog
            RunLog.Install();

            //Two lines saying what this run IS, before anything else it prints (#372) — the exe's own write
            //time and hash, and the compiled shaders beside it, so a shot or an [fps] reading filed with the
            //log carries its own proof of which shader produced it.
            BuildStamp.Report();

            //Every argument, in one object (#583): the table of spellings, kinds and the reasons each exists is
            //LaunchOptions', and it prints an [args] Ignored line for anything it did not take (#574)
            LaunchOptions options = LaunchOptions.Parse(args);

            if (ScriptedPlay.Current != null) Console.WriteLine(ScriptedPlay.Current.Describe());

            if (!string.IsNullOrWhiteSpace(options.UserDataDirectory))
            {
                UserData.UseForTesting(options.UserDataDirectory);
                Console.WriteLine($"[userdata] Testing: this run keeps the player's files in '{UserData.Directory}', not in %LOCALAPPDATA%");
            }

            //Only now, because the log lives in UserData.Directory and userdata= has to have had its say first
            RunLog.Open();

            //Anything the game throws out of its loop ends the run with a report rather than silently (#570):
            //a shipped build has no console, so without this a player's crash left nothing behind
            try
            {
                RunGame();
            }
            catch (Exception ex)
            {
                RunLog.Crash(ex, "the game loop threw");
                Environment.ExitCode = 1;
            }

            void RunGame()
            {
                using var game = new BS3DGame(options);
                game.Run();
            }
        }
    }
}
