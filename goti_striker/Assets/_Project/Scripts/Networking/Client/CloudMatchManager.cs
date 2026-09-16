using System;
using UnityEngine;
using PitStriker.Networking.Shared;
using PitStriker.Physics;
using PitStriker.Gameplay;

namespace PitStriker.Networking.Client
{
    /// <summary>
    /// Phase 4 Authoritative Client Match Synchronizer:
    /// Coordinates the in-game scene marbles, turns, timers, and scores based on authoritative snapshots
    /// streamed from the Google Cloud dedicated server.
    /// </summary>
    public class CloudMatchManager : MonoBehaviour
    {
        public static CloudMatchManager Instance { get; private set; }

        [Header("State Tracking")]
        [SerializeField] private CloudMatchPhase _currentPhase = CloudMatchPhase.WaitingForPlayers;
        [SerializeField] private int _activePlayerIndex = 0;
        [SerializeField] private float _turnTimerRemaining = NetworkProtocol.DefaultTurnDuration;

        public CloudMatchPhase CurrentPhase => _currentPhase;
        public int ActivePlayerIndex => _activePlayerIndex;
        public float TurnTimerRemaining => _turnTimerRemaining;
        public bool IsOnlineMatchActive => CloudNetworkClient.Instance != null &&
                                          CloudNetworkClient.Instance.IsConnected &&
                                          !string.IsNullOrEmpty(CloudNetworkClient.Instance.ActiveRoomCode) &&
                                          _currentPhase != CloudMatchPhase.WaitingForPlayers &&
                                          _currentPhase != CloudMatchPhase.Abandoned;

        // Marbles in scene
        private MarbleController _marble0;
        private MarbleController _marble1;

        // ---- v2 shot-input relay state ----
        // Turn and shot ids make duplicate, late and out-of-order messages safe to drop.
        private int _turnId = 0;
        private int _lastAppliedShotId = -1;
        // Shot this client fired and still owes a result for (-1 = none).
        private int _localPendingShotId = -1;
        private ShotInputData _localPendingInput;
        private bool _awaitingShotIdForLocalShot = false;
        // Shot the opponent fired that we are currently replaying.
        private int _replayingShotId = -1;
        private float _settleTimer = 0f;
        private float _awaitingResultTimer = 0f;
        // Set while reconciling so input stays disabled until both phones agree.
        private bool _reconciled = true;

        private const float SettleGraceSeconds = 0.6f;   // ignore the first moments after launch
        private const float MaxSettleSeconds = 15f;      // safety net if a marble never sleeps

        public int TurnId => _turnId;
        public bool IsReconciled => _reconciled;

        // Latest player statistics
        public CompactPlayerData Player0Data { get; private set; }
        public CompactPlayerData Player1Data { get; private set; }

        // Events for HUD and UI
        public static event Action<int> OnActivePlayerChangedEvent;
        public static event Action<float> OnTimerTickEvent;
        public static event Action<CloudMatchPhase> OnPhaseChangedEvent;
        public static event Action<int, int, int> OnPlayerStatsChangedEvent; // playerIdx, strokes, pit
        public static event Action<int> OnMatchCompletedEvent; // winnerIdx
        public static event Action OnRematchReadyEvent;
        public static event Action<int, Vector3, float> OnNetworkShotExecutedEvent;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            FindSceneMarbles();
            RegisterClientEvents();
        }

        private void OnDestroy()
        {
            UnregisterClientEvents();
            if (Instance == this) Instance = null;
        }

        public void FindSceneMarbles()
        {
            MarbleController[] found = FindObjectsByType<MarbleController>(FindObjectsInactive.Include);
            Array.Sort(found, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));

