using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using PitStriker.Networking.Shared;
using PitStrikerServer;

namespace PitStrikerServer.Tests
{
    /// <summary>
    /// Automated integration test for the v2 shot-input relay protocol.
    /// Starts the real server in-process and drives it with two WebSocket clients over the
    /// real binary protocol — no mocks — so turn ownership, shot ids, duplicate rejection,
    /// result acceptance and accepted-state broadcast are exercised end to end.
    ///
    /// Run with:  dotnet Server/PitStrikerServer.Tests/bin/Release/net10.0/PitStrikerServer.Tests.dll --v2
    /// (build first). Do not rely on "dotnet run ... -- --v2": in some shells the argument
    /// never reaches the app and the legacy suite runs instead, reporting a misleading pass.
    /// </summary>
    public static class V2ProtocolTests
    {
        private static int _passed;
        private static int _failed;
        private const int Port = 7788;   // deliberately not 7777, so a live server is untouched

        public static async Task<int> RunAsync()
        {
            _passed = 0; _failed = 0;
            Console.WriteLine("=== Pit Striker v2 shot-relay protocol test ===\n");

            // ---- opening player + finished-match rules (engine level) ----
            RunOpeningAndMatchOverChecks();

            // The flow below scripts "index 0 strikes first", so pin the opener for it. The real
            // random picker was already exercised above.
            var realPicker = AuthoritativeMatchEngine.OpeningPlayerPicker;
            AuthoritativeMatchEngine.OpeningPlayerPicker = () => 0;

            var config = new ServerConfiguration { Port = Port, Environment = "Test" };
            var roomManager = new RoomManager();
            var server = new WebSocketServer(config, roomManager);
            await server.StartAsync();
            await Task.Delay(700);

            try { await RunFlow(); }
            catch (Exception ex) { _failed++; Console.WriteLine($"  FAIL  unhandled exception\n{ex}"); }
            finally { server.Stop(); AuthoritativeMatchEngine.OpeningPlayerPicker = realPicker; }

            Console.WriteLine($"\n=== {_passed} passed, {_failed} failed ===");
            return _failed == 0 ? 0 : 1;
        }

        private static async Task RunFlow()
        {
            var a = new V2TestClient("PlayerA");
            var b = new V2TestClient("PlayerB");
            await a.ConnectAsync(Port);
            await b.ConnectAsync(Port);

            // ---- handshake + version gate ----
            await a.SendConnectRequest(NetworkProtocol.Version);
            await b.SendConnectRequest(NetworkProtocol.Version);
            Check("A handshake accepted", await a.WaitForConnectResponse());
            Check("B handshake accepted", await b.WaitForConnectResponse());

            var stale = new V2TestClient("StaleBuild");
            await stale.ConnectAsync(Port);
            await stale.SendConnectRequest(NetworkProtocol.Version - 1);
            Check("mismatched protocol version rejected", !await stale.WaitForConnectResponse());
            await stale.CloseAsync();

            // ---- quick match ----
            await a.SendOpCode(NetworkOpCode.QuickMatchRequest);
            await b.SendOpCode(NetworkOpCode.QuickMatchRequest);

            int aIdx = await a.WaitForMatchStarted();
            int bIdx = await b.WaitForMatchStarted();
            Check("both clients received MatchStarted", aIdx >= 0 && bIdx >= 0);
            Check($"distinct player indices (A={aIdx}, B={bIdx})", aIdx != bIdx);

            V2TestClient striker = aIdx == 0 ? a : b;
            V2TestClient waiter = aIdx == 0 ? b : a;

            // ---- opening toss (opener pinned: player 0 throws first) ----
            await waiter.SendShotInput(MakeToss(1, 1));
            Check("toss from the player not throwing is rejected",
                  !await waiter.WaitForOpCode(NetworkOpCode.ShotInputRelay, 900));

            await striker.SendShotInput(MakeToss(1, 0));
            ShotInputData? toss1 = await striker.WaitForShotInput(3000);
            await waiter.WaitForShotInput(3000);   // drain the relay so a later poll cannot mistake it for an echo
            Check("first toss accepted", toss1.HasValue);
            await striker.SendShotResult(MakeTossResult(1, toss1?.ShotId ?? -1, 0, 29.0f));   // 2.0 m short of pit 3
            AcceptedStateData? afterToss1 = await a.WaitForAcceptedState(3000);
            await b.WaitForAcceptedState(3000);
            Check("after the first toss the other player throws",
                  afterToss1?.Phase == CloudMatchPhase.TossPhase && afterToss1?.ActivePlayerIndex == 1);

            int toss2Turn = afterToss1?.TurnId ?? -1;
            await waiter.SendShotInput(MakeToss(toss2Turn, 1));
            ShotInputData? toss2 = await waiter.WaitForShotInput(3000);
            await striker.WaitForShotInput(3000);
            Check("second toss accepted", toss2.HasValue);
            await waiter.SendShotResult(MakeTossResult(toss2Turn, toss2?.ShotId ?? -1, 1, 24.0f));   // 7.0 m short
            AcceptedStateData? afterToss = await a.WaitForAcceptedState(3000);
            AcceptedStateData? afterTossB = await b.WaitForAcceptedState(3000);
            Check("toss ends in ReadyToAim", afterToss?.Phase == CloudMatchPhase.ReadyToAim);
            Check("closest to pit 3 plays first", afterToss?.ActivePlayerIndex == 0);
            Check("both clients agree on the toss winner",
                  afterToss.HasValue && afterTossB.HasValue && afterToss.Value.ActivePlayerIndex == afterTossB.Value.ActivePlayerIndex);
            Check("both marbles back on the tee after the toss",
                  afterToss.HasValue && Math.Abs(afterToss.Value.Marbles[0].Position.z + 6f) < 0.01f
                                     && Math.Abs(afterToss.Value.Marbles[1].Position.z + 6f) < 0.01f);
            Check("the toss is not counted as a stroke",
                  (afterToss?.Player0.TotalStrokes ?? -1) == 0 && (afterToss?.Player1.TotalStrokes ?? -1) == 0);

            int turn = afterToss?.TurnId ?? -1;

            // ---- a shot from the non-active player must be refused ----
            await waiter.SendShotInput(MakeInput(turn, 1));
            Check("shot from non-active player rejected",
                  !await waiter.WaitForOpCode(NetworkOpCode.ShotInputRelay, 900));

            // ---- legitimate shot: relayed to opponent AND echoed to striker ----
            await striker.SendShotInput(MakeInput(turn, 0));
            ShotInputData? relayed = await waiter.WaitForShotInput(3000);
            ShotInputData? echoed = await striker.WaitForShotInput(3000);
            Check("opponent received relayed shot input", relayed.HasValue);
            Check("striker received its own echo", echoed.HasValue);

            int shotId = echoed?.ShotId ?? -1;
            Check($"server assigned a shot id ({shotId})", shotId > 0);
            Check("relayed and echoed shot ids agree", relayed?.ShotId == shotId);
            Check("force preserved exactly through relay", Math.Abs((relayed?.Force ?? 0f) - 18.5f) < 0.001f);
            Check("loft mode preserved through relay", relayed?.ShotMode == 1);
            Check("loft pitch survived validation (was clamped to 0.15 in v1)",
                  (relayed?.LaunchDirection.y ?? 0f) > 0.2f);

            // ---- stale turn id is dropped ----
            await striker.SendShotInput(MakeInput(99, 0));
            Check("stale turn id rejected", !await striker.WaitForOpCode(NetworkOpCode.ShotInputRelay, 800));

            // ---- result accepted, turn advances, both clients converge ----
            await striker.SendShotResult(MakeResult(turn, shotId, 0));
            AcceptedStateData? accA = await a.WaitForAcceptedState(3000);
            AcceptedStateData? accB = await b.WaitForAcceptedState(3000);
            Check("A received accepted state", accA.HasValue);
            Check("B received accepted state", accB.HasValue);
            Check("both clients agree on turn id",
                  accA.HasValue && accB.HasValue && accA.Value.TurnId == accB.Value.TurnId);
            Check("both clients agree on active player",
                  accA.HasValue && accB.HasValue && accA.Value.ActivePlayerIndex == accB.Value.ActivePlayerIndex);
            Check("turn advanced past the shot turn", (accA?.TurnId ?? 0) > turn);
            Check("turn passed to the other player", (accA?.ActivePlayerIndex ?? -1) == 1);
            Check("accepted position matches the client-reported result",
                  accA.HasValue && accA.Value.Marbles.Length >= 1 &&
                  Math.Abs(accA.Value.Marbles[0].Position.z - 12.25f) < 0.01f);
            Check("stroke count carried through", (accA?.Player0.TotalStrokes ?? 0) == 1);

            // ---- duplicate result must not advance the turn twice ----
            int turnAfterFirst = accA?.TurnId ?? 0;
            await striker.SendShotResult(MakeResult(turn, shotId, 0));
            AcceptedStateData? dup = await a.WaitForAcceptedState(1200);
            Check("duplicate result did not advance the turn",
                  !(dup.HasValue && dup.Value.TurnId > turnAfterFirst));

            // ---- resync returns the current accepted state ----
            await b.SendOpCode(NetworkOpCode.ResyncRequest);
            AcceptedStateData? resync = await b.WaitForAcceptedState(2500);
            Check("resync request answered", resync.HasValue);
            Check("resync matches current turn", (resync?.TurnId ?? -1) == turnAfterFirst);

            // ---- second turn: the new active player may now shoot ----
            await waiter.SendShotInput(MakeInput(turnAfterFirst, 1));
            Check("new active player's shot accepted",
                  await striker.WaitForOpCode(NetworkOpCode.ShotInputRelay, 3000));

            await a.CloseAsync();
            await b.CloseAsync();
        }

