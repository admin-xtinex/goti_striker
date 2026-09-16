namespace PitStriker.Networking.Shared
{
    public static class NetworkProtocol
    {
        // Version 2: shot-input relay + client-authored final state.
        // The server no longer simulates marbles, so a v1 client (which expects a rolling
        // physics feed) cannot share a match with a v2 server. The handshake rejects mismatches.
        //
        // Version 3: matches open with a server-run toss (CloudMatchPhase.TossPhase). A v2
        // client never expects that phase, would treat it as "not my turn" forever, and hang
        // at the start of every match - so it is refused at the handshake instead.
        public const int Version = 3;

        // Toss target: the centre of pit 3 on the lane centreline, in gameplay-prefab space.
        // Must match BuildMapReadyKit.PitZ[2]. The server has no scene, so it cannot look it up.
        public const float TossTargetZ = 31.0f;
        public const uint MagicHeader = 0x50495453; // "PITS" in ASCII
        public const int DefaultPort = 7777;
        public const float DefaultTurnDuration = 30.0f;
        public const float DisconnectGracePeriod = 20.0f;
        public const float PingInterval = 5.0f;
        public const float ConnectionTimeout = 10.0f;

        // Structural bounds for rejecting malformed input — NOT simulation clamps.
        // The striking client's Unity physics decides the actual shot.
        public const float MinAllowedForce = 0.5f;
        public const float MaxAllowedForce = 45.0f;
        // Must cover the loft arc offline play can produce (ShotModeConfig.LoftMaxAngle = 0.55).
        public const float MaxAllowedPitch = 0.60f;

        // Server waits this long for the striking client's ShotResult before abandoning the turn.
        // Must cover a full roll plus a slow connection.
        public const float ShotResultTimeout = 20.0f;
        // Opponent waits this long for a relayed result before asking the server to resync.
        public const float OpponentResultTimeout = 25.0f;

        // Largest number of gameplay marbles a result/state message may describe.
        public const int MaxMarblesPerMatch = 4;

        // Positional disagreement above this (metres) is snapped rather than eased.
        public const float ReconcileEaseThreshold = 0.75f;
    }

    public enum NetworkDelivery : byte
    {
        Unreliable = 0,
        Reliable = 1
    }

    public enum NetworkConnectionState : byte
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Failed
    }

    public enum CloudMatchPhase : byte
    {
        WaitingForPlayers = 0,
        LobbyCountdown = 1,
        TossPhase = 2,
        ReadyToAim = 3,
        Rolling = 4,
        Evaluating = 5,
        MatchCompleted = 6,
        Abandoned = 7
    }

    public enum NetworkOpCode : byte
    {
        // Handshake & Heartbeat
        ConnectRequest = 1,
        ConnectResponse = 2,
        Ping = 3,
        Pong = 4,

        // Lobby & Matchmaking
        CreateRoomRequest = 10,
        CreateRoomResponse = 11,
        JoinRoomRequest = 12,
        JoinRoomResponse = 13,
        QuickMatchRequest = 14,
        MatchFound = 15,
        LeaveRoomRequest = 16,
        PlayerJoined = 17,
        PlayerLeft = 18,
        LobbyCountdown = 19,

        // Gameplay Lifecycle & Turn State
        MatchStarted = 20,
        SubmitShotIntent = 21,
        ShotBroadcast = 22,
        TurnChanged = 23,
        WorldSnapshot = 24,
        PitConquered = 25,
        MatchCompleted = 26,
        RematchRequest = 27,
        RematchConfirmed = 28,

        // v2 shot-input relay + client-authored final state.
        // ShotInput   : striker -> server, the effective values its Unity strike used
        // ShotInputRelay : server -> opponent, so it can replay the identical strike
        // ShotResult  : striker -> server, where everything settled
        // AcceptedState : server -> both, the authoritative state the next turn starts from
        // ResyncRequest/AcceptedStateFull : recovery after reconnect or a missing result
        ShotInput = 40,
        ShotInputRelay = 41,
        ShotResult = 42,
        AcceptedState = 43,
        ResyncRequest = 44,
        ShotAbandoned = 45,

        // Reliability, Reconnection & Disconnects
        ReconnectRequest = 30,
        ReconnectResponse = 31,
        OpponentDisconnected = 32,
        OpponentReconnected = 33,
        MatchAbandoned = 34,

        // Diagnostics / Errors
        ErrorMessage = 99
    }
}
