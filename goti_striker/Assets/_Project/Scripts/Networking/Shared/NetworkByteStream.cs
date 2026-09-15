using System;
using System.Text;

namespace PitStriker.Networking.Shared
{
    /// <summary>
    /// Lightweight, allocation-free binary writer that writes directly into a reusable byte array.
    /// </summary>
    public class NetworkByteWriter
    {
        private byte[] _buffer;
        private int _position;

        public byte[] Buffer => _buffer;
        public int Position => _position;

        public NetworkByteWriter(int capacity = 1024)
        {
            _buffer = new byte[capacity];
            _position = 0;
        }

        public void Reset()
        {
            _position = 0;
        }

        private void EnsureCapacity(int additionalBytes)
        {
            if (_position + additionalBytes > _buffer.Length)
            {
                int newCap = Math.Max(_buffer.Length * 2, _position + additionalBytes);
                Array.Resize(ref _buffer, newCap);
            }
        }

        public void WriteByte(byte value)
        {
            EnsureCapacity(1);
            _buffer[_position++] = value;
        }

        public void WriteBool(bool value)
        {
            WriteByte(value ? (byte)1 : (byte)0);
        }

        public void WriteInt32(int value)
        {
            EnsureCapacity(4);
            _buffer[_position++] = (byte)value;
            _buffer[_position++] = (byte)(value >> 8);
            _buffer[_position++] = (byte)(value >> 16);
            _buffer[_position++] = (byte)(value >> 24);
        }

        public void WriteUInt32(uint value)
        {
            EnsureCapacity(4);
            _buffer[_position++] = (byte)value;
            _buffer[_position++] = (byte)(value >> 8);
            _buffer[_position++] = (byte)(value >> 16);
            _buffer[_position++] = (byte)(value >> 24);
        }

        public void WriteSingle(float value)
        {
            EnsureCapacity(4);
            byte[] bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < 4; i++) _buffer[_position++] = bytes[i];
        }

