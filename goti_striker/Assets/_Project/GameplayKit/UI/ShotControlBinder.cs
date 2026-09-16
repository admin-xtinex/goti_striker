using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PitStriker.Input;
using PitStriker.Gameplay;

namespace PitStriker.GameplayKit.UI
{
    /// <summary>
    /// Wires Crea's UI_ShotControl prefab: Ground⇄Loft toggle + power-area RectTransform
    /// for SwipeLaunchController input gating + power fill feedback.
    /// </summary>
    public class ShotControlBinder : MonoBehaviour
    {
        [Header("From UI_ShotControl")]
        public RectTransform PowerArea;
        public Image PowerFill;
        public Button GroundButton;
        public Button LoftButton;
        public Image GroundPill;
        public Image LoftPill;
        public Text GroundLabel;
        public Text LoftLabel;
        public GameObject FingerTutorial;

        [Header("Swipe-to-shoot art")]
        [Tooltip("SwipeShootPanel sprite host — the visible track at the right edge.")]
        public Image PanelImage;
        [Tooltip("SwipeShootThumb sprite host — moves with the swipe, returns home on release.")]
        public RectTransform Thumb;

        [Header("Visuals")]
        public Color ActivePill = new Color(0.95f, 0.72f, 0.22f, 1f);
        public Color InactivePill = new Color(0.35f, 0.40f, 0.45f, 0.85f);

        private ShotModeConfig _config;
        private Canvas _canvas;
        private Vector2 _thumbHome;
        private bool _thumbHomeCaptured;

        private void Awake()
        {
            // Presentation from the first frame, so the old bar art never shows (e.g. on the menu).
            if (GetComponent<PitStriker.UI.SwipeControlSkin>() == null)
                gameObject.AddComponent<PitStriker.UI.SwipeControlSkin>();

            _config = ShotModeConfig.Instance != null
                ? ShotModeConfig.Instance
                : FindAnyObjectByType<ShotModeConfig>();

            AutoWire();

            if (GroundButton != null) GroundButton.onClick.AddListener(() => SetMode(ShotMode.Ground));
            if (LoftButton != null) LoftButton.onClick.AddListener(() => SetMode(ShotMode.Loft));
            // Also allow tapping whole pills
            if (GroundPill != null)
            {
                var b = GroundPill.GetComponent<Button>() ?? GroundPill.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => SetMode(ShotMode.Ground));
                GroundButton = GroundButton != null ? GroundButton : b;
            }
            if (LoftPill != null)
            {
                var b = LoftPill.GetComponent<Button>() ?? LoftPill.gameObject.AddComponent<Button>();
                b.onClick.AddListener(() => SetMode(ShotMode.Loft));
                LoftButton = LoftButton != null ? LoftButton : b;
            }
        }

        void AutoWire()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();

