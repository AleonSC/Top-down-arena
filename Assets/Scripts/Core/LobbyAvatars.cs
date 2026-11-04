using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Arena
{
    /// One player's character in the lobby's Hangout room: where it stands and which way it faces.
    public class Avatar
    {
        public V2 Pos;       // where the player's game says it is (the target other screens follow)
        public V2 Disp;      // what's drawn: Pos, smoothed (Session.StepAvatars)
        public float Aim;    // which way it faces, radians (it turns the way it walks)
        public bool Pvp;     // fighting: it can shoot and be shot (LobbyCombat.cs). Off by default; off means neither
        public float Hp = Session.RoomHp;  // 0 means knocked out until the host respawns it
        public float Down;   // the host: seconds of being knocked out left
        public byte Respawns;  // counts up each time the host puts it back at a spawn spot, so its owner knows to follow
        public double NextFire;  // (on the screen that fires for it) the earliest the next shot may go
        public double LastHit;   // the host: when it was last hurt (health returns a few seconds after)
        public double AbilityReady;  // (on the screen that casts for it) when its ability is ready again
        public float BarrierLeft;    // a Tank's barrier: seconds left (every screen counts it down)
        public float DashLeft;       // its own screen: seconds of dash left
        public V2 DashDir;           // ...and which way
        public byte SentHp = 100;  // the host: the health last broadcast, so regrowth only triggers a send when it shows
    }

    /// The lobby's Hangout room (Docs/Hangout.md): everyone's character walks around a shared room with WASD, wearing
    /// their cosmetics, and (with PvP on) shoot each other (LobbyCombat.cs). Each player moves their own avatar and sends its
    /// position to the host (Msg.LobbyMove); the host clamps it to the room and sends everyone the lot (Msg.LobbyPos).
    public partial class Session
    {
        public const float RoomW = 2400f, RoomH = 1600f, RoomMargin = 30f;  // the room (a map the Hangout window scrolls over), in its own units (1 unit = 1 UI pixel)
        public const float AvatarSpeed = 230f;                            // units a second
        const double AvatarSendEvery = 1.0 / 15.0;                        // how often a position goes out (both ways)

        public readonly SortedDictionary<int, Avatar> Avatars = new SortedDictionary<int, Avatar>();

        readonly Stopwatch avatarClock = Stopwatch.StartNew();
        double lastAvatarSend;
        bool avatarsDirty;  // the host: something moved since the last broadcast. A client: our own position hasn't gone out yet

        /// Where a new arrival stands: along the bottom of the room, one spot per seat.
        static V2 SpawnSpot(int index) => new V2(RoomW / 2f + (index - 1) * 90f, RoomH - 90f);

        static V2 ClampToRoom(V2 p)
        {
            if (float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsInfinity(p.X) || float.IsInfinity(p.Y))
                return new V2(RoomW / 2f, RoomH / 2f);
            return new V2(Math.Max(RoomMargin, Math.Min(RoomW - RoomMargin, p.X)), Math.Max(RoomMargin, Math.Min(RoomH - RoomMargin, p.Y)));
        }

        /// Keeps Avatars in step with the lobby: everyone who's in it has one, and nobody else does.
        void SyncAvatars()
        {
            var gone = new List<int>();
            foreach (int id in Avatars.Keys)
                if (!Lobby.ContainsKey(id))
                    gone.Add(id);
            foreach (int id in gone)
                Avatars.Remove(id);
            Shots.RemoveAll(sh => !Avatars.ContainsKey(sh.Owner));
            int slot = 0;
            foreach (int id in Lobby.Keys)
            {
                if (!Avatars.ContainsKey(id))
                {
                    var spot = SpawnSpot(slot);
                    Avatars[id] = new Avatar { Pos = spot, Disp = spot, Aim = -(float)Math.PI / 2f };
                    avatarsDirty = true;
                }
                slot++;
            }
        }

        /// Move your own character (the Hangout tab calls this every frame it's open). It's yours to place, so it shows at once;
        /// the host hears about it a few times a second.
        public void MoveMyAvatar(V2 pos, float aim)
        {
            Avatar a;
            if (!Lobby.ContainsKey(MyId))
                return;
            if (!Avatars.TryGetValue(MyId, out a))
                Avatars[MyId] = a = new Avatar { Pos = pos, Disp = pos, Aim = aim };
            pos = ClampToRoom(pos);
            if ((pos - a.Pos).LengthSq > 0.0001f || Math.Abs(aim - a.Aim) > 0.001f)
                avatarsDirty = true;
            a.Pos = pos;
            a.Disp = pos;
            a.Aim = aim;
        }

        /// Smooths what the other characters show towards where they are (call once a frame, dt in seconds). Yours is exact.
        public void StepAvatars(float dt)
        {
            if (Avatars.Count != Lobby.Count)
                SyncAvatars();  // (anyone in the lobby without a character yet gets one, whatever caused the gap)
            float k = 1f - (float)Math.Exp(-14f * dt);
            foreach (var kv in Avatars)
                kv.Value.Disp = kv.Key == MyId || (kv.Value.Pos - kv.Value.Disp).LengthSq > 90000f ? kv.Value.Pos : V2.Lerp(kv.Value.Disp, kv.Value.Pos, k);
        }

        /// Sends what's due: the host its broadcast of everyone's positions, a client its own position. Called from Poll.
        void SendAvatars()
        {
            if (Net == null || Screen != AppScreen.Lobby || !avatarsDirty)
                return;
            double now = avatarClock.Elapsed.TotalSeconds;
            if (now - lastAvatarSend < AvatarSendEvery)
                return;
            lastAvatarSend = now;
            avatarsDirty = false;
            if (IsHost)
            {
                var w = Proto.Begin(Msg.LobbyPos);
                w.Write((byte)Avatars.Count);
                foreach (var kv in Avatars)
                {
                    w.Write(kv.Key);
                    w.Write(kv.Value.Pos);
                    w.Write(kv.Value.Aim);
                    w.Write(kv.Value.Pvp);
                    w.Write((byte)Math.Round(kv.Value.Hp));
                    w.Write(kv.Value.Respawns);
                    kv.Value.SentHp = (byte)Math.Round(kv.Value.Hp);
                }
                Broadcast(Proto.End(w));
            }
            else
            {
                Avatar a;
                if (!Avatars.TryGetValue(MyId, out a))
                    return;
                var w = Proto.Begin(Msg.LobbyMove);
                w.Write(a.Pos);
                w.Write(a.Aim);
                w.Write(a.Pvp);
                SendToHost(Proto.End(w));
            }
        }

        /// The host: a client says where its character is.
        void HostAvatarMove(int peer, BinaryReader r)
        {
            var pos = r.ReadV2();
            float aim = r.ReadSingle();
            bool pvp = r.ReadBoolean();
            Avatar a;
            if (!Avatars.TryGetValue(peer, out a) || Game.Running)
                return;
            SetPvp(a, pvp);
            if (a.Hp <= 0f)
                return;  // knocked out: it stays where it fell
            a.Pos = ClampToRoom(pos);
            a.Aim = float.IsNaN(aim) || float.IsInfinity(aim) ? a.Aim : aim;
            avatarsDirty = true;
        }

        /// A client: the host's list of where everyone is.
        void ClientAvatarPositions(BinaryReader r)
        {
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                var pos = ClampToRoom(r.ReadV2());
                float aim = r.ReadSingle();
                bool pvp = r.ReadBoolean();
                float hp = r.ReadByte();
                byte respawns = r.ReadByte();
                if (!Lobby.ContainsKey(id))
                    continue;
                Avatar a;
                if (!Avatars.TryGetValue(id, out a))
                    Avatars[id] = a = new Avatar { Pos = pos, Disp = pos, Aim = aim, Pvp = pvp, Hp = hp, Respawns = respawns };
                else
                {
                    a.Hp = hp;
                    if (id != MyId)  // our own position and PvP choice are ours: the host's copy lags behind what we've done since
                    {
                        a.Pos = pos;
                        a.Aim = aim;
                        a.Pvp = pvp;
                    }
                    else if (respawns != a.Respawns)  // ...unless the host has put us back at a spawn spot
                    {
                        a.Pos = a.Disp = pos;
                        a.Respawns = respawns;
                    }
                    a.Respawns = respawns;
                }
            }
        }
    }
}
