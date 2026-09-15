using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using PitStriker.Networking.Shared;

namespace PitStrikerServer
{
    public class ClientSession
    {
        public string SessionId { get; }
        public string ReconnectToken { get; }
        public WebSocket? Socket { get; set; }
        public string PlayerName { get; set; } = "Player";
        public int RoomPlayerIndex { get; set; } = -1; // 0 or 1
        public Room? CurrentRoom { get; set; }
        public DateTime LastSeenUtc { get; set; }
        public bool IsConnected => Socket != null && Socket.State == WebSocketState.Open;

        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);

        public ClientSession(string sessionId, WebSocket socket, string name)
        {
            SessionId = sessionId;
            ReconnectToken = Guid.NewGuid().ToString("N");
            Socket = socket;
            PlayerName = name;
            LastSeenUtc = DateTime.UtcNow;
        }

        private readonly NetworkByteWriter _sendWriter = new NetworkByteWriter(2048);

        /// <summary>Convenience: serialise one opcode + payload and send it to this session only.</summary>
        public Task SendOpCodeAsync(NetworkOpCode opCode, Action<NetworkByteWriter>? payloadWriter = null)
        {
            byte[] data;
            int len;
            lock (_sendWriter)
            {
                _sendWriter.Reset();
                _sendWriter.WriteByte((byte)opCode);
                payloadWriter?.Invoke(_sendWriter);
                data = (byte[])_sendWriter.Buffer.Clone();
                len = _sendWriter.Position;
            }
            return SendAsync(data, len);
        }

        public async Task SendAsync(byte[] data, int length)
        {
            if (Socket == null || Socket.State != WebSocketState.Open) return;

            await _sendLock.WaitAsync();
            try
            {
                if (Socket != null && Socket.State == WebSocketState.Open)
                {
                    await Socket.SendAsync(new ArraySegment<byte>(data, 0, length), WebSocketMessageType.Binary, true, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{SessionId}] Send error: {ex.Message}");
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }
}
