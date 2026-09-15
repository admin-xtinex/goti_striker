using UnityEngine;

namespace PitStriker.UI
{
    /// <summary>
    /// Keeps a RectTransform inside <see cref="Screen.safeArea"/>, so controls are not swallowed
    /// by a notch, a punch-hole camera or the gesture bar along the bottom of modern phones.
    ///
    /// Re-applies when the safe area changes — which happens on rotation, on a foldable opening,
    /// and on some devices a frame or two after launch — so a one-shot fit in Awake is not enough.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaFitter : MonoBehaviour
    {
        [Tooltip("Extra inset in pixels applied inside the safe area, per edge.")]
        [SerializeField] private float _padding = 8f;

        private RectTransform _rt;
        private Rect _lastSafeArea;
        private Vector2Int _lastResolution;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            Apply();
        }

        private void OnEnable() => Apply();

        private void Update()
        {
            if (Screen.safeArea == _lastSafeArea &&
                Screen.width == _lastResolution.x && Screen.height == _lastResolution.y)
                return;

            Apply();
        }

        private void Apply()
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect safe = Screen.safeArea;
            _lastSafeArea = safe;
            _lastResolution = new Vector2Int(Screen.width, Screen.height);

            // Inset, then clamp: on a device with no notch the safe area is the full screen, and
            // padding alone could otherwise push the rect to a negative size on a tiny viewport.
            safe.xMin = Mathf.Min(safe.xMin + _padding, safe.xMax);
            safe.xMax = Mathf.Max(safe.xMax - _padding, safe.xMin);
            safe.yMin = Mathf.Min(safe.yMin + _padding, safe.yMax);
            safe.yMax = Mathf.Max(safe.yMax - _padding, safe.yMin);

            // Expressed as anchors so it stays correct under any CanvasScaler mode; converting to
            // pixels here would break the moment the scaler changed reference resolution.
            Vector2 min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            Vector2 max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);

            _rt.anchorMin = min;
            _rt.anchorMax = max;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
