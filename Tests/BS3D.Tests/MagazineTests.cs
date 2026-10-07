using Prazsky.BS3D;
using Prazsky.BS3D.GameStructure;
using Xunit;

namespace BS3D.Tests
{
    /// <summary>
    /// The magazine's per-slot state (#582): a slot's colour, kind and cross-fade are one
    /// <see cref="MagazineSlot"/>, so a shift, a deal and a swap move all of it together — the property three
    /// parallel arrays and three callbacks used to keep by hand, and lost once (#392).
    /// </summary>
    public class MagazineTests
    {
        //Deals the colours 1, 2, 3, … in turn, and makes every third ball dealt a wildcard, so each slot's
        //state is recognisable after it has moved
        private sealed class Dealer
        {
            private int _types;
            private int _kinds;

            public BallType NextType() => (BallType)(_types++ % BallTypes.Count + 1);

            public BallKind NextKind() => ++_kinds % 3 == 0 ? BallKind.Wildcard : BallKind.Normal;
        }

        private static Magazine Build() => Build(out _);

        private static Magazine Build(out Dealer dealer)
        {
            dealer = new Dealer();
            return new Magazine(dealer.NextType, dealer.NextKind);
        }

        /// <summary>A fresh queue is dealt whole: colour then kind, in order, and nothing mid-dissolve.</summary>
        [Fact]
        public void FreshQueueIsDealtWholeAndSettled()
        {
            Magazine magazine = Build();

            for (int slot = 0; slot < Magazine.SIZE; slot++)
            {
                MagazineSlot s = magazine.Slot(slot);
                Assert.Equal((BallType)(slot + 1), s.Type);
                Assert.Equal((slot + 1) % 3 == 0 ? BallKind.Wildcard : BallKind.Normal, s.Kind);
                Assert.Equal(s.Type, s.FadingFrom);
                Assert.Equal(0f, s.Transmute);
            }
        }

        /// <summary>
        /// The anchor cut turns a loaded round into a cutter (#213): the kind changes and everything else about the slot stays,
        /// the cutter rides a swap and an advance like any round, and firing it deals the tail fresh — a cutter is spent, not
        /// carried along to the next slot.
        /// </summary>
        [Fact]
        public void ACutterReplacesOnlyTheKindAndIsConsumedByAdvance()
        {
            Magazine magazine = Build();
            magazine.Recolour(0, (BallType)9);
            MagazineSlot before = magazine.Slot(0);

            magazine.SetKind(0, BallKind.Cutter);

            MagazineSlot cutter = magazine.Slot(0);
            Assert.Equal(BallKind.Cutter, cutter.Kind);
            Assert.Equal(before.Type, cutter.Type);
            Assert.Equal(before.FadingFrom, cutter.FadingFrom);
            Assert.Equal(before.Transmute, cutter.Transmute);

            //A swap carries it whole to the slot behind, and back
            magazine.SwapSlots(0, 1);
            Assert.Equal(BallKind.Cutter, magazine.Slot(1).Kind);
            Assert.NotEqual(BallKind.Cutter, magazine.Slot(0).Kind);
            magazine.SwapSlots(0, 1);
            Assert.Equal(BallKind.Cutter, magazine.Slot(0).Kind);

            //Firing it is an advance: what was behind takes its place and no slot is a cutter any more
            magazine.Advance();
            for (int slot = 0; slot < Magazine.SIZE; slot++) Assert.NotEqual(BallKind.Cutter, magazine.Slot(slot).Kind);
        }

        /// <summary>A mid-dissolve slot and its kind ride the shift with their colour, and the tail is dealt fresh.</summary>
        [Fact]
        public void AdvanceMovesEachSlotWhole()
        {
            Magazine magazine = Build();
            magazine.Recolour(3, (BallType)9);

            MagazineSlot before = magazine.Slot(3);
            MagazineSlot wildcard = magazine.Slot(2);

            magazine.Advance();

            Assert.Equal(before, magazine.Slot(2));
            Assert.Equal(wildcard, magazine.Slot(1));
            Assert.Equal(BallKind.Wildcard, magazine.Slot(1).Kind);

            MagazineSlot tail = magazine.Slot(Magazine.SIZE - 1);
            Assert.Equal((BallType)(Magazine.SIZE + 1), tail.Type);
            Assert.Equal(0f, tail.Transmute);
        }

