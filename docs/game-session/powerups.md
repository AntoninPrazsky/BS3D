# Power-ups: Swap, Brake and Cut

Part of [The game: a session](../game-session.md).

A **special ball is a map kind; a power-up is the player's E/Q/R** (#213, #392). The owner's order was Swap, Brake, then the anchor cut, each **with a price so the queue and the ceiling stay constraints**.

## The grants: one constant each, the campaign's shape

| Tool | Key | From block | Charges |
|---|---|---|---|
| Swap | E / pad X | `LevelSet.SWAP_FROM_BLOCK` (3) | 1 a level (`SwapChargesAt`) |
| Brake | Q / pad Y | `BRAKE_FROM_BLOCK` (4) | 1 a level (`BrakeChargesAt`), **only where `CeilingStep` is set** |
| Cut | R / pad right bumper | `CUT_FROM_BLOCK` (5) | 1 a level (`CutChargesAt`) |

- All are **0 in a set with no blocks and outside the set**. The grant is the campaign's shape, not a field of each level, so no level file changed, `LevelGen` writes nothing, and `ScoreSim` (which never uses them: the *least* helped player, the conservative side for a gate) is unaffected. One new tool per chapter (#705).
- `GrantPowerupCharges(index)` sets the charges at install (a retry is granted again). **A charge is never carried between levels** (`LevelRun`). `powerups=swap:3,brake:2,cut:1` assigns over it. Whether once a level is the right price is a **play judgement**: the dials are the three constants.
- **`Activate` is the one contract**: a power-up is not a shot, never calls `ScoreKeeper`, never feeds the ceiling's cadence. `CanActivate` refuses with the gun's dry "no" (`PlayShotRefused`) and keeps the charge. Scripted: `swap=`, `brake=`, `cut=` (`<t1,...>`, through the very call the key makes).
- **Taught where the chip first stands** (#705): three chapterless `Tool` lessons in `Tutorial`; the Brake's fires on the glass's **first pressure step** (`AnnounceCeilingStep`), the one moment it has a step to give back. A chip dims to 42 % when spent ("Swap used") and is never removed (a vanished chip reads as a fault).

## Swap

Exchanges the muzzle slot with the one behind it: no ball, no budget, no ceiling. **The price keeps "play what comes" a constraint.** `Magazine.SwapSlots(a, b, crossFade: true)` exchanges the slots at once, then each dissolves into its new colour over `Magazine.SWAP_FADE_SECONDS` (plain rounds only).

## Brake

**Lifts the glass one `CEILING_DESCENT_PER_STEP` towards `RestY`, never past it: it gives back exactly one step** (`CeilingDescent.Brake`; a lift, not a delay, which would only move the next step). It announces itself like a **feed** step (cold blue-white, soft sound, no rumble): good news.

- **Only on still glass** (#844, the owner's rule): `CeilingDescent.CanBrake` is `HasStepToGiveBack && !Moving` (sliding, or a step queued and holding). Pressed mid-slide it used to turn the slide round, and during a tall level's pour its lift was taken by the queued steps. Refused, the charge is kept and the chip says "Brake · wait". The Brake's card asks `HasStepToGiveBack` (the step's own glass is still sliding when the card is armed).
- ⚠ **It gives back a PRESSURE step, never a feed step, and is refused (charge kept) while there is none** (`CeilingDescent.HasStepToGiveBack`). A tall level's feed brings the column's underside just into reach (#843), so undoing one would leave it out of reach until the next landing fed it again. **On a tall level it is also refused while the lift would take the underside out of reach** (`BrakeCanLift`, #843): at the reach's edge a lift would be fed straight back down and the charge spent on nothing. One cached answer for the press and the chip, asked again on a landing, a queued or begun step, a brake, a lifted clamp, a load or a resize. The Brake's card fires on a pressure step only while `BrakeCanLift` holds, so it never tells the player to press when a press would be refused. While `BrakeCanLift` is false the chip is in its **not now** state (#692, #844): "Brake · wait" while the glass moves, "Brake · not needed" otherwise.
- ⚠ **`WakeForDescent` wakes every ball**: a shot-apart cluster is several islands and a kinematic plate joins none, so on a tier where balls sleep a column stayed at its old height.

## Cut (the cutter round)

**`BallKind.Cutter` is the second shot-only kind**, the wildcard's twin: `BallKinds.InCluster` refuses it (a hanging cutter is unmatchable and no shot could remove it).

- **`Activate`** turns the muzzle round into a cutter (`Magazine.SetKind`). **Refused, charge kept, when the round is already a cutter, is a wildcard or no shot is left.** Fired it is an ordinary shot: spends a ball, counts for the ceiling cadence, takes the streak by the usual rule.
- **It destroys the struck ball's whole STOREY** (#692, the owner's rule: *only the attached balls*, `CutBall` floods the struck level over its neighbours so a separate clump on it stays), then `ResolveDisconnected` drops whatever hung on nothing. The storey scores as **destroyed**, the fall as orphans, a bomb in the storey goes off **after** the walk. **Anything can be cut, the rock included.** A single ball never paid (every ball hangs by up to twelve neighbours).
- ⚠ **At most the lower half of the cluster** (the owner's verdict 2026-10-07; `CutReach.IsProtected`): the struck storey and every one under it may be no more than half the storeys the cluster hangs through. Unguarded, a top-storey strike dropped the whole cluster on most levels. A protected aim is **refused as an aim past the elevation clamp is**: `_cutterRefused` from the beam's own hit, `AimStrain` blinks the marks red, `Shoot` gives the dry "no". The protection is asked **at the trigger**, not off the preview (solved a frame before the input); a flight straying into a protected storey takes **only the ball it struck** (`DestroyBall`).
- **The cutter** is nudged down at `CUT_DROP_SPEED` (at the gun's speed it would plough through the hole), marked loose, listed **before** the cut (the drop cinematic's subject is the last `Released.Total` entries) and **retired on the step it struck** (#823).
- **The preview brightens what would be taken** (`CutReach.Measure`, per frame): `PhysicsBall.CutPreviewGlow` rides the ripple's channel, ⚠ which the ceiling's alarm wave shares and **always outranks**. **While a cutter is loaded the aim ghost is hidden** (it would show a cell that will not be there); the beam and crosshair stay (`CutterLoaded`).
- A cut pays only on spindly designs ("What one cut lets go" in `docs/formats-and-tools.md`, `LevelGen --cuts`).
