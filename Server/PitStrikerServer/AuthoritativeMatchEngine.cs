using PitStriker.Networking.Shared;

namespace PitStrikerServer
{
    /// <summary>
    /// Turn and score authority for one room.
    ///
    /// This engine does NOT simulate marbles. The striking client runs the real Unity physics
    /// and reports where everything settled; this class decides whether that report is
    /// structurally acceptable, stores it as the accepted state, and drives turn progression
    /// from it. That removes the second physics implementation that used to disagree with the
    /// client over pit positions, boundaries and vertical motion.
    ///
    /// Trust model: the striking client is trusted for the outcome of its own shot. Checks here
    /// are structural (is it your turn, is this the pending shot, are the numbers sane), not
    /// anti-cheat. Ranked authority is explicitly out of scope for this phase.
    /// </summary>
    public class AuthoritativeMatchEngine
    {
        public Room Room { get; }
        public CloudMatchPhase Phase { get; private set; } = CloudMatchPhase.WaitingForPlayers;

        public int ActivePlayerIndex { get; private set; } = 0;
        public float TurnTimerRemaining { get; private set; } = NetworkProtocol.DefaultTurnDuration;
        public uint ServerTick { get; private set; } = 0;
        public int WinnerPlayerIndex { get; private set; } = -1;

        /// <summary>Incremented every time the accepted state advances. Clients use it to drop stale messages.</summary>
        public int TurnId { get; private set; } = 0;
        /// <summary>Id of the shot currently in flight, or -1 when none is pending.</summary>
        public int PendingShotId { get; private set; } = -1;
        public int PendingShotPlayer { get; private set; } = -1;
        public int LastAppliedShotId { get; private set; } = -1;
        private int _nextShotId = 1;
        private float _pendingShotElapsed = 0f;

        public CompactPlayerData[] Players = new CompactPlayerData[2];

        /// <summary>Accepted resting state of every gameplay marble. Positions only — no velocity.</summary>
        public MarbleFinalState[] AcceptedMarbles = new MarbleFinalState[2];

        // Turn limits & bonus tracking
        private const int MaxShotsPerTurn = 3;
        private int _shotsTakenThisTurn = 0;

        // Starting tees: seed the first accepted state, and where both marbles return after the toss.
        private static readonly NetVector3 Tee0 = new NetVector3(-0.4f, 0.25f, -6.0f);
        private static readonly NetVector3 Tee1 = new NetVector3(0.4f, 0.25f, -6.0f);

        // ---- Opening toss ----
        // Each player throws once at pit 3; whoever settles closest plays first. Mirrors the
        // offline toss. Distances are measured here from the striker's own reported position.
        private static readonly NetVector3 TossTarget = new NetVector3(0f, 0f, NetworkProtocol.TossTargetZ);
        /// <summary>Distance recorded for a player who never threw, or whose throw never reported.</summary>
        public const float NoTossDistance = float.MaxValue;
        private readonly float[] _tossDistance = { -1f, -1f };   // -1 = not thrown yet
        private bool _pendingIsToss;

        /// <summary>A player's recorded toss distance: -1 if not thrown yet, NoTossDistance if forfeited.</summary>
        public float TossDistance(int playerIndex) => _tossDistance[playerIndex];

        // Rematch votes
        public bool Player0WantsRematch { get; set; } = false;
        public bool Player1WantsRematch { get; set; } = false;

        public AuthoritativeMatchEngine(Room room)
        {
            Room = room;
            ResetMatch();
        }

        public void ResetMatch()
        {
            Phase = CloudMatchPhase.WaitingForPlayers;
            ActivePlayerIndex = 0;
            TurnTimerRemaining = NetworkProtocol.DefaultTurnDuration;
            WinnerPlayerIndex = -1;
            TurnId = 0;
            PendingShotId = -1;
            PendingShotPlayer = -1;
            LastAppliedShotId = -1;
            _nextShotId = 1;
            _pendingShotElapsed = 0f;
            _shotsTakenThisTurn = 0;
            _tossDistance[0] = -1f;
            _tossDistance[1] = -1f;
            _pendingIsToss = false;
            Player0WantsRematch = false;
            Player1WantsRematch = false;

            Players[0] = new CompactPlayerData(0, Room.Player0?.PlayerName ?? "Player 1");
            Players[1] = new CompactPlayerData(1, Room.Player1?.PlayerName ?? "Player 2");

            AcceptedMarbles[0] = new MarbleFinalState { MarbleId = 0, Position = Tee0, IsRetired = false, InPitNumber = 0 };
            AcceptedMarbles[1] = new MarbleFinalState { MarbleId = 1, Position = Tee1, IsRetired = false, InPitNumber = 0 };
        }

