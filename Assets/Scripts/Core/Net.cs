using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Arena
{
    public enum NetEventType { Connected, Disconnected, Data, ConnectFailed }

    public struct NetEvent
    {
        public NetEventType Type;
        public int Peer;
        /// The message is Data[0..Length). Data is a pooled buffer, longer than the message and valid only until the
        /// next Poll: read it there and then, never keep it.
        public byte[] Data;
        public int Length;
    }

    /// Minimal TCP transport polled from the main thread. Messages are framed
    /// with a 4-byte length prefix. The host is peer 1; clients get 2, 3, ...
    public class NetPeer
    {
        class Conn
        {
            public int Id;
            public TcpClient Tcp;
            public NetworkStream Stream;
            public byte[] Buf = new byte[1 << 16];
            public int Len;
            public bool Dead;
            public bool Greeted;       // host: Session accepted its Hello (Greet); until then only small frames, and not for long
            public DateTime Since = DateTime.UtcNow;
            public DateTime LastIn = DateTime.UtcNow, LastOut = DateTime.UtcNow;  // the last bytes heard from it and sent to it (keepalive)
        }

        /// A quiet link sends an empty frame this often, and a peer heard nothing from for PeerTimeout is dropped:
        /// a Wi-Fi drop otherwise leaves a half-open connection holding its lobby seat forever.
        public const double KeepAliveEvery = 2.0;
        public static double PeerTimeout = 30.0;  // (the sim tests step games slower than real time and turn it off)
        DateTime lastPoll = DateTime.UtcNow;

        /// Host: how long a connection may stay without a Hello the game accepted, and the biggest frame it may send meanwhile.
        public const double HelloTimeout = 10.0;
        const int UngreetedMaxFrame = 1 << 16;
        /// How long a send may block on a peer that stopped reading (a frozen or sleeping machine) before it is dropped.
        public const int SendTimeoutMs = 2000;

        /// Host: the game accepted this connection's Hello (it is in the lobby): no more join timeout or small-frame limit.
        public void Greet(int peer)
        {
            Conn c;
            if (conns.TryGetValue(peer, out c))
                c.Greeted = true;
        }

        const float ConnectTimeout = 8f;

        public bool IsHost { get; private set; }

        /// Every byte read and written so far (length prefixes included), for the lag chart's bandwidth (LagNet.cs).
        public long BytesIn, BytesOut;
        public int MaxClients = 1;

        TcpListener listener;
        TcpClient pendingClient;
        Task connectTask;
        DateTime connectStarted;
        readonly Dictionary<int, Conn> conns = new Dictionary<int, Conn>();
        public int PeerCount => conns.Count;  // connected guests (a run snapshot loads only with none, RunSnapshot.cs)
        readonly List<NetEvent> events = new List<NetEvent>();
        int nextId = 2;
        /// The received messages' buffers, reused every Poll (a Data event's bytes live until the next Poll).
        readonly List<byte[]> msgPool = new List<byte[]>();
        int msgPoolUsed;
        const int PooledMaxFrame = 1 << 18;  // a bigger message (a rare large snapshot) gets its own array, so the pool stays small

        byte[] MsgBuffer(int size)
        {
            if (size > PooledMaxFrame)
                return new byte[size];
            if (msgPoolUsed == msgPool.Count)
                msgPool.Add(new byte[Math.Max(size, 1024)]);
            var buf = msgPool[msgPoolUsed];
            if (buf.Length < size)
                msgPool[msgPoolUsed] = buf = new byte[Math.Max(size, buf.Length * 2)];
            msgPoolUsed++;
            return buf;
        }

        public static NetPeer Host(int port)
        {
            var peer = new NetPeer { IsHost = true };
            peer.listener = new TcpListener(IPAddress.Any, port);
            peer.listener.Start();
            return peer;
        }

        /// No network at all: a host nobody can reach (the lobby's loadout preview, Showcase).
        public static NetPeer Offline() => new NetPeer { IsHost = true };

        public static NetPeer Join(string host, int port)
        {
            var peer = new NetPeer { IsHost = false };
            peer.pendingClient = new TcpClient();
            peer.connectTask = peer.pendingClient.ConnectAsync(host, port);
            peer.connectStarted = DateTime.UtcNow;
            return peer;
        }

        public List<NetEvent> Poll()
        {
            events.Clear();
            msgPoolUsed = 0;  // the last Poll's messages have been handled
            if (listener != null)
            {
                while (listener.Pending())
                {
                    var tcp = listener.AcceptTcpClient();
                    if (conns.Count >= MaxClients)
                    {
                        tcp.Close();
                        continue;
                    }
                    AddConn(nextId++, tcp);
                }
            }
            if (connectTask != null)
            {
                if (connectTask.IsCompleted)
                {
                    bool ok = !connectTask.IsFaulted && !connectTask.IsCanceled && pendingClient.Connected;
                    connectTask = null;
                    if (ok)
                        AddConn(1, pendingClient);
                    else
                        events.Add(new NetEvent { Type = NetEventType.ConnectFailed });
                    pendingClient = ok ? null : pendingClient;
                }
                else if ((DateTime.UtcNow - connectStarted).TotalSeconds > ConnectTimeout)
                {
                    connectTask = null;
                    pendingClient.Close();
                    events.Add(new NetEvent { Type = NetEventType.ConnectFailed });
                }
            }

            var now = DateTime.UtcNow;
            bool stalled = (now - lastPoll).TotalSeconds > KeepAliveEvery;  // this game didn't poll for a while (a hitch, a long load)
            lastPoll = now;
            foreach (var c in conns.Values)
            {
                Read(c);
                if (IsHost && !c.Greeted && (now - c.Since).TotalSeconds > HelloTimeout)
                    c.Dead = true;  // connected but never joined (a half-open or silent connection would hold a seat)
                if (c.Dead || IsHost && !c.Greeted)
                    continue;
                if (stalled)
                    c.LastIn = now;  // we weren't listening (a long hitch here): their silence doesn't count
                else if ((now - c.LastIn).TotalSeconds > PeerTimeout)
                    c.Dead = true;  // gone quiet (its keepalives stopped): a dropped link
                else if ((now - c.LastOut).TotalSeconds > KeepAliveEvery)
                    SendFrame(c, keepAlive, 0);  // an empty frame: "still here" (Read skips it)
            }
            var dead = deadIds;
            dead.Clear();
            foreach (var c in conns.Values)
                if (c.Dead)
                    dead.Add(c.Id);
            foreach (int id in dead)
            {
                conns[id].Tcp.Close();
                conns.Remove(id);
                events.Add(new NetEvent { Type = NetEventType.Disconnected, Peer = id });
            }
            return events;
        }

        void AddConn(int id, TcpClient tcp)
        {
            tcp.NoDelay = true;
            tcp.SendTimeout = SendTimeoutMs;  // a peer that stops reading can't freeze the game inside Send (it times out and is dropped)
            tcp.SendBufferSize = 1 << 18;
            tcp.ReceiveBufferSize = 1 << 18;
            conns[id] = new Conn { Id = id, Tcp = tcp, Stream = tcp.GetStream() };
            events.Add(new NetEvent { Type = NetEventType.Connected, Peer = id });
        }

        void Read(Conn c)
        {
            if (c.Dead)
                return;
            try
            {
                var sock = c.Tcp.Client;
                if (sock.Poll(0, SelectMode.SelectRead) && sock.Available == 0)
                {
                    c.Dead = true;  // remote closed the connection
                    return;
                }
                while (sock.Available > 0)
                {
                    if (c.Len == c.Buf.Length)
                        Array.Resize(ref c.Buf, c.Buf.Length * 2);
                    int n = c.Stream.Read(c.Buf, c.Len, c.Buf.Length - c.Len);
                    if (n <= 0)
                    {
                        c.Dead = true;
                        return;
                    }
                    c.Len += n;
                    BytesIn += n;
                    c.LastIn = DateTime.UtcNow;
                }
            }
            catch (Exception)
            {
                c.Dead = true;
                return;
            }

            int offset = 0;
            while (c.Len - offset >= 4)
            {
                int size = BitConverter.ToInt32(c.Buf, offset);
                if (size < 0 || size > (IsHost && !c.Greeted ? UngreetedMaxFrame : 1 << 24))
                {
                    c.Dead = true;
                    return;
                }
                if (c.Len - offset - 4 < size)
                {
                    if (4 + size > c.Buf.Length)
                        Array.Resize(ref c.Buf, 4 + size);
                    break;
                }
                if (size == 0)
                {
                    offset += 4;  // a keepalive: nothing to hand on
                    continue;
                }
                var msg = MsgBuffer(size);
                Buffer.BlockCopy(c.Buf, offset + 4, msg, 0, size);
                events.Add(new NetEvent { Type = NetEventType.Data, Peer = c.Id, Data = msg, Length = size });
                offset += 4 + size;
            }
            if (offset > 0)
            {
                Buffer.BlockCopy(c.Buf, offset, c.Buf, 0, c.Len - offset);
                c.Len -= offset;
            }
        }

        byte[] sendFrame = new byte[4096];  // reused by SendFrame: the length header and the message, one write
        static readonly byte[] keepAlive = new byte[0];
        readonly List<int> deadIds = new List<int>();  // reused by Poll

        public void Send(int peer, byte[] data)
        {
            Conn c;
            if (conns.TryGetValue(peer, out c) && !c.Dead)
                SendFrame(c, data, data.Length);
        }

        void SendFrame(Conn c, byte[] data, int size)
        {
            try
            {
                int len = size + 4;
                if (sendFrame.Length < len)
                    sendFrame = new byte[Math.Max(len, sendFrame.Length * 2)];
                sendFrame[0] = (byte)size;  // (little-endian length, as BitConverter.ToInt32 reads it)
                sendFrame[1] = (byte)(size >> 8);
                sendFrame[2] = (byte)(size >> 16);
                sendFrame[3] = (byte)(size >> 24);
                Buffer.BlockCopy(data, 0, sendFrame, 4, size);
                c.Stream.Write(sendFrame, 0, len);
                BytesOut += len;
                c.LastOut = DateTime.UtcNow;
            }
            catch (IOException)
            {
                c.Dead = true;
            }
            catch (ObjectDisposedException)
            {
                c.Dead = true;
            }
        }

        readonly List<int> broadcastIds = new List<int>();  // reused by Broadcast

        public void Broadcast(byte[] data)
        {
            broadcastIds.Clear();
            broadcastIds.AddRange(conns.Keys);
            foreach (int id in broadcastIds)
                Send(id, data);
        }

        public void Kick(int peer)
        {
            Conn c;
            if (conns.TryGetValue(peer, out c))
                c.Dead = true;
        }

        public void Close()
        {
            foreach (var c in conns.Values)
                c.Tcp.Close();
            conns.Clear();
            if (listener != null)
                listener.Stop();
            if (pendingClient != null)
                pendingClient.Close();
            listener = null;
            pendingClient = null;
            connectTask = null;
        }

        /// Local IPv4 addresses the host can share with a friend.
        public static List<string> LocalAddresses()
        {
            var list = new List<string>();
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                        continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork)
                            continue;
                        string s = ua.Address.ToString();
                        if (!s.StartsWith("127.") && !s.StartsWith("169.254.") && !list.Contains(s))
                            list.Add(s);
                    }
                }
            }
            catch (Exception)
            {
                // Some platforms don't allow listing interfaces; fall through.
            }
            if (list.Count == 0)
                list.Add("127.0.0.1");
            return list;
        }
    }
}
