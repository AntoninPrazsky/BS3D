using Prazsky.Core.Tools;
using System;

namespace MapEditor
{
    public static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            //What this run IS, before anything else (#372). The editor builds its shaders out of the Testbed's
            //content directory, so it is exposed to the same trap the other two are: MGCB skips an .fx whose
            //.xnb is newer and then copies nothing, and a look judged here would be a look judged through the
            //previous shader.
            BuildStamp.Report();

            using var game = new MapEditor();
            if (args != null && args.Length > 0) game.StartupFilePath = args[0];
            game.Run();
        }
    }
}