        /// <summary>
        /// Chooses who throws first in the opening toss, and breaks exact toss ties. Random by
        /// default; swappable so tests can pin it and stay deterministic.
        ///
        /// It used to pick who took the first turn outright, and before that the first turn was
        /// hard-coded to player 0, so the room creator shot first in every match. The toss now
        /// decides turn order on merit; this only settles who throws first.
        /// </summary>
        public static Func<int> OpeningPlayerPicker = () => Random.Shared.Next(2);

        public void StartMatch()
        {
            ResetMatch();
            Room.MatchOver = false;   // a rematch starts clean
            Phase = CloudMatchPhase.TossPhase;
            ActivePlayerIndex = OpeningPlayerPicker() == 1 ? 1 : 0;   // who throws first
            TurnId = 1;
            TurnTimerRemaining = NetworkProtocol.DefaultTurnDuration;
            Console.WriteLine($"[ROOM {Room.RoomCode}] Match started! Toss: P{ActivePlayerIndex + 1} throws first, turn {TurnId}");
        }

        // ------------------------------------------------------------------
        // Shot input: verify ownership, assign a shot id, allow the relay.
        // ------------------------------------------------------------------

        /// <summary>
        /// Validates an incoming shot input. On success <paramref name="assignedShotId"/> carries
        /// the id both clients must quote, and the caller should relay the input to the opponent.
        /// </summary>
        public bool SubmitShotInput(int playerIndex, ref ShotInputData input, out int assignedShotId, out string reason)
        {
            assignedShotId = -1;

            bool isToss = Phase == CloudMatchPhase.TossPhase;
            if (Phase != CloudMatchPhase.ReadyToAim && !isToss)
            {
                reason = $"not accepting shots (phase {Phase})";
                return false;
            }
            if (playerIndex != ActivePlayerIndex)
            {
                reason = $"not P{playerIndex + 1}'s turn (active is P{ActivePlayerIndex + 1})";
                return false;
            }
            if (PendingShotId >= 0)
            {
                reason = $"shot {PendingShotId} still pending";
                return false;
            }
            if (input.TurnId != TurnId)
            {
                // Late or duplicated input from a turn that has already resolved.
                reason = $"stale turn {input.TurnId}, server is on {TurnId}";
                return false;
            }
            if (!input.IsStructurallyValid)
            {
                reason = $"malformed input (force {input.Force}, dir.y {input.LaunchDirection.y})";
                return false;
            }

            assignedShotId = _nextShotId++;
            input.ShotId = assignedShotId;
            input.PlayerIndex = playerIndex;

            // The server's phase decides whether this is the toss, not the client's flag. A
            // mismatch means the client's view has drifted, which is worth seeing in the log.
            if (isToss != input.OpeningToss)
            {
                Console.WriteLine($"[ROOM {Room.RoomCode}] Warning: P{playerIndex + 1} sent OpeningToss={input.OpeningToss} "
                                + $"during {Phase}; treating it as {(isToss ? "the toss" : "a normal shot")}");
            }

            PendingShotId = assignedShotId;
            PendingShotPlayer = playerIndex;
            _pendingShotElapsed = 0f;
            _pendingIsToss = isToss;
            if (!isToss) _shotsTakenThisTurn++;   // the toss is not a stroke
            Phase = CloudMatchPhase.Rolling;

            Console.WriteLine($"[ROOM {Room.RoomCode}] P{playerIndex + 1} {(isToss ? "toss" : "shot")} {assignedShotId} accepted "
                            + $"(turn {TurnId}, force {input.Force:F1}, mode {(input.ShotMode == 1 ? "Loft" : "Ground")})");
            reason = "ok";
            return true;
        }

