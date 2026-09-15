using UnityEngine;
using UnityEngine.InputSystem;
using PitStriker.Physics;
using PitStriker.Gameplay;
using PitStriker.GameplayKit;
using PitStriker.GameplayKit.UI;

namespace PitStriker.Input
{
    /// <summary>
    /// Handles mobile touch and mouse drag-and-release swipe mechanics for launching marbles.
    /// Draws an aiming trajectory indicator on the ground plane.
    /// </summary>
    public class SwipeLaunchController : MonoBehaviour
    {
        [Header("Launch Physics Tuning")]
        [Tooltip("Minimum drag distance in world units required to register a stroke.")]
        [SerializeField] private float _minDragDistance = 0.2f;

        [Tooltip("Maximum drag distance in world units for 100% power.")]
        [SerializeField] private float _maxDragDistance = 3.5f;

        [Tooltip("Maximum impulse force delivered to the marble at full power.")]
        [SerializeField] private float _maxLaunchForce = 32.0f;

        [Header("Trajectory Visualizer")]
        [Tooltip("Optional LineRenderer component used to draw the aim trajectory.")]
        [SerializeField] private LineRenderer _trajectoryLine;

        [Tooltip("Length of the visual trajectory guide at maximum power.")]
        [SerializeField] private float _maxVisualTrajectoryLength = 9.0f;

        public enum AimMode
        {
            PrecisionPullBack,  // Slingshot pull-back aiming for tactical gameplay
            ForwardFlickThrow   // Fast upward flick throwing gesture for the Opening Toss Phase
        }

        [Header("Aiming Mode")]
        [SerializeField] private AimMode _aimMode = AimMode.PrecisionPullBack;
        public AimMode CurrentAimMode => _aimMode;

        // Cached References
        public static SwipeLaunchController Instance { get; private set; }
        private MarbleController _marble;

        /// Shot type chosen by the swipe direction: backward = Ground, forward = Loft.
        private ShotMode _gestureShotMode = ShotMode.Ground;
        private Camera _mainCamera;

        // Events
        public static event System.Action<float> OnPowerChanged;

        // Drag & Flick State
        private bool _isDragging = false;
        private Vector2 _dragScreenStart;
        private float _dragStartTime;
        private float _currentPower = 0f;
        private Vector3 _shootDirection = Vector3.forward;
        private bool _powerGestureActive = false;
        private ShotControlBinder _shotUI;

        private void Awake()
        {
            Instance = this;
            _marble = GetComponent<MarbleController>();
            _mainCamera = Camera.main;

            // Ensure LineRenderer has clean defaults if attached
            if (_trajectoryLine == null)
            {
                _trajectoryLine = GetComponent<LineRenderer>();
            }

            if (_trajectoryLine != null)
            {
                _trajectoryLine.positionCount = 2;
                _trajectoryLine.enabled = false;
            }
        }

        public void SetAimMode(AimMode mode)
        {
            _aimMode = mode;
            CancelDrag();
        }

        public void SetActiveMarble(MarbleController newMarble)
        {
            _marble = newMarble;
            CancelDrag();

            if (_trajectoryLine != null && _marble != null)
            {
                MeshRenderer mr = _marble.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null && mr.sharedMaterial.HasProperty("_BaseColor"))
                {
                    Color c = mr.sharedMaterial.GetColor("_BaseColor");
                    _trajectoryLine.startColor = new Color(c.r, c.g, c.b, 0.95f);
                    _trajectoryLine.endColor = new Color(c.r, c.g, c.b, 0.25f);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_marble == null) return;

            // Keyboard shortcut test launch for instant testing (Spacebar)
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (!TurnManager.CanAim()) return;

                _marble.Halt();
                _marble.ApplyImpulse(Vector3.forward, 22.0f);
                Debug.Log("<color=#00FFAA><b>[TEST LAUNCH]</b> Spacebar pressed! Marble launched forward with 22N force.</color>");
                return;
            }

            HandlePointerInput();
        }

