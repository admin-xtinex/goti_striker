using UnityEngine;
using UnityEngine.UI;
using PitStriker.Input;
using PitStriker.GameplayKit.UI;

namespace PitStriker.UI
{
    /// <summary>
    /// Swipe-to-shoot presentation: up chevrons, an arrow shaft that fills with shot power, a
    /// glowing thumb, a pointing hand and a "SWIPE / TO SHOOT" caption.
    ///
    /// Visual only. Everything is drawn inside the existing PowerArea rect, whose position and
    /// size decide where a shot gesture may start - those are not touched. The thumb keeps its
    /// RectTransform (ShotControlBinder moves it); only its artwork and size change. No graphic
    /// here is a raycast target.
    /// </summary>
    public class SwipeControlSkin : MonoBehaviour
    {
        static readonly Color Cyan = new Color(0.45f, 0.86f, 1f, 1f);

        ShotControlBinder _binder;
        Image _shaftFill, _hand;
        Image[] _chevrons;
        CanvasGroup _handGroup;
        float _power;
        Vector2 _thumbHome;
        Text _hintUp, _hintDown;

        static Text Hint(RectTransform root, string name, string word, bool up, float y)
        {
            var row = HudArt.Rect(name, root);
            HudArt.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(150f, 32f));
            var icon = HudArt.Image("Arrow", row, HudArt.ArrowHead(), new Color(0.55f, 0.90f, 1f, 1f));
            HudArt.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-46f, 0f), new Vector2(20f, 18f));
            if (!up) icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            HudArt.Shadow(icon, 0.5f, 1.5f);
            var text = HudArt.Text("Word", row, word, 28, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            HudArt.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(26f, 0f), new Vector2(96f, 32f));
            HudArt.Shadow(text, 0.55f, 1.5f);
            return text;
        }

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
            root.SetAsFirstSibling();   // behind the thumb

            // Chevrons, stacked, pointing up.
            _chevrons = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                _chevrons[i] = HudArt.Image("Chevron" + i, root, HudArt.ChevronUp(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f));
                HudArt.Place(_chevrons[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 152f - i * 22f), new Vector2(44f, 26f));
            }

            // Shaft from the thumb up to an arrow head; a brighter copy fills upward with power.
            var glow = HudArt.Image("ShaftGlow", root, HudArt.White(), new Color(Cyan.r, Cyan.g, Cyan.b, 0.25f));
            HudArt.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 58f), new Vector2(11f, 104f));
            var shaft = HudArt.Image("Shaft", root, HudArt.White(), new Color(0.85f, 0.95f, 1f, 0.9f));
            HudArt.Place(shaft.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 58f), new Vector2(4f, 104f));
            _shaftFill = HudArt.Image("ShaftPower", root, HudArt.White(), new Color(0.35f, 0.95f, 1f, 1f));
            HudArt.Place(_shaftFill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 58f), new Vector2(7f, 104f));
            _shaftFill.type = Image.Type.Filled;
            _shaftFill.fillMethod = Image.FillMethod.Vertical;
            _shaftFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            _shaftFill.fillAmount = 0f;
            var head = HudArt.Image("ArrowHead", root, HudArt.ArrowHead(), new Color(0.85f, 0.95f, 1f, 0.95f));
            HudArt.Place(head.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 114f), new Vector2(22f, 20f));

            // Hand under the thumb, fingertip on the thumb's centre.
            var handRoot = HudArt.Rect("Hand", root);
            HudArt.Place(handRoot, new Vector2(0.5f, 0.5f), new Vector2(14f, -52f), new Vector2(86f, 108f));
            _handGroup = handRoot.gameObject.AddComponent<CanvasGroup>();
            _handGroup.blocksRaycasts = false; _handGroup.interactable = false;
            _hand = HudArt.Image("Art", handRoot, HudArt.Hand(), Color.white);
            _hand.rectTransform.anchorMin = Vector2.zero; _hand.rectTransform.anchorMax = Vector2.one;
            _hand.rectTransform.offsetMin = _hand.rectTransform.offsetMax = Vector2.zero;
            HudArt.Shadow(_hand, 0.35f, 2f);

            // Short hints: flick up = lob, pull down = roll. Only the words live here; which swipe
            // makes which shot is decided by the shot controller.
            _hintUp = Hint(root, "HintUp", "LOB", up: true, y: -130f);
            _hintDown = Hint(root, "HintDown", "ROLL", up: false, y: -162f);

            // New thumb artwork: a glowing orb. The rect is the binder's; it keeps moving it.
            if (_binder.Thumb != null)
            {
                _binder.Thumb.sizeDelta = new Vector2(56f, 56f);
                _thumbHome = _binder.Thumb.anchoredPosition;
                var halo = HudArt.Image("ThumbGlow", _binder.Thumb, HudArt.Glow(), new Color(0.35f, 0.85f, 1f, 0.65f));
                HudArt.Place(halo.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 120f));
                halo.transform.SetAsFirstSibling();
                var orb = HudArt.Image("ThumbOrb", _binder.Thumb, HudArt.Orb(), new Color(0.80f, 0.95f, 1f, 1f));
                HudArt.Place(orb.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 92f));
                _binder.Thumb.SetAsLastSibling();
            }
        }

        void Update()
        {
            if (_shaftFill == null) return;
            _shaftFill.fillAmount = Mathf.MoveTowards(_shaftFill.fillAmount, _power, Time.unscaledDeltaTime * 6f);

            float t = Time.unscaledTime;
            for (int i = 0; i < _chevrons.Length; i++)
            {
                float wave = 0.55f + 0.45f * Mathf.Sin(t * 3.2f - i * 1.1f);
                _chevrons[i].color = new Color(Cyan.r, Cyan.g, Cyan.b, wave);
            }

            // Toss: one forward throw. Online every shot rolls, so the lob hint would mislead.
            var tm = PitStriker.Gameplay.TurnManager.Instance;
            bool toss = tm != null && tm.CurrentState == PitStriker.Gameplay.TurnManager.GameState.TossPhase;
            bool online = PitStriker.Gameplay.TurnManager.IsOnlineMatch;
            string upWord = toss ? "TOSS" : "LOB";
            if (_hintUp.text != upWord) _hintUp.text = upWord;
            bool showUp = toss || !online, showDown = !toss;
            var upRow = _hintUp.transform.parent.gameObject;
            var downRow = _hintDown.transform.parent.gameObject;
            if (upRow.activeSelf != showUp) upRow.SetActive(showUp);
            if (downRow.activeSelf != showDown) downRow.SetActive(showDown);

            // The hand is a hint: out of the way while a finger is actually swiping.
            bool dragging = _binder.Thumb != null && (_binder.Thumb.anchoredPosition - _thumbHome).sqrMagnitude > 4f;
            _handGroup.alpha = Mathf.MoveTowards(_handGroup.alpha, dragging ? 0f : 1f, Time.unscaledDeltaTime * 6f);
            _hand.rectTransform.anchoredPosition = new Vector2(0f, dragging ? 0f : 3f * Mathf.Sin(t * 2.4f));
        }
    }
}
