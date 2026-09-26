//InstancedModel.fx: the two tint helpers the gem, the plasma and the lava share.

//===================================================================================================
//KEEPING THIRTEEN TINTS APART IN A STYLE THAT DOES NOT SHADE THEM DIRECTLY (#315). Two helpers shared
//by the gem, the plasma and the lava, which are the three styles measured tighter on some pair than the
//MOULDED VINYL is on its own worst one - the line every style has to clear, because the vinyl is what
//the thirteen tints were tuned against.
//
//THE FAULT IS THE PEAK NORMALISATION, AND IT IS THE SAME LINE IN BOTH EMISSIVE STYLES. Both the plasma
//and the lava take the tint NORMALISED to its peak channel, which is what makes the 8-ball glow white
//instead of not at all, and both headers are right that it is necessary. What neither saw is that it
//also throws away the VALUE axis - and the thirteen are separated on value quite as much as on hue, so
//every pair sharing a hue family arrives as one ball:
//
//    black  (0.045,0.045,0.05) / 0.05 -> (0.90,0.90,1.00) | silver (0.50,0.53,0.58) / 0.58 -> (0.86,0.91,1.00)
//    orange (1.00,0.50,0.03)  / 1.00 -> (1.00,0.50,0.03)  | brown  (0.42,0.24,0.11) / 0.42 -> (1.00,0.57,0.26)
//
//Measured whole-disc CIEDE2000 on Thirteen_Colors, each style under its own chapter's scene and dome:
//lava orange/brown 2.6 and black/silver 4.2, plasma black/silver 4.7, gem black/brown 4.9 and
//black/silver 4.5 - against a vinyl control whose OWN tightest pair is 7.9 on the same cluster. All
//five are now 8.0 or better.
//
//TintEmission is the answer to it: leave the normalisation alone - it decides the HUE, and both styles
//are right about it - and scale the emission's BRIGHTNESS by the tint's own luminance. Black keeps its
//white-hot discharge and burns dimmer than silver's; brown keeps its orange seam and burns dimmer than
//orange's. Compressed (a square root) and floored, because the tints span better than a hundred to one
//in linear luminance and a proportional map would leave the 8-ball with nothing to be seen by - which is
//the failure the normalisation was introduced to fix in the first place. The floor is the guarantee that
//it is still there.
//
//A SCALAR over the whole emission and not a factor on the hue, deliberately: the lava carries a seam's
//hottest core towards LavaIncandescent, a fixed near-white, so anything applied to the hue alone leaves
//those cores burning identically on all thirteen - and a narrow bright core is a large share of what the
//eye integrates over a ball at play size.
//
//SaturateTint is the gem's, and it is a different fault needing a different cure. Nothing there
//normalises anything: the stone drowns in an UNCOLOURED environment mirror (GemEnvironment 1.3 at full
//smoothness), so a tint with little chroma of its own barely reaches the pixel and black, brown and
//silver all arrive as the same grey pebble. Lifting their brightness would only make them MORE alike, so
//this lifts their CHROMA at constant luminance - the tint pushed out towards its own fully saturated
//form with its Rec. 709 luminance held exactly where it was. On a tint that is already neutral it is the
//identity by construction, and that is the property the gem needs: neutrals cannot be told apart by hue,
//and inflating them is precisely what would push them together.
//===================================================================================================

//The darkest tint's share of the brightest one's emission. Below about 0.15 the 8-ball's seams stop
//carrying it; above about 0.3 there is not enough value left between the pairs to tell them apart.
static const float TintEmissionFloor = 0.18;

//How brightly a tint's own emission burns: 1 at the brightest of the thirteen, TintEmissionFloor at the
//darkest, compressed so the fall is spread over the range rather than spent on the first stop.
float TintEmission(float3 primary)
{
    float luminance = saturate(dot(primary, float3(0.2126, 0.7152, 0.0722)));

    return lerp(TintEmissionFloor, 1.0, sqrt(luminance));
}

//The same tint pushed towards its own fully saturated form at UNCHANGED luminance. amount 0 is the tint
//as it stands, 1 is as saturated as that hue goes. A neutral tint is its own saturated form, so this
//cannot move one - which is the property the gem needs and the reason it is written this way round.
float3 SaturateTint(float3 primary, float amount)
{
    float peak = max(primary.r, max(primary.g, primary.b));
    float3 pure = primary / max(peak, 1e-3);

    float luminance = dot(primary, float3(0.2126, 0.7152, 0.0722));
    float pureLuminance = max(dot(pure, float3(0.2126, 0.7152, 0.0722)), 1e-3);

    return lerp(primary, pure * (luminance / pureLuminance), amount);
}
