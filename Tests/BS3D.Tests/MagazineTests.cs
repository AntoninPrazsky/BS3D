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
