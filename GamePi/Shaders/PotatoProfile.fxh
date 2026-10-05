//The two compilers GamePi's effects go through (#808).
//
//GamePi itself is OpenGL by way of MojoShader, which stops at Shader Model 3.0 (compile.ps1, into Compiled/). The
//Windows build's "potato" run - the Pi's picture on a desktop, to look at it - loads the very same sources compiled for
//DirectX 11 by Prazsky.Shaders, and that compiler takes nothing below 4.0. So a technique names its two shaders'
//profiles through these, and two more things differ between the two builds of an effect, both about a pixel shader and
//the position: the one that asks where on the screen it is (PotatoModel.fx's BallDitherPS) spells it differently, and
//POTATO_PIXEL_HEAD below.
#if OPENGL
#define POTATO_VS vs_3_0
#define POTATO_PS ps_3_0
#else
#define POTATO_VS vs_4_0
#define POTATO_PS ps_4_0
#endif

//THE HEAD OF A PIXEL SHADER'S INPUT. Shader Model 3 forbids a pixel shader the position, so the six ports of desktop
//effects give theirs a structure of its own that leaves it out. DirectX 11 pairs a vertex shader's outputs with a pixel
//shader's inputs by their ORDER - register by register, whatever the semantics say - so there that structure has to
//begin with the position the vertex output begins with. Left out, every input reads its neighbour's value and nothing
//says so: the effect compiles, loads and draws (seen: the confetti fell black, the paper's colour being read out of the
//position). This is that first member under DirectX and nothing at all under OpenGL, whose compiled effects it leaves
//byte for byte what they were.
#if OPENGL
#define POTATO_PIXEL_HEAD
#else
#define POTATO_PIXEL_HEAD float4 PixelPosition : SV_POSITION;
#endif
