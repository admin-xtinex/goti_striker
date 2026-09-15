# Phase 2 P0 — Blender scripts (Hacky)

**For:** Cheify to run/validate in Blender · Crea integrates after approval  
**No Unity import from this step.**

## Assets generated (P0)

| FBX | Spec |
|-----|------|
| `Prop_BambooFence_Module.fbx` | 2.0 m span, ~1.0 m high, mats `M_Bamboo_Bark` / `M_Rope`, **no collider** |
| `Env_WaterChannel_Segment.fbx` | L4 × W0.9 × D0.35, `M_Water_Clear` / `M_RiverStone`, **no collider** |
| `Env_PaddyField_Patch.fbx` | 8×8 m + bunds, `M_Paddy_Green` / `M_Paddy_Bund`, **no collider** |
| `Bldg_KeralaHouse_A.fbx` | ~8×6 footprint, ridge ~4.5, porch +Y, plaster/wood/terracotta/stone |
| `Veg_CoconutPalm_A/B/C.fbx` | trunk 8 / 10 / 12 m + 8 fronds, `M_Palm_Trunk` / `M_Palm_Frond` |

Pivot: ground centre (Z=0). Units: metres. Export: FBX forward `-Z`, up `Y` (Unity-friendly).

## Requirements

- Blender **3.6+** or **4.x**
- These files on disk under the play tree:
  - `Tools/Blender/phase2_p0/generate_all_p0.py`
  - `Tools/Blender/phase2_p0/ps_common.py`

## How to run (Cheify)

### A — CLI (preferred)

```bat
cd /d C:\Users\Tisan\Documents\PitStriker-Working\pitstricker

"C:\Program Files\Blender Foundation\Blender 4.2\blender.exe" --background --python Tools\Blender\phase2_p0\generate_all_p0.py
```

Adjust the Blender path to whatever Hub/install you have.

### B — Inside Blender UI

1. Open Blender → Scripting
2. Open `generate_all_p0.py`
3. Run Script
4. FBXs land in `Tools/Blender/phase2_p0/output/`

## Validate checklist

- [ ] All 7 FBX files present in `output/`
- [ ] Each opens in Blender without missing data
- [ ] Origin at ground (object sits on Z=0)
- [ ] Scale applied (1,1,1)
- [ ] Material slot names match table above
- [ ] Fence module length ≈ 2 m; house footprint ≈ 8×6; palms A/B/C different heights
- [ ] Poly counts ballpark vs budget (procedural placeholders — expect under budget; Crea can retopo/art-pass later)

## Notes / known limits

- These are **blockout / kitbash proxies** for layout + density, not final art.
- House roof is two rotated slabs (simple gable), not a modeled tile set.
- Palm fronds are flat cards (allowed for fronds in spec); trunks are cylinders.
- Water is an opaque-tinted plane (URP water look is Crea’s Unity-side job).
- **Do not** enable generate-colliders on import for fence/channel/paddy/palms (P0 = visual). House box proxy = later with Hacky on integrate.
- Gameplay kit / pit transforms / menus / multiplayer stay locked.

## After Cheify validates

Ping Crea for Unity integrate. Hacky stands by for house box collider + optional palm trunk capsules on integrate.