        private static ShotInputData MakeInput(int turnId, int playerIndex) => new ShotInputData
        {
            TurnId = turnId,
            ShotId = -1,
            PlayerIndex = playerIndex,
            MarbleId = 0,
            LaunchDirection = new NetVector3(0f, 0.21f, 0.9777f),  // lofted, ~unit length
            Force = 18.5f,
            MaxPitch = 0.55f,
            ShotMode = 1,
            OpeningToss = false,
            ClientTimestamp = 0,
        };

        private static ShotResultData MakeResult(int turnId, int shotId, int playerIndex) => new ShotResultData
        {
            TurnId = turnId,
            ShotId = shotId,
            PlayerIndex = playerIndex,
            Marbles = new[]
            {
                new MarbleFinalState { MarbleId = 0, Position = new NetVector3(0.5f, 0.16f, 12.25f) },
                new MarbleFinalState { MarbleId = 1, Position = new NetVector3(0.4f, 0.16f, -6.0f) },
            },
            PitConqueredNumber = 0,
            StrokesAfter = 1,
            CurrentPitAfter = 1,
            PlayerFinished = false,
            HitOpponent = false,
            ClientTimestamp = 0,
        };

        private static void RunOpeningAndMatchOverChecks()
        {
            // The default picker must actually vary. Hard-coded 0 gave the room creator the first
            // shot in every online match.
            int zeros = 0, ones = 0;
            for (int i = 0; i < 400; i++)
            {
                var r = new Room("OPEN" + i);
                r.MatchEngine.StartMatch();
                if (r.MatchEngine.ActivePlayerIndex == 0) zeros++; else ones++;
            }
            Check($"random opener reaches both players (P1 {zeros}, P2 {ones} of 400)", zeros > 100 && ones > 100);

            var saved = AuthoritativeMatchEngine.OpeningPlayerPicker;
            try
            {
                AuthoritativeMatchEngine.OpeningPlayerPicker = () => 1;
                var pinned = new Room("OPENP2");
                pinned.MatchEngine.StartMatch();
                Check("StartMatch honours the opening player picker", pinned.MatchEngine.ActivePlayerIndex == 1);

                // A decided match must stop advancing turns.
                AuthoritativeMatchEngine.OpeningPlayerPicker = () => 0;
                var over = new Room("OVER01");
                over.MatchEngine.StartMatch();
                over.MatchOver = true;
                int turnBefore = over.MatchEngine.TurnId;
                // One tick longer than a whole turn: an unguarded engine would expire the timer here.
                over.MatchEngine.Tick(NetworkProtocol.DefaultTurnDuration + 1f);
                Check("finished match does not pass turns on timer expiry",
                      over.MatchEngine.TurnId == turnBefore && over.MatchEngine.ActivePlayerIndex == 0);

                // ...and a rematch clears the flag so play resumes.
                over.MatchEngine.StartMatch();
                Check("rematch clears MatchOver", !over.MatchOver);
            }
            finally { AuthoritativeMatchEngine.OpeningPlayerPicker = saved; }

            RunTossEdgeChecks();
            RunPitProgressChecks();
            RunPruneChecks();
        }

