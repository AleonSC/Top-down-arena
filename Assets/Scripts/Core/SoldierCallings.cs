using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Soldier's Callings (Docs/Callings.md, "Soldier"): three per role, each a weapon that exists only for that Calling,
    /// with two cards and a Very Rare capstone. Each role's three come one of each way:
    ///   - **Replace** (Siege Cannon, Defib Cannon, Railspike): a massive weapon that takes the lobby weapon's place for the
    ///     rest of the run (Recalc sets `Player.Weapon`; every game works it out from the cards, nothing new is sent).
    ///   - **Second slot** (Slug Breacher, Nano Sprayer, Rocket Pod): you keep the lobby weapon and the Swap weapon key
    ///     (`Msg.CallingSwap`) puts the Calling's weapon in your hands, or back (`Player.SecondOut`, replicated).
    ///   - **Auto-fire** (Flak Mount, Lifeline Tether, Shoulder Minigun): a shoulder mount that works on its own beside your
    ///     weapon (TickSoldierCallings; `Player.MountAim`, `MountSpin` and `TetherTo` replicated for the look).
    /// Look: Game/SoldierCallingsLook.cs. Test suite `soldiercallings`; preview `SOLDIERCALL=<calling>[,<s>]`.
    public partial class Game
    {
        // --- Numbers (first guesses) -------------------------------------------------------------------------------------

        // Tank
        public const float SiegeBlast = 84f, SiegeKnock = 70f, SiegeSlow = 0.12f, SiegeCloseShare = 0.6f;  // you move 12% slower carrying it
        public const int SiegeBombardEvery = 3, SiegeBomblets = 4;
        public const float SlugStunBase = 0.35f, SlugShove = 40f, BreachBlast = 64f;
        public const int SlugPierceBase = 2, BreachLoads = 3;
        public const float FlakMountEvery = 0.6f, FlakMountReach = 170f, ReturnFireDmg = 18f;
        // Healer
        public const float DefibStun = 0.6f, DefibHeal = 10f, DefibFullStun = 1.2f, DefibRevive = 0.3f, DefibClearHeal = 20f, DefibClearReach = 140f;
        public const int DefibEvery = 5, DefibChain = 3;
        public const float NanoReach = 170f, NanoHalfArc = 0.44f, NanoDmg = 2.2f, NanoHeal = 1.6f, GooHeal = 15f, GooReach = 90f;
        public const float LtHeal = 6f, LtReach = 320f, LtSelfShare = 0.5f, ChanceTime = 3f, ChanceEvery = 30f, ChanceHealth = 0.3f;
        // DPS
        public const float SpikeMin = 50f, SpikeMax = 180f, SpikePin = 0.5f, SkewerPin = 1f, SkewerBlast = 40f, SkewerShare = 0.4f;
        public const float PodSpread = 0.9f, PodTurn = 4.5f, PodBlast = 38f;
        public const int PodRockets = 6;
        public const float MinigunReach = 420f, MinigunEvery = 0.09f, MinigunSpinUp = 0.6f, MinigunDmg = 4f, StormAfter = 3f;

        // --- What each Calling is ----------------------------------------------------------------------------------------

        public enum CallingWay : byte { None, Replace, SecondSlot, AutoFire }

        /// Is this one of the Soldier's Callings?
        public static bool SoldierCalling(CallingId c) => c >= CallingId.SiegeCannon && c <= CallingId.ShoulderMinigun;

        public static CallingWay WayOf(CallingId c)
        {
            switch (c)
            {
                case CallingId.SiegeCannon: case CallingId.DefibCannon: case CallingId.Railspike: return CallingWay.Replace;
                case CallingId.SlugBreacher: case CallingId.NanoSprayer: case CallingId.RocketPod: return CallingWay.SecondSlot;
                case CallingId.FlakMount: case CallingId.LifelineTether: case CallingId.ShoulderMinigun: return CallingWay.AutoFire;
                default: return CallingWay.None;
            }
        }

        /// The weapon a Calling gives (null for the mounts and the Summoner's minions).
        public static WeaponDef CallingWeapon(CallingId c)
        {
            switch (c)
            {
                case CallingId.SiegeCannon: return Defs.WeaponOf(WeaponId.SiegeCannon);
                case CallingId.SlugBreacher: return Defs.WeaponOf(WeaponId.SlugBreacher);
                case CallingId.DefibCannon: return Defs.WeaponOf(WeaponId.DefibCannon);
                case CallingId.NanoSprayer: return Defs.WeaponOf(WeaponId.NanoSprayer);
                case CallingId.Railspike: return Defs.WeaponOf(WeaponId.Railspike);
                case CallingId.RocketPod: return Defs.WeaponOf(WeaponId.RocketPod);
                default: return null;
            }
        }

        /// The player's run Calling, from their cards.
        public static CallingId CallingNow(Player p) => Upgrades.CallingOf(p.Taken);

        /// Can this player swap weapons (a second-slot Calling)?
        public static bool HasSecondSlot(Player p) => WayOf(CallingNow(p)) == CallingWay.SecondSlot;

        /// Recalc: the weapon in the player's hands. The lobby weapon is kept (`LobbyWeapon`) so a swap can go back to it.
        static void PickCallingWeapon(Player p)
        {
            if (p.Weapon == null || p.Weapon.Calling == CallingId.None)
                p.LobbyWeapon = p.Weapon;  // (whatever set it last: the lobby, a drill, a dev tool)
            var c = CallingNow(p);
            var cw = CallingWeapon(c);
            if (WayOf(c) != CallingWay.SecondSlot)
                p.SecondOut = false;
            p.Weapon = cw != null && (WayOf(c) == CallingWay.Replace || p.SecondOut) ? cw : p.LobbyWeapon ?? p.Weapon;
        }

        // --- Weapons ------------------------------------------------------------------------------------------------------

        /// Never in a lobby place (WeaponDef.Calling): Defs.WeaponRegistry keeps them aside.
        internal static readonly WeaponDef[] SoldierCallingWeapons =
        {
            new WeaponDef { Id = WeaponId.SiegeCannon, Calling = CallingId.SiegeCannon, Kit = Kit.Soldier, Role = Role.Tank, Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireSiege(p, dir, muzzle, dmg, extra),
                Speed = 480f, Life = 1.1f, Damage = 55f, BotRange = 340f, FireInterval = 1.25f, Name = "Siege Cannon", AreaDesc = "blasts 25% wider", ExtraDesc = "+1 bomblet round each blast",
                Desc = "Primary Fire: Huge, slow shells that burst and knock enemies back. You move 12% slower carrying it.", Sound = Sfx.ShotCannon },
            new WeaponDef { Id = WeaponId.SlugBreacher, Calling = CallingId.SlugBreacher, Kit = Kit.Soldier, Role = Role.Tank, Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireSlug(p, dir, muzzle, dmg, extra),
                Speed = 900f, Life = 0.55f, Damage = 38f, BotRange = 260f, FireInterval = 0.85f, Name = "Slug Breacher", ExtraDesc = "The slug goes through one more enemy and hits 15% harder",
                Desc = "Primary Fire: One heavy slug that blows through enemies, stunning and shoving them.", Sound = Sfx.ShotShotgun },
            new WeaponDef { Id = WeaponId.DefibCannon, Calling = CallingId.DefibCannon, Kit = Kit.Soldier, Role = Role.Healer, Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireDefib(p, dir, muzzle, dmg, extra),
                Speed = 700f, Life = 0.8f, Damage = 26f, BotRange = 360f, FireInterval = 0.9f, Name = "Defib Cannon", ExtraDesc = "The shock goes through one more enemy",
                Desc = "Primary Fire: Shocks that stun enemies and heal allies they pass. Every 5th is a full charge: twice as hard, and it stands up a downed ally it touches.", Sound = Sfx.SpellZap },
            new WeaponDef { Id = WeaponId.NanoSprayer, Calling = CallingId.NanoSprayer, Kit = Kit.Soldier, Role = Role.Healer, Cone = true, Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireNano(p, aim, dmg, extra),
                Range = (g, p, w, x) => p.Def.BodyR + NanoReachOf(p), Damage = NanoDmg, Reach = NanoReach, BotRange = 150f, FireInterval = 0.12f, Name = "Nano Sprayer", ExtraDesc = "+20% wider mist, 15% stronger",
                Desc = "Primary Fire: A mist of nanites that heals allies in it and corrodes enemies.", Sound = Sfx.None },
            new WeaponDef { Id = WeaponId.Railspike, Calling = CallingId.Railspike, Kit = Kit.Soldier, Role = Role.Dps, Charge = true, Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireSpike(p, dir, muzzle, dmg, extra),
                Range = (g, p, w, x) => p.Def.BodyR + w.Speed * w.Life * p.Stats.ShotSpeedMult, Speed = 1800f, Life = 0.8f, Damage = SpikeMax, BotRange = 520f, FireInterval = 0.6f, Name = "Railspike", ExtraDesc = "+15% damage",
                Desc = "Primary Fire: Hold to charge a steel spike that pierces everything across the arena. A full charge pins what it passes.", Sound = Sfx.ShotSniper },
            new WeaponDef { Id = WeaponId.RocketPod, Calling = CallingId.RocketPod, Kit = Kit.Soldier, Role = Role.Dps, Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FirePod(p, aim, muzzle, dmg, extra),
                Speed = 520f, Life = 1.3f, Damage = 14f, BotRange = 380f, FireInterval = 1.8f, Name = "Rocket Pod", AreaDesc = "blasts 25% wider", ExtraDesc = "+1 rocket a salvo",
                Desc = "Primary Fire: A salvo of homing rockets that burst on what they hit.", Sound = Sfx.HeliShot },
        };

        // --- Cards ---------------------------------------------------------------------------------------------------------

        static UpgradeDef Call(Role r, CallingId c, string name, string desc, Action<Stats> also = null) =>
            new UpgradeDef { Rarity = Rarity.Rare, Kit = Arena.Kit.Soldier, Role = r, Calling = c, CallingTier = 1, MaxStacks = 1, Name = name, Desc = desc,
                Apply = s => { s.Calling = (int)c; if (also != null) also(s); } };

        static UpgradeDef CallCard(Role r, CallingId c, string name, string desc, Action<Stats> apply) =>
            new UpgradeDef { Rarity = Rarity.Rare, Kit = Arena.Kit.Soldier, Role = r, Calling = c, CallingTier = 2, MaxStacks = 2, Name = name, Desc = desc, Apply = apply };

        static UpgradeDef Capstone(Role r, CallingId c, string name, string desc, Action<Stats> apply) =>
            new UpgradeDef { Rarity = Rarity.VeryRare, Kit = Arena.Kit.Soldier, Role = r, Calling = c, CallingTier = 3, MaxStacks = 1, Name = name, Desc = desc, Apply = apply };

        /// Tier 1 the Calling, tier 2 its cards (2 stacks), tier 3 the capstone. Each role sees only its own three.
        internal static readonly UpgradeDef[] SoldierCallingCards =
        {
            // Tank
            Call(Role.Tank, CallingId.SiegeCannon, "Call the Siege Cannon", "Calling: a Siege Cannon replaces your weapon for the rest of the run: huge shells that burst and knock enemies back. You move 12% slower",
                s => s.SpeedMult -= SiegeSlow),
            CallCard(Role.Tank, CallingId.SiegeCannon, "Bigger Bore", "Siege Cannon blasts are 25% wider and hit 15% harder", s => { s.SiegeBlastMult += 0.25f; s.SiegeDmgMult += 0.15f; }),
            CallCard(Role.Tank, CallingId.SiegeCannon, "Aftershock", "Siege Cannon blasts stun enemies for 0.5 s", s => s.SiegeStun += 0.5f),
            Capstone(Role.Tank, CallingId.SiegeCannon, "Bombardment", "Every 3rd Siege Cannon shell scatters 4 bomblets where it bursts", s => s.Bombardment = true),
            Call(Role.Tank, CallingId.SlugBreacher, "Call the Slug Breacher", "Calling: a Slug Breacher joins your weapon: press Swap weapon to switch. One heavy slug that blows through enemies, stunning them"),
            CallCard(Role.Tank, CallingId.SlugBreacher, "Door Kicker", "Slugs go through 1 more enemy and hit 20% harder", s => { s.SlugPierce += 1; s.SlugDmgMult += 0.2f; }),
            CallCard(Role.Tank, CallingId.SlugBreacher, "Concussive", "Slugs stun 0.4 s longer", s => s.SlugStun += 0.4f),
            Capstone(Role.Tank, CallingId.SlugBreacher, "Breach and Clear", "Swapping to the Slug Breacher loads 3 slugs that explode where they stop", s => s.BreachClear = true),
            Call(Role.Tank, CallingId.FlakMount, "Call the Flak Mount", "Calling: a flak gun on your shoulder for the rest of the run, shooting down enemy bullets near you on its own"),
            CallCard(Role.Tank, CallingId.FlakMount, "Quick Traverse", "The Flak Mount fires 30% faster", s => s.FlakMountRate += 0.3f),
            CallCard(Role.Tank, CallingId.FlakMount, "Wide Arc", "The Flak Mount reaches 60 px further", s => s.FlakMountReach += 60f),
            Capstone(Role.Tank, CallingId.FlakMount, "Return Fire", "Each bullet the Flak Mount shoots down sends a flak round back at the nearest enemy", s => s.ReturnFire = true),
            // Healer
            Call(Role.Healer, CallingId.DefibCannon, "Call the Defib Cannon", "Calling: a Defib Cannon replaces your weapon for the rest of the run: shocks that stun enemies and heal allies; every 5th stands up a downed ally"),
            CallCard(Role.Healer, CallingId.DefibCannon, "Overvolt", "Defib shocks hit and heal 25% more", s => s.DefibMult += 0.25f),
            CallCard(Role.Healer, CallingId.DefibCannon, "Quick Paddles", "Defib full charges come 1 shock sooner", s => s.DefibCut += 1),
            Capstone(Role.Healer, CallingId.DefibCannon, "Clear!", "A full charge arcs to 3 more enemies and heals every ally near where it hits for 20", s => s.DefibClear = true),
            Call(Role.Healer, CallingId.NanoSprayer, "Call the Nano Sprayer", "Calling: a Nano Sprayer joins your weapon: press Swap weapon to switch. A mist that heals allies in it and corrodes enemies"),
            CallCard(Role.Healer, CallingId.NanoSprayer, "Dense Mist", "The Nano Sprayer's mist reaches 30% further", s => s.NanoReachMult += 0.3f),
            CallCard(Role.Healer, CallingId.NanoSprayer, "Nanite Boost", "The Nano Sprayer heals and corrodes 30% more", s => s.NanoMult += 0.3f),
            Capstone(Role.Healer, CallingId.NanoSprayer, "Gray Goo", "Enemies that die in your mist burst, healing allies near them for 15", s => s.GrayGoo = true),
            Call(Role.Healer, CallingId.LifelineTether, "Call the Lifeline Tether", "Calling: a tether on your shoulder for the rest of the run keeps a healing beam on the most hurt ally near you"),
            CallCard(Role.Healer, CallingId.LifelineTether, "Strong Link", "The Lifeline Tether heals 40% more", s => s.TetherHealMult += 0.4f),
            CallCard(Role.Healer, CallingId.LifelineTether, "Long Line", "The Lifeline Tether reaches 120 px further", s => s.TetherReach += 120f),
            Capstone(Role.Healer, CallingId.LifelineTether, "Second Chance", "The tether reaches for a downed ally first and stands them up after 3 s, at 30% health; once every 30 s", s => s.SecondChance = true),
            // DPS
            Call(Role.Dps, CallingId.Railspike, "Call the Railspike", "Calling: a Railspike replaces your weapon for the rest of the run: hold to charge a spike that pierces everything across the arena"),
            CallCard(Role.Dps, CallingId.Railspike, "Capacitors", "The Railspike charges 25% faster", s => s.SpikeChargeMult *= 0.8f),
            CallCard(Role.Dps, CallingId.Railspike, "Tungsten Core", "Railspikes hit 25% harder", s => s.SpikeDmgMult += 0.25f),
            Capstone(Role.Dps, CallingId.Railspike, "Skewer", "A full charge pins what it passes for 1 s and blasts each, for 40% of the hit", s => s.Skewer = true),
            Call(Role.Dps, CallingId.RocketPod, "Call the Rocket Pod", "Calling: a Rocket Pod joins your weapon: press Swap weapon to switch. A salvo of 6 homing rockets"),
            CallCard(Role.Dps, CallingId.RocketPod, "Extra Tubes", "+2 rockets a salvo", s => s.PodExtra += 2),
            CallCard(Role.Dps, CallingId.RocketPod, "Warheads", "Rocket blasts are 30% wider and hit 15% harder", s => { s.PodBlastMult += 0.3f; s.PodDmgMult += 0.15f; }),
            Capstone(Role.Dps, CallingId.RocketPod, "Cluster Payload", "Each rocket bursts into 2 bomblets", s => s.PodCluster = true),
            Call(Role.Dps, CallingId.ShoulderMinigun, "Call the Shoulder Minigun", "Calling: a minigun on your shoulder for the rest of the run spins up and sprays the nearest enemy on its own"),
            CallCard(Role.Dps, CallingId.ShoulderMinigun, "Belt Feed", "The Shoulder Minigun fires 25% faster", s => s.MinigunRate += 0.25f),
            CallCard(Role.Dps, CallingId.ShoulderMinigun, "Tracer Rounds", "The Shoulder Minigun hits 25% harder and reaches 80 px further", s => { s.MinigunDmgMult += 0.25f; s.MinigunReach += 80f; }),
            Capstone(Role.Dps, CallingId.ShoulderMinigun, "Bullet Storm", "After 3 s of non-stop fire, the Shoulder Minigun fires two streams", s => s.BulletStorm = true),
        };

        // --- Fire ------------------------------------------------------------------------------------------------------------

        void FireSiege(Player p, V2 dir, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;
            var b = SpawnBullet(Defs.SiegeShell, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg * p.Stats.SiegeDmgMult, p.Id);
            b.Splash = SiegeBlast * AreaMult(p) * p.Stats.SiegeBlastMult;
            b.Burst = 1;  // bursts where its flight ends too (CallShotLanded)
            b.CallShot = (byte)CallingId.SiegeCannon;
            p.CallingN = (p.CallingN + 1) % SiegeBombardEvery;
            b.Frags = p.Stats.Bombardment && p.CallingN == 0 ? SiegeBomblets : 0;
            b.Frags += extra;
        }

        void FireSlug(Player p, V2 dir, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;
            var b = SpawnBullet(Defs.SlugRound, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg * p.Stats.SlugDmgMult * (1f + 0.15f * extra), p.Id);
            b.Pierce += SlugPierceBase + p.Stats.SlugPierce + extra;
            b.CallShot = (byte)CallingId.SlugBreacher;
            if (p.CallingN > 0)
            {
                p.CallingN--;  // Breach and Clear: a loaded slug explodes where it stops
                b.Burst = 1;
                b.Splash = BreachBlast * AreaMult(p);
            }
        }

        void FireDefib(Player p, V2 dir, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;
            p.CallingN++;
            bool full = p.CallingN >= DefibEveryOf(p);
            if (full)
                p.CallingN = 0;
            var b = SpawnBullet(Defs.DefibBolt, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg * p.Stats.DefibMult * (full ? 2f : 1f), p.Id);
            b.Pierce += 1 + extra;
            b.HealAlly = DefibHeal * p.Stats.DefibMult * (full ? 2f : 1f);
            b.CallShot = (byte)CallingId.DefibCannon;
            if (full)
            {
                b.Radius *= 1.6f;
                b.Full = true;
                if (p.Stats.DefibClear)
                    b.Chain = DefibChain;
                BroadcastFx(FxKind.Ring, muzzle, Defs.BulletColor(Defs.DefibBolt), 22f, 0.25f, Sfx.LightHeal);
            }
        }

        /// Shocks to a full charge (Quick Paddles: sooner).
        public static int DefibEveryOf(Player p) => Math.Max(2, DefibEvery - p.Stats.DefibCut);

        public static float NanoReachOf(Player p) => NanoReach * p.Stats.NanoReachMult * PierceReach(p);

        /// The Nano Sprayer: everything in the mist's cone, every FireInterval. Its look draws the mist from FiringSeen.
        void FireNano(Player p, float aim, float dmg, int extra)
        {
            float reach = p.Def.BodyR + NanoReachOf(p), arc = NanoHalfArc * (1f + 0.2f * extra) * AreaMult(p);
            foreach (var e in FoesInCircle(p.Pos, reach, by: p.Id))
                if (InArc(p.Pos, aim, arc, e.Pos))
                {
                    HurtEnemy(e, NanoDmg * dmg * p.Stats.NanoMult * (1f + 0.15f * extra), p.Id);
                    e.NanoT = 0.4f;  // (Gray Goo: it died in the mist)
                    e.NanoBy = p.Id;
                }
            foreach (var q in Players.Values)
                if (q != p && !q.Downed && q.Pos.CloserThan(p.Pos, reach) && InArc(p.Pos, aim, arc, q.Pos))
                    Heal(q, NanoHeal * p.Stats.NanoMult * (1f + 0.15f * extra), p, "Nano Sprayer");
        }

        static bool InArc(V2 from, float aim, float half, V2 at) => Math.Abs(MathX.WrapPi((at - from).Angle - aim)) <= half;

        void FireSpike(Player p, V2 dir, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;
            float c = BowCharge(p, p.BowShotDraw);
            bool full = c >= 0.999f;
            var b = SpawnBullet(Defs.RailSpikeShot, muzzle, dir * w.Speed, 0f, w.Life, MathX.Lerp(SpikeMin, SpikeMax, c) * dmg * p.Stats.SpikeDmgMult * (1f + 0.15f * extra), p.Id);
            b.Pierce = 999;
            b.CallShot = (byte)CallingId.Railspike;
            b.Full = full;
            if (full)
                BroadcastFx(FxKind.Ring, muzzle, Defs.BulletColor(Defs.RailSpikeShot), 18f, 0.2f, Sfx.None);
        }

        void FirePod(Player p, float aim, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;
            int n = PodRockets + p.Stats.PodExtra + extra;
            for (int i = 0; i < n; i++)
            {
                float a = aim + (n == 1 ? 0f : (i / (n - 1f) - 0.5f) * PodSpread);
                var b = SpawnBullet(Defs.PodRocket, muzzle, V2.FromAngle(a) * w.Speed * (i % 2 == 0 ? 1f : 0.88f), 0f, w.Life, w.Damage * dmg * p.Stats.PodDmgMult, p.Id, homingTurn: PodTurn);
                b.Splash = PodBlast * AreaMult(p) * p.Stats.PodBlastMult;
                b.Burst = 1;
                b.CallShot = (byte)CallingId.RocketPod;
                if (p.Stats.PodCluster)
                    SetFrags(b, 2, Defs.GrenadeShot, w.Damage * 0.4f * dmg, 220f, 0.25f, 30f * AreaMult(p), false);
            }
        }

        // --- Shots landing (Combat.cs) ------------------------------------------------------------------------------------

        /// A Calling shot hit an enemy (OnBulletHit).
        void CallShotHit(Bullet b, Enemy e)
        {
            Player p;
            if (b.CallShot == 0 || !Players.TryGetValue(b.Owner, out p))
                return;
            switch ((CallingId)b.CallShot)
            {
                case CallingId.SlugBreacher:
                    StunFor(e, SlugStunBase + p.Stats.SlugStun);
                    Shove(e, b.Vel.Normalized() * SlugShove);
                    break;
                case CallingId.DefibCannon:
                    StunFor(e, b.Full ? DefibFullStun : DefibStun);
                    if (b.Full && p.Stats.DefibClear)
                        foreach (var q in Players.Values)
                            if (!q.Downed && q.Pos.CloserThan(e.Pos, DefibClearReach))
                                Heal(q, DefibClearHeal * p.Stats.DefibMult, p, "Clear!");
                    break;
                case CallingId.Railspike:
                    if (b.Full)
                        StunFor(e, p.Stats.Skewer ? SkewerPin : SpikePin);
                    if (b.Full && p.Stats.Skewer)
                    {
                        foreach (var o in FoesInCircle(e.Pos, SkewerBlast * AreaMult(p), by: p.Id))
                            HurtEnemy(o, b.Dmg * SkewerShare, p.Id);
                        BroadcastFx(FxKind.Boom, e.Pos, Defs.BulletColor(Defs.RailSpikeShot), SkewerBlast, 0.25f, Sfx.None);
                    }
                    break;
            }
        }

        /// A Calling shot's splash went off on what it hit (the splash loop): the Siege Cannon's knockback, stun and bomblets.
        void CallShotBurst(Bullet b)
        {
            Player p;
            if (b.CallShot != (byte)CallingId.SiegeCannon || !Players.TryGetValue(b.Owner, out p))
                return;
            foreach (var e in FoesInCircle(b.Pos, b.Splash, by: p.Id))
            {
                V2 away = e.Pos - b.Pos;
                Shove(e, (away.Length > 1f ? away.Normalized() : V2.FromAngle(b.Vel.Angle)) * SiegeKnock * (1f - 0.5f * away.Length / Math.Max(1f, b.Splash)));
                if (p.Stats.SiegeStun > 0f)
                    StunFor(e, p.Stats.SiegeStun);
            }
            BroadcastFx(FxKind.Ring, b.Pos, Rgba.White, b.Splash * 1.1f, 0.25f, Sfx.None);
            for (int i = 0; i < b.Frags; i++)
            {
                V2 d = V2.FromAngle(MathX.Tau * i / b.Frags + Rand(-0.3f, 0.3f));
                var nb = SpawnBullet(Defs.GrenadeShot, b.Pos + d * 8f, d * 240f, 0f, 0.35f, b.Dmg * 0.35f, b.Owner);
                nb.Splash = 40f * AreaMult(p);
                nb.Bomblet = true;
                nb.Burst = 1;
            }
            b.Frags = 0;
        }

        /// A Calling shot's flight ended without a hit (the landed loop). True: handled.
        bool CallShotLanded(Bullet b)
        {
            if (b.CallShot == 0)
                return false;
            foreach (var e in FoesInCircle(b.Pos, b.Splash, by: b.Owner))
                HurtEnemy(e, b.Dmg * SiegeCloseShare, b.Owner);
            BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(b.Kind), b.Splash, 0.3f, Sfx.Explode);
            PropBlast(b.Pos, b.Splash, b.Owner);
            CallShotBurst(b);
            return true;
        }

        void StunFor(Enemy e, float t) => Stun(e, e.Def.IsBoss ? t * 0.35f : t);

        void Shove(Enemy e, V2 by)
        {
            if (!e.Def.Heavy && !e.Def.IsBoss)
                KnockEnemy(e, by);
        }

        // --- The swap (second-slot Callings) ------------------------------------------------------------------------------

        /// Your game: the Swap weapon key was pressed.
        void LocalSwap(Player me)
        {
            if (!HasSecondSlot(me) || me.Downed)
                return;
            Sounds.Add(Sfx.DiskClick);
            if (IsHost)
            {
                DoSwap(me);
                return;
            }
            session.SendToHost(Proto.End(Proto.Begin(Msg.CallingSwap)));
        }

        public void OnCallingSwap(int peer)
        {
            Player p;
            if (Players.TryGetValue(peer, out p) && HasSecondSlot(p) && !p.Downed)
                DoSwap(p);
        }

        /// Host: the other weapon into the player's hands (Recalc picks it; the snapshot tells everyone).
        void DoSwap(Player p)
        {
            p.SecondOut = !p.SecondOut;
            p.FireCd = Math.Max(p.FireCd, 0.2f);
            p.BowRelease = 0f;
            if (p.SecondOut && p.Stats.BreachClear && CallingNow(p) == CallingId.SlugBreacher)
                p.CallingN = BreachLoads;  // Breach and Clear
            Recalc(p);
            BroadcastFx(FxKind.Ring, p.Pos, Rgba.Gold, p.Def.BodyR + 12f, 0.2f, Sfx.None);
        }

        void SyncSoldierCalling(NetSync s, Player p)
        {
            bool was = p.SecondOut;
            s.Bool(ref p.SecondOut);
            s.Float(ref p.MountAim);
            s.Byte(ref p.MountSpin);
            s.Byte(ref p.TetherTo);
            s.Byte(ref p.CallingN);
            if (s.Reading && was != p.SecondOut)
                Recalc(p);  // (the weapon in their hands)
        }

        // --- The mounts (host) --------------------------------------------------------------------------------------------

        void TickSoldierCallings(float dt)
        {
            foreach (var e in Enemies.Values)
                if (e.NanoT > 0f)
                    e.NanoT -= dt;
            foreach (var p in Players.Values)
            {
                var c = CallingNow(p);
                if (!SoldierCalling(c))
                    continue;
                if (p.CallingT > 0f)
                    p.CallingT -= dt;
                if (c != CallingId.ShoulderMinigun)
                    p.MountSpin = 0;
                if (c != CallingId.LifelineTether)
                    p.TetherTo = 0;
                if (p.Downed || !Battle)
                {
                    p.MountSpin = 0;
                    p.TetherTo = 0;
                    continue;
                }
                if (c == CallingId.FlakMount) TickFlakMount(p, dt);
                else if (c == CallingId.LifelineTether) TickTether(p, dt);
                else if (c == CallingId.ShoulderMinigun) TickMinigun(p, dt);
                else if (c == CallingId.DefibCannon) DefibRevives(p);
            }
        }

        public static float FlakMountReachOf(Player p) => FlakMountReach + p.Stats.FlakMountReach;

        void TickFlakMount(Player p, float dt)
        {
            if ((p.CallingCd -= dt) > 0f)
                return;
            Bullet best = null;
            float bestD = FlakMountReachOf(p);
            foreach (var o in Bullets.Values)
            {
                if (!Defs.IsEnemyShot(o.Kind) || Defs.Unblockable(o.Kind))
                    continue;
                float d = o.Pos.DistanceTo(p.Pos);
                if (d < bestD)
                {
                    bestD = d;
                    best = o;
                }
            }
            if (best == null)
                return;
            p.CallingCd = FlakMountEvery / p.Stats.FlakMountRate;
            V2 at = best.Pos;
            p.MountAim = (at - p.Pos).Angle;
            BroadcastFx(FxKind.MountTracer, MountPos(p), at, Rgba.Gold, 2f, 0.12f, Sfx.ShotTurret);
            BroadcastFx(FxKind.Boom, at, new Rgba(0.7f, 0.85f, 1f), 14f, 0.18f, Sfx.None);
            KillBullet(best.Id);
            if (p.Stats.ReturnFire)
            {
                var e = NearestEnemy(at, 600f, by: p.Id);
                if (e != null)
                {
                    V2 d = (e.Pos - at).Normalized();
                    var rb = SpawnBullet(Defs.FlakShell, at, d * 620f, 0f, 0.8f, ReturnFireDmg * p.Stats.DmgMult, p.Id);
                    rb.Splash = 34f;
                }
            }
        }

        public static float TetherReachOf(Player p) => LtReach + p.Stats.TetherReach;

        void TickTether(Player p, float dt)
        {
            float reach = TetherReachOf(p);
            Player to = null;
            if (p.Stats.SecondChance && p.CallingT <= 0f)
                foreach (var q in Players.Values)
                    if (q != p && q.Downed && q.Pos.CloserThan(p.Pos, reach) && (to == null || q.Pos.DistanceTo(p.Pos) < to.Pos.DistanceTo(p.Pos)))
                        to = q;
            if (to != null)
            {
                if (p.TetherTo != (byte)to.Id)
                    p.ChanceT = 0f;
                p.TetherTo = (byte)to.Id;
                p.MountAim = (to.Pos - p.Pos).Angle;
                if ((p.ChanceT += dt) >= ChanceTime)
                {
                    RevivePlayer(to, ChanceHealth, p);
                    p.CallingT = ChanceEvery;
                    p.ChanceT = 0f;
                    BroadcastFx(FxKind.Ring, to.Pos, new Rgba(0.4f, 1f, 0.6f), 60f, 0.5f, Sfx.Revive);
                }
                return;
            }
            p.ChanceT = 0f;
            foreach (var q in Players.Values)
                if (q != p && !q.Downed && q.Hp < q.MaxHp && q.Pos.CloserThan(p.Pos, reach) && MoreHurt(q, to))
                    to = q;
            float heal = LtHeal * p.Stats.TetherHealMult * dt;
            if (to != null)
            {
                p.TetherTo = (byte)to.Id;
                p.MountAim = (to.Pos - p.Pos).Angle;
                Heal(to, heal, p, "Lifeline Tether");
            }
            else
            {
                p.TetherTo = 0;
                if (p.Hp < p.MaxHp)
                    Heal(p, heal * LtSelfShare, p, "Lifeline Tether");  // nobody hurt in reach: a trickle for you
            }
        }

        public static float MinigunReachOf(Player p) => MinigunReach + p.Stats.MinigunReach;

        void TickMinigun(Player p, float dt)
        {
            var e = NearestEnemy(p.Pos, MinigunReachOf(p), by: p.Id);
            float spin = p.MountSpin / 255f;
            spin = e != null ? Math.Min(1f, spin + dt / MinigunSpinUp) : Math.Max(0f, spin - dt / MinigunSpinUp);
            p.MountSpin = (byte)Math.Round(spin * 255f);
            if (e == null)
            {
                p.ChanceT = 0f;  // (the Bullet Storm's heat)
                return;
            }
            p.MountAim = (e.Pos - MountPos(p)).Angle;
            if (spin < 1f || (p.CallingCd -= dt) > 0f)
                return;
            p.CallingCd += MinigunEvery / p.Stats.MinigunRate;
            if (p.CallingCd < 0f)
                p.CallingCd = 0f;
            p.ChanceT += MinigunEvery / p.Stats.MinigunRate;
            int streams = p.Stats.BulletStorm && p.ChanceT >= StormAfter ? 2 : 1;
            V2 from = MountPos(p);
            for (int i = 0; i < streams; i++)
            {
                float a = p.MountAim + Rand(-0.05f, 0.05f) + (streams == 2 ? (i - 0.5f) * 0.16f : 0f);
                SpawnBullet(Defs.MinigunRound, from, V2.FromAngle(a) * 900f, 0f, MinigunReachOf(p) / 900f, MinigunDmg * p.Stats.DmgMult * p.Stats.MinigunDmgMult, p.Id);
            }
        }

        /// A full Defib charge touching a downed ally stands them up (host, every tick).
        void DefibRevives(Player p)
        {
            foreach (var b in Bullets.Values)
            {
                if (b.Owner != p.Id || !b.Full || b.CallShot != (byte)CallingId.DefibCannon)
                    continue;
                foreach (var q in Players.Values)
                    if (q != p && q.Downed && q.Pos.CloserThan(b.Pos, q.Def.HitR + b.Radius + 6f))
                    {
                        RevivePlayer(q, DefibRevive, p);
                        BroadcastFx(FxKind.Ring, q.Pos, Defs.BulletColor(Defs.DefibBolt), 54f, 0.45f, Sfx.Revive);
                    }
            }
        }

        /// Where a mount sits: over the shoulder away from the aim's left.
        public static V2 MountPos(Player p) => p.Pos + V2.FromAngle(p.Aim - MathX.Pi * 0.5f) * (p.Def.BodyR * 0.8f) + new V2(0f, -6f);

        /// Gray Goo (the Soldier's OnKill): an enemy that died in a Nano Sprayer's mist bursts, healing allies near it.
        internal void SoldierCallingKill(Enemy e)
        {
            Player p;
            if (e.NanoT <= 0f || !Players.TryGetValue(e.NanoBy, out p) || !p.Stats.GrayGoo)
                return;
            foreach (var q in Players.Values)
                if (!q.Downed && q.Pos.CloserThan(e.Pos, GooReach))
                    Heal(q, GooHeal * p.Stats.NanoMult, p, "Gray Goo");
            BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.55f, 1f, 0.8f), GooReach, 0.4f, Sfx.LightHeal);
        }
    }

    public partial class Enemy
    {
        public float NanoT;  // host: it was in a Nano Sprayer's mist this recently (Gray Goo; SoldierCallings.cs)
        public int NanoBy;   // host: whose mist
    }
}
