using System;
using Testbed.Diagnostics;

namespace Testbed
{
    public static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            //Every switch and its spelling is TestOptions' since #73 — this used to be fourteen locals parsed
            //here and handed over as fourteen named arguments, which is the same list written out three times.
            //The argument surface itself is unchanged and must stay so: .claude/skills/verify and
            //.claude/skills/screenshot drive this executable by those exact strings.
            using (Testbed game = new(TestOptions.Parse(args))) game.Run();
        }
    }
}
