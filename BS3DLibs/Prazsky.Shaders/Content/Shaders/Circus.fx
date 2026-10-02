//Draws the big top (#690), the twenty-first scene: the inside of a circus tent between shows. Red and cream canvas
//sweeping up from a curtained side wall to a crown ring on four king poles, a bowl of red seats round a sawdust ring
//with a red and gold curb, strings of warm bulbs sagging from the crown to the wall, and coloured spotlights cutting
//through dusty air onto the island that stands in the middle of the ring. Drawn from #690's references (the big top
//seen from the ring, from the rigging, from the back row; Z-Image and klein both).
//
//Like the cavern it replaces the SKY, and everything structural follows from that: one full-screen pass over the
//renderer's shared quad, the view ray recovered per pixel through ViewRayBasis (SkyRay.Basis), drawn with the depth
//state off so the island, the cluster and the gun draw over it. The caller draws no dome and no cloud deck and takes
//the scene's own light rig. The one thing it adds is the FLOOR'S DEPTH (CircusFloorDepth below): the floor here is
//ground the island is set into, and a ball that falls past the island's rim has to vanish into it rather than fall on
//through a painted picture of sawdust.
//
//EVERYTHING IS ANALYTIC, and nothing is marched. The tent is a handful of quadrics - a cone for the canvas, a cylinder
//for the wall, a second, upturned cone for the rake of the seats, a cylinder band for the curb, a plane for the floor
//and one small cylinder per king pole - each hit in closed form, nearest wins. What makes them read as cloth, velvet,
//seats and sawdust is SHADING on the hit: a panel's belly and its seam, a curtain's folds, a row's riser and tread and
//the gap between two seats, raked circles in the sawdust. A seat is never geometry; at the distance the lens sees the
//bowl from, a row is a few pixels tall and its profile is what a normal can carry.
//
//The light is three things. The HOUSE LIGHT is the general warm light between shows, from above, that keeps the
//canvas and the seats readable. The SPOTS are real spotlights - a cone each from the rig under the crown, an inverse
//square pool where it lands (the floor, the seats, the curb, the poles), and its BEAM in the dusty air, integrated in
//closed form along the view ray (the airlight integral of a point light, over the interval of the ray that lies inside
//the cone, at two cone angles for a soft edge). The BULBS are emissive points on the strings, drawn as stars are:
//a closest-approach glow per bulb with its core widened to the pixel and its energy kept, so a bulb sixty units off
//stays one steady point instead of a shimmer. They also warm the canvas under each string.
//
//Built once by Prazsky.Shaders (#618), Shader Model 5.0.

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#include "Noise.fxh"

#define SPOT_COUNT 5
#define MAX_POLES 6
#define MAX_STRINGS 24
#define FOOTLIGHT_COUNT 4

static const float PI = 3.14159265;
static const float TWO_PI = 6.28318531;

float4x4 ViewRayBasis;   //the lens's axes over the projection's slopes - see SkyRay.Basis
float3 CameraPosition;
float CircusTime;
float PixelAngle;        //one pixel's angular size, radians: what a distance is multiplied by to be a footprint

float4x4 WorldViewProjection;  //the floor's depth pass (world is identity)

//--- The tent ------------------------------------------------------------------------------------------
float WallRadius;
float WallTopY;
float CrownRadius;
float CrownY;
float PanelCount;
float PanelSag;
float3 CanvasRed;
float3 CanvasCream;
float3 CurtainColor;
float KingPoleCount;
float KingPoleRadius;
float KingPoleThickness;
float3 PoleColor;

//--- The ring ------------------------------------------------------------------------------------------
float FloorY;
float CurbRadius;
float CurbHeight;
float CurbThickness;
float3 CurbRed;
float3 CurbGold;
float3 SawdustColor;
float3 FloorColor;

//--- The seats -----------------------------------------------------------------------------------------
float SeatInner;
float SeatOuter;
float RowDepth;
float RowRise;
float3 SeatRed;
float3 SeatWood;
float AisleCount;
float AisleWidth;
float EntranceBearing;   //radians
float EntranceWidth;

//--- The lights ----------------------------------------------------------------------------------------
float3 SpotPosition[SPOT_COUNT];
float3 SpotDirection[SPOT_COUNT];   //unit, the beam's axis
float3 SpotColor[SPOT_COUNT];       //colour times intensity, linear
float SpotCosOuter;
float SpotCosInner;
float SpotReach;                    //the distance at which a pool has the stated intensity (inverse square about it)
float BeamStrength;
float BulbStrings;
float BulbSpacing;
float BulbSag;
float3 BulbColor;
float3 HazeColor;
float HazeDensity;
float3 HouseLight;
float FootlightInset;    //the footlights: how far inside the curb, how high, and their colour (CircusBackdrop's figures)
float FootlightHeight;
float3 FootlightColor;

//--- Hits ----------------------------------------------------------------------------------------------
//What a ray met: where along it, which surface, and the surface's own normal before any shading detail.

#define HIT_NONE 0
#define HIT_FLOOR 1
#define HIT_CURB 2
#define HIT_SEATS 3
#define HIT_WALL 4
#define HIT_ROOF 5
#define HIT_POLE 6
#define HIT_GATE 7
#define HIT_FIXTURE 8
#define HIT_LENS 9
#define HIT_TRUSS 10

struct Hit
{
    float T;
    int Kind;
    float3 Normal;
    int Spot;        //which spotlight a fixture or lens hit is
};

void Consider(inout Hit best, float t, int kind, float3 normal)
{
    if (t > 0.0 && t < best.T)
    {
        best.T = t;
        best.Kind = kind;
        best.Normal = normal;
        best.Spot = 0;
    }
}

void ConsiderSpot(inout Hit best, float t, int kind, float3 normal, int spot)
{
    if (t > 0.0 && t < best.T)
    {
        best.T = t;
        best.Kind = kind;
        best.Normal = normal;
        best.Spot = spot;
    }
}


