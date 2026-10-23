#ifndef BANK_COVER_FXH
#define BANK_COVER_FXH

//WHERE THE ISLAND'S BANK SURELY STANDS OVER THE GROUND (#983). The scenes that wear their bank as their own ground (the
//forest, the Moon, Mars, the outback) draw it with the ground's own program, and the ground drawn beside it was shaded
//under it as well, for nothing: the bank covered it. A terrain pixel inside this line is turned away first, before any of
//its work. The measured costs are in docs/scenes/island.md ("The bank's cost"); drawing the bank after the grid instead of
//before it bought 0.09 ms of them, so the double shading, not the order, was the cost.
//
//The line is IslandBerm's own (RingPoint: the bank's spread off a few sines round the ring, its phase the bank's seed's)
//at BANK_COVER_REACH, inside the bank's fourth ring (2.6 out, still 0.3 times its lift over the ground) by a margin for
//the mesh's straight segments; IslandBerm.CoverRadius is the C# twin, and BankCoverTests holds the bank over the ground
//everywhere inside it. BankCoverOn 0 leaves every scene that draws no such bank as it was.
float BankCoverPhase;
float BankCoverOn;

static const float BANK_ISLAND_RADIUS = 26.0;
static const float BANK_COVER_REACH = 2.4;

bool UnderBank(float2 xz)
{
    if (BankCoverOn <= 0.0) return false;

    float r = length(xz);
    if (r > BANK_ISLAND_RADIUS + BANK_COVER_REACH * 1.55) return false;

    float a = atan2(xz.y, xz.x);
    float spread = 1.0 + 0.35 * sin(a * 3.0 + BankCoverPhase) + 0.2 * sin(a * 7.0 + BankCoverPhase * 2.3);
    return r < BANK_ISLAND_RADIUS + BANK_COVER_REACH * spread;
}

#endif
