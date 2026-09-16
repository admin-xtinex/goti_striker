using UnityEngine;
using UnityEngine.UI;
using PitStriker.Gameplay;
using PitStriker.GameplayKit.UI;
using PitStriker.Visuals;

namespace PitStriker.UI
{
    /// <summary>
    /// In-game HUD presentation: player roster (top left), pit progress (top centre) and a
    /// short-lived status pill. Visual only - it reads TurnManager state and never writes it.
    ///
    /// It draws on its own overlay canvas, laid out in the 1672x941 units of the design mockup,
    /// and shows only while the legacy HUD root is active, so menus still hide it. The legacy
    /// HUD panels it replaces are hidden, not destroyed: HUDManager keeps its references.
    /// </summary>
    public class GameHudSkin : MonoBehaviour
    {
        const float RefW = 1672f, RefH = 941f;

        static readonly Color Cyan = new Color(0.32f, 0.80f, 1f, 1f);
        static readonly Color Navy = new Color(0.07f, 0.12f, 0.22f, 1f);
        static readonly Color Ice = new Color(0.78f, 0.88f, 0.98f, 1f);

        // Offline player colours as TurnManager assigns them; used when a player has none.
        static readonly Color[] Fallback =
        {
            new Color(0f, 0.85f, 1f), new Color(1f, 0.25f, 0.25f), new Color(0.2f, 1f, 0.4f), new Color(1f, 0.75f, 0.1f)
        };

        HUDManager _hud;
        Canvas _canvas;

        // roster
        RectTransform _roster;
        Image _rosterBacking;
        readonly Row[] _rows = new Row[4];

        // progress
        readonly Image[] _stepFill = new Image[3];
        readonly Image[] _stepRim = new Image[3];
        readonly Image[] _stepGlow = new Image[3];
        readonly Text[] _stepText = new Text[3];

        // status pill
        CanvasGroup _toast;
        Text _toastText;
        float _toastUntil;

        class Row
        {
            public RectTransform Root;
            public Image Dot, DotGlow, Arrow, Divider, Separator;
            public Text Name, Pit;
        }

        /// <summary>Called by HUDManager once the scene is up.</summary>
        public static void Ensure(HUDManager hud)
        {
            if (hud == null || FindAnyObjectByType<GameHudSkin>() != null) return;
            var go = new GameObject("HUD_Skin_Canvas", typeof(RectTransform));
            go.AddComponent<GameHudSkin>().Build(hud);
        }

        void Build(HUDManager hud)
        {
            _hud = hud;
            gameObject.layer = 5;

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 61;   // just above the legacy HUD (60), below menus (300)
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight = 1f;
            // No GraphicRaycaster: nothing here is interactive, so it can never eat a touch.

            var safe = HudArt.Rect("Safe", transform);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            safe.gameObject.AddComponent<SafeAreaFitter>();

            BuildRoster(safe);
            BuildProgress(safe);
            BuildToast(safe);
            HideLegacyPanels();

            TurnManager.OnStatusMessage += ShowStatus;
            SwipeControlSkin.EnsureAll();
            TargetPitBeacon.Ensure();
        }

        void OnDestroy() => TurnManager.OnStatusMessage -= ShowStatus;

        void HideLegacyPanels()
        {
            // Replaced by this skin. Made invisible rather than deactivated: they stay alive for
            // HUDManager's references, and they keep blocking exactly the touches they blocked
            // before (camera drag checks for UI under the finger), so input is unchanged.
            foreach (var name in new[] { "Power_Meter_Panel", "Stage_Tracker_Panel", "Score_Panel", "Player_Badges_Panel" })
            {
                var t = _hud.transform.Find(name);
                if (t == null) continue;
                var group = t.GetComponent<CanvasGroup>();
                if (group == null) group = t.gameObject.AddComponent<CanvasGroup>();   // Unity null: not ??
                group.alpha = 0f;
            }
        }

        // ------------------------------------------------------------------ roster