        public void WriteDouble(double value)
        {
            EnsureCapacity(8);
            byte[] bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < 8; i++) _buffer[_position++] = bytes[i];
        }

        public void WriteString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                WriteInt32(0);
                return;
            }
            int byteCount = Encoding.UTF8.GetByteCount(value);
            WriteInt32(byteCount);
            EnsureCapacity(byteCount);
            Encoding.UTF8.GetBytes(value, 0, value.Length, _buffer, _position);
            _position += byteCount;
        }

        public void WriteVector3(NetVector3 v)
        {
            WriteSingle(v.x);
            WriteSingle(v.y);
            WriteSingle(v.z);
        }

        public void WriteMarbleState(CompactMarbleState state)
        {
            WriteVector3(state.Position);
            WriteVector3(state.Velocity);
            WriteBool(state.IsMoving);
            WriteBool(state.IsRetired);
        }

        public void WritePlayerData(CompactPlayerData p)
        {
            WriteInt32(p.PlayerIndex);
            WriteString(p.Name);
            WriteInt32(p.TotalStrokes);
            WriteInt32(p.CurrentPit);
            WriteBool(p.IsFinished);
            WriteBool(p.IsConnected);
        }

        public void WriteShotIntent(ShotIntentData intent)
        {
            WriteUInt32(intent.Sequence);
            WriteDouble(intent.ClientTimestamp);
            WriteVector3(intent.Direction);
            WriteSingle(intent.Force);
        }

        public void WriteWorldSnapshot(WorldSnapshotData snapshot)
        {
            WriteUInt32(snapshot.ServerTick);
            WriteDouble(snapshot.ServerTimestamp);
            WriteByte((byte)snapshot.Phase);
            WriteInt32(snapshot.ActivePlayerIndex);
            WriteSingle(snapshot.TurnTimerRemaining);
            WriteMarbleState(snapshot.Marble0);
            WriteMarbleState(snapshot.Marble1);
            WritePlayerData(snapshot.Player0);
            WritePlayerData(snapshot.Player1);
            WriteInt32(snapshot.WinnerPlayerIndex);
        }

        // ---- v2: shot-input relay + client-authored final state ----

        public void WriteShotInput(ShotInputData input)
        {
            WriteInt32(input.TurnId);
            WriteInt32(input.ShotId);
            WriteInt32(input.PlayerIndex);
            WriteInt32(input.MarbleId);
            WriteVector3(input.LaunchDirection);
            WriteSingle(input.Force);
            WriteSingle(input.MaxPitch);
            WriteByte(input.ShotMode);
            WriteBool(input.OpeningToss);
            WriteDouble(input.ClientTimestamp);
        }

        public void WriteMarbleFinalState(MarbleFinalState m)
        {
            WriteInt32(m.MarbleId);
            WriteVector3(m.Position);
            WriteBool(m.IsRetired);
            WriteInt32(m.InPitNumber);
        }

        private void WriteMarbleArray(MarbleFinalState[] marbles)
        {
            if (marbles == null) { WriteInt32(0); return; }
            int count = marbles.Length;
            if (count > NetworkProtocol.MaxMarblesPerMatch) count = NetworkProtocol.MaxMarblesPerMatch;
            WriteInt32(count);
            for (int i = 0; i < count; i++) WriteMarbleFinalState(marbles[i]);
        }

        public void WriteShotResult(ShotResultData r)
        {
            WriteInt32(r.TurnId);
            WriteInt32(r.ShotId);
            WriteInt32(r.PlayerIndex);
            WriteMarbleArray(r.Marbles);
            WriteInt32(r.PitConqueredNumber);
            WriteInt32(r.StrokesAfter);
            WriteInt32(r.CurrentPitAfter);
            WriteBool(r.PlayerFinished);
            WriteBool(r.HitOpponent);
            WriteDouble(r.ClientTimestamp);
        }

        public void WriteAcceptedState(AcceptedStateData s)
        {
            WriteInt32(s.TurnId);
            WriteInt32(s.LastAppliedShotId);
            WriteInt32(s.ActivePlayerIndex);
            WriteByte((byte)s.Phase);
            WriteSingle(s.TurnTimerRemaining);
            WriteMarbleArray(s.Marbles);
            WritePlayerData(s.Player0);
            WritePlayerData(s.Player1);
            WriteInt32(s.WinnerPlayerIndex);
        }
    }

    /// <summary>
    /// Allocation-free binary reader that parses packets sequentially from a byte array.
    /// </summary>
    public class NetworkByteReader
    {
        private byte[] _buffer = Array.Empty<byte>();
        private int _position;
        private int _length;

        public int Position => _position;
        public int Remaining => _length - _position;

        public NetworkByteReader(byte[] buffer, int offset = 0, int length = -1)
        {
            Reset(buffer, offset, length);
        }

        public void Reset(byte[] buffer, int offset = 0, int length = -1)
        {
            _buffer = buffer ?? Array.Empty<byte>();
            _position = offset;
            _length = length < 0 ? (buffer?.Length ?? 0) : Math.Min(buffer?.Length ?? 0, offset + length);
        }

        public byte ReadByte()
        {
            if (_position >= _length) throw new InvalidOperationException("End of byte stream reached.");
            return _buffer[_position++];
        }

        public bool ReadBool()
        {
            return ReadByte() != 0;
        }

        public int ReadInt32()
        {
            if (_position + 4 > _length) throw new InvalidOperationException("End of byte stream reached.");
            int val = _buffer[_position] |
                     (_buffer[_position + 1] << 8) |
                     (_buffer[_position + 2] << 16) |
                     (_buffer[_position + 3] << 24);
            _position += 4;
            return val;
        }

        public uint ReadUInt32()
        {
            return (uint)ReadInt32();
        }

        public float ReadSingle()
        {
            if (_position + 4 > _length) throw new InvalidOperationException("End of byte stream reached.");
            float val = BitConverter.ToSingle(_buffer, _position);
            _position += 4;
            return val;
        }

        public double ReadDouble()
        {
            if (_position + 8 > _length) throw new InvalidOperationException("End of byte stream reached.");
            double val = BitConverter.ToDouble(_buffer, _position);
            _position += 8;
            return val;
        }

        public string ReadString()
        {
            int byteCount = ReadInt32();
            if (byteCount <= 0) return string.Empty;
            if (_position + byteCount > _length) throw new InvalidOperationException("End of byte stream reached reading string.");
            string s = Encoding.UTF8.GetString(_buffer, _position, byteCount);
            _position += byteCount;
            return s;
        }

        public NetVector3 ReadVector3()
        {
            float x = ReadSingle();
            float y = ReadSingle();
            float z = ReadSingle();
            return new NetVector3(x, y, z);
        }

        public CompactMarbleState ReadMarbleState()
        {
            NetVector3 pos = ReadVector3();
            NetVector3 vel = ReadVector3();
            bool isMoving = ReadBool();
            bool isRetired = ReadBool();
            return new CompactMarbleState(pos, vel, isMoving, isRetired);
        }

        public CompactPlayerData ReadPlayerData()
        {
            int index = ReadInt32();
            string name = ReadString();
            int strokes = ReadInt32();
            int currentPit = ReadInt32();
            bool isFinished = ReadBool();
            bool isConnected = ReadBool();
            return new CompactPlayerData(index, name, strokes, currentPit, isFinished, isConnected);
        }

        public ShotIntentData ReadShotIntent()
        {
            uint seq = ReadUInt32();
            double ts = ReadDouble();
            NetVector3 dir = ReadVector3();
            float force = ReadSingle();
            return new ShotIntentData(seq, ts, dir, force);
        }

        public WorldSnapshotData ReadWorldSnapshot()
        {
            uint tick = ReadUInt32();
            double ts = ReadDouble();
            CloudMatchPhase phase = (CloudMatchPhase)ReadByte();
            int active = ReadInt32();
            float timer = ReadSingle();
            CompactMarbleState m0 = ReadMarbleState();
            CompactMarbleState m1 = ReadMarbleState();
            CompactPlayerData p0 = ReadPlayerData();
            CompactPlayerData p1 = ReadPlayerData();
            int winner = ReadInt32();
            return new WorldSnapshotData(tick, ts, phase, active, timer, m0, m1, p0, p1, winner);
        }

        // ---- v2: shot-input relay + client-authored final state ----

        public ShotInputData ReadShotInput()
        {
            return new ShotInputData
            {
                TurnId = ReadInt32(),
                ShotId = ReadInt32(),
                PlayerIndex = ReadInt32(),
                MarbleId = ReadInt32(),
                LaunchDirection = ReadVector3(),
                Force = ReadSingle(),
                MaxPitch = ReadSingle(),
                ShotMode = ReadByte(),
                OpeningToss = ReadBool(),
                ClientTimestamp = ReadDouble(),
            };
        }

        public MarbleFinalState ReadMarbleFinalState()
        {
            return new MarbleFinalState
            {
                MarbleId = ReadInt32(),
                Position = ReadVector3(),
                IsRetired = ReadBool(),
                InPitNumber = ReadInt32(),
            };
        }

        private MarbleFinalState[] ReadMarbleArray()
        {
            int count = ReadInt32();
            // A malformed or hostile count must not allocate wildly or read past the buffer.
            if (count < 0) count = 0;
            if (count > NetworkProtocol.MaxMarblesPerMatch) count = NetworkProtocol.MaxMarblesPerMatch;
            var arr = new MarbleFinalState[count];
            for (int i = 0; i < count; i++) arr[i] = ReadMarbleFinalState();
            return arr;
        }

        public ShotResultData ReadShotResult()
        {
            return new ShotResultData
            {
                TurnId = ReadInt32(),
                ShotId = ReadInt32(),
                PlayerIndex = ReadInt32(),
                Marbles = ReadMarbleArray(),
                PitConqueredNumber = ReadInt32(),
                StrokesAfter = ReadInt32(),
                CurrentPitAfter = ReadInt32(),
                PlayerFinished = ReadBool(),
                HitOpponent = ReadBool(),
                ClientTimestamp = ReadDouble(),
            };
        }

        public AcceptedStateData ReadAcceptedState()
        {
            return new AcceptedStateData
            {
                TurnId = ReadInt32(),
                LastAppliedShotId = ReadInt32(),
                ActivePlayerIndex = ReadInt32(),
                Phase = (CloudMatchPhase)ReadByte(),
                TurnTimerRemaining = ReadSingle(),
                Marbles = ReadMarbleArray(),
                Player0 = ReadPlayerData(),
                Player1 = ReadPlayerData(),
                WinnerPlayerIndex = ReadInt32(),
            };
        }
    }
}
