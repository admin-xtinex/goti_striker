# Visual Surface Polish — Post No-Ground Fix (Kriya)

**Prefab:** `Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit.prefab`

## Done (visual only)
- `VisualSurface` Y = **0.02** (above BoxCollider top at Y=0) — reduces Z-fight / submersion look
- `SurfaceEdge` rails Y = **0.028**, no colliders, soft edge mat
- Warm `M_Kit_Surface_Soil` + darker `M_Kit_Surface_Edge`
- Shadow cast off on flat surface/edges for cleaner empty-scene look
- `SurfaceVisualConfig` re-bound to VisualSurface

## Untouched (Hakki)
- `GameplayCollider` BoxCollider size/center/enabled/trigger/layer
- Pit locals 3 / 16.5 / 31
- Spawn heights / marble physics

## Empty-scene note
Internal visual stays ON by default (`ShowGameplaySurfaceVisual`). Hiding visual via `SurfaceGroundConfig` must **not** disable `GameplayCollider`.

Menu: **Pit Striker → GameplayKit → Polish Visual Surface Only (Kriya)**