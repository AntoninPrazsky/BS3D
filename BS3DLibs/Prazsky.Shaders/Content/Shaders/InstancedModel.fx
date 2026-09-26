//Draws many instances of a rigid model in a single draw call per mesh part.
//The per-instance world matrix is supplied through a second vertex stream (TEXCOORD1-TEXCOORD4 hold its rows).
//Lighting replicates BasicEffect with EnableDefaultLighting and per-pixel (Blinn-Phong) shading,
//so instanced models look the same as those rendered through ModelRenderer.

//Both the Testbed and the map editor build this for DirectX now and get Shader Model 5.0. The editor
//used to build it for DesktopGL, where MojoShader capped shaders at 3.0 and everything 5.0-only had to
//sit behind #if OPENGL; it has since moved onto WindowsDX so it can render the balls exactly as the game
//does, tonemapping and all. There is no OPENGL build of this file any more, so nothing needs a fallback.
#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

//Every color that enters the lighting math - material colors, light colors, sky palette, texture
//samples - is authored or stored in sRGB, which is a display encoding, not a quantity of light.
//Adding, multiplying and averaging those numbers is only meaningful once they are back in linear
//radiance; Tonemap.fx encodes the result for the display again at the very end of the frame. Both the
//game and the map editor render into a linear HDR target and tonemap now, so this always decodes.
float3 SrgbToLinear(float3 color)
{
    //Jim Hejl's cubic fit of the sRGB curve - accurate to well under a display bit and no pow()
    return color * (color * (color * 0.305306011 + 0.682171111) + 0.012522878);
}

//Cloud shadows. The map editor has no weather and never sets the cloud uniforms, so there CloudCoverageGain
//stays zero and CloudSunlight falls straight through to a flat 1.0 - full sun, no shadow - on its own.
#include "Clouds.fxh"

//The shared noise library, for the one field in here a sum of waves genuinely cannot be: the ice's
//fracture net (#337). Everything else on a ball is built from sines on purpose - they are cheap, they
//band-limit exactly, and a surface wants a surface. A FRACTURE wants irregular closed cells, which is
//what no number of plane waves adds up to, and the library's own header is the argument for why.
#include "Noise.fxh"

//The sun's CAST shadows (#469, #470): the map's uniforms and the nine-tap PCF, in one copy with every other
//receiver. Everything drawn through this effect receives - the island, its drain, the gun, the city and the
//balls - because the tap sits in ShadePixel, which is the one place that knows how a pixel is lit. The
//InstancedDepth technique (InstancedModel/Depth.fxh) is the matching CASTER, and it is what puts the island
//and the gun INTO the map; the two halves share ShadowViewProjection, so there is one matrix and not two.
#include "Shadows.fxh"

//THE REST OF THE EFFECT, ONE FILE PER CONCERN (#581). It is still ONE Effect - the renderer switches technique
//on one shared set of parameters, and that is the whole reason every surface in the game draws through it - but
//the 7,500 lines it had grown to are split into the includes below, and every technique is in the file of what
//it draws. Three rules hold the split together:
//
//  - THE ORDER BELOW IS THE CONSTANT BUFFER'S LAYOUT. Every uniform lands in one $Globals buffer in declaration
//    order, and the compiler vectorises reads of neighbouring uniforms by where they are packed, so reordering
//    these includes (or moving a uniform between them) changes the compiled programs even though no line of
//    HLSL changed - #581 measured exactly that when it deleted four dead uniforms. A uniform therefore stays in
//    the file where it was first declared (the vinyl's own ones are in BallCommon.fxh), and the split was
//    verified by the compiled bytecode coming out identical.
//  - A FUNCTION IS IN SCOPE ONLY BELOW ITS INCLUDE. HLSL has no forward declarations across this, so a helper
//    two concerns share lives in the earlier file (the tint helpers in BallTint.fxh sit before the gem, the
//    plasma and the lava that call them), and a ball style cannot call anything in City.fxh.
//  - "THIS FILE" in a comment below means the effect as a whole: most of them were written while it was one.
//
//A new ball style is a new Ball<Style>.fxh included after BallHeavy.fxh and a row in InstancedModelRenderer's
//technique table. Its uniforms move everything declared after them, which is expected for a real change; the
//layout rule is only a promise that a change which is MEANT to be pure (a move, a split) compiles identically.
#include "InstancedModel/Common.fxh"
#include "InstancedModel/Lighting.fxh"
#include "InstancedModel/SceneRelief.fxh"
#include "InstancedModel/BallCommon.fxh"
#include "InstancedModel/BallVinyl.fxh"
#include "InstancedModel/BallBubble.fxh"
#include "InstancedModel/BallMarble.fxh"
#include "InstancedModel/BallWool.fxh"
#include "InstancedModel/BallMetal.fxh"
#include "InstancedModel/BallIce.fxh"
#include "InstancedModel/BallTint.fxh"
#include "InstancedModel/BallGem.fxh"
#include "InstancedModel/BallPlasma.fxh"
#include "InstancedModel/BallLava.fxh"
#include "InstancedModel/BallPorcelain.fxh"
#include "InstancedModel/BallStone.fxh"
#include "InstancedModel/BallHollow.fxh"
#include "InstancedModel/BallBomb.fxh"
#include "InstancedModel/BallZap.fxh"
#include "InstancedModel/BallAcid.fxh"
#include "InstancedModel/BallFrozen.fxh"
#include "InstancedModel/BallInfectious.fxh"
#include "InstancedModel/BallGravity.fxh"
#include "InstancedModel/BallHeavy.fxh"
#include "InstancedModel/BallWildcard.fxh"
#include "InstancedModel/Triplanar.fxh"
#include "InstancedModel/City.fxh"
#include "InstancedModel/Depth.fxh"
#include "InstancedModel/Glass.fxh"
#include "InstancedModel/PolishedMetal.fxh"
