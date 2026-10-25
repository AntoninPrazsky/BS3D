//THE ROVER'S TRACKS (#746, the Moon; #745, Mars): a polyline the backdrop lays over the plain (RoverTracks.cs
//on the CPU, TRACK_POINTS points of world XZ with the distance along the track to each), printed in the ground as the
//two ruts a four-wheeled rover leaves - TRACK_GAUGE apart, each a tyre wide - with a little berm pushed up beside each
//rut and the chevron cleats of a wire-mesh wheel printed across its floor, the regolith in and beside them disturbed and
//darker, as every Apollo photograph of the rover's tracks shows it (references: 746-klein, 746-zimage). A height for the
//caller's normal and a share of disturbed ground for its colour; nothing about the light is decided here.
//
//Behind a branch on the track's own bounds: everywhere else the plain pays one comparison. Inside, the nearest segment is
//found by brute force over the runs whose box holds the pixel (TrackChunks).

static const int TRACK_POINTS = 24;

//x, z of each point and the distance along the track to it; points past TrackCount are not read
float4 TrackPath[TRACK_POINTS];
float TrackCount;          //how many points are laid; under 2 there is no track
float4 TrackBounds;        //min x, min z, max x, max z of the track, padded by its own width

//The track in TRACK_CHUNKS runs of TRACK_CHUNK segments, each with its own padded box (min x, min z, max x, max z): a
//pixel walks only the runs whose box holds it. One box round the whole track sent every pixel under it through all its
//segments, which cost a frame of the Mars plain 0.43 ms
static const int TRACK_CHUNK = 4;
static const int TRACK_CHUNKS = (TRACK_POINTS - 1 + TRACK_CHUNK - 1) / TRACK_CHUNK;
float4 TrackChunks[TRACK_CHUNKS];

static const float TRACK_GAUGE = 1.83;     //between the ruts' middles, world units (a unit is about a metre)
static const float TRACK_TYRE = 0.25;      //a rut's width
static const float TRACK_DEPTH = 0.03;     //how deep a rut is pressed
static const float TRACK_BERM = 0.012;     //how high the berm beside it stands
static const float CHEVRON_PITCH = 0.16;   //between two cleat prints along a rut
static const float TRACK_END_FADE = 30.0;  //how far from either end the tracks fade in under drifted dust

struct TrackPrint
{
    float Height;      //the print's relief, world units: negative in the ruts
    float Disturbed;   //0..1, how much of this pixel the wheels have turned over
};

TrackPrint RoverTrack(float2 p, float footprint)
{
    TrackPrint print;
    print.Height = 0.0;
    print.Disturbed = 0.0;

    [branch]
    if (TrackCount > 1.5 && p.x > TrackBounds.x && p.y > TrackBounds.y && p.x < TrackBounds.z && p.y < TrackBounds.w)
    {
        //The nearest segment: the signed distance across it and the distance along the track
        float best = 1e9;
        float lateral = 0.0;
        float along = 0.0;
        [loop]
        for (int c = 0; c < TRACK_CHUNKS; c++)
        {
            float4 box = TrackChunks[c];
            if (p.x < box.x || p.y < box.y || p.x > box.z || p.y > box.w) continue;

            [loop]
            for (int k = 0; k < TRACK_CHUNK; k++)
            {
            int i = c * TRACK_CHUNK + k;
            if (i + 1 < TrackCount)
            {
                float2 a = TrackPath[i].xy;
                float2 ab = TrackPath[i + 1].xy - a;
                float lengthSq = max(dot(ab, ab), 1e-6);
                float t = saturate(dot(p - a, ab) / lengthSq);
                float2 off = p - (a + ab * t);
                float d = dot(off, off);
                if (d < best)
                {
                    best = d;
                    float len = sqrt(lengthSq);
                    lateral = (ab.x * off.y - ab.y * off.x) / len;
                    along = TrackPath[i].z + t * len;
                }
            }
            }
        }

        //Across the nearer rut: 0 on its middle line, 1 at its edge
        float half = TRACK_TYRE * 0.5;
        float across = (abs(lateral) - TRACK_GAUGE * 0.5) / half;
        float u = abs(across);

        //Band-limited the way the grain is: the cleats go first, then the rut's shape, and past them what is left is the
        //darker band the wheels turned over, which reads at any distance
        float rutSeen = saturate(2.0 - footprint / half);
        float cleatSeen = saturate(1.0 - footprint / (CHEVRON_PITCH * 0.35));

        float trough = saturate(1.0 - u * u);
        float berm = saturate(1.0 - abs(u - 1.45) * 2.2);

        //A chevron across the floor: the cleat lines lean back from the rut's middle to both its edges
        float chevron = frac((along + u * half * 0.9) / CHEVRON_PITCH);
        float cleat = smoothstep(0.3, 0.45, chevron) * (1.0 - smoothstep(0.7, 0.85, chevron)) * trough;

        //Both ends fade in under the dust that drifts over an old track (#745), so it neither starts nor stops on a line
        float total = TrackPath[(int)TrackCount - 1].z;
        float ends = smoothstep(0.0, TRACK_END_FADE, along) * smoothstep(0.0, TRACK_END_FADE, total - along);

        print.Height = (berm * TRACK_BERM - trough * TRACK_DEPTH - cleat * TRACK_DEPTH * 0.5 * cleatSeen) * rutSeen * ends;
        float aa = footprint / half;
        print.Disturbed = saturate((1.0 - smoothstep(1.0 - aa, 1.0 + aa, u)) + 0.5 * berm) * ends;
    }

    return print;
}