//A vertical cylinder of radius r about the Y axis, between y0 and y1: the nearest crossing in front of the ray, with
//its normal turned to face the ray. Both roots are tried, so a ray from inside meets the far wall and one from outside
//the near one.
void HitCylinder(inout Hit best, float3 o, float3 d, float r, float y0, float y1, int kind)
{
    float a = dot(d.xz, d.xz);
    float b = dot(o.xz, d.xz);
    float c = dot(o.xz, o.xz) - r * r;
    float disc = b * b - a * c;
    if (a < 1e-8 || disc < 0.0) return;

    float s = sqrt(disc);
    float t0 = (-b - s) / a;
    float t1 = (-b + s) / a;

    float y = o.y + t0 * d.y;
    if (t0 > 0.0 && y >= y0 && y <= y1)
    {
        float2 p = o.xz + t0 * d.xz;
        float3 n = float3(p.x, 0.0, p.y) / r;
        Consider(best, t0, kind, dot(n, d) < 0.0 ? n : -n);
        return;
    }

    y = o.y + t1 * d.y;
    if (t1 > 0.0 && y >= y0 && y <= y1)
    {
        float2 p = o.xz + t1 * d.xz;
        float3 n = float3(p.x, 0.0, p.y) / r;
        Consider(best, t1, kind, dot(n, d) < 0.0 ? n : -n);
    }
}

//The surface r = ra + rb * y about the Y axis between y0 and y1 (a cone, or the rake of the seats with rb > 0): the
//nearest crossing in front of the ray on the nappe where ra + rb * y is positive, its normal turned to the ray.
void HitCone(inout Hit best, float3 o, float3 d, float ra, float rb, float y0, float y1, int kind)
{
    float k = ra + rb * o.y;
    float a = dot(d.xz, d.xz) - rb * rb * d.y * d.y;
    float b = dot(o.xz, d.xz) - rb * d.y * k;
    float c = dot(o.xz, o.xz) - k * k;
    float disc = b * b - a * c;
    if (disc < 0.0 || abs(a) < 1e-8) return;

    float s = sqrt(disc);
    float ta = (-b - s) / a;
    float tb = (-b + s) / a;
    float t0 = min(ta, tb);
    float t1 = max(ta, tb);

    [unroll]
    for (int i = 0; i < 2; i++)
    {
        float t = i == 0 ? t0 : t1;
        float3 p = o + t * d;
        float r = ra + rb * p.y;

        if (t > 0.0 && p.y >= y0 && p.y <= y1 && r > 0.0)
        {
            //The gradient of x2 + z2 - (ra + rb y)2
            float3 n = normalize(float3(p.x, -rb * r, p.z));
            Consider(best, t, kind, dot(n, d) < 0.0 ? n : -n);
            return;
        }
    }
}

//--- The spotlights themselves (#690, the owner's eye) ------------------------------------------------------
//A beam must come out of a LAMP. The first build drew the five beams from points under the canvas with nothing at them,
//so each cone seemed to fall through a hole in the cloth. The lamps are deliberately the plainest geometry that reads
//as one (the owner: "the geometry very simple and cheap - what matters is the light"): a dark can with a lens at its
//front, on a rod down from a plain ring of truss. What sells them is the light - the lens over the glare threshold so
//the bloom catches it, a glow round it strongest when the lens faces the lens of the camera, and the beam in the dust.

static const float FIXTURE_RADIUS = 0.8;
static const float LENS_GAIN = 2.4;        //a lens's radiance over its lamp's colour
static const float GLOW_RADIUS = 2.2;      //the halo round a lens in the dusty air, world units
static const float GLOW_MARGIN = 9.0;      //past four of its radii the glow is under e^-16 of its peak
static const float FIXTURE_BACK = 1.9;     //the can behind the beam's apex
static const float FIXTURE_FRONT = 0.2;    //the lens, just ahead of it
static const float YOKE_RADIUS = 0.13;
static const float TRUSS_HALF = 0.35;      //the ring's half-section
static const float TRUSS_ABOVE = 1.5;      //how far over the lamps' pivots the ring runs
static const float RIG_REACH = 2.0;        //how far any part of a lamp is from its pivot: the can's front end, and its radius

//Spot i's pivot, where its rod holds it: the middle of its can, PIVOT_TO_APEX behind the apex in CircusBackdrop. The
//ring is placed by the pivot and not by the apex, which turns with the lamp - read off the apex, the ring rose, fell
//and widened as spot 0 swept.
float3 Pivot(int i) { return SpotPosition[i] - SpotDirection[i] * (FIXTURE_BACK * 0.5); }
float TrussY() { return Pivot(0).y + TRUSS_ABOVE; }
float TrussRadius() { return length(Pivot(0).xz); }

//Whether the ray passes anywhere near the rig: the ring the lamps hang on, as an annulus of height round the axis,
//widened by `margin`. The lamps, the ring and the glows round the lenses are tried only behind it, since most of the
//frame never rises to the rig - tried on every pixel they cost 3.5 ms a frame at 3840x1600 (#690's review).
bool NearRig(float3 o, float3 d, float margin)
{
    float radius = TrussRadius();
    float reach = RIG_REACH + margin;
    float y0 = Pivot(0).y - reach;
    float y1 = TrussY() + TRUSS_HALF + margin;

    //The span of the ray inside the slab of height
    float t0 = 0.0, t1 = 1e5;
    if (abs(d.y) > 1e-6)
    {
        float ta = (y0 - o.y) / d.y;
        float tb = (y1 - o.y) / d.y;
        t0 = max(min(ta, tb), 0.0);
        t1 = min(max(ta, tb), 1e5);
    }
    else if (o.y < y0 || o.y > y1) return false;
    if (t1 < t0) return false;

    //The ray's distance from the axis over that span: least at its closest approach, greatest at one of the ends
    float dd = dot(d.xz, d.xz);
    float tc = dd > 1e-8 ? clamp(-dot(o.xz, d.xz) / dd, t0, t1) : t0;
    float rMin = length(o.xz + tc * d.xz);
    float rMax = max(length(o.xz + t0 * d.xz), length(o.xz + t1 * d.xz));
    return rMin <= radius + reach && rMax >= radius - reach;
}

