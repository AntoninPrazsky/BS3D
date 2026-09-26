//The two small vector helpers the rock lattices of the outback and Mars share (#581): byte-identical in both,
//where the RockLayer that calls them is not - Mars's is the outback's before it grew its bornhardt (#579) -
//and stays in each scene.

//Rotate p into the frame whose +X is `axis` (a unit vector). Taken as a vector rather than an angle because
//the angle would only ever be turned straight back into its sine and cosine: a hash gives the vector for one
//rsqrt where sin/cos of a hashed angle costs two transcendentals, and this runs per formation per tap.
float2 RotateInto(float2 p, float2 axis)
{
    return float2(dot(p, axis), dot(p, float2(-axis.y, axis.x)));
}

//A unit vector out of two rolls in 0..1. Guarded against the exact centre of the square, which would
//normalize to a NaN and take the whole formation's shape with it.
float2 RollDirection(float2 roll)
{
    float2 v = roll * 2.0 - 1.0;

    return v * rsqrt(max(dot(v, v), 1e-4));
}
