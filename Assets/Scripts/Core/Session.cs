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
        WeaponDash,  // client -> host: a weapon moved its player (the Bo Staff's vault, the Kusarigama's reel; Ninja.cs)
        ReverseEnemyBullets,  // host -> all: the Singularity's white hole turns every enemy bullet round (Singularity.cs)
        Record,      // host -> all: a boss beaten, the run ended, or a resumed run's state, for each player's profile (RunSaves.cs)
        SeatRequest, // client -> host: the saved seat this player wants when continuing a run (Session.cs)
        CooldownCut, // host -> all: take a share off one player's ability cooldown (a caught Scythe Throw); only that player's own game applies it (ReaperLoot.cs)
        RunStatsLive, // host -> all: the run's tallies so far, about once a second (RunStats.cs; the Run stats screen)
        Knock,       // host -> all: a player is knocked back (a Riot Shield's bash); only that player's own game moves them (WardenFaction.cs)
        Loot,         // client -> host: take or discard the chest item you're standing on (Props.cs)
        Recall,       // host -> all: everyone's beamed out (the Game screen's Recall.cs), then ToLobby follows
        Cosmetics,    // client -> host: this player's rank and equipped frame, for the lobby (Rank.cs; the host answers with a Lobby)
        LobbyMove,    // client -> host: where this player's character stands in the lobby's Hangout room (LobbyAvatars.cs)
        LobbyPos,     // host -> all: where everyone's character stands in the Hangout room
        Emote,        // client -> host: the emote number to play; host -> all: (player id, emote number) to show over that player (Session.cs, Game/EmoteLooks.cs)
        Chat,         // client -> host: a line of text; host -> all: (player id, name, text) to add to everyone's chat box (Session.cs, Game/ChatBox.cs)
        PingMark,     // client -> host: ping kind and arena position; host -> all: (owner id, kind, position, target enemy id or -1) to show (Pings.cs, Game/PingLooks.cs)

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
        public int Seat = -1; // continuing a saved run: which of its seats this player takes (Session.Resume)
        public int Rank = 1;  // the player's account rank (Rank.cs), cosmetic and client-reported (the host clamps it)
        public byte Frame;    // their equipped frame: 0 none, else Rank.FrameIndex
        public byte Trail;    // their equipped trail: 0 none, else Rank.TrailIndex (drawn behind them in the match)
        public byte NameFx;   // their equipped name effect: 0 none, else Rank.NameFxIndex (how their name is drawn, Game/NameEffects.cs)
        public byte CardBg;   // their equipped card background: 0 none, else Rank.CardBgIndex (the fill of their party card)
    }

    public enum AppScreen { Menu, Lobby, Game }

    /// Connection and lobby handling. The match itself lives in Game.
    public partial class Session
    {
        public const int DefaultPort = 7777;

        /// Bump this whenever the messages the two games send each other change.
        /// Players on different versions can't play together; the host turns them away with a message.
        public const int ProtocolVersion = 146;
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
        public bool HuntMode;                       // a Boss Hunt instead (Hunts.cs): HuntBoss's
        public WaveSet DesignTest;                  // host: designed waves to play in the Practice Range (the F1 wave editor's Test)
        public EnemyType HuntBoss = EnemyType.Boss;
        public string PlanEditor = "";  // who made the last change
        public int PlanEditorSel;       // the wave they were working on
        public int PlanRevision;        // bumps on every change (the UI uses it to notice edits)
        public RunMod Modifiers;        // run modifiers (the lobby's Modifiers tab; the host picks)
        public Difficulty Diff = new Difficulty();  // the run's difficulty (the lobby's Difficulty tab; the host picks)
        public RunSave Resume;          // continuing a saved run: the host's save (clients get its seats in the lobby message)

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
            SyncOwnCosmetics();  // your rank, frame and trail show from the start (not only after the first lobby broadcast, e.g. pressing ready)
            SyncAvatars();       // (and your Hangout character stands in the room from the start too)
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
            Game.Hunt = null;
            PracticeMode = false;
            HuntMode = false;
            DesignTest = null;
            Game.Design = null;
            OthersCanEdit = false;
            Modifiers = RunMod.None;
            Diff = new Difficulty();
            Resume = null;
            if (Net != null)
                Net.Close();
            Net = null;
            Connecting = false;
            awaitingWelcome = false;
            Lobby.Clear();
            Avatars.Clear();
            Emotes.Clear();
            emoteLast.Clear();
            Pings.Clear();
            pingLast.Clear();
            Chat.Clear();
            chatLast.Clear();
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
            SendAvatars();
        }

        // --- Emotes ------------------------------------------------------------
        // A short bubble over a player (Rank.cs lists them; Game/EmoteLooks.cs draws them). Anyone can play one their rank has,
        // in the lobby's Hangout and in a match, with a short pause between (the host keeps the pause for everyone).

        public const float EmoteCooldown = 2f, EmoteHangoutCooldown = 0.75f, EmoteLife = 2.4f;
        public struct EmoteShow { public byte Index; public double At; }
        /// What's showing now, by player id (it lapses after EmoteLife; Clock is when it started).
        public readonly Dictionary<int, EmoteShow> Emotes = new Dictionary<int, EmoteShow>();
        readonly Dictionary<int, double> emoteLast = new Dictionary<int, double>();
        double emoteReadyAt;
        static readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        public static double Clock => clock.Elapsed.TotalSeconds;

        public float EmotePause => Screen == AppScreen.Game ? EmoteCooldown : EmoteHangoutCooldown;
        /// Seconds until you can play another (0 = ready).
        public float EmoteWait => (float)Math.Max(0.0, emoteReadyAt - Clock);

        /// Seconds into player `id`'s emote, and which, or false when none is showing.
        public bool EmoteOf(int id, out int index, out float age)
        {
            EmoteShow s;
            index = 0;
            age = 0f;
            if (!Emotes.TryGetValue(id, out s))
                return false;
            age = (float)(Clock - s.At);
            if (age < 0f || age >= EmoteLife)
                return false;
            index = s.Index;
            return true;
        }

        /// Play emote number `index` (Rank.EmoteIndex) as this player. False: not yours yet, or still pausing.
        public bool SendEmote(int index)
        {
            if (Net == null || index < 1 || index > Arena.Rank.EmoteCount || EmoteWait > 0f || !Arena.Rank.Owns(Game.Profile.RankNow, Arena.Rank.EmoteId(index)))
                return false;
            emoteReadyAt = Clock + EmotePause;
            if (IsHost)
                HostEmote(MyId, index);
            else
            {
                var w = Proto.Begin(Msg.Emote);
                w.Write((byte)index);
                SendToHost(Proto.End(w));
            }
            return true;
        }

        void HostEmote(int id, int index)
        {
            LobbyEntry e;
            if (!Lobby.TryGetValue(id, out e) || index < 1 || index > Arena.Rank.EmoteCount || !Arena.Rank.Owns(e.Rank, Arena.Rank.EmoteId(index)))
                return;
            double last;
            if (emoteLast.TryGetValue(id, out last) && Clock - last < EmotePause * 0.8)  // (a little slack for the trip)
                return;
            emoteLast[id] = Clock;
            Emotes[id] = new EmoteShow { Index = (byte)index, At = Clock };
            var w = Proto.Begin(Msg.Emote);
            w.Write(id);
            w.Write((byte)index);
            Broadcast(Proto.End(w));
        }

        // --- Chat --------------------------------------------------------------
        // A text chat in a match (Game/ChatBox.cs). A line goes to the host, which trims it, keeps about one a second per player
        // and passes it to everyone (itself included); the last few lines are kept in Chat.

        public const int ChatMaxLength = 120, ChatKeep = 24;
        public const double ChatGap = 0.8;
        public struct ChatLine { public int Id; public string Name, Text; public double At; }
        public readonly List<ChatLine> Chat = new List<ChatLine>();
        readonly Dictionary<int, double> chatLast = new Dictionary<int, double>();

        /// Drop control characters, collapse spaces and cut to ChatMaxLength; "" when nothing is left.
        public static string CleanChat(string text)
        {
            if (text == null)
                return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in text)
            {
                if (char.IsControl(c))
                    continue;
                if (c == ' ' && (sb.Length == 0 || sb[sb.Length - 1] == ' '))
                    continue;
                sb.Append(c);
                if (sb.Length >= ChatMaxLength)
                    break;
            }
            return sb.ToString().TrimEnd();
        }

        /// Say something as this player. False: nothing to say.
        public bool SendChat(string text)
        {
            text = CleanChat(text);
            if (text.Length == 0)
                return false;
            if (IsHost || Net == null)
                HostChat(MyId, text);
            else
            {
                var w = Proto.Begin(Msg.Chat);
                w.Write(text);
                SendToHost(Proto.End(w));
            }
            return true;
        }

        void AddChat(int id, string name, string text)
        {
            Chat.Add(new ChatLine { Id = id, Name = name, Text = text, At = Clock });
            if (Chat.Count > ChatKeep)
                Chat.RemoveAt(0);
        }

        void HostChat(int id, string text)
        {
            text = CleanChat(text);
            if (text.Length == 0)
                return;
            double last;
            if (chatLast.TryGetValue(id, out last) && Clock - last < ChatGap)
                return;
            chatLast[id] = Clock;
            string name = "?";
            Player p;
            LobbyEntry e;
            if (Game.Players.TryGetValue(id, out p))
                name = p.Name;
            else if (Lobby.TryGetValue(id, out e))
                name = e.Name;
            AddChat(id, name, text);
            var w = Proto.Begin(Msg.Chat);
            w.Write(id);
            w.Write(name);
            w.Write(text);
            Broadcast(Proto.End(w));
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
                    var joined = new LobbyEntry { Id = peer, Name = CleanName(name), Role = PickRole(wanted), Kit = kit, Weapon = (byte)weapon, Ability = (byte)ability };
                    if (Resume != null)
                    {
                        // Continuing a saved run: you take back the seat with your name (whoever sat in it moves to
                        // another free seat, or out if there's none), or else the first one free.
                        int seat = Game.SeatFor(Resume, joined.Name);
                        LobbyEntry sitter = null;
                        foreach (var e in Lobby.Values)
                            if (seat >= 0 && e.Seat == seat)
                                sitter = e;
                        if (sitter != null && string.Equals(sitter.Name, joined.Name, StringComparison.OrdinalIgnoreCase))
                            seat = -1;  // someone of the same name already has it
                        if (seat < 0)
                            seat = FirstFreeSeat();
                        else if (sitter != null)
                        {
                            int other = FirstFreeSeat();
                            if (other >= 0)
                                TakeSeat(sitter, other);
                            else if (sitter.Id == MyId)
                                seat = -1;  // never put the host out of their own lobby: the newcomer is turned away instead
                            else
                            {
                                Lobby.Remove(sitter.Id);
                                Reject(sitter.Id, "That seat in the saved run belongs to " + joined.Name + ", who just joined, and there's no other seat free.");
                            }
                        }
                        if (seat < 0)
                        {
                            Reject(peer, "The host is continuing a saved run, and all of its seats are taken.");
                            return;
                        }
                        TakeSeat(joined, seat);
                    }
                    Lobby[peer] = joined;
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
                case Msg.Loot:
                    Game.OnLoot(peer, r);
                    break;
                case Msg.SetKey:
                    Game.OnSetKey(peer, r);
                    break;
                case Msg.WeaponDash:
                    Game.OnWeaponDash(peer, r);
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
                case Msg.SeatRequest:
                    SetSeat(peer, r.ReadByte());
                    break;
                case Msg.LobbyMove:
                    HostAvatarMove(peer, r);
                    break;
                case Msg.Emote:
                    HostEmote(peer, r.ReadByte());
                    break;
                case Msg.PingMark:
                {
                    byte kind = r.ReadByte();
                    float px = r.ReadSingle(), py = r.ReadSingle();
                    HostPing(peer, (PingKind)kind, new V2(px, py));
                    break;
                }
                case Msg.Chat:
                    HostChat(peer, r.ReadString());
                    break;
                case Msg.Cosmetics:
                {
                    int rank = Math.Max(1, Math.Min(Arena.Rank.MaxRank, r.ReadInt32()));
                    int frame = r.ReadByte(), trail = r.ReadByte(), card = r.ReadByte(), namefx = r.ReadByte();
                    LobbyEntry who;
                    if (Lobby.TryGetValue(peer, out who))
                    {
                        who.Rank = rank;
                        who.Frame = Arena.Rank.Owns(rank, Arena.Rank.FrameId(frame)) ? (byte)frame : (byte)0;  // only a frame or trail their rank has
                        who.Trail = Arena.Rank.Owns(rank, Arena.Rank.TrailId(trail)) ? (byte)trail : (byte)0;
                        who.CardBg = Arena.Rank.Owns(rank, Arena.Rank.CardBgId(card)) ? (byte)card : (byte)0;
                        who.NameFx = Arena.Rank.Owns(rank, Arena.Rank.NameFxId(namefx)) ? (byte)namefx : (byte)0;
                        BroadcastLobby();
                    }
                    break;
                }
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

        // --- Continuing a saved run -------------------------------------------------------------

        /// Host: open the lobby to continue a saved run. Your seat is the one with your name (or the first);
        /// friends who join take theirs. The run's modifiers come with it; loadouts are the seats'.
        public bool HostResume(RunSave save, int port, string name)
        {
            var first = save.Seats[0];
            if (!Host(port, name, first.Role, first.Weapon, first.Ability, first.Kit))
                return false;
            Resume = save;
            Modifiers = save.Mods;
            Diff = save.Diff.Copy();
            PracticeMode = false;
            HuntMode = false;
            int seat = Game.SeatFor(save, Lobby[1].Name);
            TakeSeat(Lobby[1], seat < 0 ? 0 : seat);
            BroadcastLobby();
            return true;
        }

        bool SeatTaken(int seat)
        {
            foreach (var e in Lobby.Values)
                if (e.Seat == seat)
                    return true;
            return false;
        }

        int FirstFreeSeat()
        {
            for (int i = 0; Resume != null && i < Resume.Seats.Count; i++)
                if (!SeatTaken(i))
                    return i;
            return -1;
        }

        void TakeSeat(LobbyEntry e, int seat)
        {
            var s = Resume.Seats[seat];
            e.Seat = seat;
            e.Role = s.Role;
            e.Kit = s.Kit;
            e.Weapon = (byte)s.Weapon;
            e.Ability = (byte)s.Ability;
            e.Ready = false;
        }

        /// Host: a player moves to another free seat of the saved run.
        void SetSeat(int id, int seat)
        {
            LobbyEntry e;
            if (Resume == null || Game.Running || !Lobby.TryGetValue(id, out e) || seat < 0 || seat >= Resume.Seats.Count || SeatTaken(seat))
                return;
            TakeSeat(e, seat);
            BroadcastLobby();
        }

        /// Pick a seat of the run being continued (the lobby's seat buttons).
        public void RequestSeat(int seat)
        {
            if (IsHost)
            {
                SetSeat(1, seat);
                return;
            }
            var w = Proto.Begin(Msg.SeatRequest);
            w.Write((byte)seat);
            SendToHost(Proto.End(w));
        }

        void SetRole(int id, int role)
        {
            LobbyEntry e;
            if (role < 0 || role >= Defs.Roles.Length || !Lobby.TryGetValue(id, out e) || Resume != null)
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
        static Kit ClampKit(int kit) => kit >= 0 && kit < Defs.KitCount ? (Kit)kit : Kit.Soldier;

        void SetKit(int id, int kit)
        {
            LobbyEntry e;
            if (!Lobby.TryGetValue(id, out e) || Resume != null)
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
            if (!Lobby.TryGetValue(id, out e) || Resume != null)
                return;
            e.Ability = (byte)ClampAbility(ability);
            e.Ready = false;
            BroadcastLobby();
        }

        void SetWeapon(int id, int weapon)
        {
            LobbyEntry e;
            if (!Lobby.TryGetValue(id, out e) || Resume != null)
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

        /// Host: put this player's own rank and frame (from the profile) into their lobby entry.
        void SyncOwnCosmetics()
        {
            LobbyEntry me;
            if (!IsHost || !Lobby.TryGetValue(MyId, out me))
                return;
            var p = Game.Profile;
            me.Rank = p.RankNow;
            me.Frame = Arena.Rank.Owns(me.Rank, p.Frame) ? Arena.Rank.FrameIndex(p.Frame) : (byte)0;
            me.Trail = Arena.Rank.Owns(me.Rank, p.Trail) ? Arena.Rank.TrailIndex(p.Trail) : (byte)0;
            me.CardBg = Arena.Rank.Owns(me.Rank, p.CardBg) ? Arena.Rank.CardBgIndex(p.CardBg) : (byte)0;
            me.NameFx = Arena.Rank.Owns(me.Rank, p.NameFx) ? Arena.Rank.NameFxIndex(p.NameFx) : (byte)0;
        }

        /// Tells the lobby this player's rank and equipped frame: the host refreshes its own entry, a client tells the
        /// host. Call it when the profile changes (a run ended, a frame was equipped).
        public void ShareCosmetics()
        {
            if (Net == null)
                return;
            if (IsHost)
            {
                BroadcastLobby();
                return;
            }
            var p = Game.Profile;
            var w = Proto.Begin(Msg.Cosmetics);
            w.Write(p.RankNow);
            w.Write(Arena.Rank.Owns(p.RankNow, p.Frame) ? Arena.Rank.FrameIndex(p.Frame) : (byte)0);
            w.Write(Arena.Rank.Owns(p.RankNow, p.Trail) ? Arena.Rank.TrailIndex(p.Trail) : (byte)0);
            w.Write(Arena.Rank.Owns(p.RankNow, p.CardBg) ? Arena.Rank.CardBgIndex(p.CardBg) : (byte)0);
            w.Write(Arena.Rank.Owns(p.RankNow, p.NameFx) ? Arena.Rank.NameFxIndex(p.NameFx) : (byte)0);
            SendToHost(Proto.End(w));
        }

        void BroadcastLobby()
        {
            SyncOwnCosmetics();
            SyncAvatars();  // (a new arrival gets a spot, and the next position broadcast tells everyone)
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
                w.Write((sbyte)e.Seat);
                w.Write((short)e.Rank);
                w.Write(e.Frame);
                w.Write(e.Trail);
                w.Write(e.CardBg);
                w.Write(e.NameFx);
            }
            w.Write((int)Modifiers);
            Diff.Write(w);
            w.Write(HuntMode);
            w.Write((byte)HuntBoss);
            // Continuing a saved run: its seats, so everyone's lobby can show them.
            w.Write(Resume != null);
            if (Resume != null)
            {
                w.Write(Resume.Wave);
                w.Write(Resume.Endless);
                w.Write((byte)Resume.Seats.Count);
                foreach (var s in Resume.Seats)
                {
                    w.Write(s.Name);
                    w.Write((byte)s.Role);
                    w.Write((byte)s.Kit);
                    w.Write((byte)s.Weapon);
                    w.Write((byte)s.Ability);
                    w.Write(s.Level);
                }
            }
            Broadcast(Proto.End(w));
        }

        // --- Practice Range (lobby) ----------------------------------------------

        /// Host: switch the lobby between a normal run and the Practice Range.
        public void SetPracticeMode(bool on) => SetMode(on, false);

        /// Host: the lobby's mode: a normal run, a Boss Hunt (Hunts.cs) or the Practice Range.
        public void SetMode(bool practice, bool hunt)
        {
            if (!IsHost || Game.Running)
                return;
            if (!practice)
                DesignTest = null;
            PracticeMode = practice;
            HuntMode = hunt && !practice;
            foreach (var e in Lobby.Values)
                e.Ready = false;  // what you're readying up for changed
            BroadcastLobby();
            BroadcastPractice();
        }

        /// Host: which boss the Boss Hunt is for (only one the host has unlocked).
        public void SetHuntBoss(EnemyType boss)
        {
            var h = Hunts.Get(boss);
            if (!IsHost || Game.Running || h == null || !Hunts.Available(h, Game.Profile) || HuntBoss == boss)
                return;
            HuntBoss = boss;
            foreach (var e in Lobby.Values)
                e.Ready = false;
            BroadcastLobby();
        }

        /// The hunt the lobby is set up for, if the host can play it (null: not a hunt, or not unlocked).
        public HuntDef LobbyHunt
        {
            get
            {
                var h = HuntMode && Resume == null ? Hunts.Get(HuntBoss) : null;
                return h != null && (!IsHost || Hunts.Available(h, Game.Profile)) ? h : null;
            }
        }

        /// Host: turn a run modifier on or off. Everyone's Ready is taken back, since the run changed.
        public void SetModifier(RunMod mod, bool on)
        {
            if (!IsHost || Game.Running || Resume != null)
                return;  // (a continued run keeps the modifiers it was saved with)
            Modifiers = on ? Modifiers | mod : Modifiers & ~mod;
            foreach (var e in Lobby.Values)
                e.Ready = false;
            BroadcastLobby();
        }

        /// Host: pick a difficulty preset, or Custom (which starts from the numbers you have). Ready is taken back.
        public void SetDifficulty(DifficultyLevel level)
        {
            if (!IsHost || Game.Running || Resume != null || Diff.Level == level)
                return;  // (a continued run keeps the difficulty it was saved with)
            Diff.SetLevel(level);
            DifficultyChanged();
        }

        /// Host: set one difficulty number (the run becomes Custom).
        public void SetDifficultyKnob(DiffKnob k, float v)
        {
            if (!IsHost || Game.Running || Resume != null)
                return;
            var before = Diff.Copy();
            Diff.Set(k, v);
            if (!Diff.SameAs(before))
                DifficultyChanged();
        }

        void DifficultyChanged()
        {
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
            DesignTest = null;  // the practice waves were edited: they're what plays now, not a wave editor design
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
            if (HuntMode && Resume == null && LobbyHunt == null)
            {
                Status = "That Boss Hunt is locked: beat the boss in a run first";
                return;
            }
            foreach (var e in Lobby.Values)
                e.Ready = false;  // ready up again next time you're back in the lobby
            Game.Practice = PracticeMode ? Plan.Copy() : null;  // a copy, so edits don't change a run in progress
            Game.Hunt = PracticeMode ? null : LobbyHunt;  // Hunts.cs
            Game.Design = PracticeMode ? DesignTest : null;  // the F1 wave editor's waves being tested (WaveDesign.cs)
            Game.Mods = Modifiers;
            Game.Diff = Diff.Copy();
            int startWave = StartWave;
            if (Game.Hunt != null)
                StartWave = 1;  // a hunt's own ten waves (put back below)
            if (Resume != null)
            {
                // Continuing a saved run: from the next wave, each player in their seat (unclaimed seats stay out).
                Game.ResumeFrom = Resume;
                Game.ResumeSeats.Clear();
                foreach (var e in Lobby.Values)
                    if (e.Seat >= 0)
                        Game.ResumeSeats[e.Id] = e.Seat;
                StartWave = Resume.Wave + 1;
                Resume = null;  // back in the lobby afterwards, it's a fresh run
            }
            Game.HostStart();
            StartWave = startWave;
            foreach (var e in Lobby.Values)
                e.Seat = -1;
        }

        /// Counts the host's "back to the lobby" calls this game has heard (its own or over the network). The screen notices
        /// it going up and plays the beam-out (Recall.cs), and the host calls ReturnToLobby once that's done.
        public int Recalls;

        /// The host taking everyone back to the lobby, the long way: the beam-out first on every screen.
        public void StartRecall()
        {
            if (!IsHost)
                return;
            Broadcast(Proto.End(Proto.Begin(Msg.Recall)));
            Recalls++;
        }

        public void ReturnToLobby()
        {
            if (!IsHost)
                return;
            Broadcast(Proto.End(Proto.Begin(Msg.ToLobby)));
            Game.Stop();
            Game.Practice = null;
            Game.Hunt = null;
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
                    ShareCosmetics();
                    break;
                case Msg.Reject:
                    Leave(r.ReadString());
                    break;
                case Msg.LobbyPos:
                    ClientAvatarPositions(r);
                    break;
                case Msg.Lobby:
                {
                    Lobby.Clear();
                    int n = r.ReadByte();
                    for (int i = 0; i < n; i++)
                    {
                        var e = new LobbyEntry { Id = r.ReadInt32(), Name = r.ReadString(), Role = (Role)r.ReadByte(), Kit = (Kit)r.ReadByte(), Weapon = r.ReadByte(), Ability = r.ReadByte() };
                        e.Ready = r.ReadBoolean();
                        e.Seat = r.ReadSByte();
                        e.Rank = Math.Max(1, Math.Min(Arena.Rank.MaxRank, (int)r.ReadInt16()));
                        e.Frame = r.ReadByte();
                        e.Trail = r.ReadByte();
                        e.CardBg = r.ReadByte();
                        e.NameFx = r.ReadByte();
                        Lobby[e.Id] = e;
                    }
                    SyncAvatars();
                    Modifiers = (RunMod)r.ReadInt32();
                    Diff = Difficulty.Read(r);
                    HuntMode = r.ReadBoolean();
                    HuntBoss = (EnemyType)r.ReadByte();
                    Resume = null;
                    if (r.ReadBoolean())
                    {
                        Resume = new RunSave { Wave = r.ReadInt32(), Endless = r.ReadBoolean(), Mods = Modifiers, Diff = Diff.Copy() };
                        int seats = r.ReadByte();
                        for (int i = 0; i < seats; i++)
                        {
                            var s = new SeatSave { Name = r.ReadString(), Role = (Role)r.ReadByte(), Kit = (Kit)r.ReadByte(), Weapon = r.ReadByte(), Ability = r.ReadByte() };
                            s.Level = r.ReadInt32();
                            Resume.Seats.Add(s);
                        }
                    }
                    if (!Game.Running)
                        Screen = AppScreen.Lobby;
                    break;
                }
                case Msg.Emote:
                {
                    int who = r.ReadInt32();
                    Emotes[who] = new EmoteShow { Index = r.ReadByte(), At = Clock };
                    break;
                }
                case Msg.PingMark:
                {
                    int owner = r.ReadInt32();
                    byte kind = r.ReadByte();
                    float px = r.ReadSingle(), py = r.ReadSingle();
                    int target = r.ReadInt32();
                    if (kind <= (byte)PingKind.Go && Finite(new V2(px, py)))
                        AddPing(owner, (PingKind)kind, new V2(px, py), target);
                    break;
                }
                case Msg.Chat:
                {
                    int who = r.ReadInt32();
                    string name = r.ReadString();
                    AddChat(who, name.Length > 24 ? name.Substring(0, 24) : name, CleanChat(r.ReadString()));
                    break;
                }
                case Msg.Recall:
                    Recalls++;
                    break;
                case Msg.ToLobby:
                    Game.Stop();
                    Game.Practice = null;
                    Game.Hunt = null;
                    Screen = AppScreen.Lobby;
                    ShareCosmetics();  // the run may have raised the rank
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
                    Game.Hunt = HuntMode && !PracticeMode && Resume == null ? Hunts.Get(HuntBoss) : null;
                    Game.Mods = Modifiers;
                    Game.Diff = Diff.Copy();
                    Game.OnHostMessage(type, r);
                    Resume = null;
                    break;
                default:
                    Game.OnHostMessage(type, r);
                    break;
            }
        }
    }
}
