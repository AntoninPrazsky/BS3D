# Auditing the scenes against references

Part of [The scenes](../scenes.md).

The yardstick for "is this scene good enough" (#671), used to pick which scene to improve next.

- **Three things are scored 0-3** per scene, from a frame as the player sees it (the play camera on the scene's own chapter, or the front end's orbit; at `quality=high`, because Low has no sun shadows): **detail**, **variety of objects**, **realism with respect to light**. 3 is on par with a reference drawn at the same distance, 0 is nothing of what it shows. The gap is 9 minus the sum, weighed by exposure (how many campaign levels play there).
- **The references** are img2img over the Testbed's fixed game-mode pose (`campos=0,-7.4,36.2 camtarget=0,1.65,0`, strength 0.5, both models), made on the desktop and kept outside the repository. Variety is also counted from the code: kinds times mesh variants of standing objects.
- ⚠ **An img2img prompt that names a place's contents gets them back.** The references say how contents read at the distance; whether they belong is a question about the place (a pure sand sea really is empty, #677).
- The audit's ranking is on #671 (the cavern read as a starry sky, #676; every city tower was one box, #678, built in [The city](city.md); the volcano's lava field was empty, #679; the Moon's boulders, #680, built).
- **Joins are audited too (#974).** Where two things meet the join is one continuous surface; the open ends of every procedural mesh are a CI gate (`WindingCheck --openness`), and the rule and what it cannot see are in "Where two things meet" in [The setting, in one copy](shared-setting.md).
