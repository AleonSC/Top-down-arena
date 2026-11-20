using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// A gadget: a pickup a player carries in one free hand and can use, switch off, or throw to a teammate. New kinds go at the
    /// end, before None.
    public enum GadgetId : byte { Magnet, Recall, EnergyDrink, None }

    /// A gadget on the floor or in the air (thrown). It never despawns; a living player with empty hands who walks over it
    /// (or catches it mid-flight) picks it up, keeping the time it has left.
    public class Gadget
    {
        public int Id, Thrower;
        public int Owner;  // a fresh drop's player (Docs/RoundFlow.md step 3: each player's own Magnet drops); 0 once it's been carried: anyone's
        public GadgetId Kind;
        public V2 Pos, Disp, From, To;
        public float FlyT = Game.GadgetFlyTime;  // seconds since thrown (>= GadgetFlyTime: on the floor)
        public float Left;                       // seconds of use left
        public bool Flying => FlyT < Game.GadgetFlyTime;
    }

    /// Gadgets (README "Gadgets"): rare drops and Gadget chests in the Expanding arena. A player holds one gadget in hand and up to
    /// GadgetBagSize more in a bag; holding the key (or the Gadget wheel key) opens a wheel to swap (Game/GadgetWheel.cs). Recall: switched on, a 3 s channel
    /// (a hit or a swap breaks it) carries you to the hub, and the gadget is used up. The Magnet: switched on, every coin and vector pickup on
    /// the map flies to you; 60 s of pull in all, which you can spend in bits (the key switches it on and off). The Energy Drink: the
    /// same, 60 s of a big speed boost (EnergySpeed). Hold the key
    /// to throw it where you aim: a teammate catches it or picks it up with the time it has left. Going down drops it.
    public partial class Game
    {
        public readonly Dictionary<int, Gadget> Gadgets = new Dictionary<int, Gadget>();
        int nextGadgetId = 1;
        public const float MagnetTime = 60f, MagnetPull = 620f, GadgetThrowRange = 340f, GadgetFlyTime = 0.5f, GadgetGrabR = 18f, GadgetNoCatchT = 0.15f;
        /// The Magnet drops 1 kill in MagnetDropChance (an elite: 1 in MagnetEliteChance) for each player, rolled for each, while that
        /// player has none (held, or their own on the floor).
        public const int MagnetDropChance = 90, MagnetEliteChance = 10;
        public const int GadgetBagSize = 4;
        public const float RecallChannel = 3f;
        /// The Energy Drink: 60 s of a big speed boost in all, spent in bits like the Magnet (the key switches it on and off).
        public const float EnergyTime = 60f, EnergySpeed = 1.6f;

        public static string GadgetName(GadgetId g) => g == GadgetId.Magnet ? "Magnet" : g == GadgetId.Recall ? "Recall" : g == GadgetId.EnergyDrink ? "Energy Drink" : "";
        public static float GadgetTime(GadgetId g) => g == GadgetId.Magnet ? MagnetTime : g == GadgetId.Recall ? RecallChannel : g == GadgetId.EnergyDrink ? EnergyTime : 0f;
        /// A gadget that runs down while on and shows its seconds left (the Magnet, the Energy Drink; not the Recall's channel).
        public static bool GadgetTimed(GadgetId g) => g == GadgetId.Magnet || g == GadgetId.EnergyDrink;

        /// This player's Energy Drink is on: they move EnergySpeed times as fast (Player.Speed).
        public static bool EnergyDrinkOn(Player p) => p.Gadget == GadgetId.EnergyDrink && p.GadgetOn && !p.Downed;

        /// This player's Magnet is pulling (coins and vector pickups from anywhere fly to them; Coins.cs).
        public static bool MagnetOn(Player p) => p.Gadget == GadgetId.Magnet && p.GadgetOn && !p.Downed;

        /// This player has a gadget of this kind: held, or their own copy on the floor.
        bool HasGadget(Player p, GadgetId kind)
        {
            if (p.Gadget == kind || Array.IndexOf(p.BagKind, kind) >= 0)
                return true;
            foreach (var g in Gadgets.Values)
                if (g.Kind == kind && g.Owner == p.Id)
                    return true;
            return false;
        }

        /// Room for one more gadget: an empty hand or a free bag slot.
        public static bool CanHoldGadget(Player p) => p.Gadget == GadgetId.None || Array.IndexOf(p.BagKind, GadgetId.None) >= 0;

        /// Host: put a gadget in hand if it's empty, else in the first free bag slot. False when both are full.
        bool GiveGadget(Player p, GadgetId kind, float left)
        {
            if (p.Gadget == GadgetId.None)
            {
                p.Gadget = kind;
                p.GadgetLeft = left;
                p.GadgetOn = false;
                return true;
            }
            int slot = Array.IndexOf(p.BagKind, GadgetId.None);
            if (slot < 0)
                return false;
            p.BagKind[slot] = kind;
            p.BagLeft[slot] = left;
            return true;
        }

        /// Host: the gadget in hand is used up: the first one in the bag takes its place.
        void FinishGadget(Player p)
        {
            p.Gadget = GadgetId.None;
            p.GadgetOn = false;
            p.GadgetLeft = 0f;
            RefillHand(p);
        }

        /// Host: an empty hand takes the first gadget in the bag (not while downed: it keeps its bag).
        void RefillHand(Player p)
        {
            if (p.Gadget != GadgetId.None || p.Downed)
                return;
            int slot = Array.FindIndex(p.BagKind, k => k != GadgetId.None);
            if (slot < 0)
                return;
            p.Gadget = p.BagKind[slot];
            p.GadgetLeft = p.BagLeft[slot];
            p.BagKind[slot] = GadgetId.None;
            p.BagLeft[slot] = 0f;
        }

        /// Host: swap the gadget in hand with the one in bag slot `slot` (what was in hand goes to that slot, switched off).
        void EquipGadgetSlot(Player p, int slot)
        {
            if (slot < 0 || slot >= GadgetBagSize || p.BagKind[slot] == GadgetId.None)
                return;
            CancelRecall(p);
            GadgetId kind = p.Gadget;
            float left = p.GadgetLeft;
            p.Gadget = p.BagKind[slot];
            p.GadgetLeft = p.BagLeft[slot];
            p.GadgetOn = false;
            p.BagKind[slot] = kind;
            p.BagLeft[slot] = kind == GadgetId.None ? 0f : left;
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.95f, 0.3f, 0.3f), 36f, 0.3f, Sfx.Pick);
        }

        /// Host: a hit (or a swap) breaks a Recall channel; the 3 s start over.
        void CancelRecall(Player p)
        {
            if (p.Gadget != GadgetId.Recall || !p.GadgetOn)
                return;
            p.GadgetOn = false;
            p.GadgetLeft = RecallChannel;
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.95f, 0.3f, 0.3f), 20f, 0.3f, Sfx.Hit);
        }

        /// Host: Recall finished: set the player down in the hub (a spot round its middle, clear of props and of the others who recalled).
        void RecallToHub(Player p)
        {
            var mid = new V2(Width / 2f, Height / 2f);
            V2 at = mid + V2.FromAngle((p.Id * 2.4f) % MathX.Tau) * 110f;
            at = PushOutOfProps(at, p.Def.BodyR);
            var tint = new Rgba(0.45f, 0.9f, 1f);
            BroadcastFx(FxKind.Ring, p.Pos, tint, 60f, 0.5f, Sfx.Blink);
            MovePlayer(p, at);
            BroadcastFx(FxKind.Ring, at, tint, 60f, 0.5f, Sfx.Blink);
        }

        /// Host: what a Gadget chest holds for this player: a Recall half the time, else a Magnet or an Energy Drink. Into the hand or bag,
        /// else on the floor at `at` (their own copy).
        void GiveChestGadget(Player pl, V2 at)
        {
            int roll = rng.Next(4);
            GadgetId kind = roll == 0 ? GadgetId.Magnet : roll == 1 ? GadgetId.EnergyDrink : GadgetId.Recall;
            if (GiveGadget(pl, kind, GadgetTime(kind)))
                return;
            var g = SpawnGadget(kind, ClampToArena(at + V2.FromAngle((float)rng.NextDouble() * MathX.Tau) * 24f, 14f), GadgetTime(kind));
            g.Owner = pl.Id;
        }

        /// Host: a kill in the Expanding arena may drop a Magnet (DropCoins), rolled for each player: their own copy.
        void DropGadget(Enemy e)
        {
            if (e.Def.IsBoss)
                return;
            foreach (var p in Players.Values)
            {
                if (HasGadget(p, GadgetId.Magnet) || rng.Next(e.Elite ? MagnetEliteChance : MagnetDropChance) != 0)
                    continue;
                var g = SpawnGadget(GadgetId.Magnet, ClampToArena(e.Pos + V2.FromAngle((float)rng.NextDouble() * MathX.Tau) * 16f, 14f), MagnetTime);
                g.Owner = p.Id;
            }
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
                {
                    RefillHand(p);
                    continue;
                }
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
                    if (p.Gadget == GadgetId.Recall)
                        RecallToHub(p);
                    else
                        BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.95f, 0.3f, 0.3f), 30f, 0.35f, Sfx.Hit);
                    FinishGadget(p);
                }
            }
            if (Gadgets.Count == 0)
                return;
            foreach (var g in Gadgets.Values)
                if (g.Owner != 0 && !Players.ContainsKey(g.Owner))
                    g.Owner = 0;  // its player left: anyone's now
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
                    if (p.Downed || !CanHoldGadget(p) || !CanTake(p, g.Owner))
                        continue;
                    if (p.Id == g.Thrower || (g.Flying && g.FlyT < GadgetNoCatchT))
                        continue;  // (not your own throw until you've walked off where it landed; a teammate can catch it once it's left your hand)
                    if (p.Pos.CloserThan(g.Pos, p.Def.BodyR + GadgetGrabR))
                    {
                        GiveGadget(p, g.Kind, g.Left);
                        BroadcastFx(FxKind.Ring, g.Pos, new Rgba(0.95f, 0.3f, 0.3f), 24f, 0.3f, g.Flying ? Sfx.PickupBig : Sfx.Pickup);  // (dropped or thrown later, it's anyone's: DropHeldGadget)
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
                    if (!Players.TryGetValue(g.Thrower, out t) || t.Pos.FartherThan(g.Pos, t.Def.BodyR + GadgetGrabR + 6f))
                        g.Thrower = -1;
                }
        }

        void DropHeldGadget(Player p, V2 to)
        {
            var g = SpawnGadget(p.Gadget, p.Pos, p.Gadget == GadgetId.Recall ? RecallChannel : p.GadgetLeft);
            g.Thrower = p.Id;
            g.To = ClampToArena(LayoutOn ? PushOutOfSolids(to, 12f) : to, 14f);
            g.FlyT = g.To.CloserThan(p.Pos, 4f) ? GadgetFlyTime : 0f;
            if (!g.Flying)
                g.Pos = g.To;
            FinishGadget(p);
        }

        // --- Using and throwing --------------------------------------------------------------------------------------

        /// The local player taps the Gadget key: switch the held gadget on or off.
        public void UseGadget() => SendGadget(0, default(V2));

        /// The local player holds and lets go of the Gadget key: throw it to at (clamped to GadgetThrowRange).
        public void ThrowGadget(V2 at) => SendGadget(1, at);

        /// The local player picks a gadget from the wheel: take the one in bag slot `slot` into your hand.
        public void EquipGadget(int slot) => SendGadget(2, new V2(slot, 0f));

        void SendGadget(byte action, V2 at)
        {
            var me = Me;
            if (me == null || me.Downed || (me.Gadget == GadgetId.None && action != 2))
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
            if (!Players.TryGetValue(playerId, out p) || p.Downed)
                return;
            if (action == 2)
            {
                EquipGadgetSlot(p, (int)at.X);
                return;
            }
            if (p.Gadget == GadgetId.None)
                return;
            if (action == 0)
            {
                if (p.Gadget == GadgetId.Recall && !p.GadgetOn && (InHub(p.Pos, -1f) || p.WarpT > 0f))
                    return;  // (already home)
                p.GadgetOn = !p.GadgetOn;
                if (p.Gadget == GadgetId.Recall && !p.GadgetOn)
                    p.GadgetLeft = RecallChannel;
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
            int id = g != null ? g.Id : 0, thrower = g != null ? g.Thrower : -1, owner = g != null ? g.Owner : 0;
            byte kind = g != null ? (byte)g.Kind : (byte)0;
            V2 pos = g != null ? g.Pos : default(V2), from = g != null ? g.From : default(V2), to = g != null ? g.To : default(V2);
            float fly = g != null ? g.FlyT : 0f, left = g != null ? g.Left : 0f;
            s.Int(ref id);
            s.Byte(ref kind);
            s.Int(ref thrower);
            s.Int(ref owner);
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
                g.Owner = owner;
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
