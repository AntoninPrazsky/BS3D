//Crater helpers the moon and Mars share (#581), byte-identical in both until they moved here - Mars's craters
//are the moon's construction, and the two copies were one edit away from drifting the way #579 found the
//aurora's floor had. The crater LAYERS themselves (CraterLayer, CraterField) stay in each scene: the moon's
//carries its own shadow terms and they have genuinely diverged. Needs Noise.fxh included first.

//Each octave is TURNED TO ITS OWN BEARING before it is cut into cells, and the fourth (the pixel shader's
//5-unit detail layer) with them. A separate seed makes two lattices share no crater; it does not stop them
//sharing their ROWS, and `floor()` of an unturned domain puts every octave's rows along world X and Z. So
//the four grids lined up and each one drew the others' lattice in again at its own scale - which is why
//#240 reads as a single carpet in the photographs rather than as three faint ones. Free: two multiplies
//and an add per octave, off constants folded at compile time. The angles share no small ratio.
static const float2 CRATER_TURN_0 = float2(0.97437, 0.22495);   //13 degrees
static const float2 CRATER_TURN_1 = float2(0.75471, 0.65606);   //41 degrees
static const float2 CRATER_TURN_2 = float2(0.27564, 0.96126);   //74 degrees
static const float2 CRATER_TURN_3 = float2(0.55919, 0.82903);   //56 degrees

float2 TurnCrater(float2 p, float2 turn)
{
    return float2(p.x * turn.x - p.y * turn.y, p.x * turn.y + p.y * turn.x);
}

//Gentle mare undulation under the craters, so the plain is not a snooker table between them. Two octaves
//of gradient noise - genuinely two, not a sine pair; a sum of plane waves keeps its planes (Noise.fxh's
//opening, learned by three scenes the hard way).
float MareBase(float2 p)
{
    return GradientNoise2(p * 0.011) * 0.65 + GradientNoise2(p * 0.031 + 7.3) * 0.35;
}
