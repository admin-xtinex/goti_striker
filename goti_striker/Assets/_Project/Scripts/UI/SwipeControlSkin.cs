using UnityEngine;
using UnityEngine.UI;
using PitStriker.Gameplay;
using PitStriker.Input;
using PitStriker.GameplayKit.UI;

namespace PitStriker.UI
{
    /// <summary>
    /// Swipe-to-shoot presentation built from the supplied artwork (Resources/UI/SwipeControl):
    /// up chevrons, a glowing dial that follows the finger, down chevrons and a tapping hand hint,
    /// with text captions in the HUD's font ("Swipe up for lofted shot" / "Drag down, aim and
    /// release for grounded shot"); during the toss "Swipe up to toss" replaces them.
    ///
    /// Visual only. Everything sits inside the existing PowerArea rect, whose position and size
    /// decide where a shot gesture may start - those are not touched. The dial rides on the
    /// binder's Thumb RectTransform, which ShotControlBinder already moves with the swipe; its
    /// rect size is unchanged. No graphic here is a raycast target.
    /// </summary>
    public class SwipeControlSkin : MonoBehaviour
    {
        const string ArtPath = "UI/SwipeControl/";
        const string NewLine = "\n";
        static readonly Color Idle = new Color(1f, 1f, 1f, 0.55f);

        ShotControlBinder _binder;
        Vector2 _thumbHome;
        Image _up, _down, _dial;
        Text _upCaption, _downCaption, _tossLabel;
        CanvasGroup _handGroup;
        RectTransform _hand;
        float _power;

        public static void EnsureAll()
        {
            foreach (var b in FindObjectsByType<ShotControlBinder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (b.GetComponent<SwipeControlSkin>() == null) b.gameObject.AddComponent<SwipeControlSkin>();
        }

        void Start()
        {
            _binder = GetComponent<ShotControlBinder>();
            if (_binder == null || _binder.PowerArea == null) { enabled = false; return; }
            Build(_binder.PowerArea);
        }

        void OnEnable() => SwipeLaunchController.OnPowerChanged += HandlePower;
        void OnDisable() => SwipeLaunchController.OnPowerChanged -= HandlePower;
        void HandlePower(float p) => _power = Mathf.Clamp01(p);

        static Sprite Art(string name)
        {
            var sprite = Resources.Load<Sprite>(ArtPath + name);
            if (sprite != null) return sprite;
            // Imported as a plain texture on some setups: wrap it.
            var tex = Resources.Load<Texture2D>(ArtPath + name);
            return tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)) : null;
        }

        void Build(RectTransform area)
        {
            // Retire the old artwork inside the power area by making it transparent. Not disabled:
            // a graphic that is a raycast target keeps catching the same touches, so what the
            // camera-drag input sees under a finger does not change.
            foreach (var g in area.GetComponentsInChildren<Graphic>(true))
            {
                var c = g.color; c.a = 0f; g.color = c;
            }
            if (_binder.FingerTutorial != null)
            {
                var group = _binder.FingerTutorial.GetComponent<CanvasGroup>();
                if (group == null) group = _binder.FingerTutorial.AddComponent<CanvasGroup>();
                group.alpha = 0f;
            }

            var root = HudArt.Rect("SwipeSkin", area);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = area.rect.size;
            root.SetAsFirstSibling();   // behind the dial

            // Vertical stack centred on the 356-unit-tall area: caption, chevrons, dial, chevrons,
            // caption. Captions are wider than the area; they are not raycast targets.
            _upCaption = Label(root, "CaptionUp", "Swipe up for" + NewLine + "lofted shot", 192f);
            _tossLabel = Label(root, "TossLabel", "Swipe up" + NewLine + "to toss", 192f);
            _up = HudArt.Image("ChevronsUp", root, Art("ChevronsUp"), Idle);
            HudArt.Place(_up.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(78f, 75f));
            _up.preserveAspect = true;

            _down = HudArt.Image("ChevronsDown", root, Art("ChevronsDown"), Idle);
            HudArt.Place(_down.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(78f, 66f));
            _down.preserveAspect = true;
            _downCaption = Label(root, "CaptionDown", "Drag down, aim and" + NewLine + "release for grounded shot", -192f);

            // Dial = the thumb artwork. The Thumb rect itself (size, clamping) stays the binder's.
            if (_binder.Thumb != null)
            {
                _thumbHome = _binder.Thumb.anchoredPosition;
                _dial = HudArt.Image("Dial", _binder.Thumb, Art("Dial"), Color.white);
                HudArt.Place(_dial.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(132f, 132f));
                _binder.Thumb.SetAsLastSibling();
            }

            // Tapping hand, fingertip on the dial centre. Pivot = the tap point in HandTap.png.
            // Parented to the area (same centre as the skin) so it can draw above the dial.
            _hand = HudArt.Rect("Hand", area);
            _hand.anchorMin = _hand.anchorMax = new Vector2(0.5f, 0.5f);
            _hand.pivot = new Vector2(0.36f, 0.714f);
            _hand.anchoredPosition = _thumbHome;
            _hand.sizeDelta = new Vector2(92f, 110f);
            _handGroup = _hand.gameObject.AddComponent<CanvasGroup>();
            _handGroup.blocksRaycasts = false; _handGroup.interactable = false;
            var handArt = HudArt.Image("Art", _hand, Art("HandTap"), Color.white);
            handArt.rectTransform.anchorMin = Vector2.zero; handArt.rectTransform.anchorMax = Vector2.one;
            handArt.rectTransform.offsetMin = handArt.rectTransform.offsetMax = Vector2.zero;
            handArt.preserveAspect = true;
            _hand.SetAsLastSibling();
        }

        static Text Label(RectTransform root, string name, string word, float y)
        {
            var t = HudArt.Text(name, root, word, 22, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            t.lineSpacing = 0.95f;
            HudArt.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(280f, 56f));
            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.25f, 0.45f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            HudArt.Shadow(t, 0.5f, 2f);
            return t;
        }

        void Update()
        {
            if (_up == null) return;
            float t = Time.unscaledTime;

            var tm = TurnManager.Instance;
            bool toss = tm != null && tm.CurrentState == TurnManager.GameState.TossPhase;

            // Toss: one forward throw, so its own label. Lofts work online too, so no online case.
            SetActive(_tossLabel, toss);
            SetActive(_upCaption, !toss);
            SetActive(_down, !toss);
            SetActive(_downCaption, !toss);

            // Which way the finger is going, from the thumb the binder moves.
            Vector2 offset = _binder.Thumb != null ? _binder.Thumb.anchoredPosition - _thumbHome : Vector2.zero;
            bool dragging = offset.sqrMagnitude > 4f;
            float upBias = dragging ? Mathf.Clamp01(offset.y / 40f) : 0f;
            float downBias = dragging ? Mathf.Clamp01(-offset.y / 40f) : 0f;

            // Idle: the chevrons breathe in turn. Swiping: the side you are heading lights up.
            float idleUp = 0.55f + 0.25f * Mathf.Sin(t * 3f);
            float idleDown = 0.55f + 0.25f * Mathf.Sin(t * 3f + Mathf.PI);
            Fade(_up, dragging ? Mathf.Lerp(0.35f, 1f, upBias) : idleUp);
            Fade(_down, dragging ? Mathf.Lerp(0.35f, 1f, downBias) : idleDown);
            Fade(_upCaption, dragging ? Mathf.Lerp(0.45f, 1f, upBias) : 1f);
            Fade(_downCaption, dragging ? Mathf.Lerp(0.45f, 1f, downBias) : 1f);
            Fade(_tossLabel, dragging ? Mathf.Lerp(0.45f, 1f, upBias) : 1f);

            if (_dial != null)
            {
                // A little bigger and brighter as power builds.
                float s = 1f + 0.12f * _power + (dragging ? 0f : 0.03f * Mathf.Sin(t * 2.4f));
                _dial.rectTransform.localScale = new Vector3(s, s, 1f);
            }

            // The hand is a hint: it taps while idle and gets out of the way during a swipe.
            _handGroup.alpha = Mathf.MoveTowards(_handGroup.alpha, dragging ? 0f : 1f, Time.unscaledDeltaTime * 6f);
            float tap = Mathf.Abs(Mathf.Sin(t * 2.2f));
            _hand.anchoredPosition = _thumbHome + new Vector2(4f, -4f) * tap;
            _hand.localScale = Vector3.one * (1f - 0.05f * tap);
        }

        static void Fade(Graphic g, float alpha)
        {
            var c = g.color; c.a = alpha; g.color = c;
        }

        static void SetActive(Component c, bool on)
        {
            if (c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
        }
    }
}
