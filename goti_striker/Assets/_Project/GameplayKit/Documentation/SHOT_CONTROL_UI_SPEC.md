# Shot Control — Spec

## Shot type is chosen by swipe direction

There is **no Ground/Loft toggle**. The direction of the swipe inside the power area picks
the shot, and power scales from how far *and* how fast you swipe. This applies to **every
throw, the opening toss included**, offline and online.

| Swipe | Shot | Behaviour |
|---|---|---|
| **Backward** (down the screen) | **Ground** | Slingshot pull. Rolls along the surface. Pitch = `GroundMaxPitch * 0.5` (about 2.3 deg) |
| **Forward** (up the screen) | **Loft** | Throw forward. Parabolic arc at a fixed `LoftLaunchAngle` (35 deg) |

Both launch on release. Aim direction is taken from the swipe vector projected onto the
camera's ground plane.

## Power

```
distNorm  = (dragPixels - minPixels) / (maxPixels - minPixels)
speedNorm = (dragPixels / dragSeconds) / (Screen.height * 1.2)
power     = clamp01( distNorm * lerp(0.80, 1.25, speedNorm) )
force     = power * _maxLaunchForce (32) * difficulty LaunchForceMultiplier
```

Distance sets the base, speed scales it. A long slow pull can still reach full power; a fast
flick of the same length hits harder. Marbles weigh 1 kg, so force is launch speed in m/s.

## Loft

`ShotModeConfig.LoftLaunchAngle = 35` degrees at **every** power: power sets how far a lob
goes, not whether it leaves the ground. `LoftMinLaunchSpeed = 5.5` m/s, so even a tiny swipe
up arcs (about 0.5 m peak). `MarbleController` caps height at 6.5 m (was 1.8 m, which cut
lobs off at the top).

Approximate arcs at 35 deg, ignoring drag:

| Launch speed | Lands | Peak |
|---|---|---|
| 5.5 m/s (minimum) | 3 m | 0.5 m |
| 10 m/s | 9 m (pit 1) | 1.6 m |
| 15 m/s | 22 m (pit 2) | 3.9 m |
| 20 m/s | 37 m (pit 3) | 6.4 m |

Online, lofts are sent like any shot; the server's structural check allows launch
direction y up to `NetworkProtocol.MaxAllowedPitch` (0.60; sin 35 deg = 0.574).

## Opening toss

The toss uses the same gesture and the same two shots as normal play: ground or lofted,
same angles, power and minimum loft speed. `TossPhase` only decides whose throw it is and
that the result is scored by distance to pit 3. Bots throw their toss directly and are
unaffected. The phase ends once every player has tossed (`TurnManager:663`).

## Power area

Right side of the play view. Only touches that *begin* inside it control power; everything
else goes to the camera. `ShotControlBinder.IsScreenPosInPowerArea` is the gate.

## Camera

Orbit is on the **right mouse button** (`SmoothFollowCamera:186-198`) or **two fingers**
(line 205). There is currently no single-finger camera drag.

## Finger tutorial

`FingerTutorial` under `PowerArea` hides itself once power exceeds 0.02
(`ShotControlBinder:133`). It is a visual only and blocks no input.
