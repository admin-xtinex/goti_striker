# goti_striker

## Gameplay area dimensions

All figures are in Unity world units (metres), in the gameplay prefab's local space.
The lane runs along **+Z**, width is **X**, and the playing surface top is **y = 0**.

### Plan view

```
                 X
    -10    -6.65        +6.65    +10
     |       |            |       |          Z
     +-------+------------+-------+   37.5   far wall
     |       :            :       |
     |       :     ( )    :       |   31.0   pit 3
     |       :            :       |
     |       :     ( )    :       |   16.5   pit 2
     |       :            :       |
     |       :     ( )    :       |    3.0   pit 1
     |      :              :      |
     |      :      x       :      |   -6.0   tee (spawn)
     |     :                :     |
     +-----+----------------+-----+   -8.5   near wall
        -5.15            +5.15

     |<------- 20.0 m floor ------>|
            :  playable corridor  :   (tapers 10.30 m -> 13.30 m)
```

### Collision surface — the physical floor

| | |
|---|---|
| Width (X) | −10.0 … +10.0 → **20.0 m** |
| Length (Z) | −8.5 … +37.5 → **46.0 m** |
| Top surface | y = 0 |
| Slab thickness | 0.5 m (occupies y −0.5 … 0) |

Built as tiled boxes rather than one slab, so the three pit openings stay clear.
Defined in [BuildMapReadyKit.cs](goti_striker/Assets/_Project/GameplayKit/Editor/BuildMapReadyKit.cs).

### Playable corridor — where the marble is actually held

The marble is clamped by script, inside the floor, and the corridor **widens toward the far end**:

| Position | Half-width (X) | Full width |
|---|---|---|
| Near (Z = −8.5) | ±5.15 | 10.30 m |
| Far (Z = +37.5) | ±6.65 | 13.30 m |
| Length (Z) | −8.5 … +37.5 | 46.0 m |

Enforced in [MarbleController.cs:182](goti_striker/Assets/_Project/Scripts/Physics/MarbleController.cs#L182).
The clamp sits well inside the 20 m floor, which is deliberate — the extra floor is margin so a
marble can never reach an unsupported edge and fall.

> Note: the taper interpolates over `46.5` while the actual Z span is `46.0`, so the far
> half-width resolves to **6.634**, not 6.65. The 0.016 m difference has no gameplay effect,
> but it is a real off-by-one in the constant.

### Pits

| | |
|---|---|
| Positions (Z) | **3.0**, **16.5**, **31.0** — all on the centreline, X = 0 |
| Spacing | 13.5 m (pit 1 → 2), 14.5 m (pit 2 → 3) — **uneven by design** |
| Rim radius | 0.52 m (1.04 m across) |
| Collision opening | 0.72 × 0.72 m square gap in the floor |

The square opening is fully inside the rim disc (`0.36 × √2 = 0.509 < 0.52`), so the saucer
mesh always floors the gap — a marble can enter but never drop through.

### Boundary walls

Height **1.5 m**, thickness **0.3 m**, placed just outside the floor at X ±10 and Z −8.5 / +37.5.
Because the script clamp is tighter than the walls, the clamp is what a marble meets first; the
walls are a backstop for anything the clamp misses.

### Other reference figures

| | |
|---|---|
| Marble radius | 0.16 m (0.32 m diameter) |
| Tee / spawn | (0, 0.3, −6.0) |

### Integrating a map

Align art to the figures above and the gameplay prefab drops in unchanged:

- Put the visual ground top at **y = 0** and cover at least Z −8.5 … +37.5 by X ±10.
- Leave the three pit locations clear, or cut openings at least 1.04 m across.
- Keep the pit **Z positions and their uneven spacing** — the shot distances are tuned to them.
- Anything decorative beyond X ±10 is free; nothing there is collided against.

A temporary soil placeholder lives in the **map scene**, not the prefab, so replacing it means
editing the scene only — see
[AddTemporaryGround.cs](goti_striker/Assets/_Project/GameplayKit/Editor/AddTemporaryGround.cs).
