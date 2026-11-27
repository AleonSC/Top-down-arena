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
            if (!session.HasPeers)
                return;  // playing alone: nobody to tell (no message built)
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

        readonly List<int> upgradesRead = new List<int>();  // reused by Msg.Upgrades: the cards read, checked before any is applied

        MemoryStream snapBuf;  // reused by SendSnapshot: no new stream 20 times a second (not kept in a run save: RunSnapshot.Skip)
        BinaryWriter snapWriter;

        void SendSnapshot()
        {
            if (snapBuf == null || snapWriter == null)
            {
                snapBuf = new MemoryStream();
                snapWriter = new BinaryWriter(snapBuf);
            }
            snapBuf.SetLength(0);
            var w = snapWriter;
            w.Write((byte)Msg.Snapshot);
            SyncSnapshot(new NetSync(w));  // (written even alone: a few fields the host draws from are worked out here, BarrierFrac and the like)
            w.Flush();
            if (session.HasPeers)
                session.Broadcast(snapBuf.GetBuffer(), (int)snapBuf.Length);  // (no copy: the transport frames it from the buffer)
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
                case Msg.CooldownCut:  // a caught Scythe Throw, Chakra Reserves' refresh: that player's own game takes the share off (ReaperLoot.cs)
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
                    upgradesRead.Clear();
                    for (int i = 0; i < n; i++)
                    {
                        int t = r.ReadInt16();
                        if (t < 0 || t >= Upgrades.All.Length)
                            throw new InvalidDataException("card " + t + " out of range");  // (garbled: Session drops the link before any of it is applied)
                        upgradesRead.Add(t);
                    }
                    p.Taken.Clear();
                    p.Taken.AddRange(upgradesRead);
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
            {
                snapSeen.Clear();
                for (int i = 0; i < count; i++)
                    snapSeen.Add(SyncPlayer(s, null));
                snapSeen.Add(MyId);  // (never your own)
                RemoveUnseen(Players);  // someone who left: the host dropped them (OnPeerLeft), so they go here too
            }
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
            s.Short(ref count);
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
            s.Short(ref count);
            if (s.Reading)
            {
                snapSeen.Clear();
                for (int i = 0; i < count; i++)
                    snapSeen.Add(SyncZone(s, null));
                RemoveUnseen(Zones);
            }
            else
                foreach (var z in Zones.Values)
                    SyncZone(s, z);

            SyncInk(s);      // Chalk.cs: everyone's spirit ink
            SyncHive(s);     // HiveCenser.cs: everyone's Hive Censer swarms
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

        void RemoveUnseen<T>(SortedDictionary<int, T> all)  // (the players)
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
        /// Returns the player's id.
        int SyncPlayer(NetSync s, Player p)
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
            SyncMomentum(s, p);  // Momentum.cs
            SyncIronhide(s, p);  // Ironhide.cs
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
            SyncGadgetBag(s, p);  // Gadgets.cs
            bool sprayOn = p.SprayOn;
            s.Bool(ref sprayOn);  // Arterial Spray (ArterialSpray.cs)
            bool standOn = p.StandOn;
            s.Bool(ref standOn);  // Last Stand (LastStand.cs)
            s.Float(ref p.StandAim);
            s.Float(ref p.ShieldHp);
            s.Float(ref p.ShieldT);
            SyncShrineBuffs(s, p);  // Shrines.cs
            s.Float(ref p.GustT);  // Gust Gate's boost (GustGate.cs)
            s.Float(ref p.WardT);
            s.Float(ref p.StunT);
            s.Float(ref p.SwallowT);
            s.Float(ref p.WarpT);  // (BossBeam.cs)
            s.Float(ref p.PvpSlowT);  // a slow from another player (Pvp.cs)
            s.Float(ref p.PvpSlowMult);
            SyncGrasp(s, p);  // Hemomancer.cs
            SyncJuggernaut(s, p);  // Juggernaut.cs
            SyncLiteralTank(s, p);  // LiteralTank.cs
            SyncRocketDrop(s, p);  // RocketDrop.cs
            SyncHazardFoam(s, p);  // HazardFoam.cs
            SyncKnightBanners(s, p);  // KnightBanners.cs
            SyncStoneColossus(s, p);  // StoneColossus.cs
            SyncBloodGolem(s, p);  // BloodGolem.cs
            SyncSpiritAvatar(s, p);  // SpiritAvatar.cs
            SyncVeinTithe(s, p);  // VeinTithe.cs
            SyncKnight(s, p);  // Knight.cs
            s.Float(ref p.RodT);    // Lightning Rod's charge (LightningRod.cs)
            SyncVortex(s, p);  // Vortex.cs
            SyncEye(s, p);  // EyeOfCalm.cs
            SyncConduitWeb(s, p);  // ConduitWeb.cs
            SyncLivingBolt(s, p);  // LivingBolt.cs
            SyncRockGloves(s, p);  // RockGloves.cs
            SyncArmadillo(s, p);  // IronArmadillo.cs
            SyncRavager(s, p);  // BloodRavager.cs
            SyncTrojanHorse(s, p);  // TrojanHorse.cs
            SyncBloodBoil(s, p);  // BloodBoil.cs
            SyncForbiddenPath(s, p);  // ForbiddenPath.cs
            SyncAstralProjection(s, p);  // AstralProjection.cs
            SyncHeartbeat(s, p);  // Heartbeat.cs
            SyncSwarmHeart(s, p);  // HeartOfTheSwarm.cs
            SyncStaticBubble(s, p);  // StaticBubble.cs
            SyncRodentMinigun(s, p);  // RodentMinigun.cs
            SyncHelicopter(s, p);  // AttackHelicopter.cs
            SyncDogma(s, p);  // DragonsDogma.cs
            s.Float(ref p.CaltropT);  // Caltrop Run (CaltropRun.cs)
            SyncGauntlet(s, p);  // Gauntlet.cs
            s.Float(ref p.VanishT);
            SyncCeremonialArmor(s, p);  // CeremonialArmor.cs
            SyncSenbon(s, p);  // SenbonSeekers.cs
            s.Vec(ref p.VanishFrom);
            s.Float(ref p.OrbitT);
            s.Float(ref p.DashT);  // Healing Dash's sprint (Ninja.cs)
            s.Byte(ref p.Sprint);  // the Dodge's held sprint (Dodge.cs)
            s.Byte(ref p.OrbitN);
            SyncHarvest(s, p);  // CrimsonHarvest.cs
            SyncGaleWall(s, p);  // GaleWall.cs
            SyncSanctumGlass(s, p);  // SanctumGlass.cs
            SyncWaterWard(s, p);  // WaterWard.cs
            s.Float(ref p.RainClock);  // Sanguine Rain (SanguineRain.cs)
            SyncKusarigama(s, p);  // Kusarigama.cs
            s.Byte(ref p.StarStance);  // the Shuriken's Great Star stance (GreatStar.cs)
            SyncFumaOrbit(s, p);  // FumaOrbit.cs
            s.Float(ref p.SpinUp);
            s.Int(ref p.PreyId);
            s.Byte(ref p.PackKills);
            s.Byte(ref p.Orbiters);
            s.Byte(ref p.Combo);
            SyncArcKnuckles(s, p);  // ArcKnuckles.cs
            SyncKatana(s, p);  // Katana.cs
            s.Byte(ref p.BoStep);
            s.Byte(ref p.StaffStacks);  // the Quarterstaff's Jab stacks (Quarterstaff.cs)
            byte relic = (byte)p.Relic;
            s.Byte(ref relic);          // the relic slot (Relics.cs)
            if (s.Reading && (RelicId)relic != p.Relic) { p.Relic = (RelicId)relic; Recalc(p); } else p.Relic = (RelicId)relic;  // (a faction relic changes stats)
            SyncTwinGrenades(s, p);  // TwinGrenades.cs
            SyncTwinAuto(s, p);  // TwinAuto.cs
            SyncTwinFang(s, p);  // TwinBlades.cs
            SyncOutOfAmmo(s, p);  // OutOfAmmo.cs
            SyncScattergun(s, p);  // TankAlts.cs
            SyncRepulsion(s, p);  // Repulsion.cs
            s.Byte(ref p.RailHeat);     // the Railgun's heat (RailBeam.cs)
            s.Byte(ref p.Embers);       // the Censer's Embers (Censer.cs)
            s.Byte(ref p.GreatStep);    // the Greatsword's chain (Greatsword.cs)
            s.Bool(ref p.LancePrimed);  // the Lance's hook primed the Breaching Thrust (Lance.cs)
            SyncRapier(s, p);  // Rapier.cs
            s.Byte(ref p.Light);
            s.Float(ref p.MercyT);  // a Lightbringer's ward (Lightbringer.cs)
            SyncNeedles(s, p);  // AcupunctureNeedles.cs
            s.Vec(ref p.KunaiMark);
            s.Float(ref p.KunaiMarkT);
            SyncBloodWall(s, p);  // BloodWall.cs
            SyncBloodDebt(s, p);  // BloodDebt.cs
            s.Byte(ref p.Clot);
            s.Byte(ref p.ClotMax);
            SyncBloodLash(s, p);  // BloodLash.cs
            SyncBloodBrawler(s, p);  // BloodBrawler.cs
            SyncHemomancer(s, p);  // Hemomancer.cs
            SyncRevolver(s, p);  // PistolWhip.cs
            s.Byte(ref p.StunShots);
            s.Byte(ref p.PactRez);
            s.Vec(ref p.LightPos);
            s.Bool(ref p.RadiantOn);
            s.Vec(ref p.RadiantPos);
            SyncDoubleBarrel(s, p);  // DoubleBarrel.cs
            SyncGrenadeLauncher(s, p);  // GrenadeLauncher.cs
            SyncWindClash(s, p);  // WindClash.cs
            SyncFrostLance(s, p);  // FrostLance.cs
            SyncBulwark(s, p);  // BulwarkBarrier.cs
            SyncHalberd(s, p);  // Halberd.cs
            SyncStonespear(s, p);  // Stonespear.cs
            SyncHoneybee(s, p);  // HoneybeeStaff.cs
            s.Float(ref p.Cinder);  // the Wyvern Staff (WyvernStaff.cs)
            SyncNunchaku(s, p);  // Nunchaku.cs
            SyncNodachi(s, p);  // Nodachi.cs
            SyncVowguard(s, p);  // Vowguard.cs
            SyncTwinPistols(s, p);  // TwinPistols.cs
            SyncStaticShock(s, p);  // StaticShock.cs
            SyncTwinTessen(s, p);  // TwinTessen.cs
            SyncHeartFlail(s, p);  // HeartFlail.cs
            SyncHiveCenser(s, p);  // HiveCenser.cs
            s.Float(ref p.SupplyT);  // Supply Drop (SupplyDrop.cs)
            s.Float(ref p.Armor);  // an Armor Drop vest's armor (ArmorDrop.cs)
            SyncEmbolus(s, p);  // EmbolusSpears.cs
            SyncNeedleSwarm(s, p);  // NeedleSwarm.cs
            SyncArcTags(s, p);  // ArcLock.cs
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
            s.Bool(ref p.OfferStart);
            int offers = p.Offer != null ? p.Offer.Length : 0;
            s.Byte(ref offers);
            var offer = !s.Reading ? p.Offer : offers == 0 ? null : p.Offer != null && p.Offer.Length == offers ? p.Offer : new int[offers];  // (reading: the same cards' array refilled, not a new one every snapshot)
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
                return id;

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
            return id;
        }

        /// An enemy; returns its id. Reading, a new id brings a new enemy.
        int SyncEnemy(NetSync s, Enemy e)
        {
            int id = 0, type = 0;
            V2 pos = default(V2);
            float hp = 0f, maxHp = 0f, facing = 0f;
            bool spawning = false, elite = false;
            byte phase = 0, status = 0;
            int bleedStacks = 0, stars = 0, needlesIn = 0, traits = 0, bubble = 0, cells = 0;
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
                needlesIn = e.NeedlesIn;
                e.Suppressed = e.Suppress >= SuppressShown;
                e.Poisoned = e.PoisonT > 0f;
                e.Dazed = e.DazeT > 0f || e.StunT > 0f;  // (a Stun shows the same stars)
            }
            s.Int(ref id);
            s.Byte(ref type);
            if (s.Reading && type >= Defs.Enemies.Length)
                throw new InvalidDataException("enemy type " + type + " out of range");  // (a garbled snapshot: Session drops the link)
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
            s.Byte(ref needlesIn);   // Bloodletting needles stuck in it (AcupunctureNeedles.cs)
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
            s.Short(ref challengeBy);  // (a player id: a long-hosted game's ids pass 255)  a Gauntlet's Challenge: who threw it (0: none) and how long it has left (Gauntlet.cs)
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
                s.Short(ref blastOwner);  // (a player id)
            int fpBrands = !s.Reading ? e.FpBrands : 0;
            s.Byte(ref fpBrands);  // Forbidden Path's Blood Brands on it (ForbiddenPath.cs)
            int elem = 0;
            if (!s.Reading)
            {
                e.Brittle = e.BrittleT > 0f;
                e.Dazzled = e.DazzleT > 0f;
                elem = (e.Brittle ? 1 : 0) | (e.Dazzled ? 2 : 0);
            }
            s.Byte(ref elem);  // Brittle (Brittle.cs) and Dazzled (Dazzled.cs)
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
                e.FpBrands = (byte)fpBrands;
                e.Brittle = (elem & 1) != 0;
                e.Dazzled = (elem & 2) != 0;
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
                e.NeedlesIn = needlesIn;
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
                e.Radius = kind == EnemyType.Singularity ? SingRadius(e) : kind == EnemyType.Devourer ? DevourRadius(e) : kind == EnemyType.Colossus ? ColRadius(e) : Defs.Get(kind).Radius * (elite ? EliteSize : 1f);
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
                m.GuardFrac = m.Kind == MinionKind.CloneWall || m.Kind == MinionKind.ClotCrystal || (m.Kind == MinionKind.Wall && m.MaxHp > 0f) ? MathX.Clamp01(m.BlockHp / Math.Max(1f, m.MaxHp))  // a wall clone: what is left of its health
                    : HasHealth(m) || m.Kind == MinionKind.CloneMedic || m.Kind == MinionKind.Wolf && m.Companion && m.MaxHp > 0f ? m.Hp / Math.Max(1f, m.MaxHp)  // a Warden's Guard, Force Pact thrall or Wolf Whistle wolf: its health
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
            if (s.Reading && kind >= Kits.Minions.Length)
                throw new InvalidDataException("minion kind " + kind + " out of range");  // (a garbled snapshot: Session drops the link)
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

        /// A ground zone; returns its id. Reading, a new id brings a new zone (one already known is refilled in place).
        int SyncZone(NetSync s, Zone z)
        {
            if (!s.Reading)
                z.GuardFrac = z.Kind == ZoneKind.BloodWall || z.Kind == ZoneKind.SmokeShell || z.Kind == ZoneKind.Cover || z.Kind == ZoneKind.DecoyDummy || z.Kind == ZoneKind.DeployTurret || z.Kind == ZoneKind.Ballista || z.Kind == ZoneKind.Claymore || z.Kind == ZoneKind.TrojanHorse || z.Kind == ZoneKind.Caltrop || z.Kind == ZoneKind.Tusker || z.Kind == ZoneKind.HazardFoam ? z.GuardFrac  // (a cover's health left: DeployableCover.cs)  // (a smoke canister's flight distance: GrenadeLauncher.cs)
                      // a Blood Wall node's place along its wall (BloodWall.cs)
                    : z.Kind == ZoneKind.Smoke ? (z.ExtraSlow > 0f ? 1f : 0f)  // a mastered Smoke Bomb (its slow on enemies), for drawing
                    : z.Kind == ZoneKind.Ward ? (z.SoakMax > 0f ? z.Soak / z.SoakMax : 0f) : z.Def.BlockCapacity > 0f ? z.BlockHp / z.Def.BlockCapacity : 0f;
            int id = z != null ? z.Id : 0, kind = z != null ? (int)z.Kind : 0;
            s.Int(ref id);
            s.Byte(ref kind);
            if (s.Reading && (!Zones.TryGetValue(id, out z) || (int)z.Kind != kind))  // (ids restart each run: a new kind is a new zone)
            {
                z = new Zone { Id = id };
                Zones[id] = z;
            }
            z.Kind = (ZoneKind)kind;
            s.Int(ref z.Owner);
            s.Vec(ref z.Pos);
            s.Float(ref z.Radius);
            s.Float(ref z.T);
            s.Float(ref z.Duration);
            s.Float(ref z.GuardFrac);
            s.Float(ref z.Aim);
            if (z.Kind == ZoneKind.DecoyDummy || z.Kind == ZoneKind.Claymore)
                s.Float(ref z.Marched);  // (a Claymore: 1 while a mastered spare blast is left; Claymore.cs)  // how far it was thrown, for the canister's flight (DecoyDummy.cs)
            if (z.Kind == ZoneKind.Ballista)  // its rider, bolts left and reload (Ballista.cs)
            {
                s.Int(ref z.Rider);
                int bolts = z.BoltsLeft;
                s.Byte(ref bolts);
                z.BoltsLeft = bolts;
                s.Float(ref z.GunCd);
            }
            if (z.Kind == ZoneKind.Phalanx)  // the line's walk and each skeleton's health (BonePhalanx.cs)
            {
                s.Float(ref z.Marched);
                int n = z.Bones != null ? z.Bones.Length : 0;
                s.Byte(ref n);
                if (s.Reading && (z.Bones == null || z.Bones.Length != n))
                    z.Bones = new byte[n];  // (the same line's array is refilled, not a new one every snapshot)
                for (int i = 0; i < n; i++)
                    s.Byte(ref z.Bones[i]);
            }
            if (z.Kind == ZoneKind.TrojanHorse)  // how far it has rolled, and parked (TrojanHorse.cs)
            {
                s.Float(ref z.Marched);
                s.Bool(ref z.Quiet);
            }
            if (z.Kind == ZoneKind.Stampede)  // the herd's walk (Stampede.cs)
                s.Float(ref z.Marched);
            if (z.Kind == ZoneKind.Mammoth)  // where it lands, and the enemy it's charging (MammothFall.cs)
            {
                s.Vec(ref z.Origin);
                s.Int(ref z.Side);
            }
            if (z.Kind == ZoneKind.Tusker)  // where its streams end and what they hit (TidalTusker.cs)
            {
                s.Vec(ref z.Origin);
                s.Vec(ref z.Origin2);
                s.Int(ref z.Side);
                s.Int(ref z.Rider);
            }
            if (z.Kind == ZoneKind.HoneyRaid)  // where the bee came in (HoneyRaid.cs)
                s.Vec(ref z.Origin);
            if (z.Kind == ZoneKind.FieldHospital || z.Kind == ZoneKind.BlastOfuda)  // who has used its shrine (FieldHospital.cs); the enemy a Blast Ofuda rides, 0 on the ground (Ofuda.cs)
                s.Int(ref z.Side);
            return id;
        }

        /// A health orb; returns its id.
        int SyncOrb(NetSync s, Orb o)
        {
            int id = o != null ? o.Id : 0, owner = o != null ? o.Owner : 0;
            bool big = o != null && o.Big, tiny = o != null && o.Tiny, med = o != null && o.Med, blood = o != null && o.Blood, vest = o != null && o.Vest;
            V2 pos = o != null ? o.Pos : default(V2);
            float life = o != null ? o.Life : 0f;
            s.Int(ref id);
            s.Bool(ref big);
            s.Bool(ref tiny);
            s.Bool(ref med);
            s.Bool(ref blood);
            s.Bool(ref vest);
            s.Int(ref owner);
            s.Vec(ref pos);
            s.Float(ref life);
            if (s.Reading)
            {
                if (!Orbs.TryGetValue(id, out o))
                {
                    o = new Orb { Id = id, Big = big, Tiny = tiny, Med = med, Blood = blood, Vest = vest, Owner = owner, Pos = pos, Disp = pos };
                    Orbs[id] = o;
                }
                o.Pos = pos;
                o.Life = life;
            }
            return id;
        }
    }
}
