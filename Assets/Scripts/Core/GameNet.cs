using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Networking: client inputs, the host's snapshots, steering and effect broadcasts, and the
    /// client's handling of host messages.
    public partial class Game
    {
        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        public void OnInput(int peer, BinaryReader r)
        {
            V2 pos = r.ReadV2();
            float aim = r.ReadSingle();
            V2 aimAt = r.ReadV2();
            bool firing = r.ReadBoolean();
            bool parry = r.ReadBoolean();
            float draw = r.ReadSingle(), release = r.ReadSingle();
            bool focus = r.ReadBoolean();
            byte sprint = r.ReadByte();
            bool bot = r.ReadBoolean();
            byte steer = r.ReadByte();
            Player p;
            if (!Players.TryGetValue(peer, out p))
                return;
            if (!pos.IsFinite || !aimAt.IsFinite || !Finite(aim) || !Finite(draw) || !Finite(release))
                return;  // a broken or tampered message: NaN or infinity would spread through the host's game and every snapshot
            p.DrawT = draw;
            if (release > 0f && ChargeWeapon(p.Weapon.Id))
            {
                p.BowRelease = Math.Min(10f, release);  // the shooter timed it; one release per message
                p.BowFocus = focus;                     // and saw the Focus bar (BowFocus.cs)
            }
            if (!p.Downed)
                p.Pos = KeepInArena(pos, 0f);
            p.Aim = aim;
            p.AimAt = aimAt;
            p.IsBot = bot;
            p.Firing = firing;
            p.KnuckleSteer = steer;
            p.Parrying = parry;
            p.Sprint = p.Downed ? (byte)0 : sprint;  // only a look: the owner's game moves it
        }

        public void OnWeaponDash(int peer, BinaryReader r)
        {
            int kind = r.ReadByte();
            V2 target = r.ReadV2();
            if (target.IsFinite)
                HostWeaponDash(peer, kind, target);  // Ninja.cs
        }

        public void OnAbility(int peer, BinaryReader r)
        {
            int slot = r.ReadByte();
            V2 target = r.ReadV2();
            V2 from = r.ReadV2();
            if (slot < Upgrades.SlotCount && target.IsFinite && from.IsFinite)
            {
                Player p;
                StepFrom = Players.TryGetValue(peer, out p) ? KeepInArena(from, 0f) : (V2?)null;
                HostAbility(peer, slot, target, false);
                StepFrom = null;
            }
        }

        /// Host: the exact position and course of every homing (or gravity-bent) bullet.
        void SendSteer()
        {
            steerScratch.Clear();
            foreach (var b in Bullets.Values)
                if (b.Homing > 0f || b.Pulled || b.Kind == Defs.GreatStar)  // (the Great Star follows a drawn route only the host knows: GreatStar.cs)
                    steerScratch.Add(b);
            SendSteerOf(steerScratch);
        }

        readonly List<Bullet> steerScratch = new List<Bullet>();

        /// Host: these bullets' exact position and course (Msg.Steer), e.g. a shot that just bounced off a wall (Relics.cs).
        void SendSteerOf(List<Bullet> list)
        {
            if (list.Count == 0)
                return;
            var w = Proto.Begin(Msg.Steer);
            w.Write(list.Count);
            foreach (var b in list)
            {
                w.Write(b.Id);
                w.Write(b.Pos);
                w.Write(b.Vel);
            }
            session.Broadcast(Proto.End(w));
        }

        void ReadSteer(BinaryReader r)
        {
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                V2 pos = r.ReadV2(), vel = r.ReadV2();
                Bullet b;
                if (Bullets.TryGetValue(id, out b))
                {
                    b.Pos = pos;
                    b.Vel = vel;
                }
            }
        }

        void BroadcastFx(FxKind kind, V2 pos, Rgba color, float radius, float dur, Sfx sound)
        {
            BroadcastFx(kind, pos, pos, color, radius, dur, sound);
        }

        void BroadcastFx(FxKind kind, V2 pos, V2 pos2, Rgba color, float radius, float dur, Sfx sound)
        {
            AddFx(kind, pos, color, radius, dur);
            Effects[Effects.Count - 1].Pos2 = pos2;
            Sounds.Add(sound);
            var w = Proto.Begin(Msg.Fx);
            w.Write((byte)kind);
            w.Write(pos);
            w.Write(pos2);
            w.Write(color);
            w.Write(radius);
            w.Write(dur);
            w.Write((ushort)sound);
            session.Broadcast(Proto.End(w));
        }

        void SendSnapshot()
        {
            var w = Proto.Begin(Msg.Snapshot);
            SyncSnapshot(new NetSync(w));
            session.Broadcast(Proto.End(w));
        }

        // --- Client-side replication ------------------------------------------

        public void OnHostMessage(Msg type, BinaryReader r)
        {
            if (type == Msg.Start)
            {
                ReadStart(r);
                return;
            }
            if (!Running)
                return;
            switch (type)
            {
                case Msg.PathOffer:
                    ReadPathOffer(r);
                    break;
                case Msg.PathChosen:
                    ReadPathChosen(r);
                    break;
                case Msg.HuntMapState:
                    ReadHuntMapState(r);  // HuntMapGame.cs
                    break;
                case Msg.ExpandState:
                    ReadExpandState(r);
                    break;
                case Msg.DevTraps:
                    ReadDevTraps(r);  // DevTraps.cs
                    break;
                case Msg.TrapHit:
                    ReadTrapHit(r);  // RoomTraps.cs
                    break;
                case Msg.FogCells:
                    ReadFogCells(r);  // RoomFog.cs
                    break;
                case Msg.GateState:
                    ReadGateState(r);
                    break;
                case Msg.Record:
                    ReadRecord(r);  // RunSaves.cs
                    break;
                case Msg.ShopGift:
                    ReadShopGift(r);
                    break;
                case Msg.PathPatch:
                    ReadPathPatch(r);
                    break;
                case Msg.StarThrow:
                    ReadStarThrow(r);  // the Great Star's route (GreatStar.cs)
                    break;
                case Msg.Spawn:
                {
                    int n = r.ReadInt32();
                    for (int i = 0; i < n; i++)
                    {
                        int id = r.ReadInt32();
                        byte kind = r.ReadByte();
                        int owner = r.ReadInt32();
                        V2 pos = r.ReadV2(), vel = r.ReadV2();
                        float angVel = r.ReadSingle(), life = r.ReadSingle();
                        byte flags = r.ReadByte();
                        float homing = (flags & 2) != 0 ? r.ReadSingle() : 0f;
                        MakeBullet(id, kind, owner, pos, vel, angVel, life, (flags & 1) != 0, homing).Look = (ShotLook)((flags >> 2) & 3);
                    }
                    break;
                }
                case Msg.Remove:
                {
                    int n = r.ReadInt32();
                    for (int i = 0; i < n; i++)
                    {
                        int id = r.ReadInt32();
                        Bullet b;
                        if (Bullets.TryGetValue(id, out b))
                        {
                            RemovedFx(b);
                            Bullets.Remove(id);
                        }
                    }
                    break;
                }
                case Msg.ClearEnemyBullets:
                    ClearEnemyBullets();
                    break;
                case Msg.ReverseEnemyBullets:  // the Singularity's white hole (Singularity.cs)
                    ReverseEnemyBullets();
                    break;
                case Msg.Sound:
                    Sounds.Add((Sfx)r.ReadUInt16());
                    break;
                case Msg.CooldownCut:  // a caught Scythe Throw: that player's own game takes the share off (ReaperLoot.cs)
                {
                    int id = r.ReadInt32();
                    var ab = (AbilityId)r.ReadByte();
                    float share = r.ReadSingle();
                    if (id == MyId && Me != null && !IsHost)
                        ApplyCooldownCut(Me, ab, share);
                    break;
                }
                case Msg.HangoutSpawners:  // HangoutSpawners.cs
                    ReadSpawners(r);
                    break;
                case Msg.PracticePlayer:  // a loadout changed in the lobby while Practice is up (HangoutGame.cs)
                    OnPracticePlayer(r);
                    break;
                case Msg.Graze:  // an enemy shot skimmed past someone (Graze.cs)
                {
                    int id = r.ReadInt32();
                    V2 at = r.ReadV2();
                    V2 vel = r.ReadV2();
                    Player gp;
                    if (!IsHost && Players.TryGetValue(id, out gp))
                        LandGraze(gp, at, vel);
                    break;
                }
                case Msg.Carry:  // a Knight ability carries someone (the Gauntlet's charge, Bladed Dive's rush); only their own game moves them (Gauntlet.cs)
                {
                    int id = r.ReadInt32();
                    V2 to = r.ReadV2();
                    float time = r.ReadSingle();
                    if (id == MyId && !IsHost)
                        StartCarry(to, time);
                    break;
                }
                case Msg.Knock:  // a Riot Shield bashed someone; only their own game moves them (WardenFaction.cs)
                {
                    int id = r.ReadInt32();
                    V2 dir = r.ReadV2();
                    float dist = r.ReadSingle();
                    if (id == MyId)
                        StartKnock(dir, dist);
                    break;
                }
                case Msg.Teleport:  // Substitution moved someone; each player's own game moves them
                {
                    int id = r.ReadInt32();
                    V2 pos = r.ReadV2();
                    if (id == MyId && Me != null)
                    {
                        Me.Pos = Me.Disp = pos;
                        Me.SwallowT = 0f;  // spat out by the Devourer: don't snap back into it before the next snapshot
                    }
                    break;
                }
                case Msg.Fx:
                {
                    var kind = (FxKind)r.ReadByte();
                    V2 pos = r.ReadV2(), pos2 = r.ReadV2();
                    Rgba color = r.ReadRgba();
                    float radius = r.ReadSingle(), dur = r.ReadSingle();
                    AddFx(kind, pos, color, radius, dur);
                    Effects[Effects.Count - 1].Pos2 = pos2;
                    Sounds.Add((Sfx)r.ReadUInt16());
                    if (kind == FxKind.PerfectParry && color.A > 0.75f)
                        StartWorldSlow();  // the host's world slowed: this one's shots slow with it (Parry.cs)
                    break;
                }
                case Msg.Upgrades:
                {
                    int id = r.ReadInt32();
                    int n = r.ReadInt16();
                    Player p;
                    if (!Players.TryGetValue(id, out p))
                        break;
                    p.Taken.Clear();
                    for (int i = 0; i < n; i++)
                        p.Taken.Add(r.ReadInt16());
                    for (int k = 0; k < p.CardAbilities.Length; k++)
                        p.CardAbilities[k] = (AbilityId)r.ReadByte();
                    Recalc(p);
                    if (id == MyId)
                        awaitingPick = false;
                    break;
                }
                case Msg.DevStats:
                    ReadDevStats(r);
                    break;
                case Msg.Snapshot:
                    NoteSnapshotLag((int)r.BaseStream.Length);  // the lag chart (LagNet.cs)
                    ReadSnapshot(r);
                    break;
                case Msg.Steer:
                    ReadSteer(r);
                    break;
                case Msg.Pong:
                    NotePong((float)((clock.ElapsedTicks - r.ReadInt64()) * 1000.0 / System.Diagnostics.Stopwatch.Frequency));  // LagNet.cs
                    break;
                case Msg.BulletSync:
                    ReadBulletSync(r);
                    break;
                case Msg.RunStats:
                    ReadRunStats(r);
                    break;
                case Msg.RunStatsLive:
                    ReadRunStats(r, false);  // RunStats.cs
                    break;
                case Msg.WaveReady:
                    ReadWaveReady(r);  // WaveReady.cs
                    break;
                case Msg.WaveSummary:
                    ReadWaveSummary(r);
                    break;
                case Msg.NetPings:
                    ReadNetPings(r);  // LagNet.cs
                    break;
            }
        }

        void ReadSnapshot(BinaryReader r) => SyncSnapshot(new NetSync(r));

        // Reused by every snapshot a client reads (no per-snapshot allocations for these).
        readonly HashSet<int> snapSeen = new HashSet<int>();
        readonly List<int> snapGone = new List<int>();
        readonly List<Enemy> snapGoneEnemies = new List<Enemy>();
        readonly int[] snapCharges = new int[Upgrades.SlotCount];
        readonly float[] snapTimers = new float[Upgrades.SlotCount];
        readonly Player snapSkip = new Player { Id = -1 };  // soaks up a player this game doesn't know

        /// The snapshot, 20 times a second: the host writes the run's state and clients read it, both through
        /// this one method (NetSync), so a field can't be written in one order and read in another. To replicate
        /// something new, add one line in the right Sync method and bump Session.ProtocolVersion.
        void SyncSnapshot(NetSync s)
        {
            int state = (int)State;
            s.Byte(ref state);
            State = (MatchState)state;
            s.Int(ref Wave);
            s.Bool(ref BossSlot);  // the boss after wave Wave (Hunts.cs)
            s.Byte(ref Leg);       // a Campaign wave's leg (Hunts.cs)
            s.Byte(ref LivesUsed); // Checkpoints.cs
            s.Bool(ref RetryReady);
            s.Short(ref RetryWave);
            s.Float(ref StateTimer);
            s.Int(ref Remaining);
            s.Float(ref SurvTime);  // Survival mode's clock (SurvivalGame.cs)
            s.Int(ref Score);
            s.Bool(ref Paused);
            s.Int(ref PausedBy);
            s.Bool(ref Endless);
            s.Int(ref HuntedId);  // Hunted (Modifiers.cs)
            s.Vec(ref PullCenter);
            s.Float(ref PullStrength);
            s.Float(ref TwinReviveT);
            SyncReapers(s);  // Reapers.cs
            s.Bool(ref ShopOpen);
            s.Float(ref ShopT);
            SyncRoom(s);  // a challenge room's portal and room (Challenges.cs)
            SyncBossGate(s);  // the boss gate (BossGate.cs)
            SyncHoldingCells(s);  // the Warden's Holding Cells (HoldingCell.cs)
            SyncDrill(s);  // a Training drill's runners (DrillGame.cs)

            int count = Players.Count;
            s.Byte(ref count);
            if (s.Reading)
                for (int i = 0; i < count; i++)
                    SyncPlayer(s, null);
            else
                foreach (var p in Players.Values)
                    SyncPlayer(s, p);

            count = Enemies.Count;
            s.Short(ref count);
            if (s.Reading)
            {
                snapSeen.Clear();
                for (int i = 0; i < count; i++)
                    snapSeen.Add(SyncEnemy(s, null));
                snapGoneEnemies.Clear();
                foreach (var e in Enemies.Values)
                    if (!snapSeen.Contains(e.Id))
                        snapGoneEnemies.Add(e);
                foreach (var e in snapGoneEnemies)
                {
                    DeathFx(e, e.Disp);
                    Enemies.Remove(e.Id);
                }
            }
            else
                foreach (var e in Enemies.Values)
                    SyncEnemy(s, e);

            count = Minions.Count;
            s.Byte(ref count);
            if (s.Reading)
            {
                snapSeen.Clear();
                for (int i = 0; i < count; i++)
                    snapSeen.Add(SyncMinion(s, null));
                RemoveUnseen(Minions);
            }
            else
                foreach (var m in Minions.Values)
                    SyncMinion(s, m);

            count = Zones.Count;
            s.Byte(ref count);
            if (s.Reading)
            {
                Zones.Clear();
                for (int i = 0; i < count; i++)
                    SyncZone(s, null);
            }
            else
                foreach (var z in Zones.Values)
                    SyncZone(s, z);

            SyncInk(s);      // Chalk.cs: everyone's spirit ink
            SyncHazards(s);  // LateGame.cs
            SyncCages(s);    // Singularity.cs
            SyncAccretion(s);  // the Accretion rings' bands (Infall.cs)

            count = Orbs.Count;
            s.Short(ref count);
            if (s.Reading)
            {
                snapSeen.Clear();
                for (int i = 0; i < count; i++)
                    snapSeen.Add(SyncOrb(s, null));
                RemoveUnseen(Orbs);
            }
            else
                foreach (var o in Orbs.Values)
                    SyncOrb(s, o);
            SyncCoins(s);  // Coins.cs: only the coins dropped, moved or taken since the last snapshot (a full list now and then)
            SyncGadgets(s);  // Gadgets.cs
            SyncDevSpawners(s);  // DevSpawners.cs
            SyncProps(s);  // crates, barrels, chests and chest items (Props.cs)
            SyncLayout(s);  // the floor plan (LayoutRules.cs)
        }

        /// Drop what the last snapshot didn't mention (snapSeen holds what it did).
        void RemoveUnseen<T>(Dictionary<int, T> all)
        {
            snapGone.Clear();
            foreach (int id in all.Keys)
                if (!snapSeen.Contains(id))
                    snapGone.Add(id);
            foreach (int id in snapGone)
                all.Remove(id);
        }

        /// A player. p is null when reading: the id picks who it is. Your own position, aim, cooldowns, parry, and
        /// bow draw are your game's own, so those aren't overwritten for you.
        void SyncPlayer(NetSync s, Player p)
        {
            int id = p != null ? p.Id : 0;
            s.Int(ref id);
            if (s.Reading && !Players.TryGetValue(id, out p))
                p = snapSkip;
            bool known = p != snapSkip, mine = s.Reading && id == MyId;
            V2 pos = p.Pos;
            float aim = p.Aim, hp = p.Hp;
            s.Vec(ref pos);
            s.Float(ref aim);
            s.Float(ref hp);
            s.Float(ref p.Wound);  // (Combat.cs: the part of your health no heal can bring back (carried across waves))
            s.Bool(ref p.Downed);
            s.Float(ref p.Revive);
            s.Float(ref p.BarrierT);
            s.Float(ref p.RiotT);  // Riot Gear's recharge (RiotGear.cs)
            if (!s.Reading)
                p.BarrierFrac = p.BarrierMax > 0f ? p.BarrierHp / p.BarrierMax : 1f;  // the host draws from this too
            s.Float(ref p.BarrierFrac);
            s.Float(ref p.InvulnT);
            s.Float(ref p.OverdriveT);
            s.Float(ref p.AdrenalineT);
            s.Float(ref p.SurgeT);  // Arcane Surge (your own firing reads it)
            s.Int(ref p.MomentumStacks);  // Momentum.cs
            s.Float(ref p.MomentumT);
            s.Float(ref p.IronLost);  // Ironhide.cs
            s.Float(ref p.IronWait);
            s.Float(ref p.IronCap);
            s.Float(ref p.SwiftT);  // Consecrate's Swiftness (HolyCross.cs)
            s.Float(ref p.TauntT);
            s.Float(ref p.AegisT);
            s.Float(ref p.FrenzyT);
            s.Float(ref p.LegionT);
            s.Float(ref p.UltT);
            s.Float(ref p.DomeT);
            s.Vec(ref p.DomePos);
            bool parrying = p.Parrying, firing = p.Firing;  // a client sees the host's Firing as FiringSeen
            s.Bool(ref parrying);
            s.Bool(ref firing);
            s.Float(ref p.ParryHp);
            s.Float(ref p.ParryBroken);
            bool shellOn = p.ShellOn;
            s.Bool(ref shellOn);  // Blood Shell (BloodShell.cs)
            byte gadget = (byte)p.Gadget;  // Gadgets.cs
            s.Byte(ref gadget);
            p.Gadget = (GadgetId)gadget;
            s.Float(ref p.GadgetLeft);
            s.Bool(ref p.GadgetOn);
            s.Byte(ref p.GadgetUses);
            for (int i = 0; i < p.BagKind.Length; i++)  // the gadget bag (Gadgets.cs)
            {
                byte bk = (byte)p.BagKind[i];
                s.Byte(ref bk);
                p.BagKind[i] = (GadgetId)bk;
                s.Float(ref p.BagLeft[i]);
                s.Byte(ref p.BagUses[i]);
            }
            bool sprayOn = p.SprayOn;
            s.Bool(ref sprayOn);  // Arterial Spray (ArterialSpray.cs)
            bool standOn = p.StandOn;
            s.Bool(ref standOn);  // Last Stand (LastStand.cs)
            s.Float(ref p.StandAim);
            s.Float(ref p.ShieldHp);
            s.Float(ref p.ShieldT);
            s.Float(ref p.OverHp);       // a health shrine's health past max (Shrines.cs)
            s.ULong(ref p.ShrinesUsed);
            s.Float(ref p.GustT);  // Gust Gate's boost (GustGate.cs)
            s.Float(ref p.WardT);
            s.Float(ref p.StunT);
            s.Float(ref p.SwallowT);
            s.Float(ref p.WarpT);  // (BossBeam.cs)
            s.Float(ref p.PvpSlowT);  // a slow from another player (Pvp.cs)
            s.Float(ref p.PvpSlowMult);
            int grasped = p.Grasp.Count;  // Leech Tendrils' held enemies (Hemomancer.cs)
            s.Byte(ref grasped);
            if (s.Reading)
            {
                p.Grasp.Clear();
                p.GraspCharge.Clear();
            }
            for (int k = 0; k < grasped; k++)
            {
                int held = s.Reading ? 0 : p.Grasp[k];
                float charge = s.Reading || k >= p.GraspCharge.Count ? 0f : p.GraspCharge[k];
                s.Int(ref held);
                s.Float(ref charge);  // how far this one has built up toward its blast
                if (s.Reading)
                {
                    p.Grasp.Add(held);
                    p.GraspCharge.Add(charge);
                }
            }
            s.Float(ref p.JuggT);  // the Juggernaut Suit (Juggernaut.cs)
            s.Float(ref p.JuggArmor);
            s.Float(ref p.JuggMax);
            s.Float(ref p.TankT);  // A Literal Tank (LiteralTank.cs)
            s.Float(ref p.TankArmor);
            s.Float(ref p.TankMax);
            s.Float(ref p.ColT);  // Stone Colossus (StoneColossus.cs)
            s.Float(ref p.ColAge);
            s.Float(ref p.ColWind);
            s.Float(ref p.SpT);  // Spirit Avatar (SpiritAvatar.cs)
            s.Float(ref p.SpAge);
            s.Float(ref p.SpWind);
            s.Float(ref p.SpAng);
            s.Float(ref p.TitheClock);  // Vein Tithe (VeinTithe.cs)
            int tithed = Math.Min(255, p.Tithed.Count);
            s.Byte(ref tithed);
            if (s.Reading)
                p.Tithed.Clear();
            for (int k = 0; k < tithed; k++)
            {
                int held = s.Reading ? 0 : p.Tithed[k];
                s.Int(ref held);
                if (s.Reading)
                    p.Tithed.Add(held);
            }
            for (int k = 0; k < Player.WallPlates; k++)
                s.Byte(ref p.WallPlate[k]);
            s.Float(ref p.UnbreakT);
            s.Float(ref p.RodT);    // Lightning Rod's charge (LightningRod.cs)
            s.Float(ref p.VortexT);  // Vortex (Vortex.cs)
            s.Float(ref p.VortexAge);
            s.Float(ref p.VortexR);
            s.Float(ref p.BoltT);  // Living Bolt (LivingBolt.cs)
            s.Float(ref p.BoltAge);
            s.Float(ref p.RmT);  // Rodent Minigun (RodentMinigun.cs)
            if (p.RmT > 0f)
            {
                s.Float(ref p.RmAng);
                s.Float(ref p.RmLen);
            }
            s.Byte(ref p.HeliGuns);  // the Attack Helicopter (AttackHelicopter.cs): its guns, then their targets while it's out
            if (p.HeliGuns > 0)
            {
                s.Int(ref p.HeliTgt);
                s.Int(ref p.HeliTgt2);
            }
            else if (s.Reading)
                p.HeliTgt = p.HeliTgt2 = 0;
            s.Bool(ref p.DogmaOn);  // Dragon's Dogma (DragonsDogma.cs): the head and its quarry while it's out
            if (p.DogmaOn)
            {
                s.Vec(ref p.DogmaHead);
                s.Int(ref p.DogmaTgt);
            }
            else if (s.Reading)
                p.DogmaTgt = 0;
            s.Float(ref p.SteelT);  // Caught Steel (CaughtSteel.cs)
            s.Byte(ref p.Steel);
            s.Bool(ref p.GauntletDown);  // The Gauntlet lying where it hit (Gauntlet.cs)
            if (p.GauntletDown)
                s.Vec(ref p.GauntletRest);
            s.Float(ref p.VanishT);
            s.Byte(ref p.PlateN);  // Ceremonial Armor (CeremonialArmor.cs)
            if (p.PlateN > 0)
            {
                s.Float(ref p.PlateClock);
                s.Float(ref p.PlateNewT);
                s.Float(ref p.PlateMax);
                for (int k = 0; k < p.PlateN && k < Game.CerePlateMax; k++)
                {
                    int q = s.Reading ? 0 : (int)Math.Round(MathX.Clamp01(p.PlateHp[k] / Math.Max(1f, p.PlateMax)) * 255f);
                    s.Byte(ref q);
                    if (s.Reading)
                        p.PlateHp[k] = q / 255f * p.PlateMax;
                }
            }
            s.Vec(ref p.VanishFrom);
            s.Float(ref p.OrbitT);
            s.Float(ref p.DashT);  // Healing Dash's sprint (Ninja.cs)
            s.Byte(ref p.Sprint);  // the Dodge's held sprint (Dodge.cs)
            s.Byte(ref p.OrbitN);
            s.Float(ref p.HarvestClock);  // Crimson Harvest (CrimsonHarvest.cs)
            s.Float(ref p.HarvestBlood);
            s.Float(ref p.HarvestHold);
            s.Float(ref p.GaleT);  // Gale Wall (GaleWall.cs)
            if (p.GaleT > 0f)
            {
                s.Vec(ref p.GalePos);
                s.Float(ref p.GaleAim);
                s.Bool(ref p.GaleWide);
                int dots = Math.Min(255, p.GaleDots.Count);
                s.Byte(ref dots);
                if (s.Reading)
                    p.GaleDots.Clear();
                for (int k = 0; k < dots; k++)
                {
                    int v = s.Reading ? 0 : p.GaleDots[k], lat = v & 255, rad = (v >> 8) & 255;
                    s.Byte(ref lat);
                    s.Byte(ref rad);
                    if (s.Reading)
                        p.GaleDots.Add(lat | (rad << 8));
                }
            }
            else if (s.Reading)
                p.GaleDots.Clear();
            int drops = Math.Min(Game.WwMax, p.WwOff.Count);  // Water Ward (WaterWard.cs)
            s.Byte(ref drops);
            if (drops > 0)
                s.Float(ref p.WwAng);
            if (s.Reading)
            {
                p.WwOff.Clear();
                p.WwFall.Clear();
            }
            for (int k = 0; k < drops; k++)
            {
                int off = s.Reading ? 0 : (int)Math.Round(MathX.Fmod(p.WwOff[k], MathX.Tau) / MathX.Tau * 256f) & 255;
                int fall = s.Reading ? 0 : Math.Min(255, (int)Math.Ceiling(p.WwFall[k] * 100f));
                s.Byte(ref off);
                s.Byte(ref fall);
                if (s.Reading)
                {
                    p.WwOff.Add(off / 256f * MathX.Tau);
                    p.WwFall.Add(fall / 100f);
                }
            }
            s.Float(ref p.RainClock);  // Sanguine Rain (SanguineRain.cs)
            s.Int(ref p.HookId);
            s.Float(ref p.HookT);
            s.Byte(ref p.KusariStep);  // the Kusarigama's combo, throw and whirl (Kusarigama.cs)
            s.Float(ref p.KusariAim);
            s.Bool(ref p.KusariWhirl);
            s.Float(ref p.KusariHeld);
            s.Float(ref p.KusariAng);
            s.Byte(ref p.StarStance);  // the Shuriken's Great Star stance (GreatStar.cs)
            s.Byte(ref p.FumaStance);  // the Fuma Shuriken's Orbit stance (FumaOrbit.cs)
            if (p.FumaStance != 0)  // (both ends read the stance first, so they agree)
            {
                s.Byte(ref p.FumaMove);
                s.Byte(ref p.FumaStep);
                s.Byte(ref p.FumaStars);
                s.Byte(ref p.FumaMinis);
                s.Float(ref p.FumaClock);
                s.Float(ref p.FumaAng0);
                s.Float(ref p.FumaClk0);
                s.Float(ref p.FumaSpin);
                s.Float(ref p.FumaR0);
                s.Float(ref p.FumaMoveAt);
                s.Float(ref p.FumaLashAim);
                s.Float(ref p.FumaMiniAt);
            }
            s.Float(ref p.SpinUp);
            s.Int(ref p.PreyId);
            s.Byte(ref p.PackKills);
            s.Byte(ref p.Orbiters);
            s.Byte(ref p.Combo);
            s.Bool(ref p.KnuckleArmed);
            s.Byte(ref p.KnuckleDashes);
            s.Byte(ref p.KnuckleChain);
            s.Vec(ref p.KnuckleDashTo);   // where the Rocket Dash ends, and what it locked onto (the chain turns to it)
            s.Int(ref p.KnuckleTarget);
            s.Byte(ref p.KnuckleStance);  // the Arc Knuckles' stance, the Brawl move next, the Barrage's root and the Quake Punch's charge (ArcKnucklesBrawl.cs)
            s.Byte(ref p.KnuckleMove);
            s.Bool(ref p.KnuckleRooted);
            s.Byte(ref p.KnuckleCharge);  // the dash chain's stage: your own game roots you and shows the Fire prompt  // Rocket Dashes started: your own game dashes when it changes  // the Arc Knuckles' Counter Hammer is armed (ArcKnuckles.cs)
            s.Byte(ref p.KatanaStance);  // the Katana's stance, where each combo waits, the last step and the moving steps (Katana.cs)
            s.Byte(ref p.KatanaHay);
            s.Byte(ref p.KatanaSen);
            s.Byte(ref p.KatanaStep);
            s.Byte(ref p.KatanaDashes);
            s.Byte(ref p.BoStep);
            s.Byte(ref p.StaffStacks);  // the Quarterstaff's Jab stacks (Quarterstaff.cs)
            byte relic = (byte)p.Relic;
            s.Byte(ref relic);          // the relic slot (Relics.cs)
            if (s.Reading && (RelicId)relic != p.Relic) { p.Relic = (RelicId)relic; Recalc(p); } else p.Relic = (RelicId)relic;  // (a faction relic changes stats)
            s.Byte(ref p.TwinNades);    // the Twin Rifle's grenade tubes (TwinGrenades.cs)
            s.Byte(ref p.TwinNext);
            s.Byte(ref p.TwinCharge);
            s.Float(ref p.TwinAimL);    // the Twin Rifle Auto's guns and lock (TwinAuto.cs)
            s.Float(ref p.TwinAimR);
            s.Int(ref p.TwinLock);
            s.Byte(ref p.FangOut);      // Twin Fang's spinning sabres and where they are (TwinBlades.cs)
            s.Vec(ref p.FangAt);
            s.Byte(ref p.AmmoMove);     // Out of Ammo's knife chain, swats and spin (OutOfAmmo.cs)
            s.Byte(ref p.AmmoSeq);
            s.Byte(ref p.KnifeMove);
            s.Byte(ref p.KnifeSeq);
            s.Byte(ref p.SwatSeqL);
            s.Byte(ref p.SwatSeqR);
            s.Float(ref p.SwatAngL);
            s.Float(ref p.SwatAngR);
            s.Float(ref p.AmmoSpin);
            s.Byte(ref p.ScatShells);   // the Scattergun's Double Barrel shells (TankAlts.cs)
            s.Byte(ref p.ScatCharge);
            s.Byte(ref p.RepelMeter);   // the Repulsion Cannon's Undertow (Repulsion.cs)
            s.Bool(ref p.RepelPull);
            s.Byte(ref p.RailHeat);     // the Railgun's heat (RailBeam.cs)
            s.Byte(ref p.Embers);       // the Censer's Embers (Censer.cs)
            s.Byte(ref p.GreatStep);    // the Greatsword's chain (Greatsword.cs)
            s.Bool(ref p.LancePrimed);  // the Lance's hook primed the Breaching Thrust (Lance.cs)
            s.Byte(ref p.RapierTempo);  // the Rapier's Tempo and its grace period, drawn as diamonds behind you (Game/MeleeLooks.cs)
            s.Byte(ref p.RapierGraceLeft);
            s.Bool(ref p.RapierGuard);  // the Rapier's Riposte stance and its charge (Rapier.cs)
            s.Byte(ref p.RapierCharge);
            s.Byte(ref p.Light);
            s.Float(ref p.MercyT);  // a Lightbringer's ward (Lightbringer.cs)
            s.Float(ref p.DoseT);
            s.Vec(ref p.KunaiMark);
            s.Float(ref p.KunaiMarkT);
            s.Bool(ref p.BloodWallOn);  // Blood Wall (BloodWall.cs): the drain ring is drawn from how long it has been up
            s.Float(ref p.BloodWallClock);
            s.Float(ref p.DebtT);   // Blood Debt (BloodDebt.cs): the window, what's owed and what the cast took
            s.Float(ref p.Debt);
            s.Float(ref p.DebtOwed);
            s.Byte(ref p.Clot);
            s.Byte(ref p.ClotMax);
            int chained = Math.Min(255, p.Chains.Count);  // the Blood Lash's chains (BloodLash.cs)
            s.Byte(ref chained);
            if (s.Reading)
            {
                p.Chains.Clear();
                p.ChainLinks.Clear();
            }
            for (int k = 0; k < chained; k++)
            {
                int chainId = s.Reading ? 0 : p.Chains[k];
                byte links = s.Reading ? (byte)0 : p.ChainLinks[k];
                s.Int(ref chainId);
                s.Byte(ref links);
                if (s.Reading)
                {
                    p.Chains.Add(chainId);
                    p.ChainLinks.Add(links);
                }
            }
            s.Bool(ref p.LeashOn);  // Martyr's Leash
            s.Float(ref p.LeashT);
            s.Int(ref p.BsTarget);  // the Blood Brawler's mark, thread and Sympathy (BloodBrawler.cs)
            s.Float(ref p.BsT);
            s.Float(ref p.SympT);
            s.Int(ref p.SympSrc);
            int bound = Math.Min(255, p.SympBound.Count);
            s.Byte(ref bound);
            if (s.Reading)
                p.SympBound.Clear();
            for (int k = 0; k < bound; k++)
            {
                int boundId = s.Reading ? 0 : p.SympBound[k];
                s.Int(ref boundId);
                if (s.Reading)
                    p.SympBound.Add(boundId);
            }
            int reserve = (int)p.BloodReserve;
            s.Byte(ref reserve);
            if (s.Reading)
                p.BloodReserve = reserve;
            s.Float(ref p.CoatT);
            s.Byte(ref p.RevStance);  // the Revolver (Weapons.cs, PistolWhip.cs)
            s.Byte(ref p.RevStep);
            s.Bool(ref p.RevCocked);
            s.Float(ref p.RevCockT);
            s.Float(ref p.RevT);
            s.Float(ref p.RevFanReload);
            s.Byte(ref p.StunShots);
            s.Byte(ref p.PactRez);
            s.Vec(ref p.LightPos);
            s.Bool(ref p.RadiantOn);
            s.Vec(ref p.RadiantPos);
            s.Byte(ref p.DbUsed);
            s.Byte(ref p.DbBarrel);
            s.Byte(ref p.DbShot);
            s.Float(ref p.DbReload);
            s.Byte(ref p.GlMode);  // the Grenade Launcher (GrenadeLauncher.cs)
            s.Byte(ref p.GlUsed);
            s.Byte(ref p.GlSmokeUsed);
            s.Byte(ref p.GlShot);
            s.Float(ref p.GlReload);
            s.Float(ref p.GlSwap);
            s.Float(ref p.GlCharge);
            s.Float(ref p.WcTempestT);  // Wind Clash's Tempest (WindClash.cs)
            s.Float(ref p.WcTempestCd);
            s.Float(ref p.FlZeroT);  // the Frost Lance's Absolute Zero and Rime Counter (FrostLance.cs)
            s.Float(ref p.FlZeroCd);
            s.Bool(ref p.FlRime);
            s.Bool(ref p.BbBrace);  // the Bulwark Barrier (BulwarkBarrier.cs)
            s.Float(ref p.BbRam);
            s.Byte(ref p.BbBashes);
            s.Vec(ref p.BbBashTo);
            s.Bool(ref p.HbSet);  // the Halberd's set pike (Halberd.cs)
            s.Float(ref p.HbPikeA);
            s.Short(ref p.SsMask);  // the Stonespear (Stonespear.cs)
            s.Byte(ref p.SsSide);
            s.Int(ref p.SsLock);
            s.Float(ref p.HoneyFill);  // the Honeybee Staff (HoneybeeStaff.cs)
            s.Bool(ref p.HoneyOn);
            s.Float(ref p.Cinder);  // the Wyvern Staff (WyvernStaff.cs)
            s.Bool(ref p.NunGuard);  // the Nunchaku (Nunchaku.cs)
            s.Byte(ref p.NunSwats);
            s.Byte(ref p.NodStance);  // the Nodachi (Nodachi.cs)
            s.Byte(ref p.NodStorm);
            s.Byte(ref p.NodGuard);
            s.Byte(ref p.NodCode);
            s.Byte(ref p.NodCount);
            s.Byte(ref p.VgStance);  // the Vowguard (Vowguard.cs)
            s.Byte(ref p.VgAtV);
            s.Byte(ref p.VgAtS);
            s.Byte(ref p.VgMoveId);
            s.Byte(ref p.VgCount);
            s.Byte(ref p.VgFaith);
            s.Vec(ref p.VgDashTo);
            s.Byte(ref p.TpSide);  // the Twin Pistols (TwinPistols.cs)
            s.Float(ref p.TpLaserT);
            s.Float(ref p.TpCd);
            s.Bool(ref p.StaticOn);  // Static Shock (StaticShock.cs)
            s.Float(ref p.OverchargeT);
            s.Float(ref p.SupplyT);  // Supply Drop (SupplyDrop.cs)
            for (int k = 0; k < 2; k++)  // the Embolus Spears (EmbolusSpears.cs)
            {
                s.Byte(ref p.EsSt[k]);
                s.Vec(ref p.EsPos[k]);
                s.Vec(ref p.EsDir[k]);
            }
            s.Short(ref p.NsCount);  // the Needle Swarm (NeedleSwarm.cs)
            s.Float(ref p.NsBurstCd);
            for (int k = 0; k < Game.NsTendrils; k++)
            {
                s.Int(ref p.NsTen[k]);
                s.Float(ref p.NsLen[k]);
                s.Byte(ref p.NsBound[k]);
            }
            int tagged = Math.Min(Game.ArcTagCap, p.ArcTags.Count);  // Arc Lightning's tags (ArcLock.cs)
            s.Byte(ref tagged);
            if (s.Reading)
                p.ArcTags.Clear();
            for (int k = 0; k < tagged; k++)
            {
                int tagId = s.Reading ? 0 : p.ArcTags[k];
                s.Int(ref tagId);
                if (s.Reading)
                    p.ArcTags.Add(tagId);
            }
            float drawT = p.DrawT;
            s.Float(ref drawT);
            s.Short(ref p.Level);
            s.Float(ref p.Xp);
            s.Byte(ref p.PendingPicks);
            int rerolls = p.Rerolls;
            s.Short(ref rerolls);
            s.Byte(ref p.PendingLegendary);
            s.Bool(ref p.OfferLegendary);
            s.Byte(ref p.PendingMods);
            s.Bool(ref p.OfferMod);
            s.Byte(ref p.PendingMilestones);
            s.Bool(ref p.OfferMilestone);
            s.Byte(ref p.PrizePicks);
            s.Bool(ref p.OfferPrize);
            s.Byte(ref p.PendingCards);
            s.Bool(ref p.OfferCards);
            s.Byte(ref p.WavePicks);
            s.Bool(ref p.OfferWave);
            int offers = p.Offer != null ? p.Offer.Length : 0;
            s.Byte(ref offers);
            var offer = s.Reading ? (offers > 0 ? new int[offers] : null) : p.Offer;
            for (int k = 0; k < offers; k++)
                s.Short(ref offer[k]);  // card ids are 2 bytes; -1 (no card) survives the trip
            for (int k = 0; k < Upgrades.SlotCount; k++)
            {
                snapCharges[k] = p.Slots[k].Charges;
                snapTimers[k] = p.Slots[k].Timer;
                s.Byte(ref snapCharges[k]);
                s.Float(ref snapTimers[k]);
            }
            SyncMastery(s, p, known);  // Mastery.cs (it may Recalc, which touches health and slots: those come after)
            SyncShop(s, p);            // Shop.cs
            SyncVectors(s, p);         // ExpansionGame.cs
            SyncArcanist(s, p);        // Arcanist.cs
            if (!s.Reading || !known)
                return;

            if (!mine)
            {
                p.Pos = pos;
                p.Aim = aim;
                for (int k = 0; k < Upgrades.SlotCount; k++)
                {
                    p.Slots[k].Charges = snapCharges[k];
                    p.Slots[k].Timer = snapTimers[k];
                }
                p.Parrying = parrying;  // your own comes from your mouse, without the round trip
                p.FiringSeen = firing;
                p.DrawT = drawT;        // your own draw is measured here
            }
            p.ShellOn = shellOn;
            p.SprayOn = sprayOn;
            p.StandOn = standOn;
            if (hp < p.Hp)
                p.Flash = 0.1f;
            p.Hp = hp;
            p.Rerolls = rerolls;
            if (mine && RerollPending && (rerolls < rerollsAtRequest || offers == 0))
                rerollsAtRequest = -1;
            p.Offer = offer == null || (mine && awaitingPick) ? null : offer;
        }

        /// An enemy; returns its id. Reading, a new id brings a new enemy.
        int SyncEnemy(NetSync s, Enemy e)
        {
            int id = 0, type = 0;
            V2 pos = default(V2);
            float hp = 0f, maxHp = 0f, facing = 0f;
            bool spawning = false, elite = false;
            byte phase = 0, status = 0;
            int bleedStacks = 0, stars = 0, traits = 0, bubble = 0, cells = 0;
            bool winding = false, penned = false, dormant = false, wet = false, hit = false, venom = false, charged = false, slowed = false;
            int bleedHits = 0, wetTenths = 0;
            if (!s.Reading)
            {
                bleedHits = e.BleedT > 0f ? e.BleedHits : 0;
                traits = e.Traits;
                winding = e.Winding;
                hit = e.Flash > 0f;  // a real hit in the last 0.06 s (snapshots are 0.05 s apart); burning and bleeding don't count
                penned = e.PenId != 0;
                dormant = e.Dormant;
                wet = e.WetT > 0f;
                e.Wet = wet;
                wetTenths = wet ? (int)Math.Min(255f, Math.Ceiling(e.WetT * 10f)) : 0;
                e.WetLeft = wetTenths / 10f;
                e.WetPeak = wet ? Math.Max(e.WetPeak, e.WetLeft) : 0f;
                venom = e.VenomT > 0f;
                e.Venomed = venom;
                charged = e.ChargedT > 0f;
                e.Charged = charged;
                slowed = ShowsSlowed(e);  // any slow (Chill.cs; Chilled, below, is ice only)
                e.BubbleFrac = e.BubbleHp > 0f && e.BubbleMax > 0f ? Math.Min(1f, e.BubbleHp / e.BubbleMax) : 0f;
                bubble = (int)Math.Ceiling(e.BubbleFrac * 255f);
                if (e.BubbleHp > 0f)
                    SyncCells(e);
                cells = CellMask(e);
                e.Cells = cells;  // the host draws from it too
                id = e.Id;
                type = (int)e.Type;
                pos = e.Pos;
                hp = e.Hp;
                maxHp = e.MaxHp;
                spawning = e.SpawnT > 0f;
                facing = e.Facing;
                phase = e.Phase;
                elite = e.Elite;
                e.Marked = e.MarkT > 0f;  // the host draws from these too
                e.Burning = e.BurnT > 0f || e.PoolT > 0f;
                e.Frozen = IceFrozen(e);  // real ice only: Glacial Tomb, a third chill, Blood Freeze (a Stun alone isn't ice: Daze.cs)
                e.Chilled = !e.Frozen && (e.FrostChill > 0 || e.IceSlowT > 0f);  // ice only (Chill.cs)
                e.Slowed = slowed;
                e.Bleeding = e.BleedT > 0f;
                status = (byte)((e.Marked ? 1 : 0) | (e.Burning ? 2 : 0) | (e.Chilled ? 4 : 0) | (e.Frozen ? 8 : 0) | (e.Bleeding ? 16 : 0) | (e.Caged ? 32 : 0) | (e.Suppress >= SuppressShown ? 64 : 0) | (e.PoisonT > 0f ? 128 : 0));
                bleedStacks = e.BleedStacks;
                stars = e.StarsStuck;
                e.Suppressed = e.Suppress >= SuppressShown;
                e.Poisoned = e.PoisonT > 0f;
                e.Dazed = e.DazeT > 0f || e.StunT > 0f;  // (a Stun shows the same stars)
            }
            s.Int(ref id);
            s.Byte(ref type);
            s.Vec(ref pos);
            s.Float(ref hp);
            s.Float(ref maxHp);
            s.Bool(ref spawning);
            s.Float(ref facing);
            s.Byte(ref phase);
            s.Bool(ref elite);
            s.Byte(ref status);
            s.Byte(ref bleedStacks);  // bleed stacks on it, drawn as drops gathering on it
            s.Byte(ref stars);       // Shuriken stars stuck in it
            s.Byte(ref traits);      // elite traits and a bubble (Variety.cs)
            s.Byte(ref bubble);
            if (bubble > 0)
                s.Int(ref cells);  // the bubble's twelve cells (Variety.cs)
            s.Bool(ref winding);  // about to fire (Readability.cs)
            s.Bool(ref penned);   // held in by a chalk loop (Chalk.cs)
            s.Bool(ref dormant);  // asleep in an unseen room (DormantGroups.cs)
            s.Byte(ref bleedHits);  // claw hits stacked on it, for the Bear Totem's scars (BearLooks.cs)
            s.Bool(ref wet);        // soaked by a Tsunami (Tsunami.cs)
            s.Byte(ref wetTenths);  // ...and its tenths of a second left, for the effect bar (Game/EffectBar.cs)
            s.Bool(ref venom);      // Poison Dart's venom (PoisonDart.cs)
            s.Bool(ref charged);    // charged by lightning (Charged.cs)
            s.Bool(ref slowed);     // slowed by anything (Chill.cs; Chilled, in the status bits, is ice only)
            s.Bool(ref hit);        // just hit: the white flash (not every drop in health, or a burning enemy would stay white)
            int challengeBy = !s.Reading && e.ChallengeT > 0f ? e.ChallengeBy : 0;
            s.Byte(ref challengeBy);  // a Gauntlet's Challenge: who threw it (0: none) and how long it has left (Gauntlet.cs)
            float challengeT = !s.Reading && challengeBy > 0 ? e.ChallengeT : 0f;
            if (challengeBy > 0)
                s.Float(ref challengeT);
            bool katanaShock = !s.Reading && e.KatanaShockT > 0f;
            s.Bool(ref katanaShock);  // marked by a Momentum Burst (Katana.cs)
            bool dazed = !s.Reading && e.Dazed;
            s.Bool(ref dazed);  // dazed by a close-in hit: the stars over its head (Daze.cs)
            int variant = !s.Reading ? (int)e.Variant : 0;
            s.Byte(ref variant);  // a variant of its type (EnemyVariants.cs)
            int frostChill = !s.Reading ? e.FrostChill : 0;
            s.Byte(ref frostChill);  // the Frost Lance's chill on it (FrostLance.cs)
            int blastStacks = !s.Reading ? e.BlastStacks : 0, blastOwner = !s.Reading ? e.BlastOwner : 0;
            s.Byte(ref blastStacks);  // Blood Blast stacks on it, and whose (BloodBlast.cs)
            if (blastStacks > 0)
                s.Byte(ref blastOwner);
            var kind = (EnemyType)type;
            if (s.Reading && !Enemies.TryGetValue(id, out e))
            {
                var d = Defs.Get(kind);
                e = new Enemy { Id = id, Type = kind, Def = d, Radius = d.Radius, Pos = pos, Disp = pos, Hp = hp };
                Enemies[id] = e;
            }
            if (s.Reading)
            {
                if (hit)
                    e.Flash = Math.Max(e.Flash, 0.06f);
                e.Pos = pos;
                e.Hp = hp;
                e.MaxHp = maxHp;
                e.KatanaShocked = katanaShock;
                e.Dazed = dazed;
                e.Variant = (EnemyVariant)variant;
                e.FrostChill = (byte)frostChill;
                e.BlastStacks = (byte)blastStacks;
                e.BlastOwner = blastStacks > 0 ? blastOwner : 0;
                e.Spawning = spawning;
                e.Facing = facing;
                e.Phase = phase;
                e.Elite = elite;
                e.Marked = (status & 1) != 0;
                e.Burning = (status & 2) != 0;
                e.Chilled = (status & 4) != 0;
                e.Frozen = (status & 8) != 0;
                e.Bleeding = (status & 16) != 0;
                e.Caged = (status & 32) != 0;
                e.Suppressed = (status & 64) != 0;
                e.Poisoned = (status & 128) != 0;
                e.BleedStacks = bleedStacks;
                e.StarsStuck = stars;
                e.Traits = (byte)traits;
                e.BubbleFrac = bubble / 255f;
                e.Cells = bubble > 0 ? cells : 0;
                e.Winding = winding;
                e.Penned = penned;
                e.Dormant = dormant;
                e.BleedHits = bleedHits;
                e.Wet = wet;
                e.WetLeft = wetTenths / 10f;
                e.WetPeak = wet ? Math.Max(e.WetPeak, e.WetLeft) : 0f;
                e.Venomed = venom;
                e.Charged = charged;
                e.Slowed = slowed;
                e.ChallengeBy = challengeBy;
                e.ChallengeT = challengeT;
            }
            if (kind == EnemyType.Warden)
                SyncWarden(s, e);  // Warden.cs
            else if (kind == EnemyType.HiveQueen)
                SyncQueen(s, e);   // Queen.cs
            else if (kind == EnemyType.Singularity)
                SyncSing(s, e);    // Singularity.cs
            else if (kind == EnemyType.StormHerald)
                SyncHerald(s, e);  // Herald.cs
            else if (kind == EnemyType.Eclipse)
                SyncEclipse(s, e); // Eclipse.cs
            else if (kind == EnemyType.Devourer)
                SyncDevour(s, e);  // Devourer.cs
            else if (kind == EnemyType.Duelist || kind == EnemyType.BladeDancer || kind == EnemyType.Gallant || kind == EnemyType.Harlequin)
                SyncDuel(s, e);    // Duelist.cs (a Blade Dancer's lunge and blades: CourtFaction.cs)
            else if (kind == EnemyType.Lich)
                SyncLich(s, e);    // Lich.cs
            else if (kind == EnemyType.Colossus)
                SyncColossus(s, e);  // Colossus.cs
            else if (kind == EnemyType.BoundWraith)
                SyncBound(s, e);   // ReaperFaction.cs: its partner, a revive
            else if (kind == EnemyType.Stomper)
                SyncStomper(s, e);   // QuarryFaction.cs
            else if (kind == EnemyType.Driller || kind == EnemyType.Geomancer)
                SyncCrack(s, e);     // QuarryFaction.cs
            else if (kind == EnemyType.Butcher || kind == EnemyType.Gulper || kind == EnemyType.Gnawling || kind == EnemyType.Gutworm || kind == EnemyType.Guard || kind == EnemyType.Thug)
                SyncLarder(s, e);    // LarderFaction.cs (a Guard's swing: GuardSwing.cs, a Thug's punch: ThugPunch.cs)
            else if (IsNonShooter(kind))
                SyncNonShooter(s, e);  // NonShooters.cs: the fuse, the aim, the harpoon, a pod's arming
            if (s.Reading)
                e.Radius = kind == EnemyType.Singularity ? SingRadius(e) : kind == EnemyType.Devourer ? DevourRadius(e) : Defs.Get(kind).Radius * (elite ? EliteSize : 1f);
            return id;
        }

        /// A minion; returns its id. Reading, a new id brings a new minion.
        int SyncMinion(NetSync s, Minion m)
        {
            int id = 0, kind = 0, owner = 0, link = 0, stacks = 0;
            V2 pos = default(V2);
            float aim = 0f, t = 0f, duration = 0f, guard = 0f, scale = 0f, wind = 0f;
            int pact = 255;
            if (!s.Reading)
            {
                id = m.Id;
                kind = (int)m.Kind;
                owner = m.Owner;
                pos = m.Pos;
                aim = m.Aim;
                t = m.T;
                duration = m.Duration;
                m.GuardFrac = m.Kind == MinionKind.CloneWall || (m.Kind == MinionKind.Wall && m.MaxHp > 0f) ? MathX.Clamp01(m.BlockHp / Math.Max(1f, m.MaxHp))  // a wall clone: what is left of its health
                    : HasHealth(m) || m.Kind == MinionKind.Wolf && m.Companion && m.MaxHp > 0f ? m.Hp / Math.Max(1f, m.MaxHp)  // a Warden's Guard, Force Pact thrall or Wolf Whistle wolf: its health
                    : m.Def.BlockCapacity > 0f && m.StaggerT <= 0f ? MathX.Clamp01(m.BlockHp / (m.Def.BlockCapacity * m.Scale)) : 0f;
                guard = m.GuardFrac;
                scale = m.Scale;
                link = m.LinkTo;
                stacks = m.Stacks;
                wind = m.Wind;
                pact = m.Pact;
            }
            s.Int(ref id);
            s.Byte(ref kind);
            s.Int(ref owner);
            s.Vec(ref pos);
            s.Float(ref aim);
            s.Float(ref t);
            s.Float(ref duration);
            s.Float(ref guard);
            s.Float(ref scale);
            s.Int(ref link);
            s.Byte(ref stacks);
            s.Float(ref wind);
            s.Byte(ref pact);
            if (s.Reading)
            {
                if (!Minions.TryGetValue(id, out m))
                {
                    m = new Minion { Id = id, Kind = (MinionKind)kind, Owner = owner, Pos = pos, Disp = pos };
                    Minions[id] = m;
                }
                m.Pos = pos;
                m.Aim = aim;
                m.T = t;
                m.Duration = duration;
                m.GuardFrac = guard;
                m.Scale = scale;
                m.LinkTo = link;
                m.Stacks = stacks;
                m.Wind = wind;
                m.Pact = (byte)pact;
            }
            return id;
        }

        /// A ground zone (clients rebuild the list from each snapshot).
        void SyncZone(NetSync s, Zone z)
        {
            if (s.Reading)
                z = new Zone();
            else
                z.GuardFrac = z.Kind == ZoneKind.BloodWall || z.Kind == ZoneKind.SmokeShell || z.Kind == ZoneKind.Cover || z.Kind == ZoneKind.DecoyDummy || z.Kind == ZoneKind.DeployTurret ? z.GuardFrac  // (a cover's health left: DeployableCover.cs)  // (a smoke canister's flight distance: GrenadeLauncher.cs)
                      // a Blood Wall node's place along its wall (BloodWall.cs)
                    : z.Kind == ZoneKind.Smoke ? (z.ExtraSlow > 0f ? 1f : 0f)  // a mastered Smoke Bomb (its slow on enemies), for drawing
                    : z.Kind == ZoneKind.Ward ? (z.SoakMax > 0f ? z.Soak / z.SoakMax : 0f) : z.Def.BlockCapacity > 0f ? z.BlockHp / z.Def.BlockCapacity : 0f;
            int kind = (int)z.Kind;
            s.Int(ref z.Id);
            s.Byte(ref kind);
            z.Kind = (ZoneKind)kind;
            s.Int(ref z.Owner);
            s.Vec(ref z.Pos);
            s.Float(ref z.Radius);
            s.Float(ref z.T);
            s.Float(ref z.Duration);
            s.Float(ref z.GuardFrac);
            s.Float(ref z.Aim);
            if (z.Kind == ZoneKind.DecoyDummy)
                s.Float(ref z.Marched);  // how far it was thrown, for the canister's flight (DecoyDummy.cs)
            if (z.Kind == ZoneKind.Phalanx)  // the line's walk and each skeleton's health (BonePhalanx.cs)
            {
                s.Float(ref z.Marched);
                int n = z.Bones != null ? z.Bones.Length : 0;
                s.Byte(ref n);
                if (s.Reading)
                    z.Bones = new byte[n];
                for (int i = 0; i < n; i++)
                    s.Byte(ref z.Bones[i]);
            }
            if (z.Kind == ZoneKind.Stampede)  // the herd's walk (Stampede.cs)
                s.Float(ref z.Marched);
            if (z.Kind == ZoneKind.FieldHospital)  // who has used its shrine (FieldHospital.cs)
                s.Int(ref z.Side);
            if (s.Reading)
                Zones[z.Id] = z;
        }

        /// A health orb; returns its id.
        int SyncOrb(NetSync s, Orb o)
        {
            int id = o != null ? o.Id : 0, owner = o != null ? o.Owner : 0;
            bool big = o != null && o.Big, tiny = o != null && o.Tiny, med = o != null && o.Med;
            V2 pos = o != null ? o.Pos : default(V2);
            float life = o != null ? o.Life : 0f;
            s.Int(ref id);
            s.Bool(ref big);
            s.Bool(ref tiny);
            s.Bool(ref med);
            s.Int(ref owner);
            s.Vec(ref pos);
            s.Float(ref life);
            if (s.Reading)
            {
                if (!Orbs.TryGetValue(id, out o))
                {
                    o = new Orb { Id = id, Big = big, Tiny = tiny, Med = med, Owner = owner, Pos = pos, Disp = pos };
                    Orbs[id] = o;
                }
                o.Pos = pos;
                o.Life = life;
            }
            return id;
        }
    }
}
