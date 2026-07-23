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

        // The colours live in BasicEffectParamsProvider (GetDiffuseTintByType / GetEffectByType); value 0 is
        // unused (empty cells are null, not a type). Serialized as the raw byte, so a map keeps its colours.
    }
}
