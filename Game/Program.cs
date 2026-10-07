using BS3D.Platform;
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

            //"potato" (#808): the Pi's renderer for this run. Here, before the game object exists, because which
            //renderer a run is gets asked from its first line on (BS3DGame.PotatoPath) - and a fourth [build] line,
            //for the effects such a run draws with are not the ones the third line fingerprinted
            if (options.Potato && QualityLock.HoldAtPotato()) BuildStamp.ReportShaderOverlay(PotatoContent.OVERLAY);

            if (ScriptedPlay.Current != null) Console.WriteLine(ScriptedPlay.Current.Describe());

            if (!string.IsNullOrWhiteSpace(options.UserDataDirectory))
            {
                UserData.UseForTesting(options.UserDataDirectory);
                Console.WriteLine($"[userdata] Testing: this run keeps the player's files in '{UserData.Directory}', not in %LOCALAPPDATA%");
            }

            //A player who chose Potato on the Quality row (#808): the same renderer as the argument's, read from the file
            //here because the renderer is chosen before the game object exists, and only here because userdata= has to
            //have said where the file lives. The line outranks the file, as quality= outranks a stored tier.
            if (!options.Potato && options.Quality == null
                && GameSettings.ChoosesPotato(UserData.PathTo(GameSettings.DefaultFileName)) && QualityLock.ChooseAtPotato())
                BuildStamp.ReportShaderOverlay(PotatoContent.OVERLAY);

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