            if (PowerArea == null)
            {
                var t = transform.Find("PowerArea");
                if (t != null) PowerArea = t as RectTransform;
            }
            if (PanelImage == null)
            {
                var p = transform.Find("PowerArea/PanelImage");
                if (p != null) PanelImage = p.GetComponent<Image>();
            }
            if (Thumb == null)
            {
                var th = transform.Find("PowerArea/Thumb");
                if (th != null) Thumb = th as RectTransform;
            }
            if (Thumb != null && !_thumbHomeCaptured)
            {
                _thumbHome = Thumb.anchoredPosition;
                _thumbHomeCaptured = true;
            }
            if (PowerFill == null)
            {
                var f = transform.Find("PowerArea/PowerTrack/PowerFill") ?? transform.Find("PowerArea/PowerFill");
                if (f == null)
                {
                    foreach (var img in GetComponentsInChildren<Image>(true))
                        if (img.gameObject.name == "PowerFill") { PowerFill = img; break; }
                }
                else PowerFill = f.GetComponent<Image>();
            }
            if (GroundPill == null)
            {
                var g = transform.Find("ShotModeToggle/Pill_Ground");
                if (g != null) GroundPill = g.GetComponent<Image>();
            }
            if (LoftPill == null)
            {
                var g = transform.Find("ShotModeToggle/Pill_Loft");
                if (g != null) LoftPill = g.GetComponent<Image>();
            }
            if (GroundLabel == null && GroundPill != null)
            {
                var lab = GroundPill.transform.Find("Label");
                if (lab != null) GroundLabel = lab.GetComponent<Text>();
            }
            if (LoftLabel == null && LoftPill != null)
            {
                var lab = LoftPill.transform.Find("Label");
                if (lab != null) LoftLabel = lab.GetComponent<Text>();
            }
            if (FingerTutorial == null)
            {
                var ft = transform.Find("FingerTutorial") ?? transform.Find("PowerArea/FingerTutorial");
                if (ft != null) FingerTutorial = ft.gameObject;
            }
        }

        private void OnEnable()
        {
            SwipeLaunchController.OnPowerChanged += HandlePower;
            ShotModeConfig.OnShotModeChanged += HandleModeChanged;
            RefreshModeVisuals();
            ApplyAvailability();
        }

        private void OnDisable()
        {
            SwipeLaunchController.OnPowerChanged -= HandlePower;
            ShotModeConfig.OnShotModeChanged -= HandleModeChanged;
        }

        private void SetMode(ShotMode mode)
        {
            if (_config == null) return;
            if (_config.TrySetMode(mode))
            {
                // Cancel any active power charge
                if (SwipeLaunchController.Instance != null)
                    SwipeLaunchController.Instance.CancelActiveGesture();
                RefreshModeVisuals();
            }
        }

        private void HandleModeChanged(ShotMode _) => RefreshModeVisuals();

        private void HandlePower(float p)
        {
            if (PowerFill != null)
                PowerFill.fillAmount = Mathf.Clamp01(p);
            if (p > 0.02f && FingerTutorial != null && FingerTutorial.activeSelf)
                FingerTutorial.SetActive(false);
        }

        public void ApplyAvailability()
        {
            if (_config == null) return;
            bool showToggle = _config.AllowGroundShot && _config.AllowLoftShot;
            if (GroundButton != null) GroundButton.gameObject.SetActive(_config.AllowGroundShot);
            if (LoftButton != null) LoftButton.gameObject.SetActive(_config.AllowLoftShot);
            var toggleRoot = transform.Find("ShotModeToggle");
            if (toggleRoot != null) toggleRoot.gameObject.SetActive(showToggle);
            if (!_config.AllowLoftShot && FingerTutorial != null)
                FingerTutorial.SetActive(false);
            RefreshModeVisuals();
        }

        private void RefreshModeVisuals()
        {
            if (_config == null) return;
            bool ground = _config.CurrentMode == ShotMode.Ground;
            if (GroundPill != null) GroundPill.color = ground ? ActivePill : InactivePill;
            if (LoftPill != null) LoftPill.color = !ground ? ActivePill : InactivePill;
            if (GroundLabel != null)
            {
                var c = GroundLabel.color; c.a = ground ? 1f : 0.5f; GroundLabel.color = c;
            }
            if (LoftLabel != null)
            {
                var c = LoftLabel.color; c.a = !ground ? 1f : 0.5f; LoftLabel.color = c;
            }
        }

        /// <summary>
        /// Camera that screen-point tests must be resolved against. Overlay canvases take null;
        /// anything else needs the canvas's own event camera or every hit test silently reports
        /// the wrong answer — which would route shot gestures to the camera and vice versa.
        /// </summary>
        private Camera EventCamera
        {
            get
            {
                if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
                if (_canvas == null) return null;
                return _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null
                    : (_canvas.worldCamera != null ? _canvas.worldCamera : Camera.main);
            }
        }

        /// <summary>Screen-space check: is this pixel inside the dedicated power area?</summary>
        public bool IsScreenPosInPowerArea(Vector2 screenPos)
        {
            if (PowerArea == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(PowerArea, screenPos, EventCamera);
        }

        public bool IsScreenPosOverShotUI(Vector2 screenPos)
        {
            if (IsScreenPosInPowerArea(screenPos)) return true;
            var toggle = transform.Find("ShotModeToggle") as RectTransform;
            if (toggle != null && RectTransformUtility.RectangleContainsScreenPoint(toggle, screenPos, EventCamera))
                return true;
            return false;
        }

        // ------------------------------------------------------------------ thumb

        /// <summary>
        /// Moves the thumb with the live swipe so the player can see the gesture they are making.
        /// Purely cosmetic — the shot's power and direction are computed from the raw screen
        /// delta in SwipeLaunchController and are not read back from this transform.
        /// </summary>
        public void SetThumbOffset(Vector2 screenDelta)
        {
            if (Thumb == null) return;

            if (!_thumbHomeCaptured)
            {
                _thumbHome = Thumb.anchoredPosition;
                _thumbHomeCaptured = true;
            }

            // Screen pixels are not canvas units on a scaled canvas, so convert before moving,
            // otherwise the thumb drifts by a different amount on every device resolution.
            float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            Vector2 local = screenDelta / scale;

            // Travel is capped to the panel so the thumb cannot slide out of its track, while the
            // gesture itself stays unclamped — a long swipe still reads as full power. Clamped per
            // axis rather than by magnitude, because the panel is a tall narrow strip: a single
            // radius would either let the thumb escape sideways or strangle its vertical range.
            if (PowerArea != null)
            {
                Rect area = PowerArea.rect;
                float halfThumbX = Thumb.rect.width * 0.5f;
                float halfThumbY = Thumb.rect.height * 0.5f;
                float maxX = Mathf.Max(0f, area.width * 0.5f - halfThumbX);
                float maxY = Mathf.Max(0f, area.height * 0.5f - halfThumbY);
                local.x = Mathf.Clamp(local.x, -maxX, maxX);
                local.y = Mathf.Clamp(local.y, -maxY, maxY);
            }

            Thumb.anchoredPosition = _thumbHome + local;
        }

        /// <summary>Returns the thumb to its rest position when the gesture ends or is cancelled.</summary>
        public void ResetThumb()
        {
            if (Thumb == null || !_thumbHomeCaptured) return;
            Thumb.anchoredPosition = _thumbHome;
        }
    }
}

