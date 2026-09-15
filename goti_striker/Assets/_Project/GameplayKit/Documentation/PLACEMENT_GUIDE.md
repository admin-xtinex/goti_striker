# Placement Guide

## Footprint

The kit needs a flat area of roughly **20 m wide × 42 m long** (lane local Z **-8 → 34**:
tee at Z ≈ -6, pits at Z 3.0 / 16.5 / 31.0). Check a downloaded map has that much
level ground before committing to it.

## Basic placement

1. Place `PitStriker_GameplayKit` root where play should start.
2. Rotate Y to aim the lane down your map. Rotation is fully supported — validation
   measures in the collider's local space, not a world bounding box.
3. Use **Align to Selected Surface** (raycast + height offset).
4. Pick a ground mode (below).
5. **Validate Placement** before Play.
6. Do not scale the root non-uniformly — use offset tools instead. Root scale must stay (1,1,1).

## Ground modes

The kit validates the same way whether it brings its own floor or borrows the map's.
A spawn point is accepted if **either** source covers it.

### Mode A — kit supplies the ground (default, safest)

`GameplayCollider` enabled. Works on any map, including one with no usable flat area,
because the kit carries its own 20 × 0.5 × 42 box. Use this for unknown or uneven
third-party maps.

### Mode B — host map supplies the ground

Disable `GameplayCollider` (or `SurfaceGroundConfig.EnableGameplaySurfaceCollider = false`)
when the downloaded map already has a solid floor you want the marbles to roll on.
Validation raycasts down from every spawn point and accepts a solid host collider beneath it.

Requirements for the host surface:
- A **non-trigger** collider. Trigger volumes are ignored and will fail validation.
- Present under *all* spawn points, not just the start tee.
- `Marble` ↔ host layer collision enabled in the physics matrix.

Disabling the visual surface (`ShowGameplaySurfaceVisual`) is independent — it must never
disable `GameplayCollider`.

## Validate Placement verdicts

| Verdict | Meaning |
|---------|---------|
| `VALID` | Every spawn has ground under it. Safe to Play. |
| `WARNING` | Playable, but check the note — e.g. kit collider off and relying on host ground, or collider not on the `GameplayGround` layer. |
| `INVALID` | A spawn would drop through, or the kit is misconfigured. Fix before Play. |

The report lists every spawn point and which surface covers it, e.g.

```
- OK StartTee @ (0.00, 0.30, -6.00) → kit collider (local x 0.00 in -10.0..10.0, z -19.00 in -21.0..21.0)
- OK TeeAfterPit2 @ (0.00, 0.30, 18.00) → host map ground (HostMap_Ground @ y=0.00)
```

All spawn points are checked: the start tee, both re-tees (`TeeAfterPit1` Z 4.5,
`TeeAfterPit2` Z 18.0) and every marble. If no spawn points are found at all, that is
reported as `INVALID` rather than passing silently.
