using UnityEngine;

namespace PitStriker.Gameplay
{
    public enum DifficultyMode
    {
        /// <summary>The game as it has always played: long guide, generous pits, stronger launch.</summary>
        Easy = 0,
        /// <summary>Short guide, true pit sizes, sharper bots. Also the fixed setting for online.</summary>
        Hard = 1,
        /// <summary>No aim guide at all, tight pits, near-clinical bots.</summary>
        Pro = 2
    }

    /// <summary>
    /// Difficulty profile for offline play. Replaces the old fixed "aim trajectory assist"
    /// setting: the trajectory guide is now one of several things a single difficulty choice
    /// drives, alongside pit generosity, launch strength and bot accuracy.
    ///
    /// Every gameplay knob reads <see cref="EffectiveMode"/>, never <see cref="CurrentMode"/>.
    /// That is what lets an online match pin itself to Hard without disturbing the player's
    /// saved offline preference.
    /// </summary>
    public static class GameDifficulty
    {
        const string PrefsKey = "GotiStriker.Difficulty";

        /// <summary>
        /// Online matches always play at Hard, for both players, regardless of either one's
        /// setting. This is not only for fairness: under the v2 shot-relay model each client
        /// re-runs the other's shot through its own physics, and LaunchForceMultiplier feeds the
        /// impulse. Two players on different difficulties would compute different outcomes for
        /// the same shot and desync on every exchange.
        /// </summary>
        public const DifficultyMode OnlineMode = DifficultyMode.Hard;

        static DifficultyMode _currentMode = DifficultyMode.Easy;
        static bool _loaded;

        /// <summary>The player's chosen offline difficulty. Persisted across sessions.</summary>
        public static DifficultyMode CurrentMode
        {
            get
            {
                if (!_loaded)
                {
                    _currentMode = (DifficultyMode)PlayerPrefs.GetInt(PrefsKey, (int)DifficultyMode.Easy);
                    _loaded = true;
                }
                return _currentMode;
            }
            set
            {
                _currentMode = value;
                _loaded = true;
                PlayerPrefs.SetInt(PrefsKey, (int)value);
                PlayerPrefs.Save();
                OnDifficultyChanged?.Invoke(value);
            }
        }

        public static event System.Action<DifficultyMode> OnDifficultyChanged;

        /// <summary>True while an online match is running, which pins difficulty to Hard.</summary>
        public static bool IsOnlineActive =>
            PitStriker.Networking.Client.CloudMatchManager.Instance != null &&
            PitStriker.Networking.Client.CloudMatchManager.Instance.IsOnlineMatchActive;

        /// <summary>What the game should actually use right now.</summary>
        public static DifficultyMode EffectiveMode => IsOnlineActive ? OnlineMode : CurrentMode;

        public static string DisplayName(DifficultyMode mode) => mode switch
        {
            DifficultyMode.Easy => "EASY",
            DifficultyMode.Hard => "HARD",
            DifficultyMode.Pro => "PRO",
            _ => "EASY"
        };

        public static string Description(DifficultyMode mode) => mode switch
        {
            DifficultyMode.Easy => "Full aim guide, stronger launch, forgiving opponents.",
            DifficultyMode.Hard => "Short aim guide, standard launch, sharper opponents.",
            DifficultyMode.Pro  => "No aim guide, lighter launch, near-clinical opponents.",
            _ => ""
        };

        // ---------------------------------------------------------------- player-facing knobs
        //
        // The previous config also exposed PitCatchRadiusMultiplier and PitVortexStrength. Both
        // had zero consumers anywhere in the project — they read as if difficulty changed how
        // forgiving the pits were, and it never did. They are removed rather than carried
        // forward, so this file only describes behaviour that actually exists. Pit capture is
        // deliberately left identical across difficulties; it is validated physics.

