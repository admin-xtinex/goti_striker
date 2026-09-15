using UnityEngine;

namespace PitStriker.GameplayKit
{
    /// <summary>
    /// Single placement origin for PitStriker_GameplayKit.
    /// All gameplay local offsets are relative to this transform.
    /// </summary>
    public class GameplayOrigin : MonoBehaviour
    {
        public static GameplayOrigin Instance { get; private set; }

        [Tooltip("Local start tee (marble spawn) relative to origin.")]
        public Vector3 StartCenterLocal = new Vector3(0f, 0.3f, -6.0f);

        [Tooltip("Pit centres local Z along the lane (approved spacing).")]
        public Vector3 Pit1Local = new Vector3(0f, 0f, 3.0f);
        public Vector3 Pit2Local = new Vector3(0f, 0f, 16.5f);
        public Vector3 Pit3Local = new Vector3(0f, 0f, 31.0f);

        public Vector3 TeeAfterPit1Local = new Vector3(0f, 0.3f, 4.5f);
        public Vector3 TeeAfterPit2Local = new Vector3(0f, 0.3f, 18.0f);

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public Vector3 LocalToWorld(Vector3 local) => transform.TransformPoint(local);
        public Vector3 WorldToLocal(Vector3 world) => transform.InverseTransformPoint(world);
        public Vector3 LocalDirToWorld(Vector3 localDir) => transform.TransformDirection(localDir);

        public Vector3 GetPitLocal(int pitNumber)
        {
            switch (pitNumber)
            {
                case 1: return Pit1Local;
                case 2: return Pit2Local;
                case 3: return Pit3Local;
                default: return Pit3Local;
            }
        }

        public Vector3 GetPitWorld(int pitNumber) => LocalToWorld(GetPitLocal(pitNumber));
        public Vector3 GetStartWorld() => LocalToWorld(StartCenterLocal);
    }
}
