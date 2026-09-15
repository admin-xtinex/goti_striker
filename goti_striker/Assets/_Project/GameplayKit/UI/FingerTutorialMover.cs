using UnityEngine;

namespace PitStriker.GameplayKit.UI
{
    /// <summary>Lightweight finger swipe preview loop for power-area tutorial.</summary>
    public class FingerTutorialMover : MonoBehaviour
    {
        public RectTransform finger;
        public float amplitude = 80f;
        public float speed = 1.4f;

        void Awake()
        {
            if (finger == null)
            {
                var t = transform.Find("Finger");
                if (t != null) finger = t.GetComponent<RectTransform>();
            }
        }

        void Update()
        {
            if (finger == null) return;
            float y = Mathf.PingPong(Time.unscaledTime * speed, 1f);
            finger.anchoredPosition = new Vector2(0f, Mathf.Lerp(40f, -amplitude, y));
        }
    }
}