using Microsoft.Xna.Framework;
using Prazsky.Core.Render;
using System;
using System.Collections.Generic;

namespace BS3D.Tests
{
    /// <summary>
    /// How far a point stands from the cavern's rock <b>as it is built</b> (#1017): the triangles <see cref="CavernGeometry.BuildRock"/>
    /// hands the draw, not the height field and the shell function they are sampled from, which differ from the mesh by the
    /// cell's chord. Brute force over the triangles near a point, which is plenty for a few hundred samples.
    /// <para>
    /// The rock is two sheets, the lake's bed and the shell, and the visible surface is the boundary of their union: the
    /// shell runs on under the bed (and the bed on behind the wall) where the other one hides it. So a point is judged against
    /// each sheet alone, and its signed distance is the smaller of the two (inside either is inside the rock). Whether a point is
    /// inside a sheet is not taken from the side of the nearest triangle, which flips in the creases of a bumpy island, but from
    /// how each sheet is parameterised: the bed is a height field (inside = below its triangle at that (x, z)) and the shell is
    /// star-shaped about the centre (inside = beyond the triangle the ray from the centre meets).
    /// </para>
    /// </summary>
    internal sealed class CavernRockProbe
    {
        private readonly Vector3 _centre;
        private readonly Vector3[] _positions;
        private readonly bool[] _isBed;
        private readonly int[] _indices;

        public CavernRockProbe(CavernChamber chamber, CavernGeometry.Detail detail)
        {
            _centre = chamber.Centre;
            var rock = CavernGeometry.BuildRock(chamber, detail);
            _positions = new Vector3[rock.Vertices.Length];
            _isBed = new bool[rock.Vertices.Length];
            for (int i = 0; i < _positions.Length; i++)
            {
                _positions[i] = rock.Vertices[i].Position;
                //Texture coordinate Y is 0 on the shell and 1 on the bed (BuildRock)
                _isBed[i] = rock.Vertices[i].TextureCoordinate.Y > 0.5f;
            }
            _indices = rock.Indices;
        }

        /// <summary>The triangles near a point, by sheet.</summary>
        public readonly struct Local
        {
            public readonly int[] Bed, Shell;
            public Local(int[] bed, int[] shell) { Bed = bed; Shell = shell; }
        }

        /// <summary>The triangles with any part inside the box of half-width <paramref name="radius"/> round <paramref name="centre"/>.</summary>
        public Local Near(Vector3 centre, float radius)
        {
            Vector3 lo = centre - new Vector3(radius), hi = centre + new Vector3(radius);
            var bed = new List<int>();
            var shell = new List<int>();
            for (int t = 0; t < _indices.Length; t += 3)
            {
                Vector3 a = _positions[_indices[t]], b = _positions[_indices[t + 1]], c = _positions[_indices[t + 2]];
                if (MathF.Max(a.X, MathF.Max(b.X, c.X)) < lo.X || MathF.Min(a.X, MathF.Min(b.X, c.X)) > hi.X) continue;
                if (MathF.Max(a.Y, MathF.Max(b.Y, c.Y)) < lo.Y || MathF.Min(a.Y, MathF.Min(b.Y, c.Y)) > hi.Y) continue;
                if (MathF.Max(a.Z, MathF.Max(b.Z, c.Z)) < lo.Z || MathF.Min(a.Z, MathF.Min(b.Z, c.Z)) > hi.Z) continue;
                (_isBed[_indices[t]] ? bed : shell).Add(t);
            }
            return new Local(bed.ToArray(), shell.ToArray());
        }

        /// <summary>
        /// The signed distance from <paramref name="p"/> to the rock's surface near it (from <see cref="Near"/>): positive in the
        /// hall's air, negative inside the rock. Also the nearest point on that surface.
        /// </summary>
        public float SignedDistance(Vector3 p, Local local, out Vector3 nearest)
        {
            float bed = Distance(p, local.Bed, out Vector3 nearBed);
            float shell = Distance(p, local.Shell, out Vector3 nearShell);
            if (BelowBed(p, local.Bed)) bed = -bed;
            if (BeyondShell(p, local.Shell)) shell = -shell;
            if (bed <= shell) { nearest = nearBed; return bed; }
            nearest = nearShell;
            return shell;
        }

        //Whether (x, z) lies under one of the bed's triangles, and the point is below it
        private bool BelowBed(Vector3 p, int[] triangles)
        {
            foreach (int t in triangles)
            {
                Vector3 a = _positions[_indices[t]], b = _positions[_indices[t + 1]], c = _positions[_indices[t + 2]];
                float det = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
                if (MathF.Abs(det) < 1e-9f) continue;
                float l1 = ((b.Z - c.Z) * (p.X - c.X) + (c.X - b.X) * (p.Z - c.Z)) / det;
                float l2 = ((c.Z - a.Z) * (p.X - c.X) + (a.X - c.X) * (p.Z - c.Z)) / det;
                float l3 = 1f - l1 - l2;
                const float E = -1e-5f;
                if (l1 < E || l2 < E || l3 < E) continue;
                if (p.Y <= l1 * a.Y + l2 * b.Y + l3 * c.Y) return true;
            }
            return false;
        }

        //Whether the ray from the centre through the point meets a shell triangle short of it
        private bool BeyondShell(Vector3 p, int[] triangles)
        {
            Vector3 d = p - _centre;
            float length = d.Length();
            if (length < 1e-4f) return false;
            d /= length;
            foreach (int t in triangles)
            {
                //Moller-Trumbore, both sides
                Vector3 a = _positions[_indices[t]], b = _positions[_indices[t + 1]], c = _positions[_indices[t + 2]];
                Vector3 e1 = b - a, e2 = c - a;
                Vector3 h = Vector3.Cross(d, e2);
                float det = Vector3.Dot(e1, h);
                if (MathF.Abs(det) < 1e-9f) continue;
                float inv = 1f / det;
                Vector3 s = _centre - a;
                float u = Vector3.Dot(s, h) * inv;
                if (u < -1e-5f || u > 1f + 1e-5f) continue;
                Vector3 q = Vector3.Cross(s, e1);
                float v = Vector3.Dot(d, q) * inv;
                if (v < -1e-5f || u + v > 1f + 1e-5f) continue;
                float hit = Vector3.Dot(e2, q) * inv;
                if (hit > 0f && hit <= length) return true;
            }
            return false;
        }

        private float Distance(Vector3 p, int[] triangles, out Vector3 nearest)
        {
            float best = float.MaxValue;
            nearest = p;
            foreach (int t in triangles)
            {
                Vector3 q = ClosestOnTriangle(p, _positions[_indices[t]], _positions[_indices[t + 1]], _positions[_indices[t + 2]]);
                float d = Vector3.DistanceSquared(p, q);
                if (d >= best) continue;
                best = d;
                nearest = q;
            }
            return best == float.MaxValue ? float.MaxValue : MathF.Sqrt(best);
        }

        //Ericson, Real-Time Collision Detection, 5.1.5
        private static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return a;
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
            float denom = 1f / (va + vb + vc);
            return a + ab * (vb * denom) + ac * (vc * denom);
        }
    }
}