//One lamp: a capped cylinder along its beam's axis, the front cap its lens
void HitFixture(inout Hit best, float3 o, float3 d, int spot)
{
    float3 axis = SpotDirection[spot];
    float3 co = o - SpotPosition[spot];
    float a = dot(d, axis);
    float b = dot(co, axis);
    float3 dp = d - a * axis;
    float3 op = co - b * axis;

    float A = dot(dp, dp);
    float B = dot(op, dp);
    float C = dot(op, op) - FIXTURE_RADIUS * FIXTURE_RADIUS;
    float disc = B * B - A * C;

    if (A > 1e-8 && disc >= 0.0)
    {
        float s = sqrt(disc);

        [unroll]
        for (int k = 0; k < 2; k++)
        {
            float t = (-B + (k == 0 ? -s : s)) / A;
            float along = b + t * a;
            if (t > 0.0 && along >= -FIXTURE_BACK && along <= FIXTURE_FRONT)
                ConsiderSpot(best, t, HIT_FIXTURE, normalize(op + t * dp), spot);
        }
    }

    if (abs(a) > 1e-6)
    {
        float tLens = (FIXTURE_FRONT - b) / a;
        float3 qLens = op + tLens * dp;
        if (dot(qLens, qLens) <= FIXTURE_RADIUS * FIXTURE_RADIUS) ConsiderSpot(best, tLens, HIT_LENS, axis, spot);

        float tBack = (-FIXTURE_BACK - b) / a;
        float3 qBack = op + tBack * dp;
        if (dot(qBack, qBack) <= FIXTURE_RADIUS * FIXTURE_RADIUS) ConsiderSpot(best, tBack, HIT_FIXTURE, -axis, spot);
    }

    //Its rod: straight up from the can's middle, the pivot the lamp turns about, to the ring (CircusBackdrop's
    //PIVOT_TO_APEX stands the pivot on the ring, so the rod meets it at every angle)
    float3 back = Pivot(spot);
    float2 oc = o.xz - back.xz;
    float ra = dot(d.xz, d.xz);
    float rb = dot(oc, d.xz);
    float rc = dot(oc, oc) - YOKE_RADIUS * YOKE_RADIUS;
    float rdisc = rb * rb - ra * rc;
    if (ra > 1e-8 && rdisc >= 0.0)
    {
        float t = (-rb - sqrt(rdisc)) / ra;
        float y = o.y + t * d.y;
        if (y >= back.y && y <= TrussY()) ConsiderSpot(best, t, HIT_FIXTURE, float3((oc + t * d.xz) / YOKE_RADIUS, 0.0).xzy, spot);
    }
}

//The ring of truss the lamps hang from: a plain square section round the axis
void HitTruss(inout Hit best, float3 o, float3 d)
{
    float radius = TrussRadius();
    float y0 = TrussY() - TRUSS_HALF, y1 = TrussY() + TRUSS_HALF;

    HitCylinder(best, o, d, radius - TRUSS_HALF, y0, y1, HIT_TRUSS);
    HitCylinder(best, o, d, radius + TRUSS_HALF, y0, y1, HIT_TRUSS);

    if (abs(d.y) > 1e-5)
    {
        [unroll]
        for (int k = 0; k < 2; k++)
        {
            float y = k == 0 ? y0 : y1;
            float t = (y - o.y) / d.y;
            float r = length(o.xz + t * d.xz);
            if (abs(r - radius) <= TRUSS_HALF) Consider(best, t, HIT_TRUSS, float3(0.0, k == 0 ? -1.0 : 1.0, 0.0));
        }
    }
}

//How tall the gilded band round the crown's opening is, up to the cupola
static const float CROWN_BAND = 3.5;

//The roof's own figures: r = RoofA + RoofB * y from the wall's top up to the crown.
float RoofB() { return (CrownRadius - WallRadius) / (CrownY - WallTopY); }
float RoofA() { return WallRadius - RoofB() * WallTopY; }

//The rake: the seats' front edge stands on the floor one riser up, and each row is RowRise higher RowDepth further out
float RakeFrontY() { return FloorY + RowRise; }
float RakeB() { return RowDepth / RowRise; }
float RakeA() { return SeatInner - RakeB() * RakeFrontY(); }
float RakeTopY() { return RakeFrontY() + (SeatOuter - SeatInner) / RakeB(); }

//The angle of the performers' entrance from a point's bearing, wrapped to [-pi, pi]
float FromEntrance(float bearing)
{
    float delta = bearing - EntranceBearing;
    return delta - TWO_PI * floor((delta + PI) / TWO_PI);
}

//Whether the seats are cut away here for the entrance: its half-width as an angle at the front row
bool InEntrance(float3 p)
{
    float bearing = atan2(p.z, p.x);
    return abs(FromEntrance(bearing)) * SeatInner < EntranceWidth * 0.5;
}

