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

//A branch on a uniform with a derivative inside it: Shader Model 5.0 branches, a uniform being one value across a quad;
//3.0 cannot tell a uniform from a varying there and refuses to (X3528, "can't force branch with gradients on
//non-inputs"), so it takes both sides and selects.
#define BRANCH_UNIFORM_GRADIENTS [flatten]

//A walk over the live part of a uniform array, `count` of its `max`: Shader Model 5.0 loops to the count; under GL the
//loop is unrolled to the array's size and each step gated by the count, so every index is a constant. A loop to a
//uniform count reads the array through a relative address, which MojoShader translates for one array of a program at
//most (GamePi's rule) and in some programs not at all ("relative address needs replicate swizzle", the savanna's).
//The body follows the macro as a loop's body would.
#define ARRAY_LOOP(i, count, max) [unroll] for (int i = 0; i < max; i++) if (i < count)

//A loop to a uniform count with no array to index. Shader Model 3.0 keeps such a count in an integer register, and
//MonoGame's GL runtime never uploads one (it has glUniform4fv and no glUniform4iv), so under GL the count reads 0 and
//the loop never runs: it compiles without a word, and the frame comes out black (Tonemap) or without what the loop
//adds. Under GL the loop runs to `max`, a literal, and breaks at the count, which is a float comparison. `max` is at
//most 255, Shader Model 3.0's longest rep. ARRAY_LOOP is the same cure for a loop that indexes an array.
#define COUNT_LOOP(i, count, max) [loop] for (int i = 0; i < max; i++) if (i >= count) break; else

#else

#define VS_SHADERMODEL vs_5_0
#define PS_SHADERMODEL ps_5_0

#define FRONT_FACE(name) bool name : SV_IsFrontFace
#define FRONT_FACE_BEGIN(name)

#define BRANCH_UNIFORM_GRADIENTS [branch]

#define ARRAY_LOOP(i, count, max) [loop] for (int i = 0; i < count; i++)
#define COUNT_LOOP(i, count, max) [loop] for (int i = 0; i < count; i++)

#endif