        private void HandlePointerInput()
        {
            if (!TurnManager.CanAim())
            {
                if (_isDragging) CancelDrag();
                return;
            }

            Vector2 screenPos = Vector2.zero;
            bool isPressed = false;
            bool justPressed = false;
            bool justReleased = false;

            // 1. Prioritize Touchscreen if active
            if (Touchscreen.current != null && (Touchscreen.current.primaryTouch.press.isPressed || Touchscreen.current.primaryTouch.press.wasPressedThisFrame || Touchscreen.current.primaryTouch.press.wasReleasedThisFrame))
            {
                screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
                isPressed = Touchscreen.current.primaryTouch.press.isPressed;
                justPressed = Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
                justReleased = Touchscreen.current.primaryTouch.press.wasReleasedThisFrame;
            }
            // 2. Mouse / Trackpad
            else if (Mouse.current != null)
            {
                screenPos = Mouse.current.position.ReadValue();
                isPressed = Mouse.current.leftButton.isPressed;
                justPressed = Mouse.current.leftButton.wasPressedThisFrame;
                justReleased = Mouse.current.leftButton.wasReleasedThisFrame;
            }

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) return;
            }

            // Pointer Down: power only if dedicated PowerArea exists; else legacy (non-UI)
            if (justPressed)
            {
                if (_shotUI == null)
                    _shotUI = FindAnyObjectByType<ShotControlBinder>();

                bool hasPowerArea = _shotUI != null && _shotUI.PowerArea != null;
                if (hasPowerArea)
                {
                    if (!_shotUI.IsScreenPosInPowerArea(screenPos))
                        return; // camera / other UI owns this touch
                }
                else if (UnityEngine.EventSystems.EventSystem.current != null)
                {
                    if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                    {
                        int touchId = Touchscreen.current.primaryTouch.touchId.ReadValue();
                        if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touchId)) return;
                    }
                    else if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    {
                        return;
                    }
                }

                if (_marble.CurrentSpeed < 2.5f)
                    _marble.Halt();

                _isDragging = true;
                _powerGestureActive = true;
                _dragScreenStart = screenPos;
                _dragStartTime = Time.time;
                _currentPower = 0f;
            }

            // Pointer Dragging / Flicking
            if (_isDragging && isPressed)
            {
                Vector2 screenDelta = screenPos - _dragScreenStart;

                if (_aimMode == AimMode.ForwardFlickThrow)
                {
                    // Forward Flick Mode (Toss Phase): Upward swipe on screen means forward throw
                    float dt = Mathf.Max(0.001f, Time.time - _dragStartTime);
                    float flickSpeed = screenDelta.magnitude / dt;

                    if (screenDelta.y > 15f)
                    {
                        _currentPower = Mathf.Clamp01(flickSpeed / (Screen.height * 1.5f));
                        OnPowerChanged?.Invoke(_currentPower);

                        Vector3 camFwd = Vector3.ProjectOnPlane(_mainCamera.transform.forward, Vector3.up).normalized;
                        Vector3 camRight = Vector3.ProjectOnPlane(_mainCamera.transform.right, Vector3.up).normalized;
                        Vector2 aimDir = screenDelta.normalized;
                        _shootDirection = (camRight * aimDir.x + camFwd * aimDir.y).normalized;

                        if (_trajectoryLine != null)
                        {
                            _trajectoryLine.enabled = true;
                            Vector3 marblePos = _marble != null ? _marble.transform.position : transform.position;
                            Vector3 startPos = marblePos + (Vector3.up * 0.05f);
                            Vector3 endPos = startPos + (_shootDirection * (Mathf.Max(0.3f, _currentPower) * _maxVisualTrajectoryLength * GameDifficulty.TrajectoryLengthMultiplier));

                            _trajectoryLine.SetPosition(0, startPos);
                            _trajectoryLine.SetPosition(1, endPos);
                        }
                    }
                    else
                    {
                        _currentPower = 0f;
                        if (_trajectoryLine != null) _trajectoryLine.enabled = false;
                        OnPowerChanged?.Invoke(0f);
                    }
                }
                else
                {
                    // Precision Slingshot Mode (Main Match): Pull backward to shoot forward
                    float dragPixels = screenDelta.magnitude;
                    float minPixels = Mathf.Max(10f, _minDragDistance * 60f);
                    float maxPixels = Mathf.Clamp(_maxDragDistance * 80f, 160f, Screen.height * 0.45f);

                    if (dragPixels < minPixels)
                    {
                        _currentPower = 0f;
                        if (_trajectoryLine != null) _trajectoryLine.enabled = false;
                        OnPowerChanged?.Invoke(0f);
                    }
                    else
                    {
                        // Power comes from how far AND how fast the swipe is: distance sets the
                        // base, speed scales it, so a quick flick hits harder than a slow drag
                        // of the same length. Distance alone can still reach full power.
                        float distNorm = Mathf.Clamp01((dragPixels - minPixels) / (maxPixels - minPixels));
                        float dragSeconds = Mathf.Max(0.001f, Time.time - _dragStartTime);
                        float speedNorm = Mathf.Clamp01((dragPixels / dragSeconds) / (Screen.height * 1.2f));
                        _currentPower = Mathf.Clamp01(distNorm * Mathf.Lerp(0.80f, 1.25f, speedNorm));
                        OnPowerChanged?.Invoke(_currentPower);

                        // Swipe direction picks the shot type:
                        //   backward (down the screen) = slingshot pull  -> Ground, rolls along the surface
                        //   forward  (up the screen)   = throw forward   -> Loft, parabolic arc
                        Vector2 aimScreenDir;
                        if (screenDelta.y < 0f)
                        {
                            _gestureShotMode = ShotMode.Ground;
                            aimScreenDir = -screenDelta.normalized;
                        }
                        else
                        {
                            _gestureShotMode = ShotMode.Loft;
                            aimScreenDir = screenDelta.normalized;
                        }

                        // keep the config in sync so anything reading CurrentMode follows the gesture
                        var modeCfg = ShotModeConfig.Instance;
                        if (modeCfg != null && modeCfg.CurrentMode != _gestureShotMode)
                            modeCfg.TrySetMode(_gestureShotMode);

                        Vector3 camFwd = Vector3.ProjectOnPlane(_mainCamera.transform.forward, Vector3.up).normalized;
                        Vector3 camRight = Vector3.ProjectOnPlane(_mainCamera.transform.right, Vector3.up).normalized;
                        _shootDirection = (camRight * aimScreenDir.x + camFwd * aimScreenDir.y).normalized;

                        if (_trajectoryLine != null)
                        {
                            _trajectoryLine.enabled = true;
                            Vector3 marblePos = _marble != null ? _marble.transform.position : transform.position;
                            Vector3 startPos = marblePos + (Vector3.up * 0.05f);
                            Vector3 endPos = startPos + (_shootDirection * (_currentPower * _maxVisualTrajectoryLength * GameDifficulty.TrajectoryLengthMultiplier));

                            _trajectoryLine.SetPosition(0, startPos);
                            _trajectoryLine.SetPosition(1, endPos);
                        }
                    }
                }
            }

            // Pointer Released: Execute Launch or Flick
            if (_isDragging && justReleased)
            {
                ExecuteLaunch();
            }
        }

        private void ExecuteLaunch()
        {
            var cfg = ShotModeConfig.Instance;
            float minP = cfg != null ? cfg.MinPower : 0.05f;
            float maxP = cfg != null ? cfg.MaxPower : 1f;
            float power = Mathf.Clamp(_currentPower, 0f, maxP);

            bool openingToss = TurnManager.Instance != null &&
                TurnManager.Instance.CurrentState == TurnManager.GameState.TossPhase;

            if (power > minP)
            {
                Vector3 dir = _shootDirection != Vector3.zero ? _shootDirection : Vector3.forward;
                float pitch;
                float maxPitch;
                // the swipe direction chose this, not a UI toggle
                ShotMode mode = _gestureShotMode;
                if (cfg != null && mode == ShotMode.Loft && !cfg.AllowLoftShot) mode = ShotMode.Ground;
                if (openingToss)
                {
                    pitch = 0.08f;
                    maxPitch = 0.12f;
                }
                else if (mode == ShotMode.Loft && (cfg == null || cfg.AllowLoftShot))
                {
                    pitch = cfg != null ? cfg.GetLoftPitch(power) : 0.35f;
                    maxPitch = cfg != null ? cfg.LoftMaxAngle : 0.55f;
                }
                else
                {
                    pitch = cfg != null ? cfg.GroundMaxPitch * 0.5f : 0.04f;
                    maxPitch = cfg != null ? cfg.GroundMaxPitch : 0.08f;
                    mode = ShotMode.Ground;
                }

                Vector3 launchDir = (dir + Vector3.up * pitch).normalized;
                float force = power * _maxLaunchForce * GameDifficulty.LaunchForceMultiplier;
                _marble.Halt();
                _marble.ApplyImpulse(launchDir, force, maxPitch);
                Debug.Log($"<color=#00FFAA><b>[SHOT {mode}]</b> power={power * 100:F0}% force={force:F1}N pitch={pitch:F2}</color>");
                if (!openingToss && cfg != null)
                    cfg.NotifyShotCompleted();
            }

            CancelDrag();
        }

        public void LaunchStrike(float powerFraction = -1f)
        {
            Debug.LogWarning("[SHOT] LaunchStrike is retired. Use Ground/Loft power-area swipe.");
            CancelDrag();
        }

        /// <summary>
        /// Renders the visual trajectory aim guide and power bar during autonomous AI turns or guided tutorials.
        /// </summary>
        public void ShowAimPreview(Vector3 direction, float power01)
        {
            _shootDirection = direction;
            _currentPower = Mathf.Clamp01(power01);

            if (_trajectoryLine != null)
            {
                _trajectoryLine.enabled = true;
                Vector3 marblePos = _marble != null ? _marble.transform.position : transform.position;
                Vector3 startPos = marblePos + (Vector3.up * 0.05f);
                Vector3 endPos = startPos + (_shootDirection * (Mathf.Max(0.3f, _currentPower) * _maxVisualTrajectoryLength * GameDifficulty.TrajectoryLengthMultiplier));

                _trajectoryLine.SetPosition(0, startPos);
                _trajectoryLine.SetPosition(1, endPos);
            }

            OnPowerChanged?.Invoke(_currentPower);
        }

        /// <summary>
        /// Hides the visual aim line and zeroes the power meter.
        /// </summary>
        public void HideAimPreview()
        {
            CancelDrag();
        }

        public void CancelActiveGesture() => CancelDrag();

        private void CancelDrag()
        {
            _isDragging = false;
            _currentPower = 0f;
            OnPowerChanged?.Invoke(0f);
            if (_trajectoryLine != null)
            {
                _trajectoryLine.enabled = false;
            }
        }
    }
}