Hit Trace(float3 o, float3 d)
{
    Hit best;
    best.T = 1e6;
    best.Kind = HIT_NONE;
    best.Normal = float3(0.0, 1.0, 0.0);
    best.Spot = 0;

    //The floor
    if (d.y < -1e-5) Consider(best, (FloorY - o.y) / d.y, HIT_FLOOR, float3(0.0, 1.0, 0.0));

    //The curb: its inner face, its outer face and its top
    HitCylinder(best, o, d, CurbRadius, FloorY, FloorY + CurbHeight, HIT_CURB);
    HitCylinder(best, o, d, CurbRadius + CurbThickness, FloorY, FloorY + CurbHeight, HIT_CURB);
    if (abs(d.y) > 1e-5)
    {
        float t = (FloorY + CurbHeight - o.y) / d.y;
        float2 p = o.xz + t * d.xz;
        float r = length(p);
        if (r >= CurbRadius && r <= CurbRadius + CurbThickness) Consider(best, t, HIT_CURB, float3(0.0, 1.0, 0.0));
    }

    //The seats: the rake, and the front riser under its first row - both cut away for the entrance, a hit that falls in
    //the gap simply dropped (the gap's two side walls are tested on their own below, and are what the eye meets there)
    Hit seats;
    seats.T = 1e6;
    seats.Kind = HIT_NONE;
    seats.Normal = float3(0.0, 1.0, 0.0);
    seats.Spot = 0;
    HitCone(seats, o, d, RakeA(), RakeB(), RakeFrontY(), RakeTopY(), HIT_SEATS);
    HitCylinder(seats, o, d, SeatInner, FloorY, RakeFrontY(), HIT_SEATS);
    if (seats.Kind != HIT_NONE && !InEntrance(o + seats.T * d)) Consider(best, seats.T, HIT_SEATS, seats.Normal);

    //The side wall, the canvas
    HitCylinder(best, o, d, WallRadius, FloorY, WallTopY, HIT_WALL);
    HitCone(best, o, d, RoofA(), RoofB(), WallTopY, CrownY, HIT_ROOF);

    //The crown closes the canvas: a short gilded band round the opening and a cupola over it. The cone ran on to its
    //apex once, and forty-four stripes converging on one point are a moire at any resolution.
    HitCylinder(best, o, d, CrownRadius, CrownY, CrownY + CROWN_BAND, HIT_ROOF);
    if (d.y > 1e-5)
    {
        float t = (CrownY + CROWN_BAND - o.y) / d.y;
        float2 c = o.xz + t * d.xz;
        if (dot(c, c) <= CrownRadius * CrownRadius) Consider(best, t, HIT_ROOF, float3(0.0, -1.0, 0.0));
    }

    //The entrance's two side walls: the radial planes the seats are cut along. Tested whatever the rake answered, since
    //they only exist inside the seats' own volume, where the rake in front of them is nearer everywhere but the gap.
    [unroll]
    for (int side = -1; side <= 1; side += 2)
    {
        float bearing = EntranceBearing + side * EntranceWidth * 0.5 / SeatInner;
        float3 n = float3(-sin(bearing), 0.0, cos(bearing));
        float denom = dot(d, n);

        if (abs(denom) > 1e-5)
        {
            float t = -dot(o, n) / denom;
            float3 p = o + t * d;
            float r = dot(p.xz, float2(cos(bearing), sin(bearing)));
            float rakeY = RakeFrontY() + (r - SeatInner) / RakeB();

            if (r >= SeatInner && r <= SeatOuter && p.y >= FloorY && p.y <= rakeY)
                Consider(best, t, HIT_GATE, denom < 0.0 ? n : -n);
        }
    }

    //The king poles: vertical cylinders off the axis, from the floor to the canvas
    [unroll]
    for (int i = 0; i < MAX_POLES; i++)
    {
        if (i < (int)KingPoleCount)
        {
            float angle = (i + 0.5) * TWO_PI / KingPoleCount;
            float2 centre = float2(cos(angle), sin(angle)) * KingPoleRadius;
            float2 oc = o.xz - centre;
            float a = dot(d.xz, d.xz);
            float b = dot(oc, d.xz);
            float c = dot(oc, oc) - KingPoleThickness * KingPoleThickness;
            float disc = b * b - a * c;

            if (a > 1e-8 && disc >= 0.0)
            {
                float t = (-b - sqrt(disc)) / a;
                float y = o.y + t * d.y;
                //Under the canvas only: the roof's radius at this height is still outside the pole
                if (y >= FloorY && (RoofA() + RoofB() * y) >= KingPoleRadius)
                {
                    float2 p = oc + t * d.xz;
                    Consider(best, t, HIT_POLE, float3(p.x, 0.0, p.y) / KingPoleThickness);
                }
            }
        }
    }

    //The spotlights and the ring they hang from, where the ray comes near them at all
    [branch]
    if (NearRig(o, d, 0.0))
    {
        HitTruss(best, o, d);

        [unroll]
        for (int spot = 0; spot < SPOT_COUNT; spot++)
        {
            //And each lamp only where the ray passes within RIG_REACH of its pivot
            float3 op = Pivot(spot) - o;
            float along = dot(op, d);

            [branch]
            if (dot(op, op) - along * along <= RIG_REACH * RIG_REACH) HitFixture(best, o, d, spot);
        }
    }

    return best;
}

//--- Light ---------------------------------------------------------------------------------------------

//What the spots put on a surface at p with normal n: each a cone, soft between its two angles, inverse square about
//SpotReach
float3 SpotLight(float3 p, float3 n)
{
    float3 sum = 0.0;

    [unroll]
    for (int i = 0; i < SPOT_COUNT; i++)
    {
        float3 toLight = SpotPosition[i] - p;
        float distance = length(toLight);
        float3 l = toLight / distance;
        float cone = smoothstep(SpotCosOuter, SpotCosInner, dot(-l, SpotDirection[i]));
        float falloff = SpotReach * SpotReach / (distance * distance);

        sum += SpotColor[i] * cone * falloff * saturate(dot(n, l));
    }

    return sum;
}

//How much of a spot's beam the ray scatters back between 0 and tMax, for one cone half-angle: the interval of the ray
//inside the cone, found in closed form, and the point light's airlight integral over it,
//integral of 1 / (h2 + (t - tc)2) dt = (atan((t - tc) / h)) / h.
float BeamIntegral(float3 o, float3 d, float3 apex, float3 axis, float cosAngle, float tMax, out float tMid)
{
    tMid = 0.0;
    float3 co = o - apex;
    float dd = dot(d, axis);
    float cd = dot(co, axis);
    float cos2 = cosAngle * cosAngle;

    float a = dd * dd - cos2;
    float b = 2.0 * (dd * cd - cos2 * dot(d, co));
    float c = cd * cd - cos2 * dot(co, co);
    float disc = b * b - 4.0 * a * c;
    if (disc < 0.0 || abs(a) < 1e-6) return 0.0;

    float s = sqrt(disc);
    float r0 = (-b - s) / (2.0 * a);
    float r1 = (-b + s) / (2.0 * a);
    float lo = min(r0, r1);
    float hi = max(r0, r1);

    float t0, t1;
    if (a < 0.0)
    {
        //Inside between the roots, on the forward nappe only
        t0 = lo;
        t1 = hi;
        if (cd + dd * 0.5 * (lo + hi) < 0.0) return 0.0;
    }
    else
    {
        //Inside outside the roots: the half that lies on the forward nappe
        if (cd + dd * (hi + 1.0) >= 0.0)
        {
            t0 = hi;
            t1 = 1e5;
        }
        else
        {
            t0 = -1e5;
            t1 = lo;
        }
    }

    t0 = max(t0, 0.0);
    t1 = min(t1, tMax);
    if (t1 <= t0) return 0.0;

    float tc = dot(apex - o, d);
    float h = sqrt(max(dot(apex - o, apex - o) - tc * tc, 0.25));
    tMid = 0.5 * (t0 + t1);

    return (atan((t1 - tc) / h) - atan((t0 - tc) / h)) / h;
}

