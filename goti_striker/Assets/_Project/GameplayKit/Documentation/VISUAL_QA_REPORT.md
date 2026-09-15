# GameplayKit Surface — Visual QA (Kriya / Crea)

**Prefab:** `Assets/_Project/GameplayKit/Prefabs/PitStriker_GameplayKit.prefab`  
**Date:** 2026-09-15

## Applied

| Item | Status |
|------|--------|
| `GameplaySurface` / `GameplayLane` present | Yes |
| Warm soil mat `M_Kit_Surface_Soil` | Applied (Village laterite tint) |
| Soft border `SurfaceEdge` (4 rails, **no collider**) | Added |
| `SurfaceVisualConfig` presets (Village/Beach/Forest/Desert/Courtyard/SimpleTest) | On `GameplaySurface` |
| MeshCollider size / pit locals 3 / 16.5 / 31 | Untouched |
| Décor in lane | None |

## Checklist

- [x] Pit meshes still under `Pits/` (BrightRed_PitLip / markers intact on prefab)
- [x] Surface borders/corners have edge rails (reduces hard void cut)
- [x] Materials assigned (soil + edge)
- [x] Collider dimensions not modified
- [ ] Lighting pass in clean verify scene (Cheify’s `GameplayKit_Verification`) — pending host scene
- [ ] Third-party map gap blend — use `SurfaceVisualConfig` tint/tiling/Y offset; disable surface if map ground is authoritative

## Limitations (third-party maps)

- Edge rails are a simple frame, not terrain-projected skirts — may float on steep slopes (placement tool slope check is Hacky’s).
- Centre UV seam on hole-cut lane mesh may still show under harsh lighting — prefer host-map ground + disable kit surface when matching village décor later.
- No baked lightmaps in the portable kit; relies on realtime/URP.

## How to re-apply

`Pit Striker → GameplayKit → Apply Surface Visuals (Kriya)`