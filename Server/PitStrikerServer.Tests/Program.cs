using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using PitStriker.Networking.Shared;
using PitStrikerServer;

namespace PitStrikerServer.Tests
{
    internal class Program
    {
        private static int _testsPassed = 0;
        private static int _testsFailed = 0;

        private static async Task<int> Main(string[] args)
        {
            // Automated v2 shot-relay protocol test: real server in-process, two real clients.
            if (args.Length > 0 && args[0].Equals("--v2", StringComparison.OrdinalIgnoreCase))
            {
                return await V2ProtocolTests.RunAsync();
            }

            // The legacy suite below asserts "Player 0 should be active first"; the opener is
            // random in production, so pin it here to keep these tests deterministic.
            AuthoritativeMatchEngine.OpeningPlayerPicker = () => 0;

            if (args.Length > 0 && args[0].Equals("--join", StringComparison.OrdinalIgnoreCase))
            {
                string roomToJoin = args.Length > 1 ? args[1] : "9CLGLF";
                Console.WriteLine($"[JOIN TEST] Connecting to ws://127.0.0.1:7777 to join room {roomToJoin} as Player 2...");
                using var ws = new ClientWebSocket();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    await ws.ConnectAsync(new Uri("ws://127.0.0.1:7777"), cts.Token);
                    Console.WriteLine($"[JOIN TEST] Connected! Sending handshake as 'Player 2'...");
                    var writer = new NetworkByteWriter(128);
                    writer.WriteByte((byte)NetworkOpCode.ConnectRequest);
                    writer.WriteInt32(NetworkProtocol.Version);
                    writer.WriteString("Player 2");
                    await ws.SendAsync(new ArraySegment<byte>(writer.Buffer, 0, writer.Position), WebSocketMessageType.Binary, true, cts.Token);

                    byte[] buf = new byte[2048];
                    var res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
                    var reader = new NetworkByteReader(buf, 0, res.Count);
                    var op = (NetworkOpCode)reader.ReadByte();
                    reader.ReadBool();
                    string sid = reader.ReadString();
                    string tok = reader.ReadString();
                    Console.WriteLine($"[JOIN TEST] Handshake OK! Session: {sid}");

                    // Join room
                    writer.Reset();
                    writer.WriteByte((byte)NetworkOpCode.JoinRoomRequest);
                    writer.WriteString(roomToJoin);
                    await ws.SendAsync(new ArraySegment<byte>(writer.Buffer, 0, writer.Position), WebSocketMessageType.Binary, true, cts.Token);

                    Console.WriteLine($"[JOIN TEST] Sent JoinRoomRequest for room {roomToJoin}! Listening for events...");
                    DateTime start = DateTime.Now;
                    while (ws.State == WebSocketState.Open && (DateTime.Now - start).TotalSeconds < 25)
                    {
                        res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
                        if (res.MessageType == WebSocketMessageType.Close) break;
                        reader.Reset(buf, 0, res.Count);
                        op = (NetworkOpCode)reader.ReadByte();
                        Console.WriteLine($"[JOIN TEST EVENT] Received op: {op}");
                        if (op == NetworkOpCode.MatchStarted)
                        {
                            string rCode = reader.ReadString();
                            string p1 = reader.ReadString();
                            string p2 = reader.ReadString();
                            Console.WriteLine($"\n=======================================================");
                            Console.WriteLine($"★ MATCH STARTED IN ROOM {rCode}! ★");
                            Console.WriteLine($"  Player 1: {p1} vs Player 2: {p2}");
                            Console.WriteLine($"=======================================================\n");
                            break;
                        }
                    }
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Test done", CancellationToken.None);
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[JOIN TEST ERROR] {ex.Message}");
                    return 1;
                }
            }