//Each lamp's glow in the dusty air round its lens - a closest-approach halo, widened to the pixel like the bulbs and
//weighted by how squarely the lens faces the camera, so it flares when the lamp points at the lens and is a faint rim
//from the side. Occluded by whatever stands nearer than the lens.
float3 LampGlows(float3 o, float3 d, float tMax)
{
    float3 sum = 0.0;

    [unroll]
    for (int i = 0; i < SPOT_COUNT; i++)
    {
        float3 lens = SpotPosition[i] + SpotDirection[i] * (FIXTURE_FRONT + 0.05);
        float3 ol = lens - o;
        float along = dot(ol, d);
        float h2 = max(dot(ol, ol) - along * along, 0.0);
        if (along <= 0.0 || along > tMax + FIXTURE_RADIUS || h2 > GLOW_MARGIN * GLOW_MARGIN) continue;

        float facing = saturate(dot(-d, SpotDirection[i]));
        float size = max(GLOW_RADIUS, along * PixelAngle * 1.5);
        float energy = (GLOW_RADIUS * GLOW_RADIUS) / (size * size);
        sum += SpotColor[i] * (0.15 + 2.5 * facing * facing * facing) * energy * exp(-h2 / (size * size));
    }

    return sum;
}

float3 Beams(float3 o, float3 d, float tMax)
{
    float3 sum = 0.0;

    [unroll]
    for (int i = 0; i < SPOT_COUNT; i++)
    {
        float tMid, unused;
        float inner = BeamIntegral(o, d, SpotPosition[i], SpotDirection[i], SpotCosInner, tMax, unused);
        float outer = BeamIntegral(o, d, SpotPosition[i], SpotDirection[i], SpotCosOuter, tMax, tMid);

        //Dust in the beam: the density of the air the ray crosses it in, a slow 3D noise drifting up and across, so a
        //beam is a shaft of lit dust that breathes rather than a flat cone of light. Only where the ray is in the beam
        //at all: five noises on every pixel were 2.9 ms a frame at 3840x1600 (#690's review), and a beam is a sliver
        //of the frame.
        [branch]
        if (outer > 0.0)
        {
            float3 dustAt = o + d * tMid;
            float dust = 0.6 + 0.8 * saturate(0.5 + 0.6 * GradientNoise3(dustAt * 0.16 + float3(CircusTime * 0.04, -CircusTime * 0.11, 0.0)));
            sum += SpotColor[i] * (0.5 * inner + 0.5 * outer) * dust;
        }
    }

    return sum * BeamStrength * SpotReach * SpotReach;
}

//A string of bulbs: where it starts at the crown and ends at the wall, in its own vertical plane at `bearing`
float3 StringPoint(float bearing, float u)
{
    float r0 = CrownRadius + 2.5;
    float r1 = WallRadius - 2.0;
    float y0 = CrownY - 4.0;
    float y1 = WallTopY - 0.5;

    float r = lerp(r0, r1, u);
    float y = lerp(y0, y1, u) - BulbSag * 4.0 * u * (1.0 - u);
    return float3(cos(bearing) * r, y, sin(bearing) * r);
}

//The bulbs the ray passes before tMax: per string, the bulbs about where the ray crosses the string's plane, each a
//closest-approach glow with its core widened to the pixel and its energy kept
float3 Bulbs(float3 o, float3 d, float tMax)
{
    float3 sum = 0.0;
    float r0 = CrownRadius + 2.5;
    float r1 = WallRadius - 2.0;
    float bulbCount = floor((r1 - r0) / BulbSpacing);

    [loop]
    for (int k = 0; k < MAX_STRINGS; k++)
    {
        if (k >= (int)BulbStrings) break;

        float bearing = (k + 0.25) * TWO_PI / BulbStrings;
        float3 n = float3(-sin(bearing), 0.0, cos(bearing));
        float denom = dot(d, n);
        if (abs(denom) < 1e-4) continue;

        float t = -dot(o, n) / denom;
        if (t <= 0.0) continue;

        float3 p = o + t * d;
        float r = dot(p.xz, float2(cos(bearing), sin(bearing)));
        float u = saturate((r - r0) / (r1 - r0));
        float nearest = round(u * bulbCount);

        [unroll]
        for (int j = -1; j <= 1; j++)
        {
            float index = clamp(nearest + j, 0.0, bulbCount);
            float3 bulb = StringPoint(bearing, index / bulbCount);
            float3 ob = bulb - o;
            float along = dot(ob, d);
            if (along <= 0.0 || along > tMax) continue;

            float h2 = max(dot(ob, ob) - along * along, 0.0);
            float footprint = along * PixelAngle;
            float core = max(0.28, footprint * 0.9);
            float energy = (0.28 * 0.28) / (core * core);

            //A flicker so faint it is life rather than a fault: each bulb its own phase
            float flicker = 0.93 + 0.07 * sin(CircusTime * 2.3 + index * 1.7 + k * 5.1);

            //The core alone, and no halo of its own: the core is over the glare threshold, so the bloom gives it its
            //glow. A halo drawn here could not be continuous - only the three bulbs about where the ray crosses the
            //string's plane are visited, and a ray grazing the plane passes close to bulbs far from that crossing - so
            //it lost a piece of itself wherever the nearest bulb changed, and stood on the canvas as blocks.
            sum += BulbColor * flicker * energy * exp(-h2 / (core * core));
        }
    }

    return sum;
}

//The footlights' lamps, glows on the curb like the bulbs (their light on the island is SceneLights', on the shared
//effect), and the pool each throws on the sawdust and the curb round it
float3 FootlightPoint(int k)
{
    float bearing = (k + 0.5) * TWO_PI / FOOTLIGHT_COUNT;
    float radius = CurbRadius - FootlightInset;
    return float3(cos(bearing) * radius, FloorY + FootlightHeight, sin(bearing) * radius);
}

float3 Footlights(float3 o, float3 d, float tMax)
{
    float3 sum = 0.0;

    [unroll]
    for (int k = 0; k < FOOTLIGHT_COUNT; k++)
    {
        float3 ob = FootlightPoint(k) - o;
        float along = dot(ob, d);
        if (along <= 0.0 || along > tMax + 0.5) continue;

        float h2 = max(dot(ob, ob) - along * along, 0.0);
        float core = max(0.45, along * PixelAngle * 0.9);
        float energy = (0.45 * 0.45) / (core * core);
        sum += FootlightColor * 6.0 * (energy * exp(-h2 / (core * core)) + 0.05 * exp(-h2 / 12.0));
    }

    return sum;
}

