using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using PitStriker.CameraSystem;
using PitStriker.GameplayKit.UI;

namespace PitStriker.Input
{
    /// <summary>
    /// Orbits the follow camera from a plain drag anywhere on the gameplay view.
    ///
    /// Replaces the old two-finger-only orbit: the player now just drags, with no hold, no
    /// activation tap and no dedicated camera region. The shot panel is carved out — a gesture
    /// that starts on it belongs to the shot and never moves the camera.
    ///
    /// Ownership is settled once, at press, through <see cref="GestureRouter"/>. After that the
    /// finger may wander anywhere, including across the shot panel, and this still owns it until
    /// release. That is what stops the camera from lurching when a shot swipe overshoots the
    /// panel edge.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraDragInput : MonoBehaviour
    {
        [Tooltip("Degrees of orbit per pixel of horizontal drag.")]
        [SerializeField] private float _dragSensitivity = 0.22f;

        [Tooltip("Pixels the pointer must travel before the drag counts as an orbit. Keeps a tap from nudging the camera.")]
        [SerializeField] private float _dragDeadZone = 6f;

        private ShotControlBinder _shotUI;
        private Vector2 _lastPos;
        private bool _passedDeadZone;
        private int _activePointer = int.MinValue;

        private void OnDisable()
        {
            // A disabled component never sees the release, so drop the claim or both input
            // paths stay blocked for the rest of the session.
            if (_activePointer != int.MinValue)
            {
                GestureRouter.Release(_activePointer);
                _activePointer = int.MinValue;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) EndGesture();
        }

        private void Update()
        {
            if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
            {
                UpdateTouch();
                return;
            }

            UpdateMouse();
        }

        // ---------------------------------------------------------------- touch

        private void UpdateTouch()
        {
            // Every touch is examined, not just primaryTouch: when the first finger is already
            // holding the shot panel, primaryTouch stays that finger, and a camera drag by a
            // second finger would otherwise never be seen.
            foreach (var touch in Touchscreen.current.touches)
            {
                int id = touch.touchId.ReadValue();
                Vector2 pos = touch.position.ReadValue();

                if (touch.press.wasPressedThisFrame)
                {
                    TryBegin(pos, id);
                }
                else if (touch.press.isPressed)
                {
                    if (GestureRouter.Owns(GestureOwner.Camera, id)) Drag(pos);
                }
                else if (touch.press.wasReleasedThisFrame)
                {
                    if (GestureRouter.Owns(GestureOwner.Camera, id)) EndGesture();
                }
            }
        }

        // ---------------------------------------------------------------- mouse

        private void UpdateMouse()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 pos = mouse.position.ReadValue();
            int id = GestureRouter.MousePointerId;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                TryBegin(pos, id);
            }
            else if (mouse.leftButton.isPressed)
            {
                if (GestureRouter.Owns(GestureOwner.Camera, id)) Drag(pos);
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (GestureRouter.Owns(GestureOwner.Camera, id)) EndGesture();
            }
        }

        // ---------------------------------------------------------------- shared

        private void TryBegin(Vector2 screenPos, int pointerId)
        {
            if (IsOverShotPanel(screenPos)) return;   // the shot owns this gesture
            if (IsOverBlockingUI(screenPos, pointerId)) return;   // buttons, menus, HUD

            if (!GestureRouter.TryClaim(GestureOwner.Camera, pointerId)) return;

            _activePointer = pointerId;
            _lastPos = screenPos;
            _passedDeadZone = false;
        }

        private void Drag(Vector2 screenPos)
        {
            Vector2 delta = screenPos - _lastPos;

            if (!_passedDeadZone)
            {
                if (delta.magnitude < _dragDeadZone) return;
                _passedDeadZone = true;
                _lastPos = screenPos;   // start measuring from here so the camera never jumps
                return;
            }

            var cam = SmoothFollowCamera.Instance;
            if (cam != null && !Mathf.Approximately(delta.x, 0f))
                cam.RotateOrbit(delta.x * _dragSensitivity);

            _lastPos = screenPos;
        }

        private void EndGesture()
        {
            if (_activePointer != int.MinValue) GestureRouter.Release(_activePointer);
            _activePointer = int.MinValue;
            _passedDeadZone = false;
        }

        // ---------------------------------------------------------------- hit tests

        private bool IsOverShotPanel(Vector2 screenPos)
        {
            if (_shotUI == null) _shotUI = FindAnyObjectByType<ShotControlBinder>();
            return _shotUI != null && _shotUI.IsScreenPosOverShotUI(screenPos);
        }

        /// <summary>
        /// Anything interactive that is not the shot panel — pause button, menus, score HUD.
        /// Dragging those should never spin the camera behind them.
        /// </summary>
        private bool IsOverBlockingUI(Vector2 screenPos, int pointerId)
        {
            var es = EventSystem.current;
            if (es == null) return false;

            return pointerId == GestureRouter.MousePointerId
                ? es.IsPointerOverGameObject()
                : es.IsPointerOverGameObject(pointerId);
        }
    }
}
