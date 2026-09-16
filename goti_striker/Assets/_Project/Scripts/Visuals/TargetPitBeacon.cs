using UnityEngine;
using UnityEngine.UI;
using PitStriker.Gameplay;
using PitStriker.UI;

namespace PitStriker.Visuals
{
    /// <summary>
    /// Hologram marker over the pit the viewing player is aiming at: a ring on the ground, a
    /// soft beam, a floating ring at the top and a down-pointing triangle above it.
    ///
    /// Visual only. Drawn with UI images on a world-space canvas (built-in UI shader, always in
    /// a build); no colliders, no raycaster, so it cannot touch physics or input. It reads the
    /// target from TurnManager and never writes it.
    /// </summary>
    public class TargetPitBeacon : MonoBehaviour
    {
        const float BeamHeight = 1.9f;
        static readonly Color Holo = new Color(0.40f, 0.88f, 1f, 1f);

        RectTransform _billboard, _top;
        Image _baseRing, _topRing, _beam, _triangle, _baseGlow;
        Canvas _canvas;

        public static void Ensure()
        {
            if (FindAnyObjectByType<TargetPitBeacon>() != null) return;
            new GameObject("TargetPitBeacon").AddComponent<TargetPitBeacon>();
        }

        void Awake()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(2f, 2f);
            rt.localScale = Vector3.one;

            // Flat pieces lie on the ground plane (canvas XY rotated onto world XZ).
            var flat = HudArt.Rect("Flat", transform);
            flat.localRotation = Quaternion.Euler(90f, 0f, 0f);
            flat.sizeDelta = new Vector2(2f, 2f);

            _baseGlow = HudArt.Image("BaseGlow", flat, HudArt.Glow(), new Color(Holo.r, Holo.g, Holo.b, 0.35f));
            Size(_baseGlow, 1.7f, 1.7f, Vector3.zero);
            _baseRing = HudArt.Image("BaseRing", flat, HudArt.Ring(10f), new Color(Holo.r, Holo.g, Holo.b, 0.9f));
            Size(_baseRing, 1.05f, 1.05f, Vector3.zero);

            var top = HudArt.Rect("Top", transform);
            top.localPosition = new Vector3(0f, BeamHeight, 0f);
            top.localRotation = Quaternion.Euler(90f, 0f, 0f);   // re-tilted toward the camera each frame
            top.sizeDelta = new Vector2(2f, 2f);
            _topRing = HudArt.Image("TopRing", top, HudArt.Ring(9f), new Color(Holo.r, Holo.g, Holo.b, 0.95f));
            Size(_topRing, 0.95f, 0.95f, Vector3.zero);

            // Beam and triangle turn to face the camera around the vertical axis.
            _billboard = HudArt.Rect("Billboard", transform);
            _billboard.sizeDelta = new Vector2(2f, 2f);
            _top = top;
            _beam = HudArt.Image("Beam", _billboard, HudArt.Beam(), new Color(Holo.r, Holo.g, Holo.b, 0.55f));
            Size(_beam, 0.62f, BeamHeight, new Vector3(0f, BeamHeight * 0.5f, 0f));
            _triangle = HudArt.Image("Triangle", _billboard, HudArt.TriangleDownOutline(), new Color(0.75f, 0.95f, 1f, 1f));
            Size(_triangle, 0.46f, 0.40f, new Vector3(0f, BeamHeight + 0.55f, 0f));

            SetVisible(false);
        }

        static void Size(Image img, float w, float h, Vector3 localPos)
        {
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.localPosition = localPos;
        }

        void SetVisible(bool on)
        {
            if (_canvas.enabled != on) _canvas.enabled = on;
        }

        void LateUpdate()
        {
            var tm = TurnManager.Instance;
            bool playing = tm != null && tm.CurrentState != TurnManager.GameState.Menu
                                      && tm.CurrentState != TurnManager.GameState.MatchVictory;
            var view = playing ? (tm.LocalViewPlayer ?? tm.ActivePlayer) : null;
            if (view == null || view.isFinished) { SetVisible(false); return; }

            // The whole toss aims at pit 3, while a throw is rolling too, not only while aiming.
            int pit = tm.IsTossInProgress ? 3 : Mathf.Clamp(view.currentPit, 1, 3);
            SetVisible(true);
            transform.position = tm.GetPitPosition(pit) + new Vector3(0f, 0.04f, 0f);

            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = cam.transform.position - transform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.0001f)
                {
                    _billboard.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                    // Seen nearly edge-on from a low camera a flat ring is a line; lean it toward the
                    // camera so it reads as a floating ring.
                    _top.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up) * Quaternion.Euler(60f, 0f, 0f);
                }
            }

            float t = Time.time;
            float pulse = 0.8f + 0.2f * Mathf.Sin(t * 3f);
            _baseRing.color = new Color(Holo.r, Holo.g, Holo.b, 0.9f * pulse);
            _baseGlow.color = new Color(Holo.r, Holo.g, Holo.b, 0.35f * pulse);
            _beam.color = new Color(Holo.r, Holo.g, Holo.b, 0.65f * pulse);
            _triangle.rectTransform.localPosition = new Vector3(0f, BeamHeight + 0.55f + 0.08f * Mathf.Sin(t * 2.2f), 0f);
        }
    }
}
