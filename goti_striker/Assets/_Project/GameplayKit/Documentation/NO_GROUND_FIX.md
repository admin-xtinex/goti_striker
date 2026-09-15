# No-ground fix (v2 — spawn Z coverage)

## Real root cause (Play still failed after v1)

v1 BoxCollider was at local Z **17**, size Z **34** → covered surface Z **0..34** only.

Marble spawns sit at Z **≈ -4..-6** (tee). Those points were **outside** the box, so marbles fell immediately → Safety Respawn loop.

MeshCollider non-convex was also wrong for dynamic RBs, but the remaining Play failure after v1 was **spawn outside ground bounds**.

## v2 fix

`GameplayCollider` BoxCollider:
- local Z **13**, size **20 × 0.5 × 42**
- covers surface Z **-8 .. 34** (tee through far pit)
- enabled, not trigger, layer `GameplayGround` (7)
- marble spawn Y **0.21** (r·scale 0.16 + 0.05)

Re-apply: **Tools → Pit Striker → Gameplay Placement → Fix No-Ground Failure**

Test A scene: `Assets/_Project/GameplayKit/Demo/GameplayKit_TestA_NoGround.unity`

## Follow-up (Cheify Test A Play)
Spawn tee at Z≈-6 was **outside** the original box (0→34). Prefab collider is now **20×0.5×42** at Z **13** (covers **-8→34**).

## Regression guard (verified 2026-09-15)

`SpawnBoundsCheck` is shared by **Validate Placement** and the **Test A batch verifier**,
so the automated path now fails on this regression instead of only the manual one.

Three holes in the previous check were closed:

1. **World AABB → collider local space.** The old check compared world Z against
   `collider.bounds`, a world-axis-aligned box. Once the kit is Y-rotated (the normal
   third-party-map workflow) that AABB no longer matches the real lane, and it was
   verified to produce a **false PASS** — a probe reading INSIDE its AABB while sitting
   outside the actual box (local z 29.59 vs limit 21).
2. **Only the start tee was checked.** `TeeAfterPit1` (Z 4.5) and `TeeAfterPit2` (Z 18.0)
   are now checked too, so shrinking the box or moving a re-tee can't break mid-match
   respawn behind a green validator.
3. **Vacuous pass.** Marbles are found via `GetComponentsInChildren`, so a runtime-spawned
   setup checked nothing and passed. Zero spawn points is now `INVALID`.

Verified by temporarily reverting the collider to the old 0→34 geometry: the check
reports INVALID on `StartTee` and all four marbles. A trigger-only host surface is
correctly rejected as floor.

Re-run: `Tools → Pit Striker → Gameplay Placement → Test A No-Ground Scene`
or batch `-executeMethod PitStriker.GameplayKit.EditorTools.TestA_NoGroundVerify.CreateBatch`
(writes `STRUCTURAL_PASS` / `STRUCTURAL_FAIL` plus `SpawnCoverage=` to `Library/GameplayKit_TestA.result`).
