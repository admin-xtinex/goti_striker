# Map-Ready Gameplay Kit — Integration Guide

Prefab: `Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit_MapReady.prefab`

The original `PitStriker_GameplayKit.prefab` is left untouched as a fallback.

## Hierarchy

```
PitStriker_GameplayKit_MapReady      [GameplayKitRoot, GameplayKitTestVisuals]
├── Gameplay                         origin, shot config, marbles, turn system,
│                                    camera, HUD, audio, VFX, SurfaceGroundConfig
├── PitCollidersAndDetection         Pit_01/02/03 — saucer MeshCollider + trigger + PitZone
├── SurfaceCollision                 10 invisible BoxColliders, top y = 0
├── BoundaryCollision                4 invisible walls, 1.5 m tall
└── TestVisuals                      overlay + boundary outline + pit markers (NO colliders)
```

Nothing in the prefab depends on the village terrain, fences, sky or any scene object.

## What was removed

| Removed | Replaced by |
|---|---|
| `VisualSurface` — brown mesh `GameplayLane_WithPitHoles`, material `M_Kit_Surface_Soil` | `SurfaceCollision` (invisible) + `TestVisuals/SurfaceOverlay` (faint, test-only) |
| `SurfaceEdge` — 4 thin edge rails | `BoundaryCollision` (invisible, always solid) + `TestVisuals/BoundaryOutline` |
| One 20 × 0.5 × 42 BoxCollider that sealed the pits | 10 tiled boxes leaving a 0.72 m opening at each pit |

The tall wooden/metal fences visible in the old recording were never part of the prefab —
they are village-scene décor and do not travel with the kit.

## Test visuals

Select the prefab root and use the **Gameplay Kit Test Visuals** component:

- `ShowTestVisuals` — master switch. **Uncheck for the final game.**
- `ShowSurfaceOverlay`, `ShowBoundaryOutline`, `ShowPitMarkers` — individual aids.

These objects carry **zero colliders**, so toggling them cannot change physics.
Verified: active colliders stay at 24 with visuals on and off; renderers drop 15 → 7.

The overlay sits 12 mm above the collision top and the markers 14 mm, so they never
z-fight a map surface placed at y = 0.

## Surface height and pit openings

- Gameplay collision surface top: **y = 0** (kit local).
- Pit saucer basins run from **y = 0 down to −0.18**, rim radius **0.52 m**.
- Each pit opening is a **0.72 m square gap** in the collision surface. The gap is fully
  inside the 0.52 m saucer disc (0.36 × √2 = 0.509 < 0.52), so the saucer always floors
  the gap — a marble can drop in, but never fall through.
- Pit spacing is unchanged: local Z **3.0 / 16.5 / 31.0**. Do not retarget this.

## Aligning a map with the kit

1. Drop `PitStriker_GameplayKit_MapReady` into the scene.
2. Position so the lane runs down the playable part of the map. Rotating on Y is supported.
3. Raise/lower the kit root so its **y = 0 plane sits at the map's walking surface**.
   `Tools → Pit Striker → Gameplay Placement → Align to Selected Surface` does this by raycast.
4. Choose a ground mode (below).
5. `Validate Placement` before Play.
6. Keep the root scale at (1,1,1).

Footprint needed: **20 m wide × 46 m long** (lane local Z −8.5 → 37.5).

## Ground mode — this is the part that matters

### Mode A — kit surface authoritative (recommended for any imported map)

A map floor level with the kit's surface **seals the pit basins**: the marble rests on the
map's collider at y = 0.16 instead of settling into the saucer at −0.019. Measured both ways.

Fix — on `SurfaceGroundConfig` (under `Gameplay`):

- `IgnoreHostMapCollision` = **true**
- `HostMapLayers` = the layer(s) the map's ground uses (usually `Default`)

At runtime this disables marble-vs-map collision, so only the kit's own surface, walls and
pit basins matter. The map becomes purely visual under the lane. This is the safe integration
approach when the imported map cannot have real openings cut in it.

`Physics.IgnoreLayerCollision` is global and lasts for the play session — it is only applied
when you tick the box.

### Mode B — map supplies the ground

Only valid if the map genuinely has openings at the three pit positions, which an imported
asset almost never does. If you use it, disable the kit's `SurfaceCollision` children and
verify all three pits still capture. Boundary walls should stay on.

### Duplicate collisions

With a map present and Mode A off, a raycast down the lane hits two solid floors
(kit surface at 0.000 and map at 0.000). That is the stacking to avoid — it is what
seals the pits. Mode A removes the marble's interaction with the map layer entirely,
so no duplicate contact remains.

## Known constraint — MarbleController has its own arena

`MarbleController` hardcodes boundary clamping independent of this prefab:

- X: tapers from ±5.15 at Z −8.5 to ±6.65 at Z 37.5
- Z: clamped to −8.5 … 37.5
- Y ceiling: 1.8

The collision surface deliberately spans that whole range. Previously the ground stopped at
Z 34 while the script clamped to 37.5, leaving a 3.5 m strip where the script held a marble
with no floor beneath it — it fell, and the follow camera fell with it.

If you change the lane length, change `BuildMapReadyKit.ZMin/ZMax` **and** the constants in
`MarbleController` together, or the mismatch returns.

## Rebuilding

`Tools → Pit Striker → Gameplay Placement → Build Map-Ready Kit` regenerates the prefab from
the original. `… → Create Map-Ready Test Scenes` regenerates both verification scenes.
