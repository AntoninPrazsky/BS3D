//The two targets every shared effect compiles for (#1057): DirectX 11 (Shader Model 5.0, the desktop's, the Testbed's and the
//map editor's) and OpenGL ES through MojoShader (Shader Model 3.0, the Android build's desktop path, #1056). Each effect
//includes this in place of the profile it used to define, and writes what the two targets need apart through the macros
//below, so one source compiles for both and the DirectX build is what it was before this header existed.
//
//MonoGame's effect compiler defines OPENGL for a GL platform (DesktopGL, Android) and nothing for DirectX.

#if OPENGL

#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0

//Which side of a triangle is drawn: Shader Model 5.0 hands a bool (SV_IsFrontFace); 3.0 hands VFACE, a float positive on the
//front and negative on the back, which a bool cannot be read from directly (both signs are non-zero, so both read as true).
//A pixel shader declares FRONT_FACE(name) among its parameters and opens its body with FRONT_FACE_BEGIN(name), which under
//GL makes the bool the rest of the body reads.
#define FRONT_FACE(name) float name##Face : VFACE
#define FRONT_FACE_BEGIN(name) bool name = name##Face > 0;

#else

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#define FRONT_FACE(name) bool name : SV_IsFrontFace
#define FRONT_FACE_BEGIN(name)

#endif