        /// <summary>
        /// Pit progress is server-derived. Written after a live match where players "conquered
        /// pit 1" repeatedly and no online match could ever finish.
        /// </summary>
        private static void RunPitProgressChecks()
        {
            var saved = AuthoritativeMatchEngine.OpeningPlayerPicker;
            try
            {
                AuthoritativeMatchEngine.OpeningPlayerPicker = () => 0;

                // Plays the toss so player 0 wins it, leaving the engine on player 0's first turn.
                static AuthoritativeMatchEngine PastToss(string code)
                {
                    var e = new Room(code).MatchEngine;
                    e.StartMatch();
                    for (int p = 0; p < 2; p++)
                    {
                        var t = MakeToss(e.TurnId, p);
                        e.SubmitShotInput(p, ref t, out int tid, out _);
                        e.SubmitShotResult(p, MakeTossResult(e.TurnId, tid, p, p == 0 ? 30f : 20f), out _);
                    }
                    return e;
                }

                // One shot by `player` whose client reports landing in `landedPit` but, like the
                // live clients, never advances its own reported pit.
                static bool Shoot(AuthoritativeMatchEngine e, int player, int landedPit, int strokesAfter, out string why)
                {
                    var input = MakeInput(e.TurnId, player);
                    e.SubmitShotInput(player, ref input, out int id, out _);
                    var r = MakeResult(e.TurnId, id, player);
                    r.PitConqueredNumber = landedPit;
                    r.StrokesAfter = strokesAfter;
                    r.CurrentPitAfter = 1;
                    return e.SubmitShotResult(player, r, out why);
                }

                var e1 = PastToss("PIT001");
                Shoot(e1, 0, 1, 1, out _);
                Check("conquering the target pit advances it on the server, whatever the client reports",
                      e1.Players[0].CurrentPit == 2);
                Check("a capture still earns the extra play", e1.ActivePlayerIndex == 0);

                Shoot(e1, 0, 1, 2, out _);
                Check("landing in an already-conquered pit is not a second capture",
                      e1.Players[0].CurrentPit == 2);

                var e2 = PastToss("PIT002");
                bool accepted = Shoot(e2, 0, 2, 1, out string why2);
                Check("landing in the wrong pit is accepted as a normal shot, not rejected",
                      accepted && e2.Players[0].CurrentPit == 1 && e2.PendingShotId < 0 && e2.ActivePlayerIndex == 1);

                var e3 = PastToss("PIT003");
                Shoot(e3, 0, 1, 1, out _);   // pit 1, extra play
                Shoot(e3, 0, 2, 2, out _);   // pit 2, extra play
                Shoot(e3, 0, 3, 3, out _);   // pit 3
                Check("conquering pit 3 finishes the match for that player",
                      e3.Phase == CloudMatchPhase.MatchCompleted && e3.WinnerPlayerIndex == 0);
                Check("a match finished at the table is marked over", e3.Room.MatchOver);

                // A structurally broken result from the real striker must not hold the match.
                var e4 = PastToss("PIT004");
                var bad = MakeInput(e4.TurnId, 0);
                e4.SubmitShotInput(0, ref bad, out int badId, out _);
                var broken = MakeResult(e4.TurnId, badId, 0);
                broken.StrokesAfter = 99;
                bool badAccepted = e4.SubmitShotResult(0, broken, out _);
                Check("an unusable result from the striker is abandoned at once, not after 20 s",
                      !badAccepted && e4.PendingShotId < 0 && e4.Phase == CloudMatchPhase.ReadyToAim && e4.ActivePlayerIndex == 1);
            }
            finally { AuthoritativeMatchEngine.OpeningPlayerPicker = saved; }
        }

