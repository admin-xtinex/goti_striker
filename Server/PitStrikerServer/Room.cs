using System;
using System.Threading.Tasks;
using PitStriker.Networking.Shared;

namespace PitStrikerServer
{
    public class Room
    {
        public string RoomCode { get; }
        public ClientSession? Player0 { get; set; }
        public ClientSession? Player1 { get; set; }
        public AuthoritativeMatchEngine MatchEngine { get; }

        public bool IsFull => Player0 != null && Player1 != null;
        public bool IsEmpty => Player0 == null && Player1 == null;

        /// <summary>
        /// True while at least one player still holds a live socket. A disconnect deliberately
        /// leaves the slot occupied so ReconnectRequest can re-seat the player, which means
        /// <see cref="IsEmpty"/> stays false for an abandoned match — occupancy alone is not a
        /// liveness signal.
        /// </summary>
        public bool HasConnectedPlayer => Player0?.IsConnected == true || Player1?.IsConnected == true;

        /// <summary>When the room last had no connected player at all. Null while someone is on.</summary>
        public DateTime? NoConnectionSinceUtc { get; set; }

        /// <summary>Set once the match has ended (forfeit or completion) so the room can be pruned.</summary>
        public bool MatchOver { get; set; }

        public DateTime? DisconnectGraceStartUtc { get; set; }
        public int DisconnectedPlayerIndex { get; set; } = -1;

        private readonly NetworkByteWriter _writer = new NetworkByteWriter(2048);

        public Room(string code)
        {
            RoomCode = code;
            MatchEngine = new AuthoritativeMatchEngine(this);
        }

        public bool AddPlayer(ClientSession session)
        {
            if (Player0 == null)
            {
                Player0 = session;
                session.RoomPlayerIndex = 0;
                session.CurrentRoom = this;
                return true;
            }
            else if (Player1 == null)
            {
                Player1 = session;
                session.RoomPlayerIndex = 1;
                session.CurrentRoom = this;
                return true;
            }
            return false;
        }

        public void RemovePlayer(ClientSession session)
        {
            if (Player0 == session)
            {
                Player0 = null;
            }
            else if (Player1 == session)
            {
                Player1 = null;
            }
            session.RoomPlayerIndex = -1;
            session.CurrentRoom = null;
        }

        public async Task BroadcastAsync(byte[] buffer, int length)
        {
            if (Player0?.IsConnected == true)
            {
                await Player0.SendAsync(buffer, length);
            }
            if (Player1?.IsConnected == true)
            {
                await Player1.SendAsync(buffer, length);
            }
        }

        public async Task BroadcastOpCodeAsync(NetworkOpCode opCode, Action<NetworkByteWriter>? payloadWriter = null)
        {
            lock (_writer)
            {
                _writer.Reset();
                _writer.WriteByte((byte)opCode);
                payloadWriter?.Invoke(_writer);
                // Cloned: the send is not awaited, and the next caller reuses _writer.
                byte[] data = (byte[])_writer.Buffer.Clone();
                int len = _writer.Position;
                _ = BroadcastAsync(data, len);
            }
        }

        /// <summary>
        /// Set when turn/score state advances so the tick loop emits one AcceptedState.
        /// This replaces the old continuous physics feed — the server no longer simulates
        /// marbles, so there is nothing to stream between shots.
        /// </summary>
        private int _acceptedStateDirty = 0;

        public void MarkAcceptedStateDirty() => Interlocked.Exchange(ref _acceptedStateDirty, 1);

        public bool ConsumeAcceptedStateDirty() => Interlocked.Exchange(ref _acceptedStateDirty, 0) == 1;

        /// <summary>Sends the authoritative state both clients must hold before the next shot.</summary>
        public Task BroadcastAcceptedStateAsync()
        {
            AcceptedStateData state = MatchEngine.BuildAcceptedState();
            byte[] data;
            int len;
            lock (_writer)
            {
                _writer.Reset();
                _writer.WriteByte((byte)NetworkOpCode.AcceptedState);
                _writer.WriteAcceptedState(state);
                data = (byte[])_writer.Buffer.Clone();
                len = _writer.Position;
            }
            return BroadcastAsync(data, len);
        }

        /// <summary>Sends the accepted state to a single session (reconnect / resync).</summary>
        public Task SendAcceptedStateToAsync(ClientSession session)
        {
            AcceptedStateData state = MatchEngine.BuildAcceptedState();
            byte[] data;
            int len;
            lock (_writer)
            {
                _writer.Reset();
                _writer.WriteByte((byte)NetworkOpCode.AcceptedState);
                _writer.WriteAcceptedState(state);
                data = (byte[])_writer.Buffer.Clone();
                len = _writer.Position;
            }
            return session.SendAsync(data, len);
        }

        /// <summary>Relays a payload to the other player only.</summary>
        public Task SendToOpponentAsync(ClientSession from, NetworkOpCode opCode, Action<NetworkByteWriter> payloadWriter)
        {
            ClientSession? target = ReferenceEquals(from, Player0) ? Player1 : Player0;
            if (target == null || target.IsConnected != true) return Task.CompletedTask;

            byte[] data;
            int len;
            lock (_writer)
            {
                _writer.Reset();
                _writer.WriteByte((byte)opCode);
                payloadWriter(_writer);
                data = (byte[])_writer.Buffer.Clone();
                len = _writer.Position;
            }
            return target.SendAsync(data, len);
        }
    }
}
