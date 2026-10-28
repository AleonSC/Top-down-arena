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
        public byte[] Data;
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
        }

        const float ConnectTimeout = 8f;

        public bool IsHost { get; private set; }
        public int MaxClients = 1;

        TcpListener listener;
        TcpClient pendingClient;
        Task connectTask;
        DateTime connectStarted;
        readonly Dictionary<int, Conn> conns = new Dictionary<int, Conn>();
        readonly List<NetEvent> events = new List<NetEvent>();
        int nextId = 2;

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

            foreach (var c in conns.Values)
                Read(c);
            var dead = new List<int>();
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
                if (size < 0 || size > 1 << 24)
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
                var msg = new byte[size];
                Buffer.BlockCopy(c.Buf, offset + 4, msg, 0, size);
                events.Add(new NetEvent { Type = NetEventType.Data, Peer = c.Id, Data = msg });
                offset += 4 + size;
            }
            if (offset > 0)
            {
                Buffer.BlockCopy(c.Buf, offset, c.Buf, 0, c.Len - offset);
                c.Len -= offset;
            }
        }

        public void Send(int peer, byte[] data)
        {
            Conn c;
            if (!conns.TryGetValue(peer, out c) || c.Dead)
                return;
            try
            {
                var frame = new byte[data.Length + 4];
                Buffer.BlockCopy(BitConverter.GetBytes(data.Length), 0, frame, 0, 4);
                Buffer.BlockCopy(data, 0, frame, 4, data.Length);
                c.Stream.Write(frame, 0, frame.Length);
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

        public void Broadcast(byte[] data)
        {
            foreach (int id in new List<int>(conns.Keys))
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
