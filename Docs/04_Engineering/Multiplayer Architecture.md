# Multiplayer Architecture — shot-input relay with client-authored final state

Protocol version **2**. Applies to the initial multiplayer release.

## Why

Before v2 the project ran **two different physics implementations**: Unity PhysX on the client
and a hand-written 2D Euler integrator on the server. They disagreed on pit positions, lane
bounds, damping and — critically — the server had no vertical axis at all, so loft could not
work online. Every "fix" was keeping two engines in manual agreement.

v2 removes the second implementation. There is now exactly one physics engine: the one that
already ships offline.

## Who provides the shot result

**The striking client.** It runs the normal offline shot path, waits for its own marbles to
settle, and reports where everything stopped. The server does not recompute any of it.

## When a result becomes accepted

1. Striker swipes. Its Unity strike happens immediately — unchanged from offline.
2. Striker sends `ShotInput` with the *effective* values it passed to `ApplyImpulse`
   (launch direction with pitch folded in, force, maxPitch, mode, opening-toss flag) plus
   turn and marble ids.
3. Server verifies phase, turn ownership, no shot already pending, and matching `TurnId`;
   assigns a monotonic `ShotId`; relays to the opponent and echoes back to the striker.
4. Opponent replays the identical `ApplyImpulse` through the normal gameplay path, and points
   the camera at the shooting marble — the same behaviour as offline.
5. Striker's marbles settle. It sends `ShotResult`: final position of every gameplay marble,
   pit conquest, strokes, current pit, finished flag, hit-opponent flag.
6. Server accepts **exactly one** result per `ShotId`, runs structural checks, applies it,
   advances the turn, and broadcasts `AcceptedState`.

The result is accepted at step 6. Nothing before that is authoritative.

## How both clients synchronise before the next turn

`AcceptedState` carries turn id, active player, phase, timer, every marble's position, and
both players' strokes/pit. On receipt each client:

- drops the message if `TurnId` is older than its own,
- moves every marble onto the accepted position (eased under
  `NetworkProtocol.ReconcileEaseThreshold` = 0.75 m, snapped above it),
- applies stroke and pit progression from the accepted numbers, not from its own simulation,
- sets `_reconciled = true`, which is what re-opens input.

`CloudMatchManager.IsMyTurn()` returns false while a shot is pending, while a replay is in
flight, or while unreconciled — so two phones cannot start a turn from different states.

## Reliability

| Case | Handling |
|---|---|
| Duplicate / replayed packet | Dropped by `ShotId` (`_lastAppliedShotId`) and `TurnId` |
| Out-of-order / late input | Dropped if `TurnId` != server turn |
| Late result | Only the pending `ShotId` is accepted; anything else is refused and the server re-sends accepted state |
| Missing result (striker stalls or drops) | Server abandons the shot after `ShotResultTimeout` (20 s) and passes the turn. **The turn is never advanced from an unfinished local simulation.** |
| Opponent still replaying when the result lands | It reconciles to the accepted positions; its own replay result is discarded |
| Opponent never receives a result | After `OpponentResultTimeout` (25 s) it sends `ResyncRequest` and restores from accepted state |
| Reconnect | Server sends that client the last accepted state and current phase |
| Incompatible builds | `ConnectRequest` carries the protocol version; the server rejects a mismatch at handshake, so a v1 client cannot enter a v2 match |

## Server responsibilities after v2

**Kept:** matchmaking, quick-match queue, room codes, sessions, WebSocket transport, turn
timer, reconnect grace, strokes, pit progression, match completion, rematch.

**Removed:** marble integrator, drag, boundary reflection, marble-marble collision, stop
threshold, server pit coordinates, fairway bounds, and the 15 Hz/2 Hz rolling position feed.
One `AcceptedState` is sent when state actually advances.

One process serves many rooms; there is no process or service per match.

## Limitations of client-provided results

The striking client is trusted for the outcome of its own shot. Server checks are
**structural only**:

- force within `[MinAllowedForce, MaxAllowedForce]`, direction near unit length, pitch within
  `MaxAllowedPitch`
- strokes may only rise, and by no more than the shots taken that turn
- pit progression may only stay put or advance by one, never past 3
- a claimed conquest must be the pit the player was actually aiming at
- finishing requires conquering pit 3
- positions must be finite and within range

A modified client could therefore report a favourable outcome. **Cheat prevention and ranked
authority are explicitly out of scope for this phase.** If they become necessary, the next
step is a headless Unity dedicated server running the same gameplay assembly — the work of
separating simulation from presentation is already the prerequisite for both.

## Deployment coordination

The client and server must ship **together**. A v1 client against a v2 server is refused at
handshake (by design), so a staged rollout will lock players out of matchmaking until both
sides are updated.