        void BuildRoster(RectTransform safe)
        {
            _roster = HudArt.Rect("Roster", safe);
            _roster.anchorMin = _roster.anchorMax = new Vector2(0f, 1f);
            _roster.pivot = new Vector2(0f, 1f);
            _roster.anchoredPosition = new Vector2(34f, -30f);
            _roster.sizeDelta = new Vector2(420f, 270f);

            _rosterBacking = HudArt.Image("Backing", _roster, HudArt.FadeRight(), new Color(0.05f, 0.10f, 0.20f, 0.34f));
            var b = _rosterBacking.rectTransform;
            b.anchorMin = Vector2.zero; b.anchorMax = Vector2.one; b.offsetMin = b.offsetMax = Vector2.zero;

            for (int i = 0; i < 4; i++)
            {
                var r = new Row();
                r.Root = HudArt.Rect("Row" + (i + 1), _roster);
                r.Root.anchorMin = r.Root.anchorMax = new Vector2(0f, 1f);
                r.Root.pivot = new Vector2(0f, 0.5f);
                r.Root.anchoredPosition = new Vector2(0f, -43f - i * 65f);
                r.Root.sizeDelta = new Vector2(420f, 64f);

                r.DotGlow = HudArt.Image("DotGlow", r.Root, HudArt.Glow(), new Color(1, 1, 1, 0.5f));
                HudArt.Place(r.DotGlow.rectTransform, new Vector2(0f, 0.5f), new Vector2(47f, 0f), new Vector2(66f, 66f));
                var rim = HudArt.Image("DotRim", r.Root, HudArt.Circle(), new Color(1f, 1f, 1f, 0.85f));
                HudArt.Place(rim.rectTransform, new Vector2(0f, 0.5f), new Vector2(47f, 0f), new Vector2(40f, 40f));
                r.Dot = HudArt.Image("Dot", r.Root, HudArt.Circle(), Fallback[i]);
                HudArt.Place(r.Dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(47f, 0f), new Vector2(34f, 34f));

                r.Name = HudArt.Text("Name", r.Root, "Player " + (i + 1), 27, Color.white, FontStyle.Bold);
                HudArt.Place(r.Name.rectTransform, new Vector2(0f, 0.5f), new Vector2(160f, 0f), new Vector2(140f, 40f));
                HudArt.Shadow(r.Name, 0.35f, 1.5f);

                r.Divider = HudArt.Image("Divider", r.Root, HudArt.White(), new Color(1f, 1f, 1f, 0.55f));
                HudArt.Place(r.Divider.rectTransform, new Vector2(0f, 0.5f), new Vector2(242f, 0f), new Vector2(2f, 30f));

                r.Pit = HudArt.Text("Pit", r.Root, "Pit 1", 27, Cyan, FontStyle.Bold);
                HudArt.Place(r.Pit.rectTransform, new Vector2(0f, 0.5f), new Vector2(308f, 0f), new Vector2(100f, 40f));
                HudArt.Shadow(r.Pit, 0.3f, 1.5f);

                r.Arrow = HudArt.Image("TurnArrow", r.Root, HudArt.TriangleRight(), Color.white);
                HudArt.Place(r.Arrow.rectTransform, new Vector2(0f, 0.5f), new Vector2(378f, 0f), new Vector2(30f, 30f));

                if (i < 3)
                {
                    r.Separator = HudArt.Image("Separator", r.Root, HudArt.FadeRight(), new Color(1f, 1f, 1f, 0.12f));
                    HudArt.Place(r.Separator.rectTransform, new Vector2(0f, 0.5f), new Vector2(210f, -32.5f), new Vector2(360f, 2f));
                }
                _rows[i] = r;
            }
        }

        // ------------------------------------------------------------------ progress

        void BuildProgress(RectTransform safe)
        {
            var root = HudArt.Rect("PitProgress", safe);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, -78f);
            root.sizeDelta = new Vector2(420f, 90f);

            for (int i = 0; i < 2; i++)
            {
                float x = -66.5f + i * 133f;
                var edge = HudArt.Image("LinkEdge" + i, root, HudArt.White(), new Color(0.02f, 0.05f, 0.12f, 0.75f));
                HudArt.Place(edge.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(80f, 9f));
                var link = HudArt.Image("Link" + i, root, HudArt.White(), new Color(0.35f, 0.62f, 0.90f, 0.95f));
                HudArt.Place(link.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(80f, 4f));
            }

            for (int i = 0; i < 3; i++)
            {
                float x = -133f + i * 133f;
                _stepGlow[i] = HudArt.Image("Glow" + (i + 1), root, HudArt.Glow(), new Color(0.2f, 0.75f, 1f, 0.85f));
                HudArt.Place(_stepGlow[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(118f, 118f));
                _stepRim[i] = HudArt.Image("Rim" + (i + 1), root, HudArt.Circle(), Ice);
                HudArt.Place(_stepRim[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(66f, 66f));
                _stepFill[i] = HudArt.Image("Fill" + (i + 1), root, HudArt.Circle(), Navy);
                HudArt.Place(_stepFill[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(58f, 58f));
                _stepText[i] = HudArt.Text("Num" + (i + 1), root, (i + 1).ToString(), 32, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
                HudArt.Place(_stepText[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 1f), new Vector2(60f, 60f));
                HudArt.Shadow(_stepText[i], 0.35f, 1.5f);
            }
        }

        // ------------------------------------------------------------------ status

        void BuildToast(RectTransform safe)
        {
            var root = HudArt.Rect("StatusPill", safe);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, -150f);
            root.sizeDelta = new Vector2(620f, 44f);
            _toast = root.gameObject.AddComponent<CanvasGroup>();
            _toast.alpha = 0f;
            _toast.blocksRaycasts = false;
            _toast.interactable = false;

            var bg = HudArt.Image("Backing", root, HudArt.RoundedRect(22), new Color(0.04f, 0.08f, 0.16f, 0.62f), sliced: true);
            bg.rectTransform.anchorMin = Vector2.zero; bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;
            _toastText = HudArt.Text("Text", root, "", 21, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            _toastText.rectTransform.anchorMin = Vector2.zero; _toastText.rectTransform.anchorMax = Vector2.one;
            _toastText.rectTransform.offsetMin = new Vector2(20f, 0f); _toastText.rectTransform.offsetMax = new Vector2(-20f, 0f);
        }

        void ShowStatus(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || _toastText == null) return;
            _toastText.text = message;
            float width = Mathf.Clamp(_toastText.preferredWidth + 56f, 220f, 1100f);
            ((RectTransform)_toast.transform).sizeDelta = new Vector2(width, 44f);
            _toastUntil = Time.unscaledTime + 3.2f;
        }

        // ------------------------------------------------------------------ refresh

        void LateUpdate()
        {
            bool visible = _hud != null && _hud.gameObject.activeInHierarchy;
            if (_canvas.enabled != visible) _canvas.enabled = visible;
            if (!visible) return;

            var tm = TurnManager.Instance;
            RefreshRoster(tm);
            RefreshProgress(tm);

            float target = Time.unscaledTime < _toastUntil ? 1f : 0f;
            _toast.alpha = Mathf.MoveTowards(_toast.alpha, target, Time.unscaledDeltaTime * 4f);
        }

        void RefreshRoster(TurnManager tm)
        {
            int count = tm != null ? Mathf.Clamp(tm.PlayerCount, 0, 4) : 0;
            _roster.sizeDelta = new Vector2(420f, 21f + Mathf.Max(1, count) * 65f);
            var active = tm != null ? tm.ActivePlayer : null;

            for (int i = 0; i < 4; i++)
            {
                var r = _rows[i];
                bool show = i < count;
                if (r.Root.gameObject.activeSelf != show) r.Root.gameObject.SetActive(show);
                if (!show) continue;

                var p = tm.Players[i];
                Color c = p.color.a > 0f ? p.color : Fallback[i];
                c.a = 1f;
                r.Dot.color = c;
                r.DotGlow.color = new Color(c.r, c.g, c.b, 0.45f);
                string label = string.IsNullOrEmpty(p.name) ? "Player " + (i + 1) : p.name;
                if (r.Name.text != label) r.Name.text = label;
                string pit = "Pit " + Mathf.Clamp(p.currentPit, 1, 3);
                if (r.Pit.text != pit) r.Pit.text = pit;

                bool isActive = p == active;
                r.Arrow.enabled = isActive;
                if (isActive)
                {
                    float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
                    r.Arrow.color = new Color(1f, 1f, 1f, pulse);
                    r.Arrow.rectTransform.anchoredPosition = new Vector2(378f + 3f * Mathf.Sin(Time.unscaledTime * 4f), 0f);
                }
                if (r.Separator != null) r.Separator.enabled = i < count - 1;
            }
        }

        void RefreshProgress(TurnManager tm)
        {
            var view = tm != null ? (tm.LocalViewPlayer ?? tm.ActivePlayer) : null;
            bool toss = tm != null && tm.CurrentState == TurnManager.GameState.TossPhase;
            int current = toss ? 3 : (view != null ? Mathf.Clamp(view.currentPit, 1, 3) : 1);
            bool finished = view != null && view.isFinished;
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f);

            for (int i = 0; i < 3; i++)
            {
                int pit = i + 1;
                bool done = !toss && (pit < current || (finished && pit <= 3));
                bool now = !finished && pit == current;

                _stepGlow[i].enabled = now;
                if (now) _stepGlow[i].color = new Color(0.2f, 0.75f, 1f, 0.8f * pulse);
                _stepRim[i].color = now ? new Color(0.55f, 0.92f, 1f, 1f) : (done ? new Color(0.45f, 0.70f, 0.95f, 1f) : new Color(0.70f, 0.78f, 0.88f, 0.9f));
                _stepFill[i].color = now ? new Color(0.10f, 0.42f, 0.86f, 1f) : (done ? new Color(0.13f, 0.33f, 0.62f, 1f) : Navy);
                _stepText[i].color = done || now ? Color.white : new Color(0.85f, 0.90f, 0.97f, 1f);
            }
        }
    }
}
