Local AI music generation exploration (ACE-Step 1.5 / acestep.cpp, run locally on
an AMD RX 6900 XT via Vulkan -- see each .json sidecar for the exact prompt and
parameters that produced the matching .wav).

Reference/exploration only, same spirit as Music.txt one level up. BS3D's real
music is entirely procedural (Game/Audio/ProceduralMusic.cs) -- fully authored
scores rendered from oscillators, no tracker file, no asset, no pipeline step.
Nothing here is wired into any content pipeline or asset build.

  menu-loop-v2.wav   59.2s, A minor, 100 BPM -- seamless loop (crossfaded),
                     brief taken from ProceduralMusic.cs::BakeMenu
  game-track-01.wav  90s, C major, 124 BPM -- energetic in-level piece, key/tempo
                     deliberately distinct from all five MusicTheme entries