        // ------------------------------------------------------------------
        // Shot result: the striking client reports where everything settled.
        // ------------------------------------------------------------------

        /// <summary>
        /// Accepts exactly one result for the pending shot. Duplicates and results for other
        /// shots are rejected so a replayed packet cannot advance the turn twice.
        /// </summary>
        public bool SubmitShotResult(int playerIndex, ShotResultData result, out string reason)
        {
            if (PendingShotId < 0)
            {
                reason = "no shot pending";
                return false;
            }
            if (result.ShotId != PendingShotId)
            {
                reason = $"result for shot {result.ShotId}, pending is {PendingShotId}";
                return false;
            }
            if (playerIndex != PendingShotPlayer)
            {
                reason = $"P{playerIndex + 1} is not the striker of shot {PendingShotId}";
                return false;
            }
            if (result.TurnId != TurnId)
            {
                reason = $"result turn {result.TurnId} != server turn {TurnId}";
                return false;
            }
            if (_pendingIsToss)
            {
                // A toss moves no score, so only the marble positions need to be sane.
                if (!MarblesAreSane(result, out reason)) return false;

                LastAppliedShotId = PendingShotId;
                PendingShotId = -1;
                PendingShotPlayer = -1;
                _pendingShotElapsed = 0f;
                _pendingIsToss = false;
                ApplyTossResult(playerIndex, result);
                reason = "ok";
                return true;
            }

            if (!ResultIsStructurallySane(result, playerIndex, out reason)) return false;

            ApplyAcceptedResult(playerIndex, result);
            LastAppliedShotId = PendingShotId;
            PendingShotId = -1;
            PendingShotPlayer = -1;
            _pendingShotElapsed = 0f;
            reason = "ok";
            return true;
        }

        /// <summary>
        /// Structural sanity only: the numbers must be self-consistent and move in legal
        /// directions. This does not attempt to verify the physics actually happened.
        /// </summary>
        private bool ResultIsStructurallySane(ShotResultData r, int playerIndex, out string reason)
        {
            if (r.Marbles == null || r.Marbles.Length == 0)
            {
                reason = "result contains no marbles";
                return false;
            }

            // Strokes may only increase, and only by the shots actually taken this turn.
            int strokesBefore = Players[playerIndex].TotalStrokes;
            if (r.StrokesAfter < strokesBefore || r.StrokesAfter > strokesBefore + MaxShotsPerTurn)
            {
                reason = $"stroke delta implausible ({strokesBefore} -> {r.StrokesAfter})";
                return false;
            }

            // Pit progression may only stay put or advance by one, and never past 3.
            int pitBefore = Players[playerIndex].CurrentPit;
            if (r.CurrentPitAfter < pitBefore || r.CurrentPitAfter > pitBefore + 1 || r.CurrentPitAfter > 4)
            {
                reason = $"pit progression implausible ({pitBefore} -> {r.CurrentPitAfter})";
                return false;
            }

            // A claimed pit conquest must be the pit the player was actually aiming at.
            if (r.PitConqueredNumber != 0 && r.PitConqueredNumber != pitBefore)
            {
                reason = $"claimed pit {r.PitConqueredNumber} but target was {pitBefore}";
                return false;
            }

            // Finishing is only possible by conquering pit 3.
            if (r.PlayerFinished && !(r.PitConqueredNumber == 3 || pitBefore == 3))
            {
                reason = "claimed finish without conquering pit 3";
                return false;
            }

            return MarblesAreSane(r, out reason);
        }

        private bool MarblesAreSane(ShotResultData r, out string reason)
        {
            if (r.Marbles == null || r.Marbles.Length == 0)
            {
                reason = "result contains no marbles";
                return false;
            }
            foreach (var m in r.Marbles)
            {
                if (m.MarbleId < 0 || m.MarbleId >= AcceptedMarbles.Length)
                {
                    reason = $"unknown marble id {m.MarbleId}";
                    return false;
                }
                float mag = m.Position.Magnitude;
                if (float.IsNaN(mag) || float.IsInfinity(mag) || mag > 500f)
                {
                    reason = $"marble {m.MarbleId} position out of range";
                    return false;
                }
            }

            reason = "ok";
            return true;
        }

