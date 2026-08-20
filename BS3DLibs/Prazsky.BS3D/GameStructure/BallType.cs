using System;

namespace Prazsky.BS3D.GameStructure
{
    public enum BallType : byte
    {
        Type1 = 1,   // red
        Type2 = 2,   // green
        Type3 = 3,   // blue
        Type4 = 4,   // white (drawn beige so its pattern reads against the white gores)
        Type5 = 5,   // cyan
        Type6 = 6,   // magenta
        Type7 = 7,   // yellow
        Type8 = 8,   // black

        // The five below joined with #152, to give bigger levels colour headroom. Each sits next to an
        // existing colour on purpose-made distance: silver is a cool slate (the white-gore trap, like white
        // and yellow above), navy is a deeper and more violet blue than Type3, olive far darker than green.
        // Navy read "far darker than blue" until #246, and that is exactly what went wrong with it: darker
        // than one neighbour had made it the same as the other one, Type8, whose difference from it lived
        // only in the blue channel. Type3 and Type12 were re-spaced together; the arithmetic is on their
        // cases in BasicEffectParamsProvider.
        Type9 = 9,   // orange
        Type10 = 10, // brown
        Type11 = 11, // silver (drawn a cool slate grey - a light silver would vanish against the white gores)
        Type12 = 12, // navy blue
        Type13 = 13, // olive green

        // The colours live in BasicEffectParamsProvider (GetDiffuseTintByType / GetEffectByType); value 0 is
        // unused (empty cells are null, not a type). Serialized as the raw byte, so a map keeps its colours.
    }

    /// <summary>
    /// The one count of how many ball colours there are, derived from the enum itself so a new member can
    /// never be forgotten by a copy (#152 found the count hand-pinned in the render set, where a member
    /// added without repointing it existed in logic and physics but was silently never drawn). The values
    /// are contiguous from 1, so the member count is also the highest value.
    /// </summary>
    public static class BallTypes
    {
        public static readonly int Count = Enum.GetValues<BallType>().Length;
    }
}
