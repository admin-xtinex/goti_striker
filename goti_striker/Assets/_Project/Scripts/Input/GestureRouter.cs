namespace PitStriker.Input
{
    /// <summary>What a live pointer gesture is being used for.</summary>
    public enum GestureOwner
    {
        None = 0,
        Shot = 1,    // started on the shot panel — drives power/aim
        Camera = 2   // started on the gameplay view — orbits the camera
    }

    /// <summary>
    /// Decides, once per gesture, whether a pointer belongs to the shot panel or to the camera,
    /// and holds that decision until the pointer is released.
    ///
    /// Why this exists: both <see cref="SwipeLaunchController"/> and <see cref="CameraDragInput"/>
    /// poll the same devices every frame. Without a single owner they can both react to one
    /// finger — the player swipes to shoot and the camera spins at the same time — and a finger
    /// that slides off the panel mid-drag would silently change which system is listening.
    ///
    /// The rule is "claim on press, hold until release". Where the pointer travels afterwards is
    /// irrelevant: a gesture that starts on the panel stays a shot even if the finger leaves the
    /// panel, and a gesture that starts on the view stays a camera drag even if the finger
    /// crosses over the panel.
    /// </summary>
    public static class GestureRouter
    {
        /// <summary>Unity reports the mouse as pointer id -1; touches use their own touchId.</summary>
        public const int MousePointerId = -1;

        const int NoPointer = int.MinValue;

        public static GestureOwner Owner { get; private set; } = GestureOwner.None;
        public static int PointerId { get; private set; } = NoPointer;

        public static bool IsIdle => Owner == GestureOwner.None;

        /// <summary>
        /// Takes ownership of a gesture. Fails if a different pointer already holds one, so the
        /// second finger of a multitouch cannot hijack a drag that is already in progress.
        /// Re-claiming with the same pointer and owner is a no-op and succeeds.
        /// </summary>
        public static bool TryClaim(GestureOwner who, int pointerId)
        {
            if (who == GestureOwner.None) return false;

            if (Owner != GestureOwner.None)
                return Owner == who && PointerId == pointerId;

            Owner = who;
            PointerId = pointerId;
            return true;
        }

        /// <summary>True only for the exact pointer that made the claim.</summary>
        public static bool Owns(GestureOwner who, int pointerId)
            => Owner == who && PointerId == pointerId;

        /// <summary>
        /// Ends the gesture. Ignores pointers that do not hold the claim, so a second finger
        /// lifting cannot cancel the gesture the first finger is still driving.
        /// </summary>
        public static void Release(int pointerId)
        {
            if (PointerId != pointerId) return;
            Owner = GestureOwner.None;
            PointerId = NoPointer;
        }

        /// <summary>
        /// Drops any claim unconditionally. For turn changes, pauses, scene loads and focus loss,
        /// where the pointer's release event may never arrive and a stale claim would deadlock
        /// both input paths.
        /// </summary>
        public static void ReleaseAll()
        {
            Owner = GestureOwner.None;
            PointerId = NoPointer;
        }
    }
}