        /// <summary>Toss cases a scripted two-client flow does not reach: players who never throw.</summary>
        private static void RunTossEdgeChecks()
        {
            var saved = AuthoritativeMatchEngine.OpeningPlayerPicker;
            try
            {
                AuthoritativeMatchEngine.OpeningPlayerPicker = () => 0;

                var start = new Room("TOSS00");
                start.MatchEngine.StartMatch();
                Check("an online match opens in the toss phase", start.MatchEngine.Phase == CloudMatchPhase.TossPhase);

                // P1 lets the clock run out; P2 throws. P2 must win the toss.
                var timeout = new Room("TOSS01");
                var e = timeout.MatchEngine;
                e.StartMatch();
                e.Tick(NetworkProtocol.DefaultTurnDuration + 1f);
                Check("a player who does not throw passes the toss to the other player",
                      e.Phase == CloudMatchPhase.TossPhase && e.ActivePlayerIndex == 1
                      && e.TossDistance(0) == AuthoritativeMatchEngine.NoTossDistance);

                var input = MakeToss(e.TurnId, 1);
                bool thrown = e.SubmitShotInput(1, ref input, out int id, out _);
                bool reported = e.SubmitShotResult(1, MakeTossResult(e.TurnId, id, 1, 20f), out _);
                Check("the player who threw wins the toss against one who did not",
                      thrown && reported && e.Phase == CloudMatchPhase.ReadyToAim && e.ActivePlayerIndex == 1);

                // Neither throws: the match must still start rather than stall.
                var neither = new Room("TOSS02");
                neither.MatchEngine.StartMatch();
                neither.MatchEngine.Tick(NetworkProtocol.DefaultTurnDuration + 1f);
                neither.MatchEngine.Tick(NetworkProtocol.DefaultTurnDuration + 1f);
                Check("if neither player throws, the match still starts",
                      neither.MatchEngine.Phase == CloudMatchPhase.ReadyToAim);

                // Sinking pit 3 is a bullseye and beats any distance.
                var bull = new Room("TOSS03");
                var b = bull.MatchEngine;
                b.StartMatch();
                var t0 = MakeToss(b.TurnId, 0);
                b.SubmitShotInput(0, ref t0, out int id0, out _);
                b.SubmitShotResult(0, MakeTossResult(b.TurnId, id0, 0, 30.9f), out _);   // 0.1 m away
                var t1 = MakeToss(b.TurnId, 1);
                b.SubmitShotInput(1, ref t1, out int id1, out _);
                var sunk = MakeTossResult(b.TurnId, id1, 1, 31.0f);
                sunk.PitConqueredNumber = 3;
                b.SubmitShotResult(1, sunk, out _);
                Check("sinking pit 3 in the toss beats a closer-looking throw",
                      b.Phase == CloudMatchPhase.ReadyToAim && b.ActivePlayerIndex == 1 && b.TossDistance(1) == 0f);
            }
            finally { AuthoritativeMatchEngine.OpeningPlayerPicker = saved; }
        }

        private static ShotInputData MakeToss(int turnId, int playerIndex) => new ShotInputData
        {
            TurnId = turnId,
            ShotId = -1,
            PlayerIndex = playerIndex,
            MarbleId = playerIndex,
            LaunchDirection = new NetVector3(0f, 0.08f, 0.9968f),   // the client's flat toss pitch
            Force = 20f,
            MaxPitch = 0.12f,
            ShotMode = 0,
            OpeningToss = true,
            ClientTimestamp = 0,
        };

