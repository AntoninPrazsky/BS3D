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
        // and yellow above), navy is far darker than blue, olive far darker than green.
        Type9 = 9,   // orange
        Type10 = 10, // brown
        Type11 = 11, // silver (drawn a cool slate grey - a light silver would vanish against the white gores)
        Type12 = 12, // navy blue
        Type13 = 13, // olive green

        // The colours live in BasicEffectParamsProvider (GetDiffuseTintByType / GetEffectByType); value 0 is
        // unused (empty cells are null, not a type). Serialized as the raw byte, so a map keeps its colours.
    }
}
