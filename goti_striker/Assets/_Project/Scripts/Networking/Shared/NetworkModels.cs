using System;

namespace PitStriker.Networking.Shared
{
    [Serializable]
    public struct NetVector3
    {
        public float x;
        public float y;
        public float z;

        public NetVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public float SqrMagnitude => x * x + y * y + z * z;
        public float Magnitude => MathF.Sqrt(SqrMagnitude);

        public NetVector3 Normalized
        {
            get
            {
                float mag = Magnitude;
                return mag > 0.0001f ? new NetVector3(x / mag, y / mag, z / mag) : default;
            }
        }

        public static NetVector3 Zero => new NetVector3(0f, 0f, 0f);

        public static float Distance(NetVector3 a, NetVector3 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            float dz = a.z - b.z;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }

#if UNITY_2017_1_OR_NEWER
        public static implicit operator UnityEngine.Vector3(NetVector3 v) => new UnityEngine.Vector3(v.x, v.y, v.z);
        public static implicit operator NetVector3(UnityEngine.Vector3 v) => new NetVector3(v.x, v.y, v.z);
#endif
    }

    [Serializable]
    public struct CompactMarbleState
    {
        public NetVector3 Position;
        public NetVector3 Velocity;
        public bool IsMoving;
        public bool IsRetired;

        public CompactMarbleState(NetVector3 pos, NetVector3 vel, bool isMoving, bool isRetired)
        {
            Position = pos;
            Velocity = vel;
            IsMoving = isMoving;
            IsRetired = isRetired;
        }
    }

    [Serializable]
    public struct CompactPlayerData
    {
        public int PlayerIndex; // 0 or 1
        public string Name;
        public int TotalStrokes;
        public int CurrentPit;
        public bool IsFinished;
        public bool IsConnected;

        public CompactPlayerData(int index, string name, int strokes = 0, int currentPit = 1, bool isFinished = false, bool isConnected = true)
        {
            PlayerIndex = index;
            Name = name ?? $"Player {index + 1}";
            TotalStrokes = strokes;
            CurrentPit = currentPit;
            IsFinished = isFinished;
            IsConnected = isConnected;
        }
    }

    [Serializable]
    public struct ShotIntentData
    {
        public uint Sequence;
        public double ClientTimestamp;
        public NetVector3 Direction;
        public float Force;

        public ShotIntentData(uint sequence, double timestamp, NetVector3 direction, float force)
        {
            Sequence = sequence;
            ClientTimestamp = timestamp;
            Direction = direction;
            Force = force;
        }
    }

    /// <summary>
    /// The effective values the striking client's Unity strike used, so the opponent can
    /// reproduce an identical shot. These are the arguments actually passed to
    /// MarbleController.ApplyImpulse — not raw swipe screen coordinates.
    /// </summary>
    [Serializable]
    public struct ShotInputData
    {
        public int TurnId;
        public int ShotId;
        public int PlayerIndex;
        public int MarbleId;
        public NetVector3 LaunchDirection;  // normalized; pitch already folded into y
        public float Force;
        public float MaxPitch;
        public byte ShotMode;               // 0 = Ground, 1 = Loft
        public bool OpeningToss;
        public double ClientTimestamp;

        /// <summary>Envelope check only. Does not attempt to judge whether the shot was "fair".</summary>
        public bool IsStructurallyValid =>
            Force >= NetworkProtocol.MinAllowedForce &&
            Force <= NetworkProtocol.MaxAllowedForce &&
            LaunchDirection.y >= -0.001f &&
            LaunchDirection.y <= NetworkProtocol.MaxAllowedPitch + 0.001f &&
            LaunchDirection.SqrMagnitude > 0.5f &&
            LaunchDirection.SqrMagnitude < 1.5f &&
            MarbleId >= 0 && MarbleId < NetworkProtocol.MaxMarblesPerMatch;
    }

    /// <summary>Where one marble ended up once the striking client's physics settled.</summary>
    [Serializable]
    public struct MarbleFinalState
    {
        public int MarbleId;
        public NetVector3 Position;
        public bool IsRetired;
        public int InPitNumber;   // 0 = not in a pit
    }

    /// <summary>
    /// The striking client's report of how its shot resolved. The server performs structural
    /// checks only and never recomputes these values.
    /// </summary>
    [Serializable]
    public struct ShotResultData
    {
        public int TurnId;
        public int ShotId;
        public int PlayerIndex;
        public MarbleFinalState[] Marbles;
        public int PitConqueredNumber;   // 0 = none
        public int StrokesAfter;
        public int CurrentPitAfter;
        public bool PlayerFinished;
        public bool HitOpponent;
        public double ClientTimestamp;
    }

    /// <summary>
    /// Authoritative state both clients must hold before the next shot is enabled.
    /// Built by the server from an accepted ShotResult.
    /// </summary>
    [Serializable]
    public struct AcceptedStateData
    {
        public int TurnId;               // the turn this state begins
        public int LastAppliedShotId;
        public int ActivePlayerIndex;
        public CloudMatchPhase Phase;
        public float TurnTimerRemaining;
        public MarbleFinalState[] Marbles;
        public CompactPlayerData Player0;
        public CompactPlayerData Player1;
        public int WinnerPlayerIndex;
    }

    [Serializable]
    public struct WorldSnapshotData
    {
        public uint ServerTick;
        public double ServerTimestamp;
        public CloudMatchPhase Phase;
        public int ActivePlayerIndex;
        public float TurnTimerRemaining;
        public CompactMarbleState Marble0;
        public CompactMarbleState Marble1;
        public CompactPlayerData Player0;
        public CompactPlayerData Player1;
        public int WinnerPlayerIndex;

        public WorldSnapshotData(
            uint tick,
            double timestamp,
            CloudMatchPhase phase,
            int activePlayer,
            float timer,
            CompactMarbleState m0,
            CompactMarbleState m1,
            CompactPlayerData p0,
            CompactPlayerData p1,
            int winner = -1)
        {
            ServerTick = tick;
            ServerTimestamp = timestamp;
            Phase = phase;
            ActivePlayerIndex = activePlayer;
            TurnTimerRemaining = timer;
            Marble0 = m0;
            Marble1 = m1;
            Player0 = p0;
            Player1 = p1;
            WinnerPlayerIndex = winner;
        }
    }
}
