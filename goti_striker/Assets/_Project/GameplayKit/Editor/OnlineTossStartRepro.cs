#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using PitStriker.Gameplay;
using PitStriker.Input;
using PitStriker.Networking.Client;
using PitStriker.Networking.Core;
using PitStriker.Networking.Shared;
using PitStriker.GameplayKit.UI;
using PitStriker.CameraSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace PitStriker.GameplayKit.EditorTools
{
    /// <summary>
    /// Reproduces the start of an online match on one client, in Play mode, with no network.
    ///
    /// Written for a live bug: a player chosen to throw FIRST in the toss as P2 never threw
    /// (the server logged "Toss: P2 no throw (timed out)"), while P1-first tosses worked. The
    /// client is driven through its real receive path with the same message sequence a quick
    /// match produces (MatchFound, countdown, MatchStarted, AcceptedState), given real time to
    /// settle, then a finger swipe is injected through the Input System on the shot panel and
    /// the packets that leave the client are recorded.
    /// </summary>
    public static class OnlineTossStartRepro
    {
        const string StageKey = "PitStriker.OnlineTossStartRepro.stage";
        const string Result = "Library/OnlineTossStartRepro.result";
        const string Scene = "Assets/_Project/Scenes/SC_Village_Graphics_Test.unity";

        static int _ticks;
        static readonly StringBuilder Report = new StringBuilder();
        static StubTransport _stub;
        static IEnumerator<float> _script;
        static double _resumeAt;

        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            SessionState.SetString(StageKey, "playing");
            File.WriteAllText(Result, "STARTED\n");
            Subscribe();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            if (SessionState.GetString(StageKey, "") == "playing") Subscribe();
        }

        static void Subscribe()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (++_ticks > 200000) { Finish("FAIL watchdog\n" + Report); return; }
            if (!EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < _resumeAt) return;

            try
            {
                if (_script == null) _script = Script().GetEnumerator();
                // Each yield is a wait in real seconds (0 = next editor update).
                if (_script.MoveNext()) _resumeAt = EditorApplication.timeSinceStartup + _script.Current;
                else Finish("DONE\n\n" + Report);
            }
            catch (Exception ex) { Finish("FAIL exception\n" + ex + "\n\n" + Report); }
        }

        static IEnumerable<float> Script()
        {
            yield return 3f;   // let the scene boot and the menu settle
            Setup();
            yield return 0.5f;  // CloudMatchManager subscribes in Start

            foreach (var w in Scenario("REPRO1", local: 1, firstThrower: 1, "P2 is local and throws FIRST (live failure)")) yield return w;
            foreach (var w in Scenario("REPRO2", local: 0, firstThrower: 0, "P1 is local and throws FIRST (live success)")) yield return w;
            foreach (var w in Scenario("REPRO3", local: 0, firstThrower: 1, "P1 is local, WATCHING P2 throw first")) yield return w;
            foreach (var w in Scenario("REPRO4", local: 1, firstThrower: 0, "P2 is local, WATCHING P1 throw first")) yield return w;
            foreach (var w in CaptureScenarios()) yield return w;
        }

        static IEnumerable<float> Scenario(string room, int local, int firstThrower, string title)
        {
            Report.AppendLine($"=== {title} ===");
            var client = CloudNetworkClient.Instance;
            _stub.Sent.Clear();
            Messages.Clear();

            // Quick match: the server matches, counts down 3..1 a second apart, then starts.
            Deliver(client, w =>
            {
                w.WriteByte((byte)NetworkOpCode.MatchFound);
                w.WriteString(room); w.WriteString("Player"); w.WriteString("Player"); w.WriteInt32(local);
            });
            for (int c = 3; c >= 1; c--)
            {
                int n = c;
                Deliver(client, w => { w.WriteByte((byte)NetworkOpCode.LobbyCountdown); w.WriteInt32(n); });
                yield return 1f;
            }
            Deliver(client, w =>
            {
                w.WriteByte((byte)NetworkOpCode.MatchStarted);
                w.WriteString(room); w.WriteString("Player"); w.WriteString("Player"); w.WriteInt32(local);
            });
            Deliver(client, w =>
            {
                w.WriteByte((byte)NetworkOpCode.AcceptedState);
                w.WriteAcceptedState(new AcceptedStateData
                {
                    TurnId = 1,
                    LastAppliedShotId = -1,
                    ActivePlayerIndex = firstThrower,
                    Phase = CloudMatchPhase.TossPhase,
                    TurnTimerRemaining = NetworkProtocol.DefaultTurnDuration,
                    Marbles = new[]
                    {
                        new MarbleFinalState { MarbleId = 0, Position = new NetVector3(-0.4f, 0.25f, -6f) },
                        new MarbleFinalState { MarbleId = 1, Position = new NetVector3(0.4f, 0.25f, -6f) },
                    },
                    Player0 = new CompactPlayerData(0, "Player"),
                    Player1 = new CompactPlayerData(1, "Player"),
                    WinnerPlayerIndex = -1,
                });
            });

            yield return 0.3f;
            Snapshot("after 0.3s", firstThrower);
            yield return 3f;
            Snapshot("after 3.3s", firstThrower);

            // A real finger: press inside the shot panel, flick up, lift.
            var binder = UnityEngine.Object.FindAnyObjectByType<ShotControlBinder>();
            var touch = InputSystem.GetDevice<Touchscreen>() ?? InputSystem.AddDevice<Touchscreen>();
            Vector2 start = binder != null && binder.PowerArea != null
                ? RectTransformUtility.WorldToScreenPoint(
                    typeof(ShotControlBinder).GetProperty("EventCamera", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(binder) as Camera,
                    binder.PowerArea.position)
                : new Vector2(Screen.width * 0.8f, Screen.height * 0.4f);
            Report.AppendLine($"  swipe start {start} in power area = {binder?.IsScreenPosInPowerArea(start)}  screen {Screen.width}x{Screen.height}");
            _stub.Sent.Clear();

            InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = TouchPhase.Began, position = start });
            yield return 0.05f;
            for (int i = 1; i <= 4; i++)
            {
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = TouchPhase.Moved, position = start + new Vector2(0, 60f * i) });
                yield return 0.02f;
            }
            Report.AppendLine($"  mid-swipe: dragging={Field(typeof(SwipeLaunchController), "_isDragging").GetValue(SwipeLaunchController.Instance)} power={Field(typeof(SwipeLaunchController), "_currentPower").GetValue(SwipeLaunchController.Instance)}");
            InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = TouchPhase.Ended, position = start + new Vector2(0, 240f) });
            yield return 0.3f;

            var ops = new List<string>();
            foreach (var b in _stub.Sent) ops.Add(((NetworkOpCode)b[0]).ToString());
            Report.AppendLine($"  packets sent by swipe        = [{string.Join(", ", ops)}]");
            Report.AppendLine();

            // Let the stub-owned "shot" go nowhere and clear the match for the next scenario.
            yield return 1f;
        }

        // ------------------------------------------------------------------ pit capture rules

        static IEnumerable<float> CaptureScenarios()
        {
            Report.AppendLine("=== Online pit capture (P1 local, target pit 1) ===");
            var client = CloudNetworkClient.Instance;
            var cm = CloudMatchManager.Instance;
            Deliver(client, w => { w.WriteByte((byte)NetworkOpCode.MatchFound); w.WriteString("REPRO5"); w.WriteString("Player"); w.WriteString("Player"); w.WriteInt32(0); });
            Deliver(client, w => { w.WriteByte((byte)NetworkOpCode.MatchStarted); w.WriteString("REPRO5"); w.WriteString("Player"); w.WriteString("Player"); w.WriteInt32(0); });
            Deliver(client, w =>
            {
                w.WriteByte((byte)NetworkOpCode.AcceptedState);
                w.WriteAcceptedState(new AcceptedStateData
                {
                    TurnId = 3, LastAppliedShotId = 1, ActivePlayerIndex = 0, Phase = CloudMatchPhase.ReadyToAim,
                    TurnTimerRemaining = NetworkProtocol.DefaultTurnDuration,
                    Marbles = new[]
                    {
                        new MarbleFinalState { MarbleId = 0, Position = new NetVector3(-0.4f, 0.25f, -6f) },
                        new MarbleFinalState { MarbleId = 1, Position = new NetVector3(0.4f, 0.25f, -6f) },
                    },
                    Player0 = new CompactPlayerData(0, "Player", 2, 1), Player1 = new CompactPlayerData(1, "Player", 2, 1),
                    WinnerPlayerIndex = -1,
                });
            });
            yield return 0.5f;

            PitZone Pit(int n) { foreach (var pz in UnityEngine.Object.FindObjectsByType<PitZone>(FindObjectsSortMode.None)) if (pz.PitNumber == n) return pz; return null; }
            var me = cm.GetMarble(0);
            var opp = cm.GetMarble(1);
            Vector3 Basin(int n) => Pit(n).transform.position + new Vector3(0f, 0.17f, 0f);
            Vector3 Lane(float x) => new Vector3(x, 0.25f, -3f);

            foreach (var c in new[]
            {
                (name: "A local in TARGET pit 1", mePos: Basin(1), oppPos: Lane(1.5f), expectPit: 1),
                (name: "B local in WRONG pit 2",  mePos: Basin(2), oppPos: Lane(1.5f), expectPit: 0),
                (name: "C opponent in pit 1",     mePos: Lane(-1.5f), oppPos: Basin(1), expectPit: 0),
            })
            {
                me.ResetPosition(c.mePos); opp.ResetPosition(c.oppPos);
                yield return 1.0f;   // settle into the basin
                bool meIn = false, oppIn = false;
                foreach (var pz in UnityEngine.Object.FindObjectsByType<PitZone>(FindObjectsSortMode.None)) { meIn |= pz.IsMarbleCaptured(me); oppIn |= pz.IsMarbleCaptured(opp); }

                Field(typeof(CloudMatchManager), "_localPendingShotId").SetValue(cm, 10);
                Field(typeof(CloudMatchManager), "_localPendingInput").SetValue(cm, new ShotInputData { TurnId = 3, ShotId = 10, OpeningToss = false });
                _stub.Sent.Clear();
                typeof(CloudMatchManager).GetMethod("SendLocalShotResult", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(cm, new object[] { false });

                string verdict = "no ShotResult sent";
                foreach (var b in _stub.Sent)
                {
                    if ((NetworkOpCode)b[0] != NetworkOpCode.ShotResult) continue;
                    var r = new NetworkByteReader(b, 1, b.Length - 1).ReadShotResult();
                    var m0 = r.Marbles[0].Position; var m1 = r.Marbles[1].Position;
                    verdict = $"pit={r.PitConqueredNumber} (expect {c.expectPit}) pitAfter={r.CurrentPitAfter} me=({m0.x:F2},{m0.y:F2},{m0.z:F2}) opp=({m1.x:F2},{m1.y:F2},{m1.z:F2})"
                            + (r.PitConqueredNumber == c.expectPit ? "  OK" : "  WRONG");
                }
                bool stillIn = false;
                foreach (var pz in UnityEngine.Object.FindObjectsByType<PitZone>(FindObjectsSortMode.None)) stillIn |= pz.IsMarbleInsidePit(me) || pz.IsMarbleInsidePit(opp);
                Report.AppendLine($"  {c.name}: captured before me={meIn} opp={oppIn} -> {verdict}; a marble left in a pit={stillIn}");
                yield return 0.3f;
            }
            Report.AppendLine($"  pit1={Pit(1).transform.position} pit2={Pit(2).transform.position}");
        }

        static readonly List<string> Messages = new List<string>();

        static void Snapshot(string when, int expectedThrower)
        {
            Report.AppendLine($"  status messages so far: {string.Join(" | ", Messages)}");
            var texts = new List<string>();
            foreach (var t in UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Exclude))
                if (t.isActiveAndEnabled && !string.IsNullOrWhiteSpace(t.text)) texts.Add($"{t.name}='{t.text.Replace('\n', ' ')}'");
            foreach (var t in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Exclude))
                if (t.isActiveAndEnabled && !string.IsNullOrWhiteSpace(t.text)) texts.Add($"{t.name}='{t.text.Replace('\n', ' ')}'");
            Report.AppendLine($"  visible text: {string.Join("; ", texts)}");
            var cm = CloudMatchManager.Instance;
            var tm = TurnManager.Instance;
            var swipe = SwipeLaunchController.Instance;
            var client = CloudNetworkClient.Instance;
            var marble = swipe != null ? Field(typeof(SwipeLaunchController), "_marble").GetValue(swipe) as Component : null;

            Report.AppendLine($"  -- {when}");
            Report.AppendLine($"  client.LocalPlayerIndex={client.LocalPlayerIndex} online={cm.IsOnlineMatchActive} phase={cm.CurrentPhase} "
                            + $"active={cm.ActivePlayerIndex} (server {expectedThrower}) reconciled={Field(typeof(CloudMatchManager), "_reconciled").GetValue(cm)} IsMyTurn={cm.IsMyTurn()}");
            Report.AppendLine($"  tm.state={tm.CurrentState} tm.current={tm.CurrentPlayerIndex} CanAim={TurnManager.CanAim()} aimMode={swipe?.CurrentAimMode} "
                            + $"bound={(marble != null ? marble.name : "<null>")} local={cm.GetMarble(client.LocalPlayerIndex)?.name} "
                            + $"menu={PitStriker.UI.MenuManager.Instance?.CurrentScreen}");
            var cam = UnityEngine.Object.FindAnyObjectByType<SmoothFollowCamera>();
            var camTarget = cam != null ? typeof(SmoothFollowCamera).GetField("_target", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(cam) as Transform : null;
            Report.AppendLine($"  camera target={(camTarget != null ? camTarget.name : "<unknown>")}");
        }

        static void Finish(string text)
        {
            EditorApplication.update -= Tick;
            SessionState.SetString(StageKey, "done");
            File.WriteAllText(Result, text);
            Debug.Log("[TOSSREPRO]\n" + text);
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }

        // ------------------------------------------------------------------ driving the client

        static void Setup()
        {
            // The menu creates the cloud client lazily when the player picks an online mode.
            var menu = UnityEngine.Object.FindAnyObjectByType<PitStriker.UI.MenuManager>();
            if (menu == null) throw new Exception("no MenuManager in scene");
            typeof(PitStriker.UI.MenuManager).GetMethod("EnsureCloudClient", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(menu, null);
            menu.ShowScreen(PitStriker.UI.MenuManager.ScreenType.QuickMatch);
            var client = CloudNetworkClient.Instance;
            if (client == null) throw new Exception("EnsureCloudClient did not create CloudNetworkClient");
            // Batch mode has no focused Game view; without these the injected touches are dropped.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            _stub = new StubTransport();
            Field(typeof(CloudNetworkClient), "_transport").SetValue(client, _stub);
            TurnManager.OnStatusMessage += m => Messages.Add(m);
            Report.AppendLine($"stub transport installed; client.IsConnected={client.IsConnected}");
        }

        static void Deliver(CloudNetworkClient client, Action<NetworkByteWriter> build)
        {
            var w = new NetworkByteWriter(1024);
            build(w);
            var copy = new byte[w.Position];
            Buffer.BlockCopy(w.Buffer, 0, copy, 0, w.Position);
            typeof(CloudNetworkClient)
                .GetMethod("HandleTransportDataReceived", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(client, new object[] { copy, copy.Length });
        }

        // ------------------------------------------------------------------ reflection helpers

        static FieldInfo Field(Type t, string name) =>
            t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            ?? throw new Exception($"field {t.Name}.{name} not found");

        /// <summary>A transport that is always "connected" and records what the client sends.</summary>
        sealed class StubTransport : INetworkTransport
        {
            public readonly List<byte[]> Sent = new List<byte[]>();
            public event Action OnConnected { add { } remove { } }
            public event Action<string> OnDisconnected { add { } remove { } }
            public event Action<byte[], int> OnDataReceived { add { } remove { } }
            public event Action<string> OnError { add { } remove { } }
            public NetworkConnectionState State => NetworkConnectionState.Connected;
            public float RttMilliseconds => 40f;
            public void Connect(string address) { }
            public void Disconnect() { }
            public void Send(byte[] buffer, int length, NetworkDelivery delivery = NetworkDelivery.Reliable)
            {
                var copy = new byte[length];
                Buffer.BlockCopy(buffer, 0, copy, 0, length);
                Sent.Add(copy);
            }
            public void Tick() { }
        }
    }
}
#endif
