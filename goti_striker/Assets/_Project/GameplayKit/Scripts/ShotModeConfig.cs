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
        [Tooltip("Launch elevation of a lofted shot, in degrees. The same at every power, so a short " +
                 "swipe gives a short but clearly lofted arc. (The angle used to grow with swipe length, " +
                 "so a small swipe left at ~13 degrees and read as a roll.) Online input is rejected above " +
                 "NetworkProtocol.MaxAllowedPitch, hence the 36 degree cap.")]
        [Range(15f, 36f)] public float LoftLaunchAngle = 35f;
        [Tooltip("Lowest launch speed of a lofted shot in m/s (marbles weigh 1 kg, so also the impulse). " +
                 "Keeps even a tiny swipe up off the ground: 5.5 m/s at 35 degrees peaks about 0.5 m.")]
        public float LoftMinLaunchSpeed = 5.5f;

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

        /// <summary>Upward amount to add to a unit horizontal aim so the launch leaves at <see cref="LoftLaunchAngle"/>.</summary>
        public float LoftPitch => Mathf.Tan(LoftLaunchAngle * Mathf.Deg2Rad);

        /// <summary>Largest upward component of the normalized loft direction, for MarbleController's pitch clamp.</summary>
        public float LoftMaxPitch => Mathf.Sin(LoftLaunchAngle * Mathf.Deg2Rad) + 0.01f;
    }
}