        /// <summary>
        /// Length of the aim guide, as a multiple of the base visual length. Zero on Pro, where
        /// the guide is switched off entirely — see <see cref="ShowTrajectoryGuide"/>.
        /// </summary>
        public static float TrajectoryLengthMultiplier => EffectiveMode switch
        {
            DifficultyMode.Easy => 1.25f,
            DifficultyMode.Hard => 0.55f,
            DifficultyMode.Pro => 0f,
            _ => 1.0f
        };

        /// <summary>Whether the aim guide is drawn at all. Pro plays blind.</summary>
        public static bool ShowTrajectoryGuide => EffectiveMode != DifficultyMode.Pro;

        /// <summary>
        /// Strike power, as a multiple of SwipeLaunchController's 32 N base. Rises with
        /// difficulty: harder modes hit harder, giving more reach but demanding finer control,
        /// which pairs with the shorter (or absent) aim guide.
        ///
        /// Easy stays at 1.15 deliberately — that is the value the game has always shipped, and
        /// Easy is defined as "the current gameplay". Pro tops out at 1.40 so peak force is
        /// 32 x 1.40 = 44.8 N, just under NetworkProtocol.MaxAllowedForce (45 N); going past that
        /// would have a Pro-tuned shot rejected outright if this value ever reached an online
        /// match. Feeds the impulse directly, so it must be identical for both players online —
        /// which is what OnlineMode guarantees.
        /// </summary>
        public static float LaunchForceMultiplier => EffectiveMode switch
        {
            DifficultyMode.Easy => 1.15f,
            DifficultyMode.Hard => 1.28f,
            DifficultyMode.Pro => 1.40f,
            _ => 1.15f
        };

        // ---------------------------------------------------------------- bot knobs

        /// <summary>
        /// Scales the bot's angular aim error. 1.0 keeps the long-standing Easy behaviour;
        /// lower means straighter shots, so the bot gets harder as the number falls.
        /// </summary>
        public static float BotAimErrorMultiplier => EffectiveMode switch
        {
            DifficultyMode.Easy => 1.0f,
            DifficultyMode.Hard => 0.62f,
            DifficultyMode.Pro => 0.32f,
            _ => 1.0f
        };

        /// <summary>
        /// Extra aim error applied to a bot's BONUS shot — the follow-up it earns by conquering a
        /// pit or striking an opponent. Multiplies on top of <see cref="BotAimErrorMultiplier"/>.
        ///
        /// This is what stops bots chaining pit after pit in a single turn. Bots used to be
        /// denied the bonus play entirely, which halted their turn mid-flow and gave them
        /// different rules from the player. They now take the shot and are simply much less
        /// likely to convert it.
        ///
        /// Effective error on a bonus shot, relative to the long-standing baseline spread:
        ///   Easy 1.00 x 3.20 = 3.20  - chaining is very rare, the player keeps the initiative
        ///   Hard 0.62 x 1.90 = 1.18  - converts sometimes; a player who aims well still wins
        ///   Pro  0.32 x 1.25 = 0.40  - converts often, and punishes a loose shot
        /// </summary>
        public static float BotBonusShotErrorMultiplier => EffectiveMode switch
        {
            DifficultyMode.Easy => 3.20f,
            DifficultyMode.Hard => 1.90f,
            DifficultyMode.Pro => 1.25f,
            _ => 3.20f
        };

        /// <summary>Scales the bot's shot-weight error, the same way.</summary>
        public static float BotForceErrorMultiplier => EffectiveMode switch
        {
            DifficultyMode.Easy => 1.0f,
            DifficultyMode.Hard => 0.68f,
            DifficultyMode.Pro => 0.40f,
            _ => 1.0f
        };

        /// <summary>
        /// How readily a bot spots and punishes a threatening opponent. Higher is more ruthless.
        /// Multiplies the controller's own configured awareness rate.
        /// </summary>
        public static float BotThreatAwarenessMultiplier => EffectiveMode switch
        {
            DifficultyMode.Easy => 1.0f,
            DifficultyMode.Hard => 1.35f,
            DifficultyMode.Pro => 1.75f,
            _ => 1.0f
        };
    }
}