float3 FootlightPools(float3 p, float3 n)
{
    float3 sum = 0.0;

    [unroll]
    for (int k = 0; k < FOOTLIGHT_COUNT; k++)
    {
        float3 toLight = FootlightPoint(k) - p;
        float distance = length(toLight);
        float atten = saturate(1.0 - distance / 22.0);
        sum += FootlightColor * atten * atten * saturate(dot(n, toLight / distance) * 0.8 + 0.2);
    }

    return sum;
}

//How much the bulb strings warm the canvas at a point of the roof: the nearest string's angular distance, as a glow
float StringWarmth(float bearing, float r)
{
    float spacing = TWO_PI / BulbStrings;
    float offset = bearing - 0.25 * spacing;
    float delta = (offset - spacing * round(offset / spacing)) * r;
    return exp(-delta * delta / 18.0);
}

//--- Shading -------------------------------------------------------------------------------------------

float3 HouseIrradiance(float3 n)
{
    //From above, the canvas and the lamps under it; a little from the sawdust below
    return HouseLight * (0.55 + 0.45 * n.y) + HouseLight * 0.25 * saturate(-n.y);
}

//0 on even stripes, 1 on odd ones, blended over `width` (the pixel's size in stripe units) across each boundary
float StripeMix(float coordinate, float width)
{
    return saturate(0.5 - sin(PI * coordinate) / (2.0 * PI * max(width, 1e-4)));
}

float3 ShadeRoof(float3 p, float3 n, float footprint)
{
    float bearing = atan2(p.z, p.x);
    float r = length(p.xz);

    //Over the canvas: the cupola, dark cloth with a gilded ring round a medallion, or the gilded band with its lamps
    if (p.y > CrownY + 0.01)
    {
        if (n.y < -0.5)
        {
            float ring = r / CrownRadius;
            float aa = max(0.01, footprint / CrownRadius);
            float gilt = smoothstep(0.40 - aa, 0.40, ring) * (1.0 - smoothstep(0.46, 0.46 + aa, ring))
                + (1.0 - smoothstep(0.12, 0.12 + aa, ring));
            float3 cupola = lerp(CanvasRed * 0.6, CurbGold * 0.7, saturate(gilt));
            return cupola * (HouseLight * 0.35 + BulbColor * 0.012);
        }

        float lampsRound = bearing / TWO_PI * 24.0;
        float lampAcross = abs(frac(lampsRound) - 0.5) * CrownRadius * TWO_PI / 24.0;
        float lampUp = abs(p.y - (CrownY + CROWN_BAND * 0.5));
        float lamp = exp(-(lampAcross * lampAcross + lampUp * lampUp) / 0.18);
        float3 band = CurbGold * 0.75 * (HouseIrradiance(n) * 0.6 + BulbColor * 0.03);
        return band + BulbColor * 0.6 * lamp;
    }

    //Which panel, and where across it: 0 and 1 are the two seams
    float panels = bearing / TWO_PI * PanelCount;
    float across = frac(panels);
    //The two colours alternate panel by panel, blended across each boundary over the pixel's own width there: the sign
    //of a sine with one half-period a panel, divided by its slope in pixels, the way a stripe is antialiased. A hard
    //switch on the panel's index stood as a staircase down every edge of the roof.
    float panelWidth = max(r * TWO_PI / PanelCount, 1e-3);
    float3 albedo = lerp(CanvasRed, CanvasCream, StripeMix(panels, footprint / panelWidth));

    //The belly: the canvas sags between its seams, so it faces a little towards each seam on either side of the middle,
    //and deeper towards the wall than at the crown, where the panels are narrow and pulled tight
    float depth = saturate((r - CrownRadius) / (WallRadius - CrownRadius));
    float3 tangent = normalize(float3(-sin(bearing), 0.0, cos(bearing)));
    float tilt = (across - 0.5) * 2.0 * PanelSag * 0.12 * depth;
    float3 shadingNormal = normalize(n + tangent * tilt);

    //The seam: a rope line along each panel's edge, darker, a few centimetres wide, widened to the pixel
    float seamWidth = max(0.12, footprint) / max(r * TWO_PI / PanelCount, 1e-3);
    float seam = 1.0 - smoothstep(0.0, seamWidth, min(across, 1.0 - across));

    //Creases running down the cloth, faint, and grime gathering towards the wall. Sampled on a circle in the noise's
    //own space rather than on the bearing, which jumps by two pi behind the island and stood there as a seam.
    float crease = Fbm3(float3(cos(bearing) * 60.0, sin(bearing) * 60.0, r * 0.05), 3);
    albedo *= (0.92 + 0.10 * crease) * (1.0 - 0.18 * seam) * lerp(1.0, 0.82, depth * depth);

    //The canvas's edge at the crown, bound in gilded iron
    float crownBand = 1.0 - smoothstep(0.8, 0.8 + max(0.2, footprint), r - CrownRadius);
    albedo = lerp(albedo, CurbGold * 0.7, crownBand);

    float3 light = HouseIrradiance(shadingNormal) * (0.22 + 0.78 * depth * depth);
    light += BulbColor * 0.024 * StringWarmth(bearing, r);
    light += SpotLight(p, shadingNormal) * 0.6;

    return albedo * light;
}