        // ------------------------------------------------------------------
        // Opening toss
        // ------------------------------------------------------------------

        private void ApplyTossResult(int playerIndex, ShotResultData r)
        {
            foreach (var m in r.Marbles)
            {
                if (m.MarbleId < 0 || m.MarbleId >= AcceptedMarbles.Length) continue;
                AcceptedMarbles[m.MarbleId] = m;
            }

            // Marble ids follow player indices, so the thrower's marble has the thrower's id.
            float distance = NoTossDistance;
            foreach (var m in r.Marbles)
            {
                if (m.MarbleId != playerIndex) continue;
                float dx = m.Position.x - TossTarget.x;
                float dz = m.Position.z - TossTarget.z;
                distance = MathF.Sqrt(dx * dx + dz * dz);
                break;
            }

            string? note = null;
            if (r.PitConqueredNumber == 3)
            {
                // Sinking pit 3 is a bullseye. Move the marble to the rim, as offline does, so it
                // cannot plug the pit for the second thrower.
                distance = 0f;
                note = "sank pit 3";
                var rim = AcceptedMarbles[playerIndex];
                rim.Position = new NetVector3(TossTarget.x + 2.5f, 0.25f, TossTarget.z);
                AcceptedMarbles[playerIndex] = rim;
            }

            RecordToss(playerIndex, distance, note);
        }

        /// <summary>
        /// Records one player's toss and advances: to the other player's throw, or, once both
        /// have thrown, to the first real turn for whoever landed closest.
        /// </summary>
        private void RecordToss(int playerIndex, float distance, string? note)
        {
            _tossDistance[playerIndex] = distance;
            Console.WriteLine($"[ROOM {Room.RoomCode}] Toss: P{playerIndex + 1} {FormatToss(distance)}"
                            + (note != null ? $" ({note})" : ""));

            int other = 1 - playerIndex;
            if (_tossDistance[other] < 0f)
            {
                ActivePlayerIndex = other;
                Phase = CloudMatchPhase.TossPhase;
                TurnTimerRemaining = NetworkProtocol.DefaultTurnDuration;
                TurnId++;
                return;
            }

            float d0 = _tossDistance[0];
            float d1 = _tossDistance[1];
            int winner = d0 < d1 ? 0 : d1 < d0 ? 1 : (OpeningPlayerPicker() == 1 ? 1 : 0);

            // The toss only decides order. Everyone starts the real game from the tee.
            AcceptedMarbles[0] = new MarbleFinalState { MarbleId = 0, Position = Tee0, IsRetired = false, InPitNumber = 0 };
            AcceptedMarbles[1] = new MarbleFinalState { MarbleId = 1, Position = Tee1, IsRetired = false, InPitNumber = 0 };

            ActivePlayerIndex = winner;
            _shotsTakenThisTurn = 0;
            Phase = CloudMatchPhase.ReadyToAim;
            TurnTimerRemaining = NetworkProtocol.DefaultTurnDuration;
            TurnId++;
            Console.WriteLine($"[ROOM {Room.RoomCode}] TOSS RESULT: P1 {FormatToss(d0)}, P2 {FormatToss(d1)} "
                            + $"-> P{winner + 1} plays first (turn {TurnId})"
                            + (d0 == d1 ? " [tie broken at random]" : ""));
        }

        private static string FormatToss(float d) => d >= NoTossDistance ? "no throw" : $"{d:F2}m";

