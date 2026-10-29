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
        // added later; kept at the end so existing values don't shift
        Pick, Upgrades, Reroll, WeaponRequest, AbilityRequest, KitRequest, Pause, BulletSync, RunStats, Steer, ReadyRequest, Ping, Pong,
        DevStats, SetKey, Sound,
        Practice, PracticeEdit, PracticeWave,
        PathOffer, PathChosen, PathPatch,  // divergent paths: the two on offer; the one the host took (Paths.cs)  // the lobby's Practice Range setup; a client's edit to it; the practice wave being fought
        Teleport,  // host -> all: a player is moved (the Ninja's Substitution); only that player's own game applies it
        Shop, ShopGift,  // the Bazaar: a player's shop action (client -> host); gold given to a teammate (host -> all) (Shop.cs)

        // Fixed value so every version can understand it, whatever else changes.
        Reject = 200,
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
        public Kit Kit;       // Soldier, Mage or Summoner
        public byte Weapon;   // index into Defs.Weapon(Role, Kit, i)
        public byte Ability;  // index into Upgrades.RoleAbility(Role, Kit, i)
        public bool Ready;    // clicked Ready in the lobby; the match only starts once everyone has
    }

    public enum AppScreen { Menu, Lobby, Game }

    /// Connection and lobby handling. The match itself lives in Game.
    public class Session
    {
        public const int DefaultPort = 7777;

        /// Bump this whenever the messages the two games send each other change.
        /// Players on different versions can't play together; the host turns them away with a message.
        public const int ProtocolVersion = 67;
        public static string VersionText => "Version " + ProtocolVersion;

        const double WelcomeTimeout = 6.0;  // seconds to wait for the host to answer our hello
        public const int MaxPlayers = 3;

        public NetPeer Net { get; private set; }
        public bool IsHost => Net != null && Net.IsHost;
        public bool Connecting { get; private set; }
        public int MyId = 1;
        public AppScreen Screen = AppScreen.Menu;
        public string Status = "";
        public int Port = DefaultPort;
        public readonly SortedDictionary<int, LobbyEntry> Lobby = new SortedDictionary<int, LobbyEntry>();
        public readonly Game Game;

        // Lobby mode (host decides; everyone sees it). In Practice Range mode the match plays Plan's waves
        // instead of a normal run. The host can let the others edit the plan too.
        public bool PracticeMode;
        public PracticePlan Plan = PracticePlan.Default();
        public bool OthersCanEdit;
        public string PlanEditor = "";  // who made the last change
        public int PlanEditorSel;       // the wave they were working on
        public int PlanRevision;        // bumps on every change (the UI uses it to notice edits)
        public RunMod Modifiers;        // run modifiers (the lobby's Modifiers tab; the host picks)

        // Testing helpers (see the README's command-line flags).
        public int AutoStart;
        public int Version = ProtocolVersion;  // tests can pretend to be another version
        public int StartWave = 1;

        string joinName = "Player";
        int joinRole = 255;
        int joinWeapon, joinAbility, joinKit;
        DateTime helloSentAt;
        bool awaitingWelcome;

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

        /// A private match with no network: this game hosts and nobody can join (the loadout preview, Showcase).
        public void HostOffline(Role role, Kit kit, int weapon, int ability)
        {
            Leave("");
            Net = NetPeer.Offline();
            MyId = 1;
            Lobby[1] = new LobbyEntry { Id = 1, Name = "Preview", Role = role, Kit = kit, Weapon = (byte)ClampWeapon(weapon), Ability = (byte)ClampAbility(ability) };
        }

        public bool Host(int port, string name, Role role, int weapon = 0, int ability = 0, Kit kit = Kit.Soldier)
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
            Lobby[1] = new LobbyEntry { Id = 1, Name = CleanName(name), Role = role, Kit = kit, Weapon = (byte)ClampWeapon(weapon), Ability = (byte)ClampAbility(ability) };
            Screen = AppScreen.Lobby;
            return true;
        }

        /// role: preferred role, or null to take the first one nobody has.
        public void Join(string address, int port, string name, Role? role, int weapon = 0, int ability = 0, Kit kit = Kit.Soldier)
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
            joinWeapon = ClampWeapon(weapon);
            joinAbility = ClampAbility(ability);
            joinKit = (int)kit;
            Connecting = true;
            Status = "Connecting to " + address + ":" + port + "...";
        }

        public void Leave(string status)
        {
            Game.Stop();
            Game.Practice = null;
            PracticeMode = false;
            OthersCanEdit = false;
            Modifiers = RunMod.None;
            if (Net != null)
                Net.Close();
            Net = null;
            Connecting = false;
            awaitingWelcome = false;
            Lobby.Clear();
            Screen = AppScreen.Menu;
            Status = status;
        }

        public void Poll()
        {
            if (Net == null)
                return;
            if (awaitingWelcome && (DateTime.UtcNow - helloSentAt).TotalSeconds > WelcomeTimeout)
            {
                Leave("Connected, but the host didn't answer. Make sure you're both on the same version of the game " +
                      "(it's shown at the bottom of the main menu; you're on " + VersionText + ").");
                return;
            }
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
                    try
                    {
                        HandleHostMessage(ev.Peer, ev.Data);
                    }
                    catch (Exception e) when (e is EndOfStreamException || e is IOException || e is ArgumentException)
                    {
                        // Garbled or old-format message: most likely a different version of the game.
                        Reject(ev.Peer, "Couldn't understand your game. Make sure you're both on the same version (the host is on " + VersionText + ").");
                        Status = "Someone tried to join with a different version of the game. You're on " + VersionText + ".";
                    }
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
                    int version = r.ReadInt32();
                    if (version != Version)
                    {
                        // Builds from before version checks don't send a number, so this reads as garbage.
                        string theirs = version > 0 && version < 10000 ? "version " + version : "an older version";
                        Reject(peer, "Version mismatch: the host is on " + VersionText + " and you're on " + theirs +
                                     ". Both players need the same build of the game.");
                        Status = "A friend tried to join on " + theirs + ", but you're on " + VersionText + ". You both need the same build.";
                        return;
                    }
                    string name = r.ReadString();
                    int wanted = r.ReadByte();
                    int weapon = ClampWeapon(r.ReadByte());
                    int ability = ClampAbility(r.ReadByte());
                    var kit = ClampKit(r.ReadByte());
                    if (Game.Running || Lobby.Count >= MaxPlayers)
                    {
                        Reject(peer, Game.Running ? "That game has already started. Ask the host to go back to the lobby, then join."
                            : "That game is full (" + MaxPlayers + " players max).");
                        return;
                    }
                    Lobby[peer] = new LobbyEntry { Id = peer, Name = CleanName(name), Role = PickRole(wanted), Kit = kit, Weapon = (byte)weapon, Ability = (byte)ability };
                    var w = Proto.Begin(Msg.Welcome);
                    w.Write(peer);
                    Net.Send(peer, Proto.End(w));
                    BroadcastLobby();
                    BroadcastPractice();
                    if (AutoStart > 0 && Lobby.Count >= AutoStart)
                        StartMatch();
                    break;
                }
                case Msg.Input:
                    Game.OnInput(peer, r);
                    break;
                case Msg.Ability:
                    Game.OnAbility(peer, r);
                    break;
                case Msg.Pick:
                    Game.OnPick(peer, r);
                    break;
                case Msg.Shop:
                    Game.OnShop(peer, r);
                    break;
                case Msg.SetKey:
                    Game.OnSetKey(peer, r);
                    break;
                case Msg.Pause:
                    Game.OnPause(peer, r);
                    break;
                case Msg.RoleRequest:
                    SetRole(peer, r.ReadByte());
                    break;
                case Msg.WeaponRequest:
                    SetWeapon(peer, r.ReadByte());
                    break;
                case Msg.AbilityRequest:
                    SetAbility(peer, r.ReadByte());
                    break;
                case Msg.KitRequest:
                    SetKit(peer, r.ReadByte());
                    break;
                case Msg.ReadyRequest:
                    SetReady(peer, r.ReadBoolean());
                    break;
                case Msg.Reroll:
                    Game.HostReroll(peer);
                    break;
                case Msg.PracticeEdit:
                {
                    string text = r.ReadString();
                    int sel = r.ReadInt32();
                    LobbyEntry who;
                    if (!OthersCanEdit || Game.Running || !Lobby.TryGetValue(peer, out who))
                    {
                        BroadcastPractice();  // put their copy back the way it is
                        break;
                    }
                    List<string> problems;
                    var plan = PracticePlan.Parse(text, out problems);
                    if (plan.Waves.Count > 0)
                        ApplyPractice(plan, who.Name, sel);
                    break;
                }
                case Msg.Ping:  // echo it straight back so the client can time the round trip
                {
                    var w = Proto.Begin(Msg.Pong);
                    w.Write(r.ReadInt64());
                    Net.Send(peer, Proto.End(w));
                    break;
                }
            }
        }

        void Reject(int peer, string reason)
        {
            var w = Proto.Begin(Msg.Reject);
            w.Write(reason);
            Net.Send(peer, Proto.End(w));
            Net.Kick(peer);
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
            if (e.Role != (Role)role)
            {
                e.Weapon = 0;  // weapons and abilities differ per role
                e.Ability = 0;
            }
            e.Role = (Role)role;
            e.Ready = false;  // changing your loadout takes back your Ready
            BroadcastLobby();
        }

        static int ClampWeapon(int weapon) => weapon >= 0 && weapon < Defs.WeaponsPerRole ? weapon : 0;
        static int ClampAbility(int ability) => ability >= 0 && ability < Upgrades.RoleAbilityChoices ? ability : 0;
        static Kit ClampKit(int kit) => kit >= 0 && kit <= (int)Kit.BloodMage ? (Kit)kit : Kit.Soldier;

        void SetKit(int id, int kit)
        {
            LobbyEntry e;
            if (!Lobby.TryGetValue(id, out e))
                return;
            if (e.Kit != ClampKit(kit))
            {
                e.Weapon = 0;  // each kit has its own weapons and abilities
                e.Ability = 0;
            }
            e.Kit = ClampKit(kit);
            e.Ready = false;
            BroadcastLobby();
        }

        void SetAbility(int id, int ability)
        {
            LobbyEntry e;
            if (!Lobby.TryGetValue(id, out e))
                return;
            e.Ability = (byte)ClampAbility(ability);
            e.Ready = false;
            BroadcastLobby();
        }

        void SetWeapon(int id, int weapon)
        {
            LobbyEntry e;
            if (!Lobby.TryGetValue(id, out e))
                return;
            e.Weapon = (byte)ClampWeapon(weapon);
            e.Ready = false;
            BroadcastLobby();
        }

        void SetReady(int id, bool ready)
        {
            LobbyEntry e;
            if (!Lobby.TryGetValue(id, out e) || Game.Running)
                return;
            e.Ready = ready;
            BroadcastLobby();
        }

        /// Everyone in the lobby has clicked Ready.
        public bool AllReady
        {
            get
            {
                if (Lobby.Count == 0)
                    return false;
                foreach (var e in Lobby.Values)
                    if (!e.Ready)
                        return false;
                return true;
            }
        }

        public void RequestReady(bool ready)
        {
            if (IsHost)
            {
                SetReady(1, ready);
            }
            else
            {
                var w = Proto.Begin(Msg.ReadyRequest);
                w.Write(ready);
                SendToHost(Proto.End(w));
            }
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
                w.Write((byte)e.Kit);
                w.Write(e.Weapon);
                w.Write(e.Ability);
                w.Write(e.Ready);
            }
            w.Write((int)Modifiers);
            Broadcast(Proto.End(w));
        }

        // --- Practice Range (lobby) ----------------------------------------------

        /// Host: switch the lobby between a normal run and the Practice Range.
        public void SetPracticeMode(bool on)
        {
            if (!IsHost || Game.Running)
                return;
            PracticeMode = on;
            foreach (var e in Lobby.Values)
                e.Ready = false;  // what you're readying up for changed
            BroadcastLobby();
            BroadcastPractice();
        }

        /// Host: turn a run modifier on or off. Everyone's Ready is taken back, since the run changed.
        public void SetModifier(RunMod mod, bool on)
        {
            if (!IsHost || Game.Running)
                return;
            Modifiers = on ? Modifiers | mod : Modifiers & ~mod;
            foreach (var e in Lobby.Values)
                e.Ready = false;
            BroadcastLobby();
        }

        /// Host: whether the other players can edit the practice waves.
        public void SetOthersCanEdit(bool on)
        {
            if (!IsHost)
                return;
            OthersCanEdit = on;
            BroadcastPractice();
        }

        /// Can this player change the practice waves right now?
        public bool CanEditPractice => !Game.Running && (IsHost || OthersCanEdit);

        /// You changed the practice plan (Plan, edited in place): the host shares it; a client sends it to the
        /// host, who shares it with everyone (or puts it back if editing isn't allowed).
        public void PracticeEdited(int sel)
        {
            if (!CanEditPractice)
                return;
            LobbyEntry me;
            string name = Lobby.TryGetValue(MyId, out me) ? me.Name : "";
            if (IsHost)
            {
                ApplyPractice(Plan, name, sel);
                return;
            }
            PlanEditor = name;
            PlanEditorSel = sel;
            PlanRevision++;
            var w = Proto.Begin(Msg.PracticeEdit);
            w.Write(Plan.ToText());
            w.Write(sel);
            SendToHost(Proto.End(w));
        }

        /// Host: replace the plan (a load, or a client's edit) and share it.
        public void ApplyPractice(PracticePlan plan, string editor, int sel)
        {
            if (!IsHost)
                return;
            Plan = plan;
            PlanEditor = editor;
            PlanEditorSel = sel;
            PlanRevision++;
            BroadcastPractice();
        }

        void BroadcastPractice()
        {
            var w = Proto.Begin(Msg.Practice);
            w.Write(PracticeMode);
            w.Write(OthersCanEdit);
            w.Write(Plan.ToText());
            w.Write(PlanEditor ?? "");
            w.Write(PlanEditorSel);
            Broadcast(Proto.End(w));
        }

        /// From the lobby the match only starts once everyone is ready ("Play again" after
        /// a run starts straight away; AutoStart is for tests).
        public void StartMatch()
        {
            if (!IsHost)
                return;
            if (Screen == AppScreen.Lobby && AutoStart == 0 && !AllReady)
                return;
            foreach (var e in Lobby.Values)
                e.Ready = false;  // ready up again next time you're back in the lobby
            Game.Practice = PracticeMode ? Plan.Copy() : null;  // a copy, so edits don't change a run in progress
            Game.Mods = Modifiers;
            Game.HostStart();
        }

        public void ReturnToLobby()
        {
            if (!IsHost)
                return;
            Broadcast(Proto.End(Proto.Begin(Msg.ToLobby)));
            Game.Stop();
            Game.Practice = null;
            Screen = AppScreen.Lobby;
            foreach (var e in Lobby.Values)
                e.Ready = false;
            BroadcastLobby();
        }

        public void RequestWeapon(int weapon)
        {
            if (IsHost)
            {
                SetWeapon(1, weapon);
            }
            else
            {
                var w = Proto.Begin(Msg.WeaponRequest);
                w.Write((byte)weapon);
                SendToHost(Proto.End(w));
            }
        }

        public void RequestKit(Kit kit)
        {
            if (IsHost)
            {
                SetKit(1, (int)kit);
            }
            else
            {
                var w = Proto.Begin(Msg.KitRequest);
                w.Write((byte)kit);
                SendToHost(Proto.End(w));
            }
        }

        public void RequestAbility(int ability)
        {
            if (IsHost)
            {
                SetAbility(1, ability);
            }
            else
            {
                var w = Proto.Begin(Msg.AbilityRequest);
                w.Write((byte)ability);
                SendToHost(Proto.End(w));
            }
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
                    w.Write(Version);
                    w.Write(joinName);
                    w.Write((byte)joinRole);
                    w.Write((byte)joinWeapon);
                    w.Write((byte)joinAbility);
                    w.Write((byte)joinKit);
                    SendToHost(Proto.End(w));
                    awaitingWelcome = true;
                    helloSentAt = DateTime.UtcNow;
                    break;
                }
                case NetEventType.ConnectFailed:
                    Leave("Could not connect. Check the address and port, and that the host is in the lobby.");
                    break;
                case NetEventType.Disconnected:
                    Leave("Disconnected from the host (the game may already have been in progress).");
                    break;
                case NetEventType.Data:
                    try
                    {
                        HandleClientMessage(ev.Data);
                    }
                    catch (Exception e) when (e is EndOfStreamException || e is IOException || e is ArgumentException)
                    {
                        Leave("Couldn't understand the host's game. Make sure you're both on the same version (you're on " + VersionText + ").");
                    }
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
                    awaitingWelcome = false;
                    break;
                case Msg.Reject:
                    Leave(r.ReadString());
                    break;
                case Msg.Lobby:
                {
                    Lobby.Clear();
                    int n = r.ReadByte();
                    for (int i = 0; i < n; i++)
                    {
                        var e = new LobbyEntry { Id = r.ReadInt32(), Name = r.ReadString(), Role = (Role)r.ReadByte(), Kit = (Kit)r.ReadByte(), Weapon = r.ReadByte(), Ability = r.ReadByte() };
                        e.Ready = r.ReadBoolean();
                        Lobby[e.Id] = e;
                    }
                    Modifiers = (RunMod)r.ReadInt32();
                    if (!Game.Running)
                        Screen = AppScreen.Lobby;
                    break;
                }
                case Msg.ToLobby:
                    Game.Stop();
                    Game.Practice = null;
                    Screen = AppScreen.Lobby;
                    break;
                case Msg.Practice:
                {
                    PracticeMode = r.ReadBoolean();
                    OthersCanEdit = r.ReadBoolean();
                    List<string> problems;
                    Plan = PracticePlan.Parse(r.ReadString(), out problems);
                    PlanEditor = r.ReadString();
                    PlanEditorSel = r.ReadInt32();
                    PlanRevision++;
                    break;
                }
                case Msg.Start:
                    Game.Practice = PracticeMode ? Plan.Copy() : null;  // for the HUD: which waves this practice has
                    Game.Mods = Modifiers;
                    Game.OnHostMessage(type, r);
                    break;
                default:
                    Game.OnHostMessage(type, r);
                    break;
            }
        }
    }
}
