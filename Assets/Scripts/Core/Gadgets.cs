using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// A gadget: a pickup a player carries in one free hand and can use, switch off, or throw to a teammate. New kinds go at the
    /// end, before None.
    public enum GadgetId : byte { Magnet, Lodestone, EnergyDrink, ShieldGenerator, Medkit, Grenade, SmokeBomb, GasGrenade, Incendiary, Cloak, OverclockChip, LandMine, Emp, None }

    /// A gadget on the floor or in the air (thrown). It never despawns; a living player with empty hands who walks over it
    /// (or catches it mid-flight) picks it up, keeping the time and uses it has left.
    public class Gadget
    {
        public int Id, Thrower;
        public int Owner;  // a fresh drop's player (Docs/RoundFlow.md step 3: each player's own Magnet drops); 0 once it's been carried: anyone's
        public GadgetId Kind;
        public V2 Pos, Disp, From, To;
        public float FlyT = Game.GadgetFlyTime;  // seconds since thrown (>= GadgetFlyTime: on the floor)
        public float Left;                       // seconds of use left
        public byte Uses;                        // uses left (the Lodestone, the Medkit, the throwables; GadgetUses)
        public float Fizzle;                     // a Tactical Vest drop: seconds left on the floor before it fizzles out (0: never; TacticalVest.cs)
        public bool Flying => FlyT < Game.GadgetFlyTime;
    }

    /// Gadgets (README "Gadgets"): rare drops and Gadget chests in the Expanding arena. A player holds one gadget in hand and up to
    /// GadgetBagSize more in a bag; the Gadget wheel key (or holding the Gadget key) opens a wheel to swap (Game/GadgetWheel.cs).
    /// Tap the Gadget key to use the one in hand; hold it to throw it where you aim (a teammate catches it or picks it up with the time
    /// and uses it has left). Going down drops it.
    ///   Timed (switched on and off, a total time spent in bits): the Magnet (every coin and vector pickup on the map flies to you), the
    ///     Energy Drink (EnergySpeed move speed), the Shield Generator (a dome that wipes enemy bullets out, ShieldGenR round you).
    ///   The Lodestone: a LodestoneChannel cast (a hit or a swap breaks it) carries you to the hub's middle; LodestoneUses casts.
    ///   The Medkit: MedkitUses taps, each clears your wound and heals MedkitHeal of your max health (UseMedkit).
    ///   More timed ones: the Cloak (enemies can't pick you out: Player.VanishT), the Overclock Chip (OverclockMult fire rate and cooldowns).
    ///   The EMP: EmpUses pulses round you (EmpPulse: wipes enemy bullets, stuns and hurts enemies).
    ///   Throwables (ThrowUses each, ThrowGap apart, lobbed to the cursor): the Land Mine (a Flak Cannon mine lying armed), the Grenade (a blast, Grenade launcher HE), the Smoke Bomb (a
    ///     Grenade launcher smoke cloud: enemies in it are slowed and can't attack), the Gas Grenade (ZoneKind.ToxicGas: hurts and slows),
    ///     the Incendiary (burning ground, a Flame Wall zone). Their damage grows with the wave (GadgetPower).
    public partial class Game
    {
        public readonly Dictionary<int, Gadget> Gadgets = new Dictionary<int, Gadget>();
        int nextGadgetId = 1;
        public const float MagnetTime = 60f, MagnetPull = 620f, GadgetThrowRange = 340f, GadgetFlyTime = 0.5f, GadgetGrabR = 18f, GadgetNoCatchT = 0.15f;
        /// A gadget drops 1 kill in GadgetDropChance (an elite: 1 in GadgetEliteChance) for each player (DropGadget).
        public const int GadgetDropChance = 45, GadgetEliteChance = 5;
        public const int GadgetBagSize = 4;
        public const float LodestoneChannel = 3f;
        public const int LodestoneUses = 3, ThrowUses = 3;
        /// The Energy Drink: 60 s of a big speed boost in all, spent in bits like the Magnet (the key switches it on and off).
        public const float EnergyTime = 60f, EnergySpeed = 1.6f;
        /// The Shield Generator: ShieldGenTime s of a dome ShieldGenR round you that wipes out the enemy bullets coming into it.
        public const float ShieldGenTime = 20f, ShieldGenR = 66f;
        /// The Medkit: MedkitUses taps, each clearing your wound and healing MedkitHeal of your max health (owner Nov 25: was the Defib Kit).
        public const int MedkitUses = 3;
        public const float MedkitHeal = 0.3f;
        /// Throwables: one every ThrowGap s, ThrowMin to GadgetThrowRange px out.
        public const float ThrowGap = 0.6f, ThrowMin = 60f;
        public const float GadgetNadeDmg = 90f, GadgetNadeRadius = 95f;
        public const float SmokeBombLife = 7f, SmokeBombRadius = 115f;
        public const float GadgetGasLife = 6f, GadgetGasRadius = 100f, GadgetGasDps = 22f, GadgetGasSlow = 0.3f;
        public const float GadgetFireLife = 5f, GadgetFireRadius = 85f;
        /// The Cloak (CloakTime s in all) and the Overclock Chip (OverclockTime s in all, OverclockMult fire rate and cooldowns).
        public const float CloakTime = 15f, OverclockTime = 30f, OverclockMult = 1.4f;
        /// The Land Mine: lobbed like a throwable, it lies armed for LandMineLife s; an enemy stepping on it sets off a Flak mine burst
        /// (LandMineDmg in Flak Cannon MineBurst px).
        public const float LandMineLife = 45f, LandMineDmg = 100f;
        /// The EMP: EmpUses pulses round you, EmpRadius px: every enemy bullet in it is wiped out, every enemy stunned EmpStun s (bosses
        /// EmpBossStun) and hurt EmpDmg.
        public const int EmpUses = 2;
        public const float EmpRadius = 280f, EmpStun = 1.6f, EmpBossStun = 0.4f, EmpDmg = 20f;

        /// What a gadget does, in a line (the wiki; Docs/Glossary.md's wording).
        public static string GadgetDesc(GadgetId g)
        {
            switch (g)
            {
                case GadgetId.Magnet: return "Timed, 60 s: switched on, every coin and vector pickup on the map flies to you; tap again to switch it off and save the rest.";
                case GadgetId.Lodestone: return "Carries you to the hub's middle after a 3 s cast. 3 uses.";
                case GadgetId.EnergyDrink: return "You move 60% faster while it's on (60 s in all).";
                case GadgetId.ShieldGenerator: return "A dome that wipes out enemy bullets round you while it's on (20 s in all).";
                case GadgetId.Medkit: return "3 uses: each clears your wound and heals 30% of your max health.";
                case GadgetId.Grenade: return "A throwable (3 charges, lobbed at the cursor): bursts for 90 in 95 px with a small shove.";
                case GadgetId.SmokeBomb: return "A throwable (3 charges): a 115 px smoke cloud for 7 s; enemies in it are slowed and can't attack (bosses are only slowed).";
                case GadgetId.GasGrenade: return "A throwable (3 charges): a 100 px cloud for 6 s that deals 22 a second and slows 30%.";
                case GadgetId.Incendiary: return "A throwable (3 charges): 85 px of burning ground for 5 s.";
                case GadgetId.Cloak: return "Hides you from enemies while it's on (15 s in all).";
                case GadgetId.OverclockChip: return "Your fire rate and cooldowns are 40% faster while it's on (30 s in all).";
                case GadgetId.LandMine: return "A throwable (3 charges): lies armed for 45 s; the first enemy to step on it sets it off for 100 in 95 px.";
                case GadgetId.Emp: return "Two pulses that wipe enemy bullets round you and stun enemies.";
            }
            return "";
        }

        public static string GadgetName(GadgetId g)
        {
            switch (g)
            {
                case GadgetId.Magnet: return "Magnet";
                case GadgetId.Lodestone: return "Lodestone";
                case GadgetId.EnergyDrink: return "Energy Drink";
                case GadgetId.ShieldGenerator: return "Shield Generator";
                case GadgetId.Medkit: return "Medkit";
                case GadgetId.Grenade: return "Grenade";
                case GadgetId.SmokeBomb: return "Smoke Bomb";
                case GadgetId.GasGrenade: return "Gas Grenade";
                case GadgetId.Incendiary: return "Incendiary";
                case GadgetId.Cloak: return "Cloak";
                case GadgetId.OverclockChip: return "Overclock Chip";
                case GadgetId.LandMine: return "Land Mine";
                case GadgetId.Emp: return "EMP";
                default: return "";
            }
        }

        /// A fresh gadget's time: the timed ones' seconds of use, the Lodestone's cast.
        public static float GadgetTime(GadgetId g) => g == GadgetId.Magnet ? MagnetTime : g == GadgetId.Lodestone ? LodestoneChannel : g == GadgetId.EnergyDrink ? EnergyTime
            : g == GadgetId.ShieldGenerator ? ShieldGenTime : g == GadgetId.Cloak ? CloakTime : g == GadgetId.OverclockChip ? OverclockTime : 0f;
        /// A fresh gadget's uses (0: a timed one).
        public static byte GadgetUses(GadgetId g) => (byte)(g == GadgetId.Lodestone ? LodestoneUses : g == GadgetId.Medkit ? MedkitUses : g == GadgetId.Emp ? EmpUses : Throwable(g) ? ThrowUses : 0);
        /// A gadget that runs down while on and shows its seconds left (the Magnet, the Energy Drink, the Shield Generator).
        public static bool GadgetTimed(GadgetId g) => g == GadgetId.Magnet || g == GadgetId.EnergyDrink || g == GadgetId.ShieldGenerator || g == GadgetId.Cloak || g == GadgetId.OverclockChip;
        /// A gadget the Gadget key lobs a charge of (the Grenade, the Smoke Bomb, the Gas Grenade, the Incendiary).
        public static bool Throwable(GadgetId g) => g == GadgetId.Grenade || g == GadgetId.SmokeBomb || g == GadgetId.GasGrenade || g == GadgetId.Incendiary || g == GadgetId.LandMine;

        /// This player's Magnet is pulling (coins and vector pickups from anywhere fly to them; Coins.cs).
        public static bool MagnetOn(Player p) => p.Gadget == GadgetId.Magnet && p.GadgetOn && !p.Downed;

        /// This player's Energy Drink is on: they move EnergySpeed times as fast (Player.Speed).
        public static bool EnergyDrinkOn(Player p) => p.Gadget == GadgetId.EnergyDrink && p.GadgetOn && !p.Downed;

        /// This player's Cloak is on: enemies can't pick them out (it keeps Player.VanishT up, as the Ninja's Vanish does).
        public static bool CloakOn(Player p) => p.Gadget == GadgetId.Cloak && p.GadgetOn && !p.Downed;

        /// This player's Overclock Chip is on: OverclockMult times the fire rate and the ability cooldowns' speed.
        public static bool OverclockOn(Player p) => p.Gadget == GadgetId.OverclockChip && p.GadgetOn && !p.Downed;

        /// This player's Shield Generator is up.
        public static bool ShieldGenOn(Player p) => p.Gadget == GadgetId.ShieldGenerator && p.GadgetOn && !p.Downed;

        /// A thrown gadget's damage: the thrower's damage, growing with the wave as the enemies' health does.
        float GadgetPower(Player p) => p.Stats.DmgMult * (1f + 0.15f * Math.Max(0, Wave - 1));

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

        /// Host: a fresh gadget (its full uses) in hand if it's empty, else in the first free bag slot. False when both are full.
        bool GiveGadget(Player p, GadgetId kind, float left) => GiveGadgetUses(p, kind, left, GadgetUses(kind));

        bool GiveGadgetUses(Player p, GadgetId kind, float left, byte uses)
        {
            if (p.Gadget == GadgetId.None)
            {
                p.Gadget = kind;
                p.GadgetLeft = left;
                p.GadgetUses = uses;
                p.GadgetOn = false;
                return true;
            }
            int slot = Array.IndexOf(p.BagKind, GadgetId.None);
            if (slot < 0)
                return false;
            p.BagKind[slot] = kind;
            p.BagLeft[slot] = left;
            p.BagUses[slot] = uses;
            return true;
        }

        /// Host: the gadget in hand is used up: the first one in the bag takes its place.
        void FinishGadget(Player p)
        {
            p.Gadget = GadgetId.None;
            p.GadgetOn = false;
            p.GadgetLeft = 0f;
            p.GadgetUses = 0;
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
            p.GadgetUses = p.BagUses[slot];
            p.BagKind[slot] = GadgetId.None;
            p.BagLeft[slot] = 0f;
            p.BagUses[slot] = 0;
        }

        /// Host: swap the gadget in hand with the one in bag slot `slot` (what was in hand goes to that slot, switched off).
        void EquipGadgetSlot(Player p, int slot, bool quiet = false)
        {
            if (slot < 0 || slot >= GadgetBagSize || p.BagKind[slot] == GadgetId.None)
                return;
            CancelLodestone(p);
            GadgetId kind = p.Gadget;
            float left = p.GadgetLeft;
            byte uses = p.GadgetUses;
            p.Gadget = p.BagKind[slot];
            p.GadgetLeft = p.BagLeft[slot];
            p.GadgetUses = p.BagUses[slot];
            p.GadgetOn = false;
            p.BagKind[slot] = kind;
            p.BagLeft[slot] = kind == GadgetId.None ? 0f : left;
            p.BagUses[slot] = kind == GadgetId.None ? (byte)0 : uses;
            if (!quiet)
                BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.95f, 0.3f, 0.3f), 36f, 0.3f, Sfx.Pick);
        }

        /// Host: a hit (or a swap) breaks a Lodestone cast; the 3 s start over (no use spent).
        void CancelLodestone(Player p)
        {
            if (p.Gadget != GadgetId.Lodestone || !p.GadgetOn)
                return;
            p.GadgetOn = false;
            p.GadgetLeft = LodestoneChannel;
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.95f, 0.3f, 0.3f), 20f, 0.3f, Sfx.Hit);
        }

        /// Host: a Lodestone cast finished: set the player down in the hub (a spot round its middle, clear of props and of the others who came).
        void LodestoneToHub(Player p)
        {
            var mid = new V2(Width / 2f, Height / 2f);
            V2 at = mid + V2.FromAngle((p.Id * 2.4f) % MathX.Tau) * 110f;
            at = PushOutOfProps(at, p.Def.BodyR);
            var tint = new Rgba(0.45f, 0.9f, 1f);
            BroadcastFx(FxKind.Ring, p.Pos, tint, 60f, 0.5f, Sfx.Blink);
            MovePlayer(p, at);
            BroadcastFx(FxKind.Ring, at, tint, 60f, 0.5f, Sfx.Blink);
        }

        /// Gadget rolls (chests, kill drops) come from their own Random, so they don't shift every other random choice of the run (made
        /// on the first roll, after the run's start has made its own side Randoms).
        Random gadgetRng;
        Random GadgetRng => gadgetRng ?? (gadgetRng = NewSideRandom());

        /// What a Gadget chest gives, by weight (the Lodestone the most often).
        static readonly GadgetId[] ChestGadgets = { GadgetId.Lodestone, GadgetId.Lodestone, GadgetId.Lodestone, GadgetId.Magnet, GadgetId.Magnet, GadgetId.EnergyDrink, GadgetId.EnergyDrink,
            GadgetId.ShieldGenerator, GadgetId.ShieldGenerator, GadgetId.Medkit, GadgetId.Medkit, GadgetId.Grenade, GadgetId.Grenade, GadgetId.SmokeBomb, GadgetId.SmokeBomb,
            GadgetId.GasGrenade, GadgetId.GasGrenade, GadgetId.Incendiary, GadgetId.Incendiary, GadgetId.Cloak, GadgetId.Cloak, GadgetId.OverclockChip, GadgetId.OverclockChip,
            GadgetId.LandMine, GadgetId.LandMine, GadgetId.Emp, GadgetId.Emp };

        /// Host: what a Gadget chest holds for this player (ChestGadgets). Into the hand or bag, else on the floor at `at` (their own copy).
        void GiveChestGadget(Player pl, V2 at)
        {
            GadgetId kind = ChestGadgets[GadgetRng.Next(ChestGadgets.Length)];
            if (GiveGadget(pl, kind, GadgetTime(kind)))
                return;
            var g = SpawnGadget(kind, ClampToArena(at + V2.FromAngle((float)GadgetRng.NextDouble() * MathX.Tau) * 24f, 14f), GadgetTime(kind));
            g.Owner = pl.Id;
        }

        /// What a kill can drop: every kind but the Lodestone (Gadget chests only).
        static readonly GadgetId[] KillGadgets = { GadgetId.Magnet, GadgetId.EnergyDrink, GadgetId.ShieldGenerator, GadgetId.Medkit, GadgetId.Grenade,
            GadgetId.SmokeBomb, GadgetId.GasGrenade, GadgetId.Incendiary, GadgetId.Cloak, GadgetId.OverclockChip, GadgetId.LandMine, GadgetId.Emp };
        readonly List<GadgetId> dropPool = new List<GadgetId>();

        /// Host: a kill in the Expanding arena may drop a gadget (DropCoins), rolled for each player: 1 in GadgetDropChance (an elite 1 in
        /// GadgetEliteChance), a kind from KillGadgets they don't have yet (held, bagged or their own on the floor); their own copy.
        void DropGadget(Enemy e)
        {
            if (e.Def.IsBoss)
                return;
            foreach (var p in Players.Values)
            {
                if (GadgetRng.Next(e.Elite ? GadgetEliteChance : GadgetDropChance) != 0)
                    continue;
                dropPool.Clear();
                foreach (var k in KillGadgets)
                    if (!HasGadget(p, k))
                        dropPool.Add(k);
                if (dropPool.Count == 0)
                    continue;
                GadgetId kind = dropPool[GadgetRng.Next(dropPool.Count)];
                var g = SpawnGadget(kind, ClampToArena(e.Pos + V2.FromAngle((float)GadgetRng.NextDouble() * MathX.Tau) * 16f, 14f), GadgetTime(kind));
                g.Owner = p.Id;
            }
        }

        /// Host: a gadget lying on the floor at pos (also the F1 dock and tests), with its full uses.
        public Gadget SpawnGadget(GadgetId kind, V2 pos, float left)
        {
            var g = new Gadget { Id = nextGadgetId++, Kind = kind, Pos = pos, Disp = pos, From = pos, To = pos, Left = left, Uses = GadgetUses(kind), Thrower = -1 };
            Gadgets[g.Id] = g;
            return g;
        }

        void TickGadgets(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.GadgetCd = Math.Max(0f, p.GadgetCd - dt);
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
                    if (p.Gadget == GadgetId.Lodestone)
                    {
                        LodestoneToHub(p);
                        if (p.GadgetUses > 1)  // casts left: ready again
                        {
                            p.GadgetUses--;
                            p.GadgetOn = false;
                            p.GadgetLeft = LodestoneChannel;
                            continue;
                        }
                    }
                    else
                        BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.95f, 0.3f, 0.3f), 30f, 0.35f, Sfx.Hit);
                    FinishGadget(p);
                }
            }
            TickShieldGens();
            TickToxicGas(dt);
            TickVestFizzle(dt);  // TacticalVest.cs
            foreach (var p in Players.Values)
                if (CloakOn(p))
                    p.VanishT = Math.Max(p.VanishT, 0.2f);  // (the Ninja's tick runs it down; it lapses 0.2 s after the Cloak goes off)
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
                        GiveGadgetUses(p, g.Kind, g.Left, g.Uses);
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
            var g = SpawnGadget(p.Gadget, p.Pos, p.Gadget == GadgetId.Lodestone ? LodestoneChannel : p.GadgetLeft);
            g.Uses = p.GadgetUses;
            g.Thrower = p.Id;
            g.To = ClampToArena(LayoutOn ? PushOutOfSolids(to, 12f) : to, 14f);
            g.FlyT = g.To.CloserThan(p.Pos, 4f) ? GadgetFlyTime : 0f;
            if (!g.Flying)
                g.Pos = g.To;
            FinishGadget(p);
        }

        // --- The Medkit and the Shield Generator ---------------------------------------------------------------------

        public static readonly Rgba MedkitInk = new Rgba(1f, 0.45f, 0.5f);

        /// Host: tap with a Medkit: clear your wound and heal MedkitHeal of your max health. False (no use spent) when you're
        /// already whole: full health and no wound.
        bool UseMedkit(Player p)
        {
            if (p.Wound <= 0f && p.Hp >= p.MaxHp)
            {
                BroadcastFx(FxKind.Ring, p.Pos, MedkitInk.WithAlpha(0.5f), 20f, 0.3f, Sfx.Pick);
                return false;
            }
            p.Wound = 0f;
            p.Hp = Math.Min(p.HealCap, p.Hp + p.MaxHp * MedkitHeal);
            BroadcastFx(FxKind.Ring, p.Pos, MedkitInk, 70f, 0.5f, Sfx.PickupBig);
            return true;
        }

        readonly List<int> shieldWiped = new List<int>();

        /// Host: every Shield Generator that's up wipes out the enemy bullets inside its dome.
        void TickShieldGens()
        {
            foreach (var p in Players.Values)
            {
                if (!ShieldGenOn(p))
                    continue;
                shieldWiped.Clear();
                foreach (var b in Bullets.Values)
                    if (b.Owner == 0 && Defs.IsEnemyShot(b.Kind) && !Defs.Unblockable(b.Kind) && b.Pos.CloserThan(p.Pos, ShieldGenR + b.Radius))
                        shieldWiped.Add(b.Id);
                for (int i = 0; i < shieldWiped.Count; i++)
                {
                    if (i < 3)
                        BroadcastFx(FxKind.Spark, Bullets[shieldWiped[i]].Pos, ShieldInk, 8f, 0.15f, i == 0 ? Sfx.SpellZap : Sfx.None);
                    KillBullet(shieldWiped[i]);
                }
            }
        }

        public static readonly Rgba ShieldInk = new Rgba(0.4f, 0.72f, 1f), EmpInk = new Rgba(0.7f, 0.92f, 1f);

        /// Host: an EMP pulse round `p`: enemy bullets in EmpRadius wiped out, enemies stunned and hurt.
        void EmpPulse(Player p)
        {
            shieldWiped.Clear();
            foreach (var b in Bullets.Values)
                if (b.Owner == 0 && Defs.IsEnemyShot(b.Kind) && !Defs.Unblockable(b.Kind) && b.Pos.CloserThan(p.Pos, EmpRadius + b.Radius))
                    shieldWiped.Add(b.Id);
            foreach (int id in shieldWiped)
                KillBullet(id);
            float dmg = EmpDmg * GadgetPower(p);
            foreach (var e in FoesInCircle(p.Pos, EmpRadius, by: p.Id))
            {
                Stun(e, e.Def.IsBoss || e.Def.Mini ? EmpBossStun : EmpStun);
                HurtEnemy(e, dmg, p.Id);
                Charge(e, p.Id);  // Charged.cs
            }
            p.GadgetCd = ThrowGap;
            BroadcastFx(FxKind.Ring, p.Pos, EmpInk, EmpRadius, 0.5f, Sfx.SpellZap);
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 1f, 1f, 0.8f), EmpRadius * 0.6f, 0.3f, Sfx.None);
        }

        // --- Throwables ----------------------------------------------------------------------------------------------

        /// Host: lob one charge of the throwable in hand toward the cursor (`at`).
        void ThrowCharge(Player p, V2 target)
        {
            V2 toward = target - p.Pos;
            float aim = toward.Length > 1f ? toward.Angle : p.Aim;
            V2 dir = V2.FromAngle(aim), from = p.Pos + dir * p.Def.BodyR;
            float dist = Math.Max(ThrowMin, Math.Min(GadgetThrowRange, toward.Length));
            V2 at = ClampToArena(p.Pos + dir * dist, 12f);
            dist = at.DistanceTo(from);
            float power = GadgetPower(p);
            switch (p.Gadget)
            {
                case GadgetId.Grenade:
                case GadgetId.Incendiary:
                {
                    float life = GlFlight(dist) + GlHopTime;
                    V2 end = at + dir * GlHop;
                    var b = SpawnBullet(Defs.GrenadeShot, from, (end - from) / life, 0f, life, GadgetNadeDmg * power, p.Id);
                    b.Splash = GadgetNadeRadius;
                    b.GlNade = true;
                    b.Burst = 1;  // (lands where its flight ends: MoveBullets)
                    if (p.Gadget == GadgetId.Incendiary)
                    {
                        b.Incendiary = true;
                        b.Dmg = power;  // (the fire's damage multiplier)
                    }
                    break;
                }
                case GadgetId.SmokeBomb:
                {
                    V2 rest = ClampToArena(at + dir * (GlSmokeHop1 + GlSmokeHop2), 12f);
                    var z = AddZone(ZoneKind.SmokeShell, p, rest, SmokeBombRadius, GlSmokeLand(dist) + SmokeBombLife);
                    z.Aim = dir.Angle;
                    z.GuardFrac = dist;
                    break;
                }
                case GadgetId.LandMine:
                {
                    var m = AddZone(ZoneKind.FlakMine, p, at, MineTrigger, LandMineLife);
                    m.Aim = dir.Angle;
                    m.Radius = MineBurst;  // (drawn as the burst's reach; it goes off at MineTrigger: TickTankAlts)
                    m.DmgMult = power * LandMineDmg / MineDamage;
                    break;
                }
                case GadgetId.GasGrenade:
                {
                    var z = AddZone(ZoneKind.ToxicGas, p, at, GadgetGasRadius, TearGasFlight + GadgetGasLife);
                    z.Aim = dir.Angle;
                    z.DmgMult = power;
                    break;
                }
            }
            p.GadgetCd = ThrowGap;
            Sounds.Add(Sfx.Throw);
            BroadcastFx(FxKind.Ring, from, new Rgba(1f, 1f, 1f, 0.6f), 12f, 0.2f, Sfx.TankGasLaunch);
        }

        /// Host: an Incendiary lands (ExplodeGrenade): a pop and burning ground that doesn't block bullets.
        void IncendiaryBurst(Bullet b)
        {
            Player p;
            BroadcastFx(FxKind.Boom, b.Pos, Kits.Get(ZoneKind.FlameWall).Color, GadgetFireRadius, 0.45f, Sfx.SpellFire);
            if (!Players.TryGetValue(b.Owner, out p))
                return;
            var z = AddZone(ZoneKind.FlameWall, p, b.Pos, GadgetFireRadius, GadgetFireLife);
            z.DmgMult = b.Dmg;
            z.BlockHp = 0f;
            PropBlast(b.Pos, GadgetFireRadius * 0.6f, b.Owner);
        }

        /// Host: Gas Grenade clouds, once landed, hurt and slow the enemies in them.
        void TickToxicGas(float dt)
        {
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.ToxicGas || TankAltInFlight(z))
                    continue;
                if (!z.Landed)
                {
                    z.Landed = true;
                    BroadcastFx(FxKind.Spark, z.Pos, z.Def.Color, 6f, 0.1f, Sfx.TankGasHiss);
                }
                foreach (var e in FoesInCircle(z.Pos, z.Radius, by: z.Owner))
                {
                    HurtEnemy(e, GadgetGasDps * z.DmgMult * dt, z.Owner);
                    Slow(e, 0.3f, 1f - GadgetGasSlow);
                }
            }
        }

        // --- Using and throwing --------------------------------------------------------------------------------------

        /// The local player taps the Gadget key: use the gadget in hand (switch it on or off, cast it, lob a charge to `at`, the cursor).
        public void UseGadget(V2 at) => SendGadget(0, at);

        /// The same, lobbing toward where you aim.
        public void UseGadget() => UseGadget(Me != null ? Me.AimAt : default(V2));

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
            if (action != 0)
            {
                DropHeldGadget(p, p.Pos + (at - p.Pos).LimitLength(GadgetThrowRange));
                return;
            }
            if (Throwable(p.Gadget))
            {
                if (p.GadgetCd > 0f)
                    return;
                ThrowCharge(p, at);
                if (--p.GadgetUses == 0)
                    FinishGadget(p);
                return;
            }
            if (p.Gadget == GadgetId.Medkit)
            {
                if (p.GadgetCd > 0f)
                    return;
                if (!UseMedkit(p))
                    return;
                p.GadgetCd = ThrowGap;  // (a double tap doesn't spend two)
                if (--p.GadgetUses == 0)
                    FinishGadget(p);
                return;
            }
            if (p.Gadget == GadgetId.Emp)
            {
                if (p.GadgetCd > 0f)
                    return;
                EmpPulse(p);
                if (--p.GadgetUses == 0)
                    FinishGadget(p);
                return;
            }
            if (p.Gadget == GadgetId.Cloak && !p.GadgetOn)
                p.VanishFrom = p.Pos;  // (enemies search where they last saw you)
            if (p.Gadget == GadgetId.Lodestone && !p.GadgetOn && (InHub(p.Pos, -1f) || p.WarpT > 0f))
                return;  // (already home)
            p.GadgetOn = !p.GadgetOn;
            if (p.Gadget == GadgetId.Lodestone && !p.GadgetOn)
                p.GadgetLeft = LodestoneChannel;
            Rgba ink = p.Gadget == GadgetId.ShieldGenerator ? ShieldInk : p.Gadget == GadgetId.Cloak ? new Rgba(0.5f, 0.46f, 0.95f) : p.Gadget == GadgetId.OverclockChip ? new Rgba(1f, 0.8f, 0.25f) : new Rgba(0.95f, 0.3f, 0.3f);
            BroadcastFx(FxKind.Ring, p.Pos, ink, p.GadgetOn ? 60f : 20f, 0.4f, p.GadgetOn ? Sfx.Ability : Sfx.Pick);
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
            float fly = g != null ? g.FlyT : 0f, left = g != null ? g.Left : 0f, fizzle = g != null ? g.Fizzle : 0f;
            byte uses = g != null ? g.Uses : (byte)0;
            s.Int(ref id);
            s.Byte(ref kind);
            s.Int(ref thrower);
            s.Int(ref owner);
            s.Vec(ref pos);
            s.Vec(ref from);
            s.Vec(ref to);
            s.Float(ref fly);
            s.Float(ref left);
            s.Byte(ref uses);
            s.Float(ref fizzle);  // a Tactical Vest drop's time left (TacticalVest.cs)
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
                g.Uses = uses;
                g.Fizzle = fizzle;
            }
            return id;
        }

        // --- Network: the player's gadget slot and bag fields (SyncPlayer) ---

        void SyncGadgetBag(NetSync s, Player p)
        {
            byte gadget = (byte)p.Gadget;  // Gadgets.cs
            s.Byte(ref gadget);
            p.Gadget = (GadgetId)gadget;
            s.Float(ref p.GadgetLeft);
            s.Bool(ref p.GadgetOn);
            s.Byte(ref p.GadgetUses);
            for (int i = 0; i < p.BagKind.Length; i++)  // the gadget bag
            {
                byte bk = (byte)p.BagKind[i];
                s.Byte(ref bk);
                p.BagKind[i] = (GadgetId)bk;
                s.Float(ref p.BagLeft[i]);
                s.Byte(ref p.BagUses[i]);
            }
        }
    }
}
