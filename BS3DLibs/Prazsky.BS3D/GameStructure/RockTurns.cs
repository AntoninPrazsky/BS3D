using Microsoft.Xna.Framework;
using Prazsky.BS3D.GameStructure.DataBags;
using System;

namespace Prazsky.BS3D.GameStructure
{
    /// <summary>
    /// The face each rock ball is drawn showing — one fixed turn per cell, so a heap is one stone seen a
    /// hundred ways instead of one stone stamped a hundred times (#356).
    /// <para>
    /// <b>The stone technique never needed a per-rock seed. It needed the orientation it had already been
    /// promised.</b> <c>InstancedModel.fx</c>'s <c>StoneVS</c> carves the sphere in OBJECT space, and its
    /// header rested the whole of a heap's variety on that: <i>"the physics gives every rock its own
    /// orientation, so a pile shows the same stone from a hundred angles"</i>. Measured on Cairn — 209 rocks,
    /// the densest of the five stone levels — that was simply false. <c>BallsConstraintsBuilder</c> creates
    /// every body at identity, and a lattice hanging from a ceiling never turns one: over sixty seconds the
    /// mean tilt from identity was <b>0.05°</b> and the largest <b>0.28°</b>, with only the first second after
    /// the build reaching 6.3° as the constraints took up. Every rock in the level showed the same solid at the
    /// same angle, down to the same pale swirl in the same place on every one of them.
    /// </para>
    /// <para>
    /// So the turn is supplied here, in the <b>draw matrix</b>, and that is the part of the answer worth
    /// having: the world matrix is already four of <see cref="Prazsky.Core.Render.ModelInstance"/>'s elements,
    /// so this costs <b>no instance channel</b> — there is none free, the struct is full at 88 bytes — and no
    /// shader change at all. It cannot reach the simulation either: a ball's collider is a sphere and its
    /// constraints anchor off its body pose, and this is neither.
    /// </para>
    /// <para>
    /// <b>Keyed on the CELL, never on the position.</b> A position moves, and a field seeded from one would
    /// swim over the stone as a rock fell — the trap the shader's own header names. A rock's
    /// <c>PhysicsBall.ArrayPosition</c> is set once when the cluster is built and never changes again (a rock
    /// is a wall: it is never shot, so it is never re-celled), so one rock keeps one face all the way down the
    /// drain.
    /// </para>
    /// <para>
    /// It is deliberately the <b>rock alone</b>. The coloured balls are moulded vinyl from one mould and a
    /// batch of them being identical is the fiction rather than a fault in it; a quarry's rubble being
    /// identical is not.
    /// </para>
    /// </summary>
    public static class RockTurns
    {
        //A power of two, so the pick below is a mask rather than a modulo. 128 faces against a level's ~200
        //rocks means a face recurs, and that is not a defect — one quarry's rubble repeats too. What the eye
        //reads is whether the rocks BESIDE each other differ, which is the hash's job and not the count's.
        private const int TURN_COUNT = 128;

        //Built once at type load, so a frame pays a hash, an array read and one quaternion multiply per rock.
        private static readonly Quaternion[] TURNS = BuildTurns();

        /// <summary>
        /// The turn a rock built in <paramref name="cell"/> is drawn with. It is applied <b>before</b> the
        /// body's own pose — it is the stone's orientation inside the ball, so the ball still turns normally on
        /// top of it and a rolling rock still reads as rolling. Deterministic, so a level shows the same heap
        /// on every machine and in every run.
        /// </summary>
        public static Quaternion For(in XZLevel cell) => TURNS[IndexOf(cell)];

        //Three large odd multipliers so the three axes cannot cancel each other, then an avalanche so that
        //neighbouring cells land far apart in the table. Adjacent rocks reading as different stones is the
        //whole job, and a hash that sent (x, z, level) and (x + 1, z, level) to adjacent entries would throw
        //it away wherever the table's own neighbours happened to be close rotations.
        private static int IndexOf(in XZLevel cell)
        {
            unchecked
            {
                uint h = (uint)cell.X * 0x8DA6B343u ^ (uint)cell.Z * 0xD8163841u ^ (uint)cell.Level * 0xCB1AB31Fu;

                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;

                return (int)(h & (TURN_COUNT - 1));
            }
        }

        //Shoemake's uniform random rotation, fed a Halton point rather than three numbers out of a generator.
        //Both halves of that are deliberate. Three EULER angles are uniform in their own parameters and clump
        //hard at the poles, which would hand the heap a preferred axis — the one thing this exists to remove.
        //And a low-discrepancy sequence rather than a PRNG because 128 draws of a PRNG leave visible gaps and
        //clusters, while these 128 have to cover the space of rotations between them.
        private static Quaternion[] BuildTurns()
        {
            Quaternion[] turns = new Quaternion[TURN_COUNT];

            for (int i = 0; i < TURN_COUNT; i++)
            {
                //The first three primes, which is what keeps the three coordinates from correlating
                float u1 = RadicalInverse(i + 1, 2);
                float u2 = RadicalInverse(i + 1, 3);
                float u3 = RadicalInverse(i + 1, 5);

                //x² + y² + z² + w² = (1 - u1) + u1 = 1 by construction, so nothing here needs normalizing
                float near = MathF.Sqrt(1f - u1);
                float far = MathF.Sqrt(u1);
                float angleA = MathHelper.TwoPi * u2;
                float angleB = MathHelper.TwoPi * u3;

                turns[i] = new Quaternion(near * MathF.Sin(angleA), near * MathF.Cos(angleA),
                    far * MathF.Sin(angleB), far * MathF.Cos(angleB));
            }

            return turns;
        }

        //The radical inverse of an index in a base: its digits reflected about the point. The classic Halton
        //generator, and the one thing about it worth stating here is that the index starts at ONE — zero
        //inverts to zero in every base, so an index of 0 would make the first entry the identity and leave one
        //rock in the heap unturned.
        private static float RadicalInverse(int index, int radix)
        {
            float result = 0f;
            float fraction = 1f / radix;

            while (index > 0)
            {
                result += index % radix * fraction;
                index /= radix;
                fraction /= radix;
            }

            return result;
        }
    }
}
