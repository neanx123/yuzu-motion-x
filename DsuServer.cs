using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace YuzuMotionX;

static class Crc32
{
    static readonly uint[] Table = Make();

    static uint[] Make()
    {
        var t = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[i] = c;
        }
        return t;
    }

    public static uint Compute(byte[] data)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte b in data)
            c = Table[(int)((c ^ b) & 0xFF)] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}

/// <summary>Minimal DSU ("cemuhook") motion server, protocol version 1001.</summary>
public sealed class DsuServer : IDisposable
{
    const ushort Version = 1001;
    const uint MsgVersion = 0x100000, MsgInfo = 0x100001, MsgData = 0x100002;

    readonly Socket sock;
    readonly uint serverId;
    readonly Dictionary<EndPoint, DateTime> clients = new Dictionary<EndPoint, DateTime>();
    readonly object lk = new object();
    readonly Thread thread;
    volatile bool running = true;
    uint packetNo;
    readonly byte macId;

    public DsuServer(string host, int port, byte macId = 2)
    {
        this.macId = macId;
        sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        sock.Bind(new IPEndPoint(IPAddress.Parse(host), port));
        sock.ReceiveTimeout = 500;
        try { sock.IOControl(-1744830452, new byte[] { 0, 0, 0, 0 }, null); } catch { } // ignore ICMP resets
        serverId = (uint)Random.Shared.NextInt64(1, uint.MaxValue);
        thread = new Thread(Loop) { IsBackground = true };
    }

    public void Start() => thread.Start();

    public int ClientCount
    {
        get
        {
            DateTime now = DateTime.UtcNow;
            lock (lk) return clients.Count(kv => (now - kv.Value).TotalSeconds < 10);
        }
    }

    void Loop()
    {
        byte[] buf = new byte[2048];
        while (running)
        {
            EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            int n;
            try
            {
                n = sock.ReceiveFrom(buf, ref remote);
            }
            catch (SocketException e)
            {
                if (e.SocketErrorCode != SocketError.TimedOut) Thread.Sleep(5);
                continue;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            try { Handle(buf, n, remote); } catch { }
        }
    }

    byte[] Shared(byte slot, bool connected)
    {
        var b = new byte[11];
        b[0] = slot;
        b[1] = (byte)(connected ? 2 : 0);   // slot state
        b[2] = (byte)(connected ? 2 : 0);   // model: full gyro
        b[3] = (byte)(connected ? 1 : 0);   // connection: USB
        if (connected) b[9] = (byte)(macId + slot);    // last byte of the 6-byte MAC (bytes 4..9)
        b[10] = (byte)(connected ? 5 : 0);  // battery: full
        return b;
    }

    byte[] Build(uint type, byte[] payload)
    {
        var pkt = new byte[20 + payload.Length];
        pkt[0] = (byte)'D'; pkt[1] = (byte)'S'; pkt[2] = (byte)'U'; pkt[3] = (byte)'S';
        BinaryPrimitives.WriteUInt16LittleEndian(pkt.AsSpan(4), Version);
        BinaryPrimitives.WriteUInt16LittleEndian(pkt.AsSpan(6), (ushort)(4 + payload.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(pkt.AsSpan(12), serverId);
        BinaryPrimitives.WriteUInt32LittleEndian(pkt.AsSpan(16), type);
        payload.CopyTo(pkt, 20);
        BinaryPrimitives.WriteUInt32LittleEndian(pkt.AsSpan(8), Crc32.Compute(pkt));
        return pkt;
    }

    void Send(EndPoint to, uint type, byte[] payload)
    {
        try { sock.SendTo(Build(type, payload), to); } catch { }
    }

    void Handle(byte[] buf, int n, EndPoint remote)
    {
        if (n < 20 || buf[0] != 'D' || buf[1] != 'S' || buf[2] != 'U' || buf[3] != 'C') return;
        int length = BinaryPrimitives.ReadUInt16LittleEndian(buf.AsSpan(6));
        if (n < 16 + length) return;
        uint msg = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(16));

        if (msg == MsgVersion)
        {
            var p = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(p, Version);
            Send(remote, MsgVersion, p);
        }
        else if (msg == MsgInfo && length >= 8)
        {
            int count = Math.Clamp(BinaryPrimitives.ReadInt32LittleEndian(buf.AsSpan(20)), 0, 4);
            for (int i = 0; i < count && 24 + i < n; i++)
            {
                byte slot = buf[24 + i];
                var p = new byte[12];            // 11 shared bytes + 1 zero byte
                Shared(slot, slot == 0).CopyTo(p, 0);
                Send(remote, MsgInfo, p);
            }
        }
        else if (msg == MsgData)
        {
            lock (lk) clients[remote] = DateTime.UtcNow;
        }
    }

    public void SendMotion(float[] accel, float[] gyro)
    {
        List<EndPoint> targets;
        DateTime now = DateTime.UtcNow;
        lock (lk)
        {
            var dead = clients.Where(kv => (now - kv.Value).TotalSeconds > 10).Select(kv => kv.Key).ToList();
            foreach (var d in dead) clients.Remove(d);
            targets = clients.Keys.ToList();
        }
        if (targets.Count == 0) return;

        packetNo++;
        var pl = new byte[80];
        Shared(0, true).CopyTo(pl, 0);                           // 0..10
        pl[11] = 1;                                              // pad active
        BinaryPrimitives.WriteUInt32LittleEndian(pl.AsSpan(12), packetNo);
        // 16..19 buttons/home/touch = 0
        pl[20] = 128; pl[21] = 128; pl[22] = 128; pl[23] = 128;  // sticks centred
        // 24..47 analog buttons + two touch points = 0
        ulong micros = (ulong)(Stopwatch.GetTimestamp() * 1000000.0 / Stopwatch.Frequency);
        BinaryPrimitives.WriteUInt64LittleEndian(pl.AsSpan(48), micros);
        for (int i = 0; i < 3; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(pl.AsSpan(56 + 4 * i), accel[i]);
            BinaryPrimitives.WriteSingleLittleEndian(pl.AsSpan(68 + 4 * i), gyro[i]);
        }
        byte[] pkt = Build(MsgData, pl);
        foreach (var ep in targets)
        {
            try { sock.SendTo(pkt, ep); } catch { }
        }
    }

    public void Dispose()
    {
        running = false;
        try { sock.Close(); } catch { }
    }
}
