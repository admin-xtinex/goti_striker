# Phase 2 P1 — Blender scripts (Hacky)

**Assets:** `Bldg_KeralaHouse_B_Distant` (+ `_B`/`_C` variants), `Veg_BananaClump`, `Veg_BankPlant_Low`  
**Output:** `Tools/Blender/phase2_p1/output/`  
**No Unity import from this step. No colliders.**

## Run (Cheify)

```bat
cd /d C:\Users\Tisan\Documents\PitStriker-Working\pitstricker
"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" --background --python Tools\Blender\phase2_p1\generate_all_p1.py
```

## Validate

- [ ] 5 FBX present
- [ ] Ground pivot (z=0), scale applied
- [ ] House B footprint ~5×4, height ~3.2 — no billboards
- [ ] Banana ~2.5–3.5 m · bank plant ~0.3–0.6 m
- [ ] Mat slots: plaster/wood/terracotta/stone · banana leaf/stem · herb green

Blockouts for layout density only — Crea art-pass later.
