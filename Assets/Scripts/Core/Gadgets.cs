using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// A gadget: a pickup a player carries in one free hand and can use, switch off, or throw to a teammate. New kinds go at the
    /// end, before None.
    public enum GadgetId : byte { Magnet, None }

    /// A gadget on the floor or in the air (thrown). It never despawns; a living player with empty hands who walks over it
    /// (or catches it mid-flight) picks it up, keeping the time it has left.
    public class Gadget
    {
        public int Id, Thrower;
        public GadgetId Kind;
        public V2 Pos, Disp, From, To;
        public float FlyT = Game.GadgetFlyTime;  // seconds since thrown (>= GadgetFlyTime: on the floor)
        public float Left;                       // seconds of use left
        public bool Flying => FlyT < Game.GadgetFlyTime;
    }

    /// Gadgets (README "Gadgets"): rare drops in the Expanding arena. The Magnet: switched on, every coin and vector pickup on
    /// the map flies to you; 10 s of pull in all, which you can spend in bits (the key switches it on and off). Hold the key
    /// to throw it where you aim: a teammate catches it or picks it up with the time it has left. Going down drops it.
    public partial class Game
    {
        public readonly Dictionary<int, Gadget> Gadgets = new Dictionary<int, Gadget>();
        int nextGadgetId = 1;
        public const float MagnetTime = 10f, MagnetPull = 620f, GadgetThrowRange = 340f, GadgetFlyTime = 0.5f, GadgetGrabR = 18f, GadgetNoCatchT = 0.15f;
        /// The Magnet drops 1 kill in MagnetDropChance (an elite: 1 in MagnetEliteChance) while fewer than one per player exist.
        public const int MagnetDropChance = 90, MagnetEliteChance = 10;

        public static string GadgetName(GadgetId g) => g == GadgetId.Magnet ? "Magnet" : "";
        public static float GadgetTime(GadgetId g) => g == GadgetId.Magnet ? MagnetTime : 0f;

        /// This player's Magnet is pulling (coins and vector pickups from anywhere fly to them; Coins.cs).
        public static bool MagnetOn(Player p) => p.Gadget == GadgetId.Magnet && p.GadgetOn && !p.Downed;

        int GadgetsAlive(GadgetId kind)
        {
            int n = 0;
            foreach (var g in Gadgets.Values)
                if (g.Kind == kind) n++;
            foreach (var p in Players.Values)
                if (p.Gadget == kind) n++;
            return n;
        }

        /// Host: a kill in the Expanding arena may drop a Magnet (DropCoins).
        void DropGadget(Enemy e)
        {
            if (e.Def.IsBoss || GadgetsAlive(GadgetId.Magnet) >= Math.Max(1, Players.Count))
                return;
            if (rng.Next(e.Elite ? MagnetEliteChance : MagnetDropChance) != 0)
                return;
            SpawnGadget(GadgetId.Magnet, ClampToArena(e.Pos + V2.FromAngle((float)rng.NextDouble() * MathX.Tau) * 16f, 14f), MagnetTime);
        }

        /// Host: a gadget lying on the floor at pos (also the F1 dock and tests).
        public Gadget SpawnGadget(GadgetId kind, V2 pos, float left)
        {
            var g = new Gadget { Id = nextGadgetId++, Kind = kind, Pos = pos, Disp = pos, From = pos, To = pos, Left = left, Thrower = -1 };
            Gadgets[g.Id] = g;
            return g;
        }

        void TickGadgets(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.Gadget == GadgetId.None)
                    continue;
                if (p.Downed)  // going down drops it at your feet, switched off
                {
                    DropHeldGadget(p, p.Pos);
                    continue;
                }
                if (!p.GadgetOn)
                    continue;
                p.GadgetLeft -= dt;
                if (p.GadgetLeft <= 0f)  // used up
                {
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.95f, 0.3f, 0.3f), 30f, 0.35f, Sfx.Hit);
                    p.Gadget = GadgetId.None;
                    p.GadgetOn = false;
                    p.GadgetLeft = 0f;
                }
            }
            if (Gadgets.Count == 0)
                return;
            int taken = -1;
            foreach (var g in Gadgets.Values)
            {
                if (g.Flying)
                {
                    g.FlyT = Math.Min(GadgetFlyTime, g.FlyT + dt);
                    g.Pos = V2.Lerp(g.From, g.To, g.FlyT / GadgetFlyTime);
                }
                if (taken >= 0)
                    continue;
                foreach (var p in Players.Values)
                {
                    if (p.Downed || p.Gadget != GadgetId.None)
                        continue;
                    if (p.Id == g.Thrower || (g.Flying && g.FlyT < GadgetNoCatchT))
                        continue;  // (not your own throw until you've walked off where it landed; a teammate can catch it once it's left your hand)
                    if (p.Pos.DistanceTo(g.Pos) < p.Def.BodyR + GadgetGrabR)
                    {
                        p.Gadget = g.Kind;
                        p.GadgetLeft = g.Left;
                        p.GadgetOn = false;
                        BroadcastFx(FxKind.Ring, g.Pos, new Rgba(0.95f, 0.3f, 0.3f), 24f, 0.3f, g.Flying ? Sfx.PickupBig : Sfx.Pickup);
                        taken = g.Id;
                        break;
                    }
                }
            }
            if (taken >= 0)
                Gadgets.Remove(taken);
            foreach (var g in Gadgets.Values)  // the thrower can take it back once they've walked off it
                if (!g.Flying && g.Thrower >= 0)
                {
                    Player t;
                    if (!Players.TryGetValue(g.Thrower, out t) || t.Pos.DistanceTo(g.Pos) > t.Def.BodyR + GadgetGrabR + 6f)
                        g.Thrower = -1;
                }
        }

        void DropHeldGadget(Player p, V2 to)
        {
            var g = SpawnGadget(p.Gadget, p.Pos, p.GadgetLeft);
            g.Thrower = p.Id;
            g.To = ClampToArena(LayoutOn ? PushOutOfSolids(to, 12f) : to, 14f);
            g.FlyT = g.To.DistanceTo(p.Pos) < 4f ? GadgetFlyTime : 0f;
            if (!g.Flying)
                g.Pos = g.To;
            p.Gadget = GadgetId.None;
            p.GadgetOn = false;
            p.GadgetLeft = 0f;
        }

        // --- Using and throwing --------------------------------------------------------------------------------------

        /// The local player taps the Gadget key: switch the held gadget on or off.
        public void UseGadget() => SendGadget(0, default(V2));

        /// The local player holds and lets go of the Gadget key: throw it to at (clamped to GadgetThrowRange).
        public void ThrowGadget(V2 at) => SendGadget(1, at);

        void SendGadget(byte action, V2 at)
        {
            var me = Me;
            if (me == null || me.Downed || me.Gadget == GadgetId.None)
                return;
            if (action == 1)
                Sounds.Add(Sfx.Throw);
            if (IsHost)
            {
                HostGadget(MyId, action, at);
                return;
            }
            var w = Proto.Begin(Msg.Gadget);
            w.Write(action);
            w.Write(at);
            session.SendToHost(Proto.End(w));
        }

        public void OnGadget(int peer, BinaryReader r)
        {
            byte action = r.ReadByte();
            HostGadget(peer, action, r.ReadV2());
        }

        void HostGadget(int playerId, byte action, V2 at)
        {
            Player p;
            if (!Players.TryGetValue(playerId, out p) || p.Downed || p.Gadget == GadgetId.None)
                return;
            if (action == 0)
            {
                p.GadgetOn = !p.GadgetOn;
                BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.95f, 0.3f, 0.3f), p.GadgetOn ? 60f : 20f, 0.4f, p.GadgetOn ? Sfx.Ability : Sfx.Pick);
            }
            else
            {
                DropHeldGadget(p, p.Pos + (at - p.Pos).LimitLength(GadgetThrowRange));
            }
        }

        // --- Snapshot ------------------------------------------------------------------------------------------------

        void SyncGadgets(NetSync s)
        {
            int count = Gadgets.Count;
            s.Short(ref count);
            if (s.Reading)
            {
                snapSeen.Clear();
                for (int i = 0; i < count; i++)
                    snapSeen.Add(SyncGadget(s, null));
                RemoveUnseen(Gadgets);
            }
            else
                foreach (var g in Gadgets.Values)
                    SyncGadget(s, g);
        }

        int SyncGadget(NetSync s, Gadget g)
        {
            int id = g != null ? g.Id : 0, thrower = g != null ? g.Thrower : -1;
            byte kind = g != null ? (byte)g.Kind : (byte)0;
            V2 pos = g != null ? g.Pos : default(V2), from = g != null ? g.From : default(V2), to = g != null ? g.To : default(V2);
            float fly = g != null ? g.FlyT : 0f, left = g != null ? g.Left : 0f;
            s.Int(ref id);
            s.Byte(ref kind);
            s.Int(ref thrower);
            s.Vec(ref pos);
            s.Vec(ref from);
            s.Vec(ref to);
            s.Float(ref fly);
            s.Float(ref left);
            if (s.Reading)
            {
                if (!Gadgets.TryGetValue(id, out g))
                {
                    g = new Gadget { Id = id, Pos = pos, Disp = pos };
                    Gadgets[id] = g;
                }
                g.Kind = (GadgetId)kind;
                g.Thrower = thrower;
                g.Pos = pos;
                g.From = from;
                g.To = to;
                g.FlyT = fly;
                g.Left = left;
            }
            return id;
        }
    }
}
