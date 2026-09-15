using UnityEngine;
using PitStriker.Physics;

namespace PitStriker.GameplayKit
{
    /// <summary>
    /// Fallback-only safety: detect void falls, stop respawn loops, hold + report bad ground.
    /// </summary>
    [RequireComponent(typeof(MarbleController))]
    public class MarbleSafetyGuard : MonoBehaviour
    {
        [SerializeField] float _voidYOffset = -5f;
        [SerializeField] float _loopWindow = 3f;
        [SerializeField] int _loopThreshold = 3;

        MarbleController _marble;
        float _lastRespawnTime = -999f;
        int _respawnCount;
        bool _locked;
        Vector3 _holdPos;
        static bool _loggedBadGround;

        private void Awake()
        {
            _marble = GetComponent<MarbleController>();
        }

        private void FixedUpdate()
        {
            if (_marble == null || _marble.IsRetired) return;

            if (_locked)
            {
                _marble.Halt();
                _marble.ResetPosition(_holdPos);
                return;
            }

            float surfaceY = SurfaceGroundConfig.Instance != null
                ? SurfaceGroundConfig.Instance.GetSurfaceTopWorldY()
                : 0f;
            float voidY = surfaceY + _voidYOffset;

            if (transform.position.y < voidY)
            {
                RespawnSafe("below void threshold");
            }
        }

        void RespawnSafe(string reason)
        {
            float now = Time.time;
            if (now - _lastRespawnTime < _loopWindow)
                _respawnCount++;
            else
                _respawnCount = 1;
            _lastRespawnTime = now;

            float r = _marble.WorldRadius;
            Vector3 hint = GameplayOrigin.Instance != null
                ? GameplayOrigin.Instance.StartCenterLocal
                : new Vector3(0f, 0.3f, -6f);
            Vector3 safe = SurfaceGroundConfig.Instance != null
                ? SurfaceGroundConfig.Instance.GetSafeSpawnWorld(hint, r, true)
                : new Vector3(hint.x, surfaceFallback() + r + 0.05f, hint.z);

            _marble.Halt();
            _marble.ResetPosition(safe);

            if (_respawnCount >= _loopThreshold)
            {
                _locked = true;
                _holdPos = safe;
                if (!_loggedBadGround)
                {
                    _loggedBadGround = true;
                    Debug.LogError($"[GameplayKit] INVALID ground/collider — marble '{name}' respawn-looped ({reason}). Input locked. Check GameplayCollider.");
                }
            }
            else
            {
                Debug.LogWarning($"[GameplayKit] Safety respawn ({reason}) → {safe}");
            }
        }

        float surfaceFallback()
        {
            return GameplayOrigin.Instance != null ? GameplayOrigin.Instance.transform.position.y : 0f;
        }
    }
}
