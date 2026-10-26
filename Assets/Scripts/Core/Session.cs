using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;

namespace Arena
{
    public enum Msg : byte
    {
        // client -> host
        Hello = 1, Input, Ability, RoleRequest,
        // host -> clients
        Welcome, Lobby, Start, Spawn, Remove, ClearEnemyBullets, Fx, Snapshot, ToLobby,
    }

    public static class Proto
    {
        public static BinaryWriter Begin(Msg m)
        {
            var w = new BinaryWriter(new MemoryStream());
            w.Write((byte)m);
            return w;
        }

        public static byte[] End(BinaryWriter w)
        {
            w.Flush();
            return ((MemoryStream)w.BaseStream).ToArray();
        }

        public static void Write(this BinaryWriter w, V2 v) { w.Write(v.X); w.Write(v.Y); }
        public static V2 ReadV2(this BinaryReader r) => new V2(r.ReadSingle(), r.ReadSingle());

        public static void Write(this BinaryWriter w, Rgba c) { w.Write(c.R); w.Write(c.G); w.Write(c.B); w.Write(c.A); }
        public static Rgba ReadRgba(this BinaryReader r) => new Rgba(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }

    public class LobbyEntry
    {
        public int Id;
        public string Name;
        public Role Role;
    }

    public enum AppScreen { Menu, Lobby, Game }

    /// Connection and lobby handling. The match itself lives in Game.
    public class Session
    {
        public const int DefaultPort = 7777;
        public const int MaxPlayers = 2;

        public NetPeer Net { get; private set; }
        public bool IsHost => Net != null && Net.IsHost;
        public bool Connecting { get; private set; }
        public int MyId = 1;
        public AppScreen Screen = AppScreen.Menu;
        public string Status = "";
        public int Port = DefaultPort;
        public readonly SortedDictionary<int, LobbyEntry> Lobby = new SortedDictionary<int, LobbyEntry>();
        public readonly Game Game;

        // Testing helpers (see the README's command-line flags).
        public int AutoStart;
        public int StartWave = 1;

        string joinName = "Player";
        int joinRole = 255;

        public Session()
        {
            Game = new Game(this);
        }

        public static string CleanName(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length > 16)
                name = name.Substring(0, 16);
            return name.Length > 0 ? name : "Player";
        }

        public bool Host(int port, string name, Role role)
        {
            Leave("");
            try
            {
                Net = NetPeer.Host(port);
                Net.MaxClients = MaxPlayers - 1;
            }
            catch (SocketException e)
            {
                Status = "Could not host on port " + port + " (" + e.Message + "). Is another copy already hosting?";
                Net = null;
                return false;
            }
            Port = port;
            MyId = 1;
            Lobby[1] = new LobbyEntry { Id = 1, Name = CleanName(name), Role = role };
            Screen = AppScreen.Lobby;
            return true;
        }

        /// role: preferred role, or null to take the first one nobody has.
        public void Join(string address, int port, string name, Role? role)
        {
            Leave("");
            address = (address ?? "").Trim();
            try
            {
                Net = NetPeer.Join(address, port);
            }
            catch (Exception e)
            {
                Status = "Could not start connecting: " + e.Message;
                Net = null;
                return;
            }
            Port = port;
            joinName = CleanName(name);
            joinRole = role.HasValue ? (int)role.Value : 255;
            Connecting = true;
            Status = "Connecting to " + address + ":" + port + "...";
        }

        public void Leave(string status)
        {
            Game.Stop();
            if (Net != null)
                Net.Close();
            Net = null;
            Connecting = false;
            Lobby.Clear();
            Screen = AppScreen.Menu;
            Status = status;
        }

        public void Poll()
        {
            if (Net == null)
                return;
            foreach (var ev in new List<NetEvent>(Net.Poll()))
            {
                if (Net == null)
                    break;
                if (IsHost)
                    HandleHostEvent(ev);
                else
                    HandleClientEvent(ev);
            }
        }

        // --- Sending -----------------------------------------------------------

        public void SendToHost(byte[] msg)
        {
            if (Net != null && !IsHost)
                Net.Send(1, msg);
        }

        public void Broadcast(byte[] msg)
        {
            if (Net != null && IsHost)
                Net.Broadcast(msg);
        }

        // --- Host side ---------------------------------------------------------

        void HandleHostEvent(NetEvent ev)
        {
            switch (ev.Type)
            {
                case NetEventType.Data:
                    HandleHostMessage(ev.Peer, ev.Data);
                    break;
                case NetEventType.Disconnected:
                    Lobby.Remove(ev.Peer);
                    Game.OnPeerLeft(ev.Peer);
                    BroadcastLobby();
                    break;
            }
        }

        void HandleHostMessage(int peer, byte[] data)
        {
            var r = new BinaryReader(new MemoryStream(data));
            var type = (Msg)r.ReadByte();
            switch (type)
            {
                case Msg.Hello:
                {
                    string name = r.ReadString();
                    int wanted = r.ReadByte();
                    if (Game.Running || Lobby.Count >= MaxPlayers)
                    {
                        Net.Kick(peer);
                        return;
                    }
                    Lobby[peer] = new LobbyEntry { Id = peer, Name = CleanName(name), Role = PickRole(wanted) };
                    var w = Proto.Begin(Msg.Welcome);
                    w.Write(peer);
                    Net.Send(peer, Proto.End(w));
                    BroadcastLobby();
                    if (AutoStart > 0 && Lobby.Count >= AutoStart)
                        StartMatch();
                    break;
                }
                case Msg.Input:
                    Game.OnInput(peer, r);
                    break;
                case Msg.Ability:
                    Game.HostAbility(peer);
                    break;
                case Msg.RoleRequest:
                    SetRole(peer, r.ReadByte());
                    break;
            }
        }

        Role PickRole(int wanted)
        {
            if (wanted >= 0 && wanted < Defs.Roles.Length)
                return (Role)wanted;
            for (int i = 0; i < Defs.Roles.Length; i++)
            {
                bool taken = false;
                foreach (var e in Lobby.Values)
                    taken |= (int)e.Role == i;
                if (!taken)
                    return (Role)i;
            }
            return Role.Dps;
        }

        void SetRole(int id, int role)
        {
            LobbyEntry e;
            if (role < 0 || role >= Defs.Roles.Length || !Lobby.TryGetValue(id, out e))
                return;
            e.Role = (Role)role;
            BroadcastLobby();
        }

        void BroadcastLobby()
        {
            var w = Proto.Begin(Msg.Lobby);
            w.Write((byte)Lobby.Count);
            foreach (var e in Lobby.Values)
            {
                w.Write(e.Id);
                w.Write(e.Name);
                w.Write((byte)e.Role);
            }
            Broadcast(Proto.End(w));
        }

        public void StartMatch()
        {
            if (IsHost)
                Game.HostStart();
        }

        public void ReturnToLobby()
        {
            if (!IsHost)
                return;
            Broadcast(Proto.End(Proto.Begin(Msg.ToLobby)));
            Game.Stop();
            Screen = AppScreen.Lobby;
        }

        public void RequestRole(Role role)
        {
            if (IsHost)
            {
                SetRole(1, (int)role);
            }
            else
            {
                var w = Proto.Begin(Msg.RoleRequest);
                w.Write((byte)role);
                SendToHost(Proto.End(w));
            }
        }

        // --- Client side -------------------------------------------------------

        void HandleClientEvent(NetEvent ev)
        {
            switch (ev.Type)
            {
                case NetEventType.Connected:
                {
                    Connecting = false;
                    Status = "";
                    var w = Proto.Begin(Msg.Hello);
                    w.Write(joinName);
                    w.Write((byte)joinRole);
                    SendToHost(Proto.End(w));
                    break;
                }
                case NetEventType.ConnectFailed:
                    Leave("Could not connect. Check the address and port, and that the host is in the lobby.");
                    break;
                case NetEventType.Disconnected:
                    Leave("Disconnected from the host (the game may already have been in progress).");
                    break;
                case NetEventType.Data:
                    HandleClientMessage(ev.Data);
                    break;
            }
        }

        void HandleClientMessage(byte[] data)
        {
            var r = new BinaryReader(new MemoryStream(data));
            var type = (Msg)r.ReadByte();
            switch (type)
            {
                case Msg.Welcome:
                    MyId = r.ReadInt32();
                    break;
                case Msg.Lobby:
                {
                    Lobby.Clear();
                    int n = r.ReadByte();
                    for (int i = 0; i < n; i++)
                    {
                        var e = new LobbyEntry { Id = r.ReadInt32(), Name = r.ReadString(), Role = (Role)r.ReadByte() };
                        Lobby[e.Id] = e;
                    }
                    if (!Game.Running)
                        Screen = AppScreen.Lobby;
                    break;
                }
                case Msg.ToLobby:
                    Game.Stop();
                    Screen = AppScreen.Lobby;
                    break;
                default:
                    Game.OnHostMessage(type, r);
                    break;
            }
        }
    }
}