        /// <summary>Toss result with the thrower's marble resting on the centreline at <paramref name="z"/>.</summary>
        private static ShotResultData MakeTossResult(int turnId, int shotId, int playerIndex, float z) => new ShotResultData
        {
            TurnId = turnId,
            ShotId = shotId,
            PlayerIndex = playerIndex,
            Marbles = new[]
            {
                new MarbleFinalState { MarbleId = playerIndex, Position = new NetVector3(0f, 0.16f, z) },
            },
            PitConqueredNumber = 0,
            StrokesAfter = 0,
            CurrentPitAfter = 1,
            PlayerFinished = false,
            HitOpponent = false,
            ClientTimestamp = 0,
        };

        /// <summary>
        /// Exercises RoomManager.PruneDeadRooms directly. The first version of the match-over
        /// rule shipped unreachable and no test caught it, because nothing tested pruning; it
        /// was only noticed when live rooms were all pruned by the slower abandoned rule.
        /// </summary>
        private static void RunPruneChecks()
        {
            // Players whose sockets were never opened: they hold a slot but IsConnected is false,
            // exactly like a player who dropped and has not reconnected.
            static Room SeatTwoDisconnected(RoomManager rm)
            {
                var room = rm.CreateRoom();
                room.AddPlayer(new ClientSession("s-" + Guid.NewGuid().ToString("N"), new ClientWebSocket(), "P1"));
                room.AddPlayer(new ClientSession("s-" + Guid.NewGuid().ToString("N"), new ClientWebSocket(), "P2"));
                return room;
            }

            static bool Contains(RoomManager rm, Room room)
            {
                foreach (var r in rm.ActiveRooms) if (ReferenceEquals(r, room)) return true;
                return false;
            }

            var rm = new RoomManager();

            var decided = SeatTwoDisconnected(rm);
            decided.MatchOver = true;
            decided.NoConnectionSinceUtc = DateTime.UtcNow;          // only just emptied
            var fresh = SeatTwoDisconnected(rm);
            fresh.NoConnectionSinceUtc = DateTime.UtcNow;            // still inside reconnect window
            var stale = SeatTwoDisconnected(rm);
            stale.NoConnectionSinceUtc = DateTime.UtcNow.AddSeconds(-(NetworkProtocol.DisconnectGracePeriod + 11));

            rm.PruneDeadRooms();

            Check("decided match with nobody connected is pruned immediately", !Contains(rm, decided));
            Check("undecided room inside the reconnect window is kept", Contains(rm, fresh));
            Check("undecided room past the reconnect window is pruned", !Contains(rm, stale));

            // Disconnect grace after the match is decided must not declare a forfeit.
            var rm2 = new RoomManager();
            var finished = SeatTwoDisconnected(rm2);
            finished.MatchOver = true;
            finished.DisconnectedPlayerIndex = 1;
            finished.DisconnectGraceStartUtc = DateTime.UtcNow.AddSeconds(-(NetworkProtocol.DisconnectGracePeriod + 1));
            var live = SeatTwoDisconnected(rm2);
            live.DisconnectedPlayerIndex = 1;
            live.DisconnectGraceStartUtc = DateTime.UtcNow.AddSeconds(-(NetworkProtocol.DisconnectGracePeriod + 1));
            rm2.Tick(0.05f);
            Check("leaving a finished match is not a forfeit",
                  finished.ForfeitWinnerIndex == -1 && finished.DisconnectGraceStartUtc == null);
            Check("failing to return to an unfinished match still forfeits it",
                  live.ForfeitWinnerIndex == 0 && live.MatchOver);
        }

        private static void Check(string name, bool ok)
        {
            if (ok) { _passed++; Console.WriteLine($"  PASS  {name}"); }
            else { _failed++; Console.WriteLine($"  FAIL  {name}"); }
        }
    }

