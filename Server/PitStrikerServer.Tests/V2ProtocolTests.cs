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
    /// Run with:  dotnet run --project Server/PitStrikerServer.Tests -- --v2
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

            var config = new ServerConfiguration { Port = Port, Environment = "Test" };
            var roomManager = new RoomManager();
            var server = new WebSocketServer(config, roomManager);
            await server.StartAsync();
            await Task.Delay(700);

            try { await RunFlow(); }
            catch (Exception ex) { _failed++; Console.WriteLine($"  FAIL  unhandled exception\n{ex}"); }
            finally { server.Stop(); }

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

            // ---- a shot from the non-active player must be refused ----
            await waiter.SendShotInput(MakeInput(1, 1));
            Check("shot from non-active player rejected",
                  !await waiter.WaitForOpCode(NetworkOpCode.ShotInputRelay, 900));

            // ---- legitimate shot: relayed to opponent AND echoed to striker ----
            await striker.SendShotInput(MakeInput(1, 0));
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
            await striker.SendShotResult(MakeResult(1, shotId, 0));
            AcceptedStateData? accA = await a.WaitForAcceptedState(3000);
            AcceptedStateData? accB = await b.WaitForAcceptedState(3000);
            Check("A received accepted state", accA.HasValue);
            Check("B received accepted state", accB.HasValue);
            Check("both clients agree on turn id",
                  accA.HasValue && accB.HasValue && accA.Value.TurnId == accB.Value.TurnId);
            Check("both clients agree on active player",
                  accA.HasValue && accB.HasValue && accA.Value.ActivePlayerIndex == accB.Value.ActivePlayerIndex);
            Check("turn advanced past the shot turn", (accA?.TurnId ?? 0) > 1);
            Check("turn passed to the other player", (accA?.ActivePlayerIndex ?? -1) == 1);
            Check("accepted position matches the client-reported result",
                  accA.HasValue && accA.Value.Marbles.Length >= 1 &&
                  Math.Abs(accA.Value.Marbles[0].Position.z - 12.25f) < 0.01f);
            Check("stroke count carried through", (accA?.Player0.TotalStrokes ?? 0) == 1);

            // ---- duplicate result must not advance the turn twice ----
            int turnAfterFirst = accA?.TurnId ?? 0;
            await striker.SendShotResult(MakeResult(1, shotId, 0));
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
