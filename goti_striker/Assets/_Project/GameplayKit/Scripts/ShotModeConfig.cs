using UnityEngine;

namespace PitStriker.GameplayKit
{
    public enum ShotMode
    {
        Ground = 0,
        Loft = 1
    }

    /// <summary>
    /// Map/prefab-level shot configuration for Ground⇄Loft.
    /// </summary>
    public class ShotModeConfig : MonoBehaviour
    {
        public static ShotModeConfig Instance { get; private set; }

        [Header("Availability")]
        public bool AllowGroundShot = true;
        public bool AllowLoftShot = true;
        public ShotMode DefaultShotMode = ShotMode.Ground;
        public bool ResetModeAfterShot = true;

        [Header("Power")]
        public float MinPower = 0.05f;
        public float MaxPower = 1.0f;

        [Header("Loft")]
        [Tooltip("Upward component mixed into launch direction (0-1 before normalize).")]
        public float LoftVerticalForce = 0.45f;
        public float LoftMinAngle = 0.20f;
        public float LoftMaxAngle = 0.55f;

        [Header("Ground")]
        public float GroundMaxPitch = 0.08f;

        public ShotMode CurrentMode { get; private set; } = ShotMode.Ground;

        public static event System.Action<ShotMode> OnShotModeChanged;

        private void Awake()
        {
            Instance = this;
            CurrentMode = DefaultShotMode;
            if (!AllowGroundShot && AllowLoftShot) CurrentMode = ShotMode.Loft;
            if (!AllowLoftShot) CurrentMode = ShotMode.Ground;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool TrySetMode(ShotMode mode)
        {
            if (mode == ShotMode.Ground && !AllowGroundShot) return false;
            if (mode == ShotMode.Loft && !AllowLoftShot) return false;
            if (CurrentMode == mode) return true;
            CurrentMode = mode;
            OnShotModeChanged?.Invoke(CurrentMode);
            return true;
        }

        public void ToggleMode()
        {
            if (!AllowLoftShot || !AllowGroundShot)
            {
                TrySetMode(AllowLoftShot ? ShotMode.Loft : ShotMode.Ground);
                return;
            }
            TrySetMode(CurrentMode == ShotMode.Ground ? ShotMode.Loft : ShotMode.Ground);
        }

        public void NotifyShotCompleted()
        {
            if (ResetModeAfterShot && AllowGroundShot)
                TrySetMode(ShotMode.Ground);
        }

        public float GetLoftPitch(float power01)
        {
            return Mathf.Lerp(LoftMinAngle, LoftMaxAngle, Mathf.Clamp01(power01)) * LoftVerticalForce;
        }
    }
}