    /// <summary>Minimal WebSocket client speaking the real binary protocol.</summary>
    internal class V2TestClient
    {
        private readonly ClientWebSocket _socket = new ClientWebSocket();
        private readonly NetworkByteWriter _writer = new NetworkByteWriter(2048);
        private readonly System.Collections.Concurrent.ConcurrentQueue<(NetworkOpCode op, byte[] data)> _inbox = new();
        public string Name { get; }
        public int PlayerIndex { get; private set; } = -1;

        public V2TestClient(string name) { Name = name; }

        public async Task ConnectAsync(int port)
        {
            await _socket.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), CancellationToken.None);
            _ = ReceiveLoopAsync();
        }

        private async Task ReceiveLoopAsync()
        {
            var buf = new byte[8192];
            try
            {
                while (_socket.State == WebSocketState.Open)
                {
                    var res = await _socket.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
                    if (res.MessageType == WebSocketMessageType.Close) break;
                    if (res.Count <= 0) continue;
                    var copy = new byte[res.Count];
                    Array.Copy(buf, copy, res.Count);
                    _inbox.Enqueue(((NetworkOpCode)copy[0], copy));
                }
            }
            catch { /* socket closed */ }
        }

        public Task SendOpCode(NetworkOpCode op, Action<NetworkByteWriter>? payload = null)
        {
            _writer.Reset();
            _writer.WriteByte((byte)op);
            payload?.Invoke(_writer);
            return _socket.SendAsync(new ArraySegment<byte>(_writer.Buffer, 0, _writer.Position),
                                     WebSocketMessageType.Binary, true, CancellationToken.None);
        }

        public Task SendConnectRequest(int version) => SendOpCode(NetworkOpCode.ConnectRequest, w =>
        {
            w.WriteInt32(version);
            w.WriteString(Name);
        });

        public Task SendShotInput(ShotInputData i) => SendOpCode(NetworkOpCode.ShotInput, w => w.WriteShotInput(i));
        public Task SendShotResult(ShotResultData r) => SendOpCode(NetworkOpCode.ShotResult, w => w.WriteShotResult(r));

        private async Task<byte[]?> PollAsync(NetworkOpCode want, int timeoutMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                if (_inbox.TryDequeue(out var msg))
                {
                    if (msg.op == want) return msg.data;
                    continue;   // ignore unrelated traffic
                }
                await Task.Delay(20);
            }
            return null;
        }

        public async Task<bool> WaitForOpCode(NetworkOpCode op, int timeoutMs) => await PollAsync(op, timeoutMs) != null;

        public async Task<bool> WaitForConnectResponse()
        {
            var data = await PollAsync(NetworkOpCode.ConnectResponse, 3000);
            if (data == null) return false;
            return new NetworkByteReader(data, 1, data.Length - 1).ReadBool();
        }

        public async Task<int> WaitForMatchStarted()
        {
            var data = await PollAsync(NetworkOpCode.MatchStarted, 9000);
            if (data == null) return -1;
            var r = new NetworkByteReader(data, 1, data.Length - 1);
            r.ReadString(); r.ReadString(); r.ReadString();
            PlayerIndex = r.Remaining >= 4 ? r.ReadInt32() : -1;
            return PlayerIndex;
        }

        public async Task<ShotInputData?> WaitForShotInput(int timeoutMs)
        {
            var data = await PollAsync(NetworkOpCode.ShotInputRelay, timeoutMs);
            if (data == null) return null;
            return new NetworkByteReader(data, 1, data.Length - 1).ReadShotInput();
        }

        public async Task<AcceptedStateData?> WaitForAcceptedState(int timeoutMs)
        {
            var data = await PollAsync(NetworkOpCode.AcceptedState, timeoutMs);
            if (data == null) return null;
            return new NetworkByteReader(data, 1, data.Length - 1).ReadAcceptedState();
        }

        public async Task CloseAsync()
        {
            try { await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); }
            catch { }
        }
    }
}
