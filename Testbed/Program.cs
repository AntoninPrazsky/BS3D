using Prazsky.Core.Tools;
using System;
using Testbed.Diagnostics;

namespace Testbed
{
    public static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            //Two lines saying what this run IS, before anything else it prints (#372): the exe's own write
            //time and hash, and the compiled shaders beside it. A capture or a measurement filed with the log
            //then carries its own proof of which shader took it, which is the thing three rounds of
            //"measurement" once could not be asked and were wrong about.
            BuildStamp.Report();

            //Every switch and its spelling is TestOptions' since #73 — this used to be fourteen locals parsed
            //here and handed over as fourteen named arguments, which is the same list written out three times.
            //The argument surface itself is unchanged and must stay so: .claude/skills/verify and
            //.claude/skills/screenshot drive this executable by those exact strings.
            using (Testbed game = new(TestOptions.Parse(args))) game.Run();
        }
    }
}