            if (found.Length >= 2)
            {
                _marble0 = found[0];
                _marble1 = found[1];
            }
            else if (found.Length == 1)
            {
                _marble0 = found[0];
            }
        }

        public MarbleController GetMarble(int playerIndex)
        {
            return playerIndex == 0 ? _marble0 : _marble1;
        }

        public bool IsMyTurn()
        {
            if (!IsOnlineMatchActive) return true;
            if (CloudNetworkClient.Instance == null) return false;
            // Input stays closed while a shot is in flight or while we are still reconciling,
            // so two phones can never start a turn from different states.
            if (!_reconciled || _localPendingShotId >= 0 || _replayingShotId >= 0) return false;
            // The opening toss is a throw too: input opens for whichever player is up to toss.
            return CloudNetworkClient.Instance.LocalPlayerIndex == _activePlayerIndex
                   && (_currentPhase == CloudMatchPhase.ReadyToAim || _currentPhase == CloudMatchPhase.TossPhase);
        }

        /// <summary>
        /// Called by SwipeLaunchController immediately after it applies the local impulse.
        /// The strike has already happened in Unity physics — this only reports the effective
        /// values so the opponent can reproduce the identical shot.
        /// </summary>
        public void ReportLocalShot(int marbleId, Vector3 launchDirection, float force,
                                    float maxPitch, bool isLoft, bool openingToss)
        {
            if (!IsOnlineMatchActive || CloudNetworkClient.Instance == null) return;

            var input = new ShotInputData
            {
                TurnId = _turnId,
                ShotId = -1, // assigned by the server
                PlayerIndex = CloudNetworkClient.Instance.LocalPlayerIndex,
                MarbleId = marbleId,
                LaunchDirection = launchDirection,
                Force = force,
                MaxPitch = maxPitch,
                ShotMode = (byte)(isLoft ? 1 : 0),
                OpeningToss = openingToss,
                ClientTimestamp = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds,
            };

            _localPendingInput = input;
            _awaitingShotIdForLocalShot = true;
            _settleTimer = 0f;
            _reconciled = false;

            CloudNetworkClient.Instance.SubmitShotInput(input);
            Debug.Log($"[CLOUD MATCH] Sent ShotInput turn={_turnId} force={force:F1} "
                    + $"mode={(isLoft ? "Loft" : "Ground")} marble={marbleId}");
        }

        // ------------------------------------------------------------------
        // v2 handlers
        // ------------------------------------------------------------------

        /// <summary>
        /// Relayed shot input. On the striker this is the echo carrying the assigned ShotId.
        /// On the opponent it is the shot to replay with the identical Unity impulse.
        /// </summary>
        private void HandleShotInputRelayed(ShotInputData input)
        {
            int localIdx = CloudNetworkClient.Instance?.LocalPlayerIndex ?? -1;

            // Drop anything from a turn we have already moved past.
            if (input.TurnId != _turnId)
            {
                Debug.LogWarning($"[CLOUD MATCH] Dropping shot input for turn {input.TurnId} (we are on {_turnId}).");
                return;
            }
            // Drop a shot we have already applied (duplicate or re-delivered packet).
            if (input.ShotId >= 0 && input.ShotId == _lastAppliedShotId) return;

            if (input.PlayerIndex == localIdx)
            {
                // Our own shot came back stamped with its id; we owe a result for it.
                if (_awaitingShotIdForLocalShot)
                {
                    _localPendingShotId = input.ShotId;
                    _localPendingInput = input;
                    _awaitingShotIdForLocalShot = false;
                    _settleTimer = 0f;
                    Debug.Log($"[CLOUD MATCH] Local shot accepted as id {input.ShotId}.");
                }
                return;
            }

            // Opponent's shot: replay the identical strike through the normal gameplay path.
            MarbleController marble = GetMarble(input.PlayerIndex);
            if (marble == null)
            {
                Debug.LogWarning($"[CLOUD MATCH] No marble for player {input.PlayerIndex}; requesting resync.");
                CloudNetworkClient.Instance?.RequestResync();
                return;
            }

            _replayingShotId = input.ShotId;
            _reconciled = false;
            _awaitingResultTimer = 0f;

            marble.Halt();
            marble.ApplyImpulse(input.LaunchDirection, input.Force, input.MaxPitch);
            OnNetworkShotExecutedEvent?.Invoke(input.PlayerIndex, input.LaunchDirection, input.Force);

            // Watch the opponent's shot, then hand the view back exactly as it was.
            BeginSpectateOpponent(marble);
            Debug.Log($"[CLOUD MATCH] Replaying opponent shot {input.ShotId} (force {input.Force:F1}); "
                    + "spectating until it settles.");
        }

        /// <summary>The opponent's settled result. Used to reconcile our replay.</summary>
        private void HandleShotResultRelayed(ShotResultData result)
        {
            if (result.ShotId == _lastAppliedShotId) return; // duplicate
            ApplyFinalStates(result.Marbles, ease: true);
            _replayingShotId = -1;
            _awaitingResultTimer = 0f;
        }

        /// <summary>
        /// Authoritative state for the next turn. Both clients converge here before input
        /// re-opens, so neither can continue from a conflicting starting state.
        /// </summary>
        private void HandleAcceptedState(AcceptedStateData state)
        {
            if (state.TurnId < _turnId)
            {
                Debug.LogWarning($"[CLOUD MATCH] Ignoring stale accepted state (turn {state.TurnId} < {_turnId}).");
                return;
            }

            _turnId = state.TurnId;
            _lastAppliedShotId = state.LastAppliedShotId;

            if (_currentPhase != state.Phase)
            {
                _currentPhase = state.Phase;
                OnPhaseChangedEvent?.Invoke(_currentPhase);
            }
            if (_activePlayerIndex != state.ActivePlayerIndex)
            {
                _activePlayerIndex = state.ActivePlayerIndex;
                OnActivePlayerChangedEvent?.Invoke(_activePlayerIndex);
            }

            _turnTimerRemaining = state.TurnTimerRemaining;
            OnTimerTickEvent?.Invoke(_turnTimerRemaining);

            // Positions are authoritative: snap or ease every marble onto the accepted state.
            ApplyFinalStates(state.Marbles, ease: true);

            if (Player0Data.TotalStrokes != state.Player0.TotalStrokes || Player0Data.CurrentPit != state.Player0.CurrentPit)
            {
                Player0Data = state.Player0;
                OnPlayerStatsChangedEvent?.Invoke(0, state.Player0.TotalStrokes, state.Player0.CurrentPit);
            }
            if (Player1Data.TotalStrokes != state.Player1.TotalStrokes || Player1Data.CurrentPit != state.Player1.CurrentPit)
            {
                Player1Data = state.Player1;
                OnPlayerStatsChangedEvent?.Invoke(1, state.Player1.TotalStrokes, state.Player1.CurrentPit);
            }

            // The shot being watched has settled and authority has spoken, so give the player
            // their own marble and their own framing back. Done here rather than inside
            // SyncTurnManagerFromAccepted, which returns early when TurnManager is missing and
            // would leave the camera stuck on the opponent.
            EndSpectate();

            // Push pit progression into TurnManager so scoring matches the accepted result.
            SyncTurnManagerFromAccepted(state);

            _localPendingShotId = -1;
            _awaitingShotIdForLocalShot = false;
            _replayingShotId = -1;
            _awaitingResultTimer = 0f;
            _reconciled = true;

            if (state.WinnerPlayerIndex >= 0 && state.Phase == CloudMatchPhase.MatchCompleted)
                OnMatchCompletedEvent?.Invoke(state.WinnerPlayerIndex);

            Debug.Log($"[CLOUD MATCH] Accepted state: turn {_turnId}, active P{_activePlayerIndex + 1}, phase {_currentPhase}.");
        }

        /// <summary>Moves marbles onto authoritative positions, easing small differences.</summary>
        private void ApplyFinalStates(MarbleFinalState[] marbles, bool ease)
        {
            if (marbles == null) return;
            foreach (var m in marbles)
            {
                MarbleController mc = GetMarble(m.MarbleId);
                if (mc == null) continue;

                Vector3 target = m.Position;
                float delta = Vector3.Distance(mc.transform.position, target);

                mc.Halt();
                if (!ease || delta > NetworkProtocol.ReconcileEaseThreshold)
                {
                    // Too far to hide — snap rather than slide visibly across the lane.
                    mc.ResetPosition(target);
                }
                else if (delta > 0.01f)
                {
                    // Small float divergence between two runs of the same physics: ease it.
                    mc.ResetPosition(Vector3.Lerp(mc.transform.position, target, 0.85f));
                    mc.ResetPosition(target);
                }
                mc.IsRetired = m.IsRetired;
            }
        }

        private void SyncTurnManagerFromAccepted(AcceptedStateData state)
        {
            var tm = TurnManager.Instance;
            if (tm == null) return;
            tm.CurrentPlayerIndex = state.ActivePlayerIndex;

            // Force the authoritative score into TurnManager on EVERY accepted state, even when
            // the server's numbers have not moved.
            //
            // This is what stops a desync becoming permanent. SendLocalShotResult reads strokes
            // from TurnManager, which counts every shot the player takes locally. The server only
            // counts shots whose result it accepted. So each abandoned shot — a stalled client, a
            // dropped result — leaves the local count one higher than authority, and nothing
            // brought it back: the stats event above only fires when the server's value CHANGES,
            // and after an abandoned shot it deliberately does not change ("state unchanged").
            //
            // The drift then compounds until it exceeds MaxShotsPerTurn, at which point the
            // server's sanity check rejects every result the player sends, every shot times out,
            // and the player is locked out of the match for good. Observed live as
            // "stroke delta implausible (1 -> 6)".
            ApplyAuthoritativeStats(tm, 0, state.Player0);
            ApplyAuthoritativeStats(tm, 1, state.Player1);
        }

        private static void ApplyAuthoritativeStats(TurnManager tm, int playerIdx, CompactPlayerData data)
        {
            var players = tm.Players;
            if (players == null || playerIdx >= players.Count) return;

            var p = players[playerIdx];
            if (p == null) return;

            if (p.totalStrokes != data.TotalStrokes || p.currentPit != data.CurrentPit)
            {
                Debug.Log($"[CLOUD MATCH] Correcting P{playerIdx + 1} local score to authority: "
                        + $"strokes {p.totalStrokes}->{data.TotalStrokes}, pit {p.currentPit}->{data.CurrentPit}.");
            }

            p.totalStrokes = data.TotalStrokes;
            p.currentPit = data.CurrentPit;
            p.isFinished = data.IsFinished;
        }

        private void RegisterClientEvents()
        {
            if (CloudNetworkClient.Instance != null)
            {
                CloudNetworkClient.Instance.OnMatchStarted += HandleMatchStarted;
                CloudNetworkClient.Instance.OnSnapshotReceived += HandleSnapshotReceived;
                CloudNetworkClient.Instance.OnShotInputRelayed += HandleShotInputRelayed;
                CloudNetworkClient.Instance.OnShotResultRelayed += HandleShotResultRelayed;
                CloudNetworkClient.Instance.OnAcceptedState += HandleAcceptedState;
                CloudNetworkClient.Instance.OnShotBroadcastReceived += HandleShotBroadcast;
                CloudNetworkClient.Instance.OnMatchCompleted += HandleMatchCompleted;
                CloudNetworkClient.Instance.OnRematchConfirmed += HandleRematchConfirmed;
            }
        }

        private void UnregisterClientEvents()
        {
            if (CloudNetworkClient.Instance != null)
            {
                CloudNetworkClient.Instance.OnMatchStarted -= HandleMatchStarted;
                CloudNetworkClient.Instance.OnSnapshotReceived -= HandleSnapshotReceived;
                CloudNetworkClient.Instance.OnShotInputRelayed -= HandleShotInputRelayed;
                CloudNetworkClient.Instance.OnShotResultRelayed -= HandleShotResultRelayed;
                CloudNetworkClient.Instance.OnAcceptedState -= HandleAcceptedState;
                CloudNetworkClient.Instance.OnShotBroadcastReceived -= HandleShotBroadcast;
                CloudNetworkClient.Instance.OnMatchCompleted -= HandleMatchCompleted;
                CloudNetworkClient.Instance.OnRematchConfirmed -= HandleRematchConfirmed;
            }
        }

        private void HandleMatchStarted(string roomCode, string p1, string p2)
        {
            FindSceneMarbles();

            _currentPhase = CloudMatchPhase.ReadyToAim;
            _activePlayerIndex = 0;   // placeholder only — the server picks the opener at random
            _turnTimerRemaining = NetworkProtocol.DefaultTurnDuration;

            // v2: both clients start turn 1 from the same accepted state.
            _turnId = 1;
            _lastAppliedShotId = -1;
            _localPendingShotId = -1;
            _replayingShotId = -1;
            _awaitingShotIdForLocalShot = false;

            // Keep input closed until the server's first AcceptedState says whose turn it is.
            // The opening player is random, so the 0 above is a guess; with _reconciled = true
            // the second seat could briefly see "your turn" and aim before being corrected. The
            // server sends AcceptedState immediately after MatchStarted (rematches included), and
            // HandleAcceptedState reopens input.
            _reconciled = false;

            // Reset marble positions
            if (_marble0 != null)
            {
                _marble0.ResetPosition(new Vector3(-0.4f, 0.25f, -6.0f));
                _marble0.Halt();
                _marble0.IsRetired = false;
            }
            if (_marble1 != null)
            {
                _marble1.ResetPosition(new Vector3(0.4f, 0.25f, -6.0f));
                _marble1.Halt();
                _marble1.IsRetired = false;
            }

            // Sync with TurnManager so cameras, marbles, and HUD are properly configured
            if (TurnManager.Instance != null)
            {
                TurnManager.Instance.ConfigureAndStartMatch(2, new bool[] { false, false }, new string[] { p1, p2 });
                TurnManager.Instance.CurrentPlayerIndex = 0;
            }

            // Point the camera at THIS client's marble. Without this the camera keeps whatever
            // target it had from the menu until someone fires a shot, and since both clients
            // start on CurrentPlayerIndex 0 the joining player would watch the host's marble.
            FocusCameraOnLocalMarble();

            OnPhaseChangedEvent?.Invoke(_currentPhase);
            OnActivePlayerChangedEvent?.Invoke(_activePlayerIndex);

            // Switch to in-game screen if MenuManager exists
            if (UI.MenuManager.Instance != null)
            {
                UI.MenuManager.Instance.ShowScreen(UI.MenuManager.ScreenType.InGame);
            }
        }

        /// <summary>Aims the follow camera at the local player's own marble.</summary>
        // ------------------------------------------------------------------ spectating
        //
        // While the opponent shoots there is nothing to watch on our own marble, so the camera
        // follows theirs — but the player's own framing is not forfeited to do it. The orbit
        // angle they set up is captured on the way in and restored on the way out, so the view
        // they get back for their next shot is the one they left, not a reset one.
        //
        // This is the distinction that made the original behaviour feel broken: it switched
        // targets on every turn change AND discarded the orbit, so the player's aim setup
        // vanished mid-match. Here the switch lasts only for the duration of the opponent's
        // shot and is always undone.

        private float _savedOrbitAngle;
        private bool _spectating;

        private void BeginSpectateOpponent(MarbleController opponentMarble)
        {
            if (opponentMarble == null) return;
            var cam = PitStriker.CameraSystem.SmoothFollowCamera.Instance;
            if (cam == null) return;

            if (!_spectating)
            {
                _savedOrbitAngle = cam.ManualOrbitAngle;
                _spectating = true;
            }
            cam.SetTarget(opponentMarble.transform);
        }

        /// <summary>Hands the view back to this player, with their own orbit restored.</summary>
        private void EndSpectate()
        {
            if (!_spectating) return;
            _spectating = false;

            FocusCameraOnLocalMarble();
            var cam = PitStriker.CameraSystem.SmoothFollowCamera.Instance;
            if (cam != null) cam.SetOrbitAngle(_savedOrbitAngle);
        }

        private void FocusCameraOnLocalMarble()
        {
            int localIdx = CloudNetworkClient.Instance?.LocalPlayerIndex ?? 0;
            if (localIdx < 0) localIdx = 0;
            MarbleController localMarble = GetMarble(localIdx);
            if (localMarble == null) return;

            var cam = UnityEngine.Object.FindAnyObjectByType<PitStriker.CameraSystem.SmoothFollowCamera>();
            if (cam == null) return;

            cam.SetTarget(localMarble.transform);
            Debug.Log($"[CLOUD MATCH] Camera focused on local marble '{localMarble.name}' (P{localIdx + 1}).");
        }

        private void HandleSnapshotReceived(WorldSnapshotData snapshot)
        {
            if (_currentPhase != snapshot.Phase)
            {
                _currentPhase = snapshot.Phase;
                OnPhaseChangedEvent?.Invoke(_currentPhase);
            }

            if (_activePlayerIndex != snapshot.ActivePlayerIndex)
            {
                _activePlayerIndex = snapshot.ActivePlayerIndex;
                OnActivePlayerChangedEvent?.Invoke(_activePlayerIndex);
            }

            _turnTimerRemaining = snapshot.TurnTimerRemaining;
            OnTimerTickEvent?.Invoke(_turnTimerRemaining);

            // Check player scores
            if (Player0Data.TotalStrokes != snapshot.Player0.TotalStrokes || Player0Data.CurrentPit != snapshot.Player0.CurrentPit)
            {
                Player0Data = snapshot.Player0;
                OnPlayerStatsChangedEvent?.Invoke(0, snapshot.Player0.TotalStrokes, snapshot.Player0.CurrentPit);
            }
            if (Player1Data.TotalStrokes != snapshot.Player1.TotalStrokes || Player1Data.CurrentPit != snapshot.Player1.CurrentPit)
            {
                Player1Data = snapshot.Player1;
                OnPlayerStatsChangedEvent?.Invoke(1, snapshot.Player1.TotalStrokes, snapshot.Player1.CurrentPit);
            }

            // Push snapshot to prediction and interpolation controller
            if (PredictionAndInterpolationController.Instance != null)
            {
                PredictionAndInterpolationController.Instance.PushSnapshot(snapshot);
            }

            int localIdx = CloudNetworkClient.Instance?.LocalPlayerIndex ?? 0;
            CompactMarbleState localState = localIdx == 0 ? snapshot.Marble0 : snapshot.Marble1;
            MarbleController localMarble = GetMarble(localIdx);
            if (PredictionAndInterpolationController.Instance != null)
            {
                PredictionAndInterpolationController.Instance.ReconcileLocalMarble(localMarble, localState);
            }
        }

        private void Update()
        {
            if (!IsOnlineMatchActive) return;

            // The striking client owns the outcome: once its physics settles it reports
            // where everything stopped. Nothing is sent until motion actually ends.
            if (_localPendingShotId >= 0)
            {
                _settleTimer += Time.deltaTime;
                if (_settleTimer >= SettleGraceSeconds && (AllMarblesAtRest() || _settleTimer >= MaxSettleSeconds))
                {
                    SendLocalShotResult(timedOut: _settleTimer >= MaxSettleSeconds);
                }
            }

            // If the opponent's result never arrives we ask the server rather than
            // advancing the turn from our own replay.
            if (_replayingShotId >= 0)
            {
                _awaitingResultTimer += Time.deltaTime;
                if (_awaitingResultTimer >= NetworkProtocol.OpponentResultTimeout)
                {
                    Debug.LogWarning($"[CLOUD MATCH] No result for opponent shot {_replayingShotId} "
                                   + $"after {_awaitingResultTimer:F1}s - requesting resync.");
                    _awaitingResultTimer = 0f;
                    _replayingShotId = -1;
                    CloudNetworkClient.Instance?.RequestResync();
                }
            }
        }

        private bool AllMarblesAtRest()
        {
            if (_marble0 != null && _marble0.IsMoving) return false;
            if (_marble1 != null && _marble1.IsMoving) return false;
            return true;
        }

        /// <summary>Builds and sends the result for the shot this client fired. Sent once.</summary>
        private void SendLocalShotResult(bool timedOut)
        {
            int shotId = _localPendingShotId;
            _localPendingShotId = -1;   // guard: exactly one result per shot
            _settleTimer = 0f;

            var client = CloudNetworkClient.Instance;
            if (client == null) return;

            int localIdx = client.LocalPlayerIndex;
            var tm = TurnManager.Instance;

            var states = new System.Collections.Generic.List<MarbleFinalState>();
            AddFinalState(states, 0, _marble0);
            AddFinalState(states, 1, _marble1);

            int pitConquered = 0;
            int currentPitAfter = tm?.ActivePlayer?.currentPit ?? 1;
            int strokesAfter = tm?.ActivePlayer?.totalStrokes ?? 0;
            bool finished = tm?.ActivePlayer?.isFinished ?? false;

            // A pit counts as conquered when the local marble settled inside the pit it was
            // aiming at — decided by the same PitZone logic offline play uses.
            MarbleController localMarble = GetMarble(localIdx);
            if (localMarble != null)
            {
                foreach (var pz in FindObjectsByType<PitZone>(FindObjectsSortMode.None))
                {
                    // Same rule the offline game uses, so what we report as a capture is what
                    // the pit would actually have kept.
                    if (pz.IsMarbleCaptured(localMarble)) { pitConquered = pz.PitNumber; break; }
                }
            }

            var result = new ShotResultData
            {
                TurnId = _turnId,
                ShotId = shotId,
                PlayerIndex = localIdx,
                Marbles = states.ToArray(),
                PitConqueredNumber = pitConquered,
                StrokesAfter = strokesAfter,
                CurrentPitAfter = currentPitAfter,
                PlayerFinished = finished,
                HitOpponent = tm != null && tm.HitOpponentThisTurn,
                ClientTimestamp = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds,
            };

            client.SubmitShotResult(result);
            Debug.Log($"[CLOUD MATCH] Sent ShotResult shot={shotId} pit={pitConquered} "
                    + $"strokes={strokesAfter}{(timedOut ? " (settle timeout)" : "")}");
        }

        private void AddFinalState(System.Collections.Generic.List<MarbleFinalState> list, int id, MarbleController m)
        {
            if (m == null) return;
            list.Add(new MarbleFinalState
            {
                MarbleId = id,
                Position = m.transform.position,
                IsRetired = m.IsRetired,
                InPitNumber = 0,
            });
        }

        private void HandleShotBroadcast(int playerIndex, ShotIntentData intent)
        {
            int localIdx = CloudNetworkClient.Instance?.LocalPlayerIndex ?? 0;
            MarbleController shotMarble = GetMarble(playerIndex);

            if (playerIndex != localIdx)
            {
                // Remote shot: play juice and launch remote marble
                if (shotMarble != null)
                {
                    shotMarble.ApplyImpulse(intent.Direction, intent.Force);
                }
            }

            // Each player keeps their own point of view. The camera deliberately does NOT follow
            // whoever is shooting: yanking it to the opponent's marble every turn stole the view
            // the player had aimed with, and undid any orbit they had set up. Online this is a
            // per-client view, not a shared broadcast camera, so it stays on the local marble and
            // the opponent's shot plays out wherever it happens to be on screen.
            FocusCameraOnLocalMarble();

            OnNetworkShotExecutedEvent?.Invoke(playerIndex, intent.Direction, intent.Force);
        }

        private void HandleMatchCompleted(int winnerIndex)
        {
            _currentPhase = CloudMatchPhase.MatchCompleted;
            OnMatchCompletedEvent?.Invoke(winnerIndex);
        }

        private void HandleRematchConfirmed()
        {
            OnRematchReadyEvent?.Invoke();
            HandleMatchStarted(CloudNetworkClient.Instance?.ActiveRoomCode ?? "", "P1", "P2");
        }
    }
}
