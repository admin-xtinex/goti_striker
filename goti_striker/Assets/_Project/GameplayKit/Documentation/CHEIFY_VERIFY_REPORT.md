# Cheify — GameplayKit Clean Verify (structural)

**Date:** 2026-09-15  
**Scene:** `Assets/_Project/GameplayKit/Demo/GameplayKit_Verification.unity`  
**Prefab:** `Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit.prefab`

## Result: STRUCTURAL PASS ✅ (Play Mode / lighting still open for eyes-on)

### Instantiated on plain surface
- `Verify_PlainSurface` (scaled Plane)
- Prefab instance at `(0, 0.02, 0)` — no village map dependency

### Pit locals (from Unity log on instantiate)
| Pit | Local Z |
|-----|---------|
| Pit_01_Round | **3.0** |
| Pit_02_Round | **16.5** |
| Pit_03_Round | **31.0** |

### Prefab contents confirmed present
- `GameplayOrigin`, `GameplaySurface` / `GameplayLane`, `SurfaceEdge`
- Nested `UI_ShotControl` + `ShotControlBinder`
- `ShotModeConfig`, Ground/Loft path (LaunchStrike retired)

### Open follow-ups (documented, not blockers for kit Ready)
1. Net ShotMode bit not wired yet
2. Camera: rest-of-screen still mostly 2-finger orbit (single-finger next)
3. `unitypackage` export after XTINEX Play Mode OK
4. Lighting / look — open `GameplayKit_Verification`, hit Play, confirm soil/edge/HUD look under default Directional Light

### How to re-run
`Tools → Pit Striker → Gameplay Placement → Create Verification Scene`