float3 ShadeWall(float3 p, float3 n, float footprint)
{
    float bearing = atan2(p.z, p.x);
    float arc = bearing * WallRadius;

    //The valance under the canvas: scalloped, alternating the roof's colours
    float scallopPeriod = WallRadius * TWO_PI / (PanelCount * 2.0);
    float scallop = frac(arc / scallopPeriod);
    float valanceDepth = 2.6 + 1.2 * sqrt(saturate(1.0 - pow(2.0 * scallop - 1.0, 2.0)));
    float aboveValance = p.y - (WallTopY - valanceDepth);
    float valance = smoothstep(-max(0.08, footprint), max(0.08, footprint), aboveValance);
    float3 valanceColor = lerp(CanvasRed, CanvasCream, StripeMix(arc / scallopPeriod, footprint / scallopPeriod));
    valanceColor = lerp(valanceColor, CurbGold * 0.8, 1.0 - smoothstep(0.15, 0.35, aboveValance));

    //The curtain: red velvet hung in folds, about one every two units, shaded as a cosine across it - a WHOLE NUMBER of
    //folds round the wall, and of the slower wander in their spacing, so the pattern meets itself where the bearing
    //wraps instead of standing there as a seam (it was a fold every 2.2 units exactly, which the circumference is not)
    float folds = round(WallRadius * TWO_PI / 2.2);
    float fold = sin(bearing * folds + 0.6 * sin(bearing * 13.0));
    float3 tangent = float3(-sin(bearing), 0.0, cos(bearing));
    float3 foldNormal = normalize(n + tangent * fold * 0.45);
    float sheen = pow(saturate(1.0 - abs(dot(foldNormal, normalize(CameraPosition - p)))), 3.0) * 0.35;

    //The performers' entrance: an arch of gold round a dark opening, warm light spilling from inside
    float fromEntrance = FromEntrance(bearing) * WallRadius;
    float archHalf = EntranceWidth * 0.55;
    float archTop = FloorY + 11.0 + 2.5 * sqrt(saturate(1.0 - pow(fromEntrance / archHalf, 2.0)));
    float inArch = (abs(fromEntrance) < archHalf && p.y < archTop) ? 1.0 : 0.0;
    float archFrame = (abs(fromEntrance) < archHalf + 0.45 && p.y < archTop + 0.45) ? 1.0 - inArch : 0.0;

    float3 light = HouseIrradiance(foldNormal) + SpotLight(p, foldNormal);
    float3 curtain = CurtainColor * light * (0.75 + 0.25 * fold) + CurtainColor * sheen;
    float3 color = lerp(curtain, valanceColor * (HouseIrradiance(n) + SpotLight(p, n)), valance);

    //Through the arch the passage to the ring stables: dark, a warm light low down where it turns out of sight
    float3 doorway = float3(0.10, 0.05, 0.022) * (0.25 + 0.75 * saturate((archTop - p.y) / 12.0));
    color = lerp(color, doorway, inArch);
    color = lerp(color, CurbGold * 0.6 * (HouseIrradiance(n) + SpotLight(p, n)), archFrame);

    return color;
}

float3 ShadeSeats(float3 p, float3 n, float footprint)
{
    float r = length(p.xz);
    float bearing = atan2(p.z, p.x);
    float3 radial = float3(p.x, 0.0, p.z) / max(r, 1e-3);

    //The front riser, under the first row: plain dark wood with a gold rail on it
    if (r < SeatInner + 0.05 && p.y < RakeFrontY())
    {
        float rail = smoothstep(RakeFrontY() - 0.35, RakeFrontY() - 0.25, p.y);
        float3 albedo = lerp(SeatWood * 0.8, CurbGold * 0.7, rail);
        return albedo * (HouseIrradiance(-radial) * 0.8 + SpotLight(p, -radial));
    }

    //Which row, and where in it: a row's front third is the seat back and seat (red), the rest the tread (wood)
    float rowCoord = (r - SeatInner) / RowDepth;
    float row = floor(rowCoord);
    float inRow = frac(rowCoord);

    //The aisles: every so often a stair cuts the bowl, wood and twice the steps
    float arc = bearing * r;
    float aislePeriod = r * TWO_PI / AisleCount;
    float aisleDistance = abs(frac(arc / aislePeriod + 0.5) - 0.5) * aislePeriod;
    float aisle = 1.0 - smoothstep(AisleWidth * 0.5 - max(0.1, footprint), AisleWidth * 0.5, aisleDistance);

    //Individual seats along a row, with a dark gap between neighbours: a whole number of seats in each row at that
    //row's radius, so the last seat of a row is as wide as the first rather than cut short where the bearing wraps
    float seatsInRow = max(round(TWO_PI * (SeatInner + (row + 0.5) * RowDepth) / 1.15), 1.0);
    float seatWidth = TWO_PI * r / seatsInRow;
    float seatAcross = frac(bearing / TWO_PI * seatsInRow);
    float gapWidth = max(0.06, footprint) / seatWidth;
    float gap = 1.0 - smoothstep(0.0, gapWidth, min(seatAcross, 1.0 - seatAcross));

    float edge = max(0.02, footprint / RowDepth);
    float seatPart = 1.0 - smoothstep(0.42 - edge, 0.42 + edge, inRow);

    //The profile's normal: the seat back faces the ring (inward and a little up), the tread faces up
    float3 backNormal = normalize(-radial + float3(0.0, 0.35, 0.0));
    float3 treadNormal = float3(0.0, 1.0, 0.0);
    float3 shadingNormal = normalize(lerp(treadNormal, backNormal, seatPart * (1.0 - aisle)));

    float3 seatColor = SeatRed * (0.85 + 0.15 * Hash21(float2(row, floor(bearing / TWO_PI * seatsInRow)))) * (1.0 - 0.55 * gap);
    float3 albedo = lerp(SeatWood, seatColor, seatPart);
    albedo = lerp(albedo, SeatWood * 1.25 * (0.8 + 0.2 * step(0.5, frac(rowCoord * 2.0))), aisle);

    //Under the back of each row the tread is in the shadow of the row behind
    float occlusion = lerp(0.55, 1.0, smoothstep(0.42, 1.0, inRow)) * (1.0 - seatPart) + seatPart;

    float3 light = HouseIrradiance(shadingNormal) * occlusion + SpotLight(p, shadingNormal);
    return albedo * light;
}

//The lamps' cans, rods and the truss: dark painted metal, lit only by the house light and a little of the bulbs
float3 ShadeRig(float3 n)
{
    return float3(0.035, 0.032, 0.03) * (HouseIrradiance(n) * 1.4 + BulbColor * 0.01);
}

//A lens, seen from in front: the lamp's own colour, hottest at its centre and over the glare threshold so it blooms
float3 ShadeLens(float3 p, int spot)
{
    float3 axis = SpotDirection[spot];
    float3 q = p - SpotPosition[spot];
    float3 across = q - dot(q, axis) * axis;
    float rim = saturate(length(across) / FIXTURE_RADIUS);
    return SpotColor[spot] * LENS_GAIN * (0.35 + 0.65 * (1.0 - rim * rim));
}

float3 ShadeGate(float3 p, float3 n, float footprint)
{
    float r = length(p.xz);
    float rakeY = RakeFrontY() + (r - SeatInner) / RakeB();
    float edge = 1.0 - smoothstep(0.25, 0.25 + max(0.05, footprint), rakeY - p.y);
    float boards = 0.85 + 0.15 * step(0.5, frac(p.y / 0.6));
    float3 albedo = lerp(SeatWood * 0.7 * boards, CurbGold * 0.7, edge);

    return albedo * (HouseIrradiance(n) * 0.7 + SpotLight(p, n));
}

