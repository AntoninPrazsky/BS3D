using Prazsky.Core.Render;
using System;

namespace Prazsky.BS3D.GameStructure
{
    /// <summary>
    /// Which ball colours carry a <b>second cue</b> in the #737 prototypes, and which of the four figures each one
    /// carries. A player could not tell two wool balls apart by colour alone; the owner wants every colour to have
    /// a figure of its own as well, and whether that figure is painted on the round ball or carved into its shape
    /// (<see cref="ColourCueForm"/>) is the question the prototypes put to him. Only then are all thirteen built.
    /// <para>
    /// <b>Four colours, chosen as the tightest pairs the palette has</b> (the issue's own measurements): the dark
    /// trio black/brown/silver (3.8-21 dE under wool) and the wool's "two golds", the beige white and the yellow
    /// (10.0-12.5). Black keeps no figure - the plain one of the three - and the other four take one each, so
    /// every pair in both groups differs in its figure as well as its colour. That is the property
    /// <c>ColourCueTests</c> holds the table to.
    /// </para>
    /// <para>
    /// The slot is a figure in both forms, and the shader's table (<c>BallColourCue.fxh</c>) says what it is:
    /// 1 stripes / ridges, 2 dots / dimples, 3 checks / facets, 4 stars / twisted grooves.
    /// </para>
    /// </summary>
    public static class ColourCues
    {
        /// <summary>The slot of a colour that carries no figure: the shipped program draws it.</summary>
        public const int NONE = 0;

        /// <summary>How many figures the prototypes have, numbered 1..this in the shader.</summary>
        public const int SLOT_COUNT = 4;

        /// <summary>The figure this colour carries, <see cref="NONE"/> for one that carries none.</summary>
        public static int SlotOf(BallType type) => type switch
        {
            BallType.Type11 => 1, //silver: three tilted stripes / six ridges
            BallType.Type10 => 2, //brown: twelve dots / twelve dimples
            BallType.Type4 => 3,  //white (drawn beige): checks / twenty facets
            BallType.Type7 => 4,  //yellow: six stars / four twisted grooves
            _ => NONE,
        };

        /// <summary>
        /// The <c>colourcue=</c> argument's spellings, shared by the Testbed and the Game so one script drives
        /// either: <c>none</c> (or <c>off</c>), <c>motif</c> (or <c>a</c>, the owner's own name for the painted
        /// reading) and <c>shape</c> (or <c>b</c>, the carved one).
        /// </summary>
        public static bool TryParseForm(string name, out ColourCueForm form)
        {
            switch (name?.Trim().ToLowerInvariant())
            {
                case "none":
                case "off":
                    form = ColourCueForm.None;
                    return true;

                case "motif":
                case "a":
                    form = ColourCueForm.Motif;
                    return true;

                case "shape":
                case "b":
                    form = ColourCueForm.Shape;
                    return true;

                default:
                    form = ColourCueForm.None;
                    return false;
            }
        }

        /// <summary>The form's name in <see cref="TryParseForm"/>'s spelling, for a log line.</summary>
        public static string Name(ColourCueForm form) => form switch
        {
            ColourCueForm.Motif => "motif",
            ColourCueForm.Shape => "shape",
            ColourCueForm.None => "none",
            _ => throw new ArgumentOutOfRangeException(nameof(form), form, null),
        };
    }
}
