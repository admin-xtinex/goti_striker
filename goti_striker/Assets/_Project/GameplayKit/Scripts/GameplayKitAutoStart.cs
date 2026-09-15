using UnityEngine;
using PitStriker.Gameplay;

namespace PitStriker.GameplayKit
{
    /// <summary>
    /// Test-scene helper. TurnManager boots into GameState.Menu when _startInMenu is set, and
    /// blocks all gameplay input until a menu calls ConfigureAndStartMatch(). A bare kit scene
    /// has no menu, so the match never starts and the power swipe does nothing.
    /// This starts a match automatically so the kit can be played standalone.
    /// Do not ship this in a build that has a real main menu.
    /// </summary>
    public class GameplayKitAutoStart : MonoBehaviour
    {
        [Tooltip("Players to start with. The kit prefab ships configured for 2.")]
        [Range(1, 4)] public int PlayerCount = 2;

        [Tooltip("Which players are AI. Entries past PlayerCount are ignored; missing entries default to human.")]
        public bool[] IsAI = new bool[] { false, false };

        [Tooltip("Seconds to wait before starting, so TurnManager.Start() has run first.")]
        public float StartDelay = 0.25f;

        private bool _started;

        private void Start() => Invoke(nameof(Begin), StartDelay);

        private void Begin()
        {
            if (_started) return;
            var tm = TurnManager.Instance;
            if (tm == null)
            {
                Debug.LogWarning("[GameplayKitAutoStart] No TurnManager in scene.");
                return;
            }

            var flags = new bool[PlayerCount];
            for (int i = 0; i < PlayerCount; i++)
                flags[i] = IsAI != null && i < IsAI.Length && IsAI[i];

            tm.ConfigureAndStartMatch(PlayerCount, flags);
            _started = true;
            Debug.Log($"[GameplayKitAutoStart] Started match: {PlayerCount} players, state={tm.CurrentState}");
        }
    }
}