float3 ShadeFloor(float3 p, float footprint)
{
    float r = length(p.xz);
    float3 n = float3(0.0, 1.0, 0.0);

    //Inside the ring the sawdust, raked in circles round the island; outside it trodden earth and boards
    float grain = Fbm2BandLimited(p.xz * 0.9, 4, footprint * 0.9);
    float rake = sin(r * 5.0 + 1.5 * GradientNoise2(p.xz * 0.15));
    float rakeFade = saturate(1.0 - footprint * 5.0 / PI);
    float3 sawdust = SawdustColor * (0.88 + 0.16 * grain + 0.06 * rake * rakeFade);
    float3 outside = FloorColor * (0.85 + 0.25 * grain);
    float inRing = 1.0 - smoothstep(CurbRadius - max(0.05, footprint), CurbRadius, r);
    float3 albedo = lerp(outside, sawdust, inRing);

    float3 light = HouseIrradiance(n) * 0.8 + SpotLight(p, n) + FootlightPools(p, n);
    return albedo * light;
}

float3 ShadeCurb(float3 p, float3 n, float footprint)
{
    //Red faces, a gold lip on the top and a gold line along the foot
    float height = (p.y - FloorY) / CurbHeight;
    float top = step(0.98, height);
    float lip = smoothstep(0.80, 0.82, height) + (1.0 - smoothstep(0.10, 0.12, height));
    float3 albedo = lerp(CurbRed, CurbGold, saturate(lip + top));

    float3 light = HouseIrradiance(n) + SpotLight(p, n) + FootlightPools(p, n);
    return albedo * light;
}

float3 ShadePole(float3 p, float3 n, float footprint)
{
    //Painted wood, with a gilded band every eight units and grain running up it
    float band = 1.0 - smoothstep(0.35, 0.35 + max(0.05, footprint), abs(frac((p.y - FloorY) / 8.0) - 0.5) * 8.0 - 3.3);
    float grain = GradientNoise3(float3(n.x * 0.5, n.z * 0.5, p.y * 0.3));
    float3 albedo = lerp(PoleColor * (0.9 + 0.15 * grain), CurbGold * 0.8, band);

    float3 light = HouseIrradiance(n) + SpotLight(p, n);
    return albedo * light;
}

//--- The pass ------------------------------------------------------------------------------------------

struct CircusVertexInput
{
    float4 Position : POSITION0;
};

struct CircusVertexOutput
{
    float4 Position : SV_POSITION;
    float3 Ray : TEXCOORD0;
};

CircusVertexOutput CircusVS(CircusVertexInput input)
{
    CircusVertexOutput output;
    output.Position = float4(input.Position.xy, 0.0, 1.0);

    //The corner unprojected to the far plane; the pixel shader normalizes the interpolated ray
    output.Ray = mul(float4(input.Position.xy, 1.0, 0.0), ViewRayBasis).xyz;

    return output;
}

float4 CircusScene(CircusVertexOutput input, uniform bool detail) : COLOR
{
    float3 o = CameraPosition;
    float3 d = normalize(input.Ray);

    Hit hit = Trace(o, d);
    float3 p = o + hit.T * d;
    float footprint = hit.T * PixelAngle;

    float3 color = 0.0;

    if (hit.Kind == HIT_FLOOR) color = ShadeFloor(p, footprint);
    else if (hit.Kind == HIT_CURB) color = ShadeCurb(p, hit.Normal, footprint);
    else if (hit.Kind == HIT_SEATS) color = ShadeSeats(p, hit.Normal, footprint);
    else if (hit.Kind == HIT_WALL) color = ShadeWall(p, hit.Normal, footprint);
    else if (hit.Kind == HIT_ROOF) color = ShadeRoof(p, hit.Normal, footprint);
    else if (hit.Kind == HIT_POLE) color = ShadePole(p, hit.Normal, footprint);
    else if (hit.Kind == HIT_GATE) color = ShadeGate(p, hit.Normal, footprint);
    else if (hit.Kind == HIT_FIXTURE || hit.Kind == HIT_TRUSS) color = ShadeRig(hit.Normal);
    else if (hit.Kind == HIT_LENS) color = ShadeLens(p, hit.Spot);

    //The dusty air: what lies further off sinks into a warm haze lit by the house lights
    float haze = 1.0 - exp(-hit.T * HazeDensity);
    color = lerp(color, HazeColor, haze);

    //And what the air between carries: the spots' beams, and the bulbs hanging in it
    if (detail) color += Beams(o, d, hit.T);
    color += Bulbs(o, d, hit.T);
    color += Footlights(o, d, hit.T);

    //The glow fades to nothing a few of its radii from a lens (exp(-h2 / size2)), so past GLOW_MARGIN it is never tried
    [branch]
    if (NearRig(o, d, GLOW_MARGIN)) color += LampGlows(o, d, hit.T);

    return float4(color, 1.0);
}

float4 CircusPS(CircusVertexOutput input) : COLOR { return CircusScene(input, true); }
float4 CircusReducedPS(CircusVertexOutput input) : COLOR { return CircusScene(input, false); }

//--- The floor's depth -----------------------------------------------------------------------------------
//The floor as an annulus round the island, drawn with colour writes off (the caller's blend state) so it writes only
//depth: what falls below the sawdust is hidden by it, and nothing it draws can be seen.

struct FloorVertexOutput
{
    float4 Position : SV_POSITION;
};

FloorVertexOutput FloorDepthVS(float4 position : POSITION0)
{
    FloorVertexOutput output;
    output.Position = mul(position, WorldViewProjection);
    return output;
}

float4 FloorDepthPS(FloorVertexOutput input) : COLOR { return float4(0.0, 0.0, 0.0, 0.0); }

technique Circus
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CircusVS();
        PixelShader = compile PS_SHADERMODEL CircusPS();
    }
};

technique CircusReduced
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL CircusVS();
        PixelShader = compile PS_SHADERMODEL CircusReducedPS();
    }
};

technique CircusFloorDepth
{
    pass P0
    {
        VertexShader = compile VS_SHADERMODEL FloorDepthVS();
        PixelShader = compile PS_SHADERMODEL FloorDepthPS();
    }
};