        /// <summary>A swap exchanges the two slots whole — not two one-way copies (#392).</summary>
        [Fact]
        public void SwapExchangesBothSlotsWhole()
        {
            Magazine magazine = Build();
            magazine.Recolour(0, (BallType)11);

            MagazineSlot a = magazine.Slot(0), b = magazine.Slot(2);

            magazine.SwapSlots(0, 2);

            Assert.Equal(b, magazine.Slot(0));
            Assert.Equal(a, magazine.Slot(2));
        }

        /// <summary>
        /// The Swap the player presses is seen to trade (#705): each slot holds the other's round at once and dissolves into
        /// it from the colour it was showing, over the swap's own shorter time; a swap with a wildcard is the whole exchange
        /// alone, as it was; and a slot caught mid-dissolve fades from the colour it was fading out of.
        /// </summary>
        [Fact]
        public void ASwapSeenToTradeDissolvesEachIntoTheOthersColour()
        {
            //Colours 1 and 2 in the first two slots, a wildcard in the third (the dealer's every third)
            Magazine magazine = Build();
            magazine.SwapSlots(0, 1, crossFade: true);

            MagazineSlot first = magazine.Slot(0), second = magazine.Slot(1);
            Assert.Equal((BallType)2, first.Type);
            Assert.Equal((BallType)1, first.FadingFrom);
            Assert.Equal(1f, first.Transmute);
            Assert.Equal(Magazine.SWAP_FADE_SECONDS, first.TransmuteSeconds);
            Assert.Equal((BallType)1, second.Type);
            Assert.Equal((BallType)2, second.FadingFrom);

            magazine.Step(Magazine.SWAP_FADE_SECONDS * 0.5f);
            Assert.InRange(magazine.Slot(0).Transmute, 0.49f, 0.51f);
            magazine.Step(Magazine.SWAP_FADE_SECONDS * 0.5f + 1e-4f);
            Assert.Equal(0f, magazine.Slot(0).Transmute);

            MagazineSlot plain = magazine.Slot(1), wildcard = magazine.Slot(2);
            Assert.Equal(BallKind.Wildcard, wildcard.Kind);
            magazine.SwapSlots(1, 2, crossFade: true);
            Assert.Equal(wildcard, magazine.Slot(1));
            Assert.Equal(plain, magazine.Slot(2));

            magazine = Build();
            magazine.Recolour(0, (BallType)9);
            magazine.SwapSlots(0, 1, crossFade: true);
            Assert.Equal((BallType)2, magazine.Slot(0).Type);
            Assert.Equal((BallType)1, magazine.Slot(0).FadingFrom);
            Assert.Equal((BallType)9, magazine.Slot(1).Type);
            Assert.Equal((BallType)2, magazine.Slot(1).FadingFrom);
        }

        /// <summary>
        /// A re-colour fades out of the colour on screen — for a slot caught mid-fade, the one it was already
        /// fading out of — and the countdown runs linearly to exactly zero.
        /// </summary>
        [Fact]
        public void RecolourFadesOutOfTheVisibleColourAndSettles()
        {
            Magazine magazine = Build();
            BallType original = magazine.Peek(1);

            magazine.Recolour(1, (BallType)7);
            magazine.Step(Magazine.TRANSMUTE_SECONDS / 2f);
            magazine.Recolour(1, (BallType)8);

            MagazineSlot s = magazine.Slot(1);
            Assert.Equal((BallType)8, s.Type);
            Assert.Equal(original, s.FadingFrom);
            Assert.Equal(1f, s.Transmute);
            Assert.Equal(0f, s.TransmuteProgress);

            magazine.Step(Magazine.TRANSMUTE_SECONDS * 2f);
            Assert.Equal(0f, magazine.Slot(1).Transmute);
            Assert.Equal((BallType)8, magazine.Peek(1));
        }

        /// <summary>A refill starts a level clean: no dissolve survives it.</summary>
        [Fact]
        public void RefillClearsEveryDissolve()
        {
            Magazine magazine = Build();
            for (int slot = 0; slot < Magazine.SIZE; slot++) magazine.Recolour(slot, (BallType)13);

            magazine.Refill();

            for (int slot = 0; slot < Magazine.SIZE; slot++)
            {
                Assert.Equal(0f, magazine.Slot(slot).Transmute);
                Assert.Equal(magazine.Slot(slot).Type, magazine.Slot(slot).FadingFrom);
            }
        }
    }
}