        /// <summary>Applies an accepted result and advances turn/score state from it.</summary>
        private void ApplyAcceptedResult(int playerIndex, ShotResultData r)
        {
            foreach (var m in r.Marbles)
            {
                if (m.MarbleId < 0 || m.MarbleId >= AcceptedMarbles.Length) continue;
                AcceptedMarbles[m.MarbleId] = m;
            }

            Players[playerIndex].TotalStrokes = r.StrokesAfter;
            Players[playerIndex].CurrentPit = r.CurrentPitAfter;

            if (r.PitConqueredNumber != 0)
                Console.WriteLine($"[ROOM {Room.RoomCode}] P{playerIndex + 1} conquered pit {r.PitConqueredNumber}");

            if (r.PlayerFinished)
            {
                Players[playerIndex].IsFinished = true;
                WinnerPlayerIndex = playerIndex;
                Phase = CloudMatchPhase.MatchCompleted;
                TurnId++;
                Console.WriteLine($"[ROOM {Room.RoomCode}] MATCH FINISHED - winner P{playerIndex + 1} ({Players[playerIndex].Name})");
                return;
            }

            // Bonus play rule preserved from the original engine.
            bool earnedBonus = (r.PitConqueredNumber != 0 || r.HitOpponent) && _shotsTakenThisTurn < MaxShotsPerTurn;
            if (earnedBonus)
            {
                Console.WriteLine($"[ROOM {Room.RoomCode}] P{playerIndex + 1} earned EXTRA PLAY ({_shotsTakenThisTurn}/{MaxShotsPerTurn})");
                Phase = CloudMatchPhase.ReadyToAim;
                TurnTimerRemaining = NetworkProtocol.DefaultTurnDuration;
                TurnId++;
            }
            else
            {
                PassTurn();
            }
        }

        private void PassTurn()
        {
            _shotsTakenThisTurn = 0;

            int otherIdx = 1 - ActivePlayerIndex;
            if (!Players[otherIdx].IsFinished) ActivePlayerIndex = otherIdx;

            Phase = CloudMatchPhase.ReadyToAim;
            TurnTimerRemaining = NetworkProtocol.DefaultTurnDuration;
            TurnId++;
            Console.WriteLine($"[ROOM {Room.RoomCode}] Turn {TurnId} passed to P{ActivePlayerIndex + 1}");
        }

        // ------------------------------------------------------------------
        // Timing
        // ------------------------------------------------------------------

        public void Tick(float dt)
        {
            ServerTick++;

            // A decided match must not keep running its turn clock. After a forfeit the room
            // lingers until it is pruned, and without this it went on logging "Turn timer
            // expired" and passing turns between players in a match that was already over.
            if (Room.MatchOver) return;

            if (Phase == CloudMatchPhase.ReadyToAim || Phase == CloudMatchPhase.TossPhase)
            {
                TurnTimerRemaining -= dt;
                if (TurnTimerRemaining <= 0f)
                {
                    Console.WriteLine($"[ROOM {Room.RoomCode}] Turn timer expired for P{ActivePlayerIndex + 1}");
                    // A player who does not throw in the toss loses it, rather than stalling the start.
                    if (Phase == CloudMatchPhase.TossPhase) RecordToss(ActivePlayerIndex, NoTossDistance, "timed out");
                    else PassTurn();
                    Room.MarkAcceptedStateDirty();
                }
            }
            else if (Phase == CloudMatchPhase.Rolling && PendingShotId >= 0)
            {
                // The turn never advances off a local guess: if the striker's result never
                // arrives, abandon that shot and move on rather than inventing an outcome.
                _pendingShotElapsed += dt;
                if (_pendingShotElapsed >= NetworkProtocol.ShotResultTimeout)
                {
                    Console.WriteLine($"[ROOM {Room.RoomCode}] Shot {PendingShotId} from P{PendingShotPlayer + 1} "
                                    + $"timed out after {_pendingShotElapsed:F1}s - abandoning shot, state unchanged");
                    int striker = PendingShotPlayer;
                    bool wasToss = _pendingIsToss;
                    PendingShotId = -1;
                    PendingShotPlayer = -1;
                    _pendingShotElapsed = 0f;
                    _pendingIsToss = false;
                    if (wasToss) RecordToss(striker, NoTossDistance, "result never arrived");
                    else PassTurn();
                    Room.MarkAcceptedStateDirty();
                }
            }
        }

        // ------------------------------------------------------------------
        // State export
        // ------------------------------------------------------------------

        public AcceptedStateData BuildAcceptedState()
        {
            return new AcceptedStateData
            {
                TurnId = TurnId,
                LastAppliedShotId = LastAppliedShotId,
                ActivePlayerIndex = ActivePlayerIndex,
                Phase = Phase,
                TurnTimerRemaining = TurnTimerRemaining,
                Marbles = (MarbleFinalState[])AcceptedMarbles.Clone(),
                Player0 = Players[0],
                Player1 = Players[1],
                WinnerPlayerIndex = WinnerPlayerIndex,
            };
        }
    }
}
