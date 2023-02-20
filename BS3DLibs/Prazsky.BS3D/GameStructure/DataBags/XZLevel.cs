using System;

namespace Prazsky.BS3D.GameStructure.DataBags
{
    public struct XZLevel
    {
        public int X;
        public int Z;
        public int Level;

        public XZLevel(int x, int z, int level)
        {
            X = x;
            Z = z;
            Level = level;
        }

        public static XZLevel FromArray(Array array)
        {
            if (array == null || array.Rank != 3) throw new ArgumentException("The array must be not null and three-dimensional.", nameof(array));
            return new XZLevel(array.GetLength(0), array.GetLength(1), array.GetLength(2));
        }
    }
}