            if (args.Length > 0 && args[0].StartsWith("ws", StringComparison.OrdinalIgnoreCase))
            {
                string targetUrl = args[0];
                Console.WriteLine($"[REMOTE TEST] Testing connection to {targetUrl}...");
                using var ws = new ClientWebSocket();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try
                {
                    await ws.ConnectAsync(new Uri(targetUrl), cts.Token);
                    Console.WriteLine($"[REMOTE TEST] Connected successfully! State: {ws.State}");
                    
                    // Send ConnectRequest handshake
                    var writer = new NetworkByteWriter(128);
                    writer.WriteByte((byte)NetworkOpCode.ConnectRequest);
                    writer.WriteInt32(NetworkProtocol.Version);
                    writer.WriteString("ValidationClient");
                    await ws.SendAsync(new ArraySegment<byte>(writer.Buffer, 0, writer.Position), WebSocketMessageType.Binary, true, cts.Token);

                    byte[] buf = new byte[1024];
                    var res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
                    var reader = new NetworkByteReader(buf, 0, res.Count);
                    var op = (NetworkOpCode)reader.ReadByte();
                    Console.WriteLine($"[REMOTE TEST] Handshake response: {op}");
                    bool compatible = reader.ReadBool();
                    string sessionId = reader.ReadString();
                    string token = reader.ReadString();
                    Console.WriteLine($"[REMOTE TEST] Handshake OK! SessionId: {sessionId}");

                    // Create Room
                    writer.Reset();
                    writer.WriteByte((byte)NetworkOpCode.CreateRoomRequest);
                    await ws.SendAsync(new ArraySegment<byte>(writer.Buffer, 0, writer.Position), WebSocketMessageType.Binary, true, cts.Token);

                    res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
                    reader.Reset(buf, 0, res.Count);
                    op = (NetworkOpCode)reader.ReadByte();
                    string roomCode = reader.ReadString();
                    int playerIdx = reader.ReadInt32();
                    Console.WriteLine($"[REMOTE TEST] Room created successfully! Code: {roomCode}, PlayerIndex: {playerIdx}");
                    
                    await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[REMOTE TEST FAILED] {ex.GetType().Name}: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"[INNER EXCEPTION] {ex.InnerException.Message}");
                    }
                    return 1;
                }
            }

            Console.WriteLine("=================================================");
            Console.WriteLine("★ PitStriker Cloud Multiplayer Test Suite ★");
            Console.WriteLine("=================================================\n");

            RunTest("Protocol Serialization & Deserialization", TestProtocolSerialization);
            RunTest("Authoritative Match Engine & Physics", TestAuthoritativeMatchEngine);
            await RunAsyncTest("End-to-End Dual-Client & Reconnect Integration", TestEndToEndDualClientAsync);

            Console.WriteLine("\n=================================================");
            Console.WriteLine($"Test Results: {_testsPassed} PASSED, {_testsFailed} FAILED");
            Console.WriteLine("=================================================");

            return _testsFailed == 0 ? 0 : 1;
        }

        private static void RunTest(string name, Action testAction)
        {
            Console.Write($"[TEST] {name}... ");
            try
            {
                testAction();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED");
                Console.ResetColor();
                _testsPassed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FAILED: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine(ex.StackTrace);
                _testsFailed++;
            }
        }

        private static async Task RunAsyncTest(string name, Func<Task> testFunc)
        {
            Console.Write($"[TEST] {name}... ");
            try
            {
                await testFunc();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED");
                Console.ResetColor();
                _testsPassed++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FAILED: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine(ex.StackTrace);
                _testsFailed++;
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        private static void TestProtocolSerialization()
        {
            var writer = new NetworkByteWriter(1024);

            // 1. Test ShotIntentData
            var intent = new ShotIntentData(42, 1234567.89, new NetVector3(0.5f, 0.05f, 0.866f), 25.5f);
            writer.WriteByte((byte)NetworkOpCode.SubmitShotIntent);
            writer.WriteShotIntent(intent);

            var reader = new NetworkByteReader(writer.Buffer, 0, writer.Position);
            var op = (NetworkOpCode)reader.ReadByte();
            Assert(op == NetworkOpCode.SubmitShotIntent, $"Opcode should be SubmitShotIntent, was {op}");
            var readIntent = reader.ReadShotIntent();
            Assert(readIntent.Sequence == 42, $"Sequence mismatch: {readIntent.Sequence}");
            Assert(Math.Abs(readIntent.ClientTimestamp - 1234567.89) < 0.01, "Timestamp mismatch");
            Assert(Math.Abs(readIntent.Direction.x - 0.5f) < 0.001f, "Direction X mismatch");
            Assert(Math.Abs(readIntent.Force - 25.5f) < 0.001f, "Force mismatch");

            // 2. Test WorldSnapshotData
            writer.Reset();
            var m0 = new CompactMarbleState(new NetVector3(1f, 0.25f, 3f), new NetVector3(0f, 0f, 0.5f), true, false);
            var m1 = new CompactMarbleState(new NetVector3(-1f, 0.25f, -2f), NetVector3.Zero, false, false);
            var p0 = new CompactPlayerData(0, "Alice", 3, 2, false, true);
            var p1 = new CompactPlayerData(1, "Bob", 4, 1, false, true);

            var snap = new WorldSnapshotData(100, 99999.0, CloudMatchPhase.Rolling, 0, 24.5f, m0, m1, p0, p1, -1);
            writer.WriteByte((byte)NetworkOpCode.WorldSnapshot);
            writer.WriteWorldSnapshot(snap);

            reader.Reset(writer.Buffer, 0, writer.Position);
            var snapOp = (NetworkOpCode)reader.ReadByte();
            Assert(snapOp == NetworkOpCode.WorldSnapshot, "Opcode should be WorldSnapshot");
            var readSnap = reader.ReadWorldSnapshot();
            Assert(readSnap.ServerTick == 100, $"Tick mismatch: {readSnap.ServerTick}");
            Assert(readSnap.Phase == CloudMatchPhase.Rolling, "Phase mismatch");
            Assert(readSnap.ActivePlayerIndex == 0, "Active player mismatch");
            Assert(readSnap.Player0.Name == "Alice", "P0 Name mismatch");
            Assert(readSnap.Player1.Name == "Bob", "P1 Name mismatch");
            Assert(readSnap.Marble0.IsMoving == true, "Marble0 IsMoving mismatch");
            Assert(readSnap.Marble1.IsMoving == false, "Marble1 IsMoving mismatch");
        }

        private static void TestAuthoritativeMatchEngine()
        {
            var room = new Room("TEST01");
            var engine = room.MatchEngine;

            Assert(engine.Phase == CloudMatchPhase.WaitingForPlayers, "Initial phase should be WaitingForPlayers");

            engine.StartMatch();
            Assert(engine.Phase == CloudMatchPhase.TossPhase, "Match should open with the toss");

            // Play the toss out so Player 0 wins it (lands closer to pit 3) and shoots first.
            for (int thrower = 0; thrower < 2; thrower++)
            {
                var toss = MakeV2Input(engine.TurnId, thrower);
                toss.OpeningToss = true;
                toss.MarbleId = thrower;
                Assert(engine.SubmitShotInput(thrower, ref toss, out int tossId, out _), $"P{thrower + 1} toss should be accepted");
                var landed = new ShotResultData
                {
                    TurnId = engine.TurnId,
                    ShotId = tossId,
                    PlayerIndex = thrower,
                    Marbles = new[] { new MarbleFinalState { MarbleId = thrower, Position = new NetVector3(0f, 0.16f, thrower == 0 ? 30f : 20f) } },
                    CurrentPitAfter = 1,
                };
                Assert(engine.SubmitShotResult(thrower, landed, out _), $"P{thrower + 1} toss result should be accepted");
            }

            Assert(engine.Phase == CloudMatchPhase.ReadyToAim, "Phase should reach ReadyToAim once both have tossed");
            Assert(engine.ActivePlayerIndex == 0, "Player 0 won the toss and should be active first");

            // v2: the engine arbitrates turns; it no longer simulates marbles.
            var badInput = MakeV2Input(engine.TurnId, 1);
            bool badAccepted = engine.SubmitShotInput(1, ref badInput, out _, out _);
            Assert(!badAccepted, "Player 1 should not be allowed to shoot on Player 0's turn");

            // Malformed input (force beyond the structural envelope) must be refused.
            var wildInput = MakeV2Input(engine.TurnId, 0);
            wildInput.Force = 100f;
            bool wildAccepted = engine.SubmitShotInput(0, ref wildInput, out _, out _);
            Assert(!wildAccepted, "Force outside the allowed envelope should be rejected");

            var goodInput = MakeV2Input(engine.TurnId, 0);
            bool accepted = engine.SubmitShotInput(0, ref goodInput, out int shotId, out _);
            Assert(accepted, "Valid shot by Player 0 should be accepted");
            Assert(shotId > 0, "Server should assign a shot id");
            Assert(engine.Phase == CloudMatchPhase.Rolling, "Phase should be Rolling while the shot is in flight");

            // No physics here: the turn only advances when the striker reports its result.
            for (int i = 0; i < 20; i++) engine.Tick(0.05f);
            Assert(engine.Phase == CloudMatchPhase.Rolling, "Phase must stay Rolling until a result arrives");

            int turnBefore = engine.TurnId;
            var result = new ShotResultData
            {
                TurnId = turnBefore,
                ShotId = shotId,
                PlayerIndex = 0,
                Marbles = new[] { new MarbleFinalState { MarbleId = 0, Position = new NetVector3(0f, 0.16f, 10f) } },
                PitConqueredNumber = 0,
                StrokesAfter = 1,
                CurrentPitAfter = 1,
            };
            Assert(engine.SubmitShotResult(0, result, out _), "Striker's result should be accepted");
            Assert(engine.Players[0].TotalStrokes == 1, "Stroke count should follow the accepted result");
            Assert(engine.Phase == CloudMatchPhase.ReadyToAim, "Phase should return to ReadyToAim after the result");
            Assert(engine.ActivePlayerIndex == 1, "Turn should pass to Player 1");
            Assert(engine.TurnId > turnBefore, "Turn id should advance");

            // A replayed packet must not advance the turn a second time.
            int turnAfter = engine.TurnId;
            Assert(!engine.SubmitShotResult(0, result, out _), "Duplicate result should be rejected");
            Assert(engine.TurnId == turnAfter, "Duplicate result must not advance the turn");
        }

        private static ShotInputData MakeV2Input(int turnId, int playerIndex) => new ShotInputData
        {
            TurnId = turnId,
            ShotId = -1,
            PlayerIndex = playerIndex,
            MarbleId = playerIndex,
            LaunchDirection = new NetVector3(0f, 0.04f, 0.9992f),
            Force = 18f,
            MaxPitch = 0.08f,
            ShotMode = 0,
            OpeningToss = false,
            ClientTimestamp = 0,
        };

        private static async Task TestEndToEndDualClientAsync()
        {
            var config = new ServerConfiguration
            {
                Port = 7788,
                Host = "127.0.0.1",
                Environment = "Test"
            };

            var roomManager = new RoomManager();
            var server = new WebSocketServer(config, roomManager);
            await server.StartAsync();

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

                // 1. Client 1 Connect
                var client1 = new ClientWebSocket();
                await client1.ConnectAsync(new Uri("ws://127.0.0.1:7788/"), cts.Token);
                Assert(client1.State == WebSocketState.Open, "Client 1 should connect");

                // Handshake Client 1
                var writer1 = new NetworkByteWriter();
                writer1.WriteByte((byte)NetworkOpCode.ConnectRequest);
                writer1.WriteInt32(NetworkProtocol.Version);
                writer1.WriteString("TestPlayer1");
                await client1.SendAsync(new ArraySegment<byte>(writer1.Buffer, 0, writer1.Position), WebSocketMessageType.Binary, true, cts.Token);

                byte[] buffer1 = new byte[1024];
                var res1 = await client1.ReceiveAsync(new ArraySegment<byte>(buffer1), cts.Token);
                var reader1 = new NetworkByteReader(buffer1, 0, res1.Count);
                Assert((NetworkOpCode)reader1.ReadByte() == NetworkOpCode.ConnectResponse, "Client 1 should receive ConnectResponse");
                Assert(reader1.ReadBool() == true, "Handshake version should be compatible");
                string sessionId1 = reader1.ReadString();
                string reconnectToken1 = reader1.ReadString();
                Assert(!string.IsNullOrEmpty(sessionId1), "Session ID should be valid");
                Assert(!string.IsNullOrEmpty(reconnectToken1), "Reconnect Token should be valid");

                // Ping / Pong test
                writer1.Reset();
                writer1.WriteByte((byte)NetworkOpCode.Ping);
                writer1.WriteUInt32(777);
                await client1.SendAsync(new ArraySegment<byte>(writer1.Buffer, 0, writer1.Position), WebSocketMessageType.Binary, true, cts.Token);

                res1 = await client1.ReceiveAsync(new ArraySegment<byte>(buffer1), cts.Token);
                reader1.Reset(buffer1, 0, res1.Count);
                Assert((NetworkOpCode)reader1.ReadByte() == NetworkOpCode.Pong, "Should receive Pong");
                Assert(reader1.ReadUInt32() == 777, "Ping ID in Pong should match");

                // Client 1 Create Room
                writer1.Reset();
                writer1.WriteByte((byte)NetworkOpCode.CreateRoomRequest);
                await client1.SendAsync(new ArraySegment<byte>(writer1.Buffer, 0, writer1.Position), WebSocketMessageType.Binary, true, cts.Token);

                res1 = await client1.ReceiveAsync(new ArraySegment<byte>(buffer1), cts.Token);
                reader1.Reset(buffer1, 0, res1.Count);
                Assert((NetworkOpCode)reader1.ReadByte() == NetworkOpCode.CreateRoomResponse, "Should receive CreateRoomResponse");
                string roomCode = reader1.ReadString();
                Assert(roomCode.Length == 6, $"Room code should be 6 chars, was {roomCode}");

                // 2. Client 2 Connect & Join Room
                var client2 = new ClientWebSocket();
                await client2.ConnectAsync(new Uri("ws://127.0.0.1:7788/"), cts.Token);
                Assert(client2.State == WebSocketState.Open, "Client 2 should connect");

                var writer2 = new NetworkByteWriter();
                writer2.WriteByte((byte)NetworkOpCode.ConnectRequest);
                writer2.WriteInt32(NetworkProtocol.Version);
                writer2.WriteString("TestPlayer2");
                await client2.SendAsync(new ArraySegment<byte>(writer2.Buffer, 0, writer2.Position), WebSocketMessageType.Binary, true, cts.Token);

                byte[] buffer2 = new byte[1024];
                var res2 = await client2.ReceiveAsync(new ArraySegment<byte>(buffer2), cts.Token);
                var reader2 = new NetworkByteReader(buffer2, 0, res2.Count);
                Assert((NetworkOpCode)reader2.ReadByte() == NetworkOpCode.ConnectResponse, "Client 2 should receive ConnectResponse");
                Assert(reader2.ReadBool() == true, "Client 2 compatible");
                string sessionId2 = reader2.ReadString();
                string reconnectToken2 = reader2.ReadString();

                // Client 2 Join Room
                writer2.Reset();
                writer2.WriteByte((byte)NetworkOpCode.JoinRoomRequest);
                writer2.WriteString(roomCode);
                await client2.SendAsync(new ArraySegment<byte>(writer2.Buffer, 0, writer2.Position), WebSocketMessageType.Binary, true, cts.Token);

                res2 = await client2.ReceiveAsync(new ArraySegment<byte>(buffer2), cts.Token);
                reader2.Reset(buffer2, 0, res2.Count);
                Assert((NetworkOpCode)reader2.ReadByte() == NetworkOpCode.JoinRoomResponse, "Should receive JoinRoomResponse");
                Assert(reader2.ReadBool() == true, "Join should succeed");

                // Read countdown / match start on Client 1
                res1 = await client1.ReceiveAsync(new ArraySegment<byte>(buffer1), cts.Token);
                reader1.Reset(buffer1, 0, res1.Count);
                var op = (NetworkOpCode)reader1.ReadByte();
                Assert(op == NetworkOpCode.PlayerJoined || op == NetworkOpCode.LobbyCountdown, "Client 1 should see player joined or countdown");

                // 3. Reconnection Test: Simulate Client 2 disconnect & reconnect with token
                try { await client2.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Simulated disconnect", CancellationToken.None); } catch { }
                client2.Dispose();
                await Task.Delay(100);

                var client2Recon = new ClientWebSocket();
                await client2Recon.ConnectAsync(new Uri("ws://127.0.0.1:7788/"), cts.Token);

                writer2.Reset();
                writer2.WriteByte((byte)NetworkOpCode.ReconnectRequest);
                writer2.WriteString(reconnectToken2);
                await client2Recon.SendAsync(new ArraySegment<byte>(writer2.Buffer, 0, writer2.Position), WebSocketMessageType.Binary, true, cts.Token);

                res2 = await client2Recon.ReceiveAsync(new ArraySegment<byte>(buffer2), cts.Token);
                reader2.Reset(buffer2, 0, res2.Count);
                Assert((NetworkOpCode)reader2.ReadByte() == NetworkOpCode.ReconnectResponse, "Should receive ReconnectResponse");
                Assert(reader2.ReadBool() == true, "Reconnect with token should succeed!");
                string reconRoom = reader2.ReadString();
                Assert(reconRoom == roomCode, $"Reconnected room code should match {roomCode}");

                // Clean disconnects
                try { await client1.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None); } catch { }
                try { await client2Recon.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None); } catch { }
                client1.Dispose();
                client2Recon.Dispose();
            }
            finally
            {
                server.Stop();
            }
        }
    }
}
