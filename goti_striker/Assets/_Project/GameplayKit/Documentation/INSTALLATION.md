# PitStriker GameplayKit — Installation & Export

## Requirements
- Unity 6000.6.x (project version)
- URP project
- Layers/tags: see DEPENDENCIES.md

## Which prefab

| Prefab | Use |
|---|---|
| `PitStriker_GameplayKit_MapReady.prefab` | **Current.** Map-independent; no brown ground or edge rails. See MAP_INTEGRATION.md |
| `PitStriker_GameplayKit.prefab` | Original, keeps its own brown lane visual. Kept as fallback |

## Correct export workflow

A gameplay prefab is **not a model**. Exporting it as FBX/OBJ keeps only meshes and
transforms — every MonoBehaviour (`PitZone`, `MarbleController`, `TurnManager`,
`SwipeLaunchController`), every collider setup and every inspector reference is dropped.
That produces marbles and shapes with no behaviour, which is the usual cause of a kit
"exporting" but not working.

Use one of these instead:

### A. `.unitypackage` (moving to another Unity project)

1. `Tools → Pit Striker → Gameplay Placement → Export Kit .unitypackage`
2. Writes `PitStriker_GameplayKit.unitypackage` next to the project folder (~20 MB).
   It includes `Assets/_Project/GameplayKit` **and** `Assets/_Project/Scripts`, because
   the prefab's components live in Scripts.
3. In the target project: `Assets → Import Package → Custom Package…`, select the file,
   keep everything ticked, Import.
4. Recreate the layers `Marble`, `GameplayGround`, `Obstacle`, `PitTrigger` if that project
   doesn't have them — layers are project settings and do not travel inside a package.
5. Drag the prefab into a scene and run `Validate Placement`.

### B. Same project

Just drag `PitStriker_GameplayKit_MapReady.prefab` into any scene. Nothing to export.

## After importing — check for missing references

1. Select the prefab root; the Inspector must show no **"Missing (Mono Script)"**.
2. `Tools → Pit Striker → Gameplay Placement → Open Placement Window → Validate Placement`
   — expect `VALID` with one OK line per spawn point.
3. Enter Play mode and confirm the Console has no `[GameplayKit]` errors.

If scripts are missing, the package was imported without `Assets/_Project/Scripts`, or the
target project is missing the four layers.

## Verification scenes

- `Demo/GameplayKit_MapReady_Clean.unity` — kit alone, no ground of any kind.
- `Demo/GameplayKit_MapReady_WithMap.unity` — kit over a stand-in map surface with
  `IgnoreHostMapCollision` enabled.

Regenerate with `… → Create Map-Ready Test Scenes`.

## Preserve
Do not delete or overwrite the original village gameplay scene — it remains the reference fallback.
