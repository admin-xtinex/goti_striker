# Shot Control — Spec

## Shot type is chosen by swipe direction

There is **no Ground/Loft toggle**. The direction of the swipe inside the power area picks
the shot, and power scales from how far *and* how fast you swipe.

| Swipe | Shot | Behaviour |
|---|---|---|
| **Backward** (down the screen) | **Ground** | Slingshot pull. Rolls along the surface. Pitch = `GroundMaxPitch * 0.5` ≈ 2.3° |
| **Forward** (up the screen) | **Loft** | Throw forward. Parabolic arc. Pitch = `GetLoftPitch(power)`, up to ~33° |

Both launch on release. Aim direction is taken from the swipe vector projected onto the
camera's ground plane, exactly as before.

## Power

```
distNorm  = (dragPixels - minPixels) / (maxPixels - minPixels)
speedNorm = (dragPixels / dragSeconds) / (Screen.height * 1.2)
power     = clamp01( distNorm * lerp(0.80, 1.25, speedNorm) )
```

Distance sets the base, speed scales it. A long slow pull can still reach full power;
a fast flick of the same length hits harder. Power then drives both:

- **Distance** — `force = power * _maxLaunchForce (32)`
- **Height** — `GetLoftPitch(power) = Lerp(LoftMinAngle, LoftMaxAngle, power) * LoftVerticalForce`

So height and distance both follow the swipe automatically.

## Loft tuning

`ShotModeConfig.LoftVerticalForce = 1.20` (was 0.45, which made the arc nearly flat).

Measured through the real launch path:

| Shot | Angle | Peak height | Travel |
|---|---|---|---|
| Ground @100% | 2.3° | 1.54 m | 43.3 m |
| Loft @50% | 24.2° | 1.71 m | 22.7 m |
| Loft @100% | 33.4° | 5.96 m | 43.3 m |

Loft peaks ~3.9× higher than a ground shot. Tune live via the kit root → `ShotModeConfig`
→ `LoftVerticalForce` (0.90 → 26°/4.65 m, 1.60 → 41°/6.43 m but travel starts dropping).

## Opening toss phase — deliberate exception

While `TurnManager.CurrentState == TossPhase`, `SwipeLaunchController.ExecuteLaunch`
hard-codes `pitch = 0.08`, `maxPitch = 0.12` and **ignores the shot mode entirely**
(`SwipeLaunchController:309-313`). The opening throw is always a flat lag shot.

The toss uses `AimMode.ForwardFlickThrow` — a **fast upward flick**, and it is speed-based,
so a slow drag will not register. The phase ends only once every player has tossed
(`TurnManager:663`). Loft becomes available after that.

## Power area

Right side of the play view. Only touches that *begin* inside it control power; everything
else goes to the camera. `ShotControlBinder.IsScreenPosInPowerArea` is the gate.

## Camera

Orbit is on the **right mouse button** (`SmoothFollowCamera:186-198`) or **two fingers**
(line 205). There is currently no single-finger camera drag.

## Finger tutorial

`FingerTutorial` under `PowerArea` hides itself once power exceeds 0.02
(`ShotControlBinder:133`). It is a visual only and blocks no input.
