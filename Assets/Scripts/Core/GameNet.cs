using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Networking: client inputs, the host's snapshots, steering and effect broadcasts, and the
    /// client's handling of host messages.
    public partial class Game
    {
        public void OnInput(int peer, BinaryReader r)
        {
            V2 pos = r.ReadV2();
            float aim = r.ReadSingle();
            V2 aimAt = r.ReadV2();
            bool firing = r.ReadBoolean();
            bool parry = r.ReadBoolean();
            float draw = r.ReadSingle(), release = r.ReadSingle();
            Player p;
            if (!Players.TryGetValue(peer, out p))
                return;
            p.DrawT = draw;
            if (release > 0f && p.Weapon.Id == WeaponId.MendersBow)
                p.BowRelease = Math.Min(10f, release);  // the shooter timed it; one release per message
            if (!p.Downed)
                p.Pos = KeepInArena(pos, 0f);
            p.Aim = aim;
            p.AimAt = aimAt;
            p.Firing = firing;
            p.Parrying = parry;
        }

        public void OnAbility(int peer, BinaryReader r)
        {
            int slot = r.ReadByte();
            V2 target = r.ReadV2();
            if (slot < Upgrades.SlotCount)
                HostAbility(peer, slot, target, false);
        }

        /// Host: the exact position and course of every homing (or gravity-bent) bullet.
        void SendSteer()
        {
            int n = 0;
            foreach (var b in Bullets.Values)
                if (b.Homing > 0f || b.Pulled)
                    n++;
            if (n == 0)
                return;
            var w = Proto.Begin(Msg.Steer);
            w.Write(n);
            foreach (var b in Bullets.Values)
                if (b.Homing > 0f || b.Pulled)
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
            w.Write((byte)sound);
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
                case Msg.PracticeWave:
                    PracticeIndex = r.ReadInt32();
                    break;
                case Msg.PathOffer:
                    ReadPathOffer(r);
                    break;
                case Msg.PathChosen:
                    ReadPathChosen(r);
                    break;
                case Msg.ShopGift:
                    ReadShopGift(r);
                    break;
                case Msg.PathPatch:
                    ReadPathPatch(r);
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
                        MakeBullet(id, kind, owner, pos, vel, angVel, life, (flags & 1) != 0, homing);
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
                case Msg.Sound:
                    Sounds.Add((Sfx)r.ReadByte());
                    break;
                case Msg.Teleport:  // Substitution moved someone; each player's own game moves them
                {
                    int id = r.ReadInt32();
                    V2 pos = r.ReadV2();
                    if (id == MyId && Me != null)
                        Me.Pos = Me.Disp = pos;
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
                    Sounds.Add((Sfx)r.ReadByte());
                    break;
                }
                case Msg.Upgrades:
                {
                    int id = r.ReadInt32();
                    int n = r.ReadByte();
                    Player p;
                    if (!Players.TryGetValue(id, out p))
                        break;
                    p.Taken.Clear();
                    for (int i = 0; i < n; i++)
                        p.Taken.Add(r.ReadByte());
                    for (int k = 0; k < p.BonusKeys.Length; k++)
                        p.BonusKeys[k] = (AbilityId)r.ReadByte();
                    Recalc(p);
                    if (id == MyId)
                        awaitingPick = false;
                    break;
                }
                case Msg.DevStats:
                    ReadDevStats(r);
                    break;
                case Msg.Snapshot:
                    ReadSnapshot(r);
                    break;
                case Msg.Steer:
                    ReadSteer(r);
                    break;
                case Msg.Pong:
                    PingMs = (float)((clock.ElapsedTicks - r.ReadInt64()) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                    break;
                case Msg.BulletSync:
                    ReadBulletSync(r);
                    break;
                case Msg.RunStats:
                    ReadRunStats(r);
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

        /// The snapshot, 20 times a second: the host writes the match's state and clients read it, both through
        /// this one method (NetSync), so a field can't be written in one order and read in another. To replicate
        /// something new, add one line in the right Sync method and bump Session.ProtocolVersion.
        void SyncSnapshot(NetSync s)
        {
            int state = (int)State;
            s.Byte(ref state);
            State = (MatchState)state;
            s.Int(ref Wave);
            s.Float(ref StateTimer);
            s.Int(ref Remaining);
            s.Int(ref Score);
            s.Bool(ref Paused);
            s.Int(ref PausedBy);
            s.Bool(ref Endless);
            s.Vec(ref PullCenter);
            s.Float(ref PullStrength);
            s.Float(ref TwinReviveT);
            s.Bool(ref ShopOpen);
            s.Float(ref ShopT);

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

            SyncHazards(s);  // LateGame.cs
            SyncCages(s);    // Singularity.cs

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
            s.Bool(ref p.Downed);
            s.Float(ref p.Revive);
            s.Float(ref p.BarrierT);
            if (!s.Reading)
                p.BarrierFrac = p.BarrierMax > 0f ? p.BarrierHp / p.BarrierMax : 1f;  // the host draws from this too
            s.Float(ref p.BarrierFrac);
            s.Float(ref p.InvulnT);
            s.Float(ref p.OverdriveT);
            s.Float(ref p.TauntT);
            s.Float(ref p.LinkT);
            s.Int(ref p.LinkTo);
            s.Float(ref p.AegisT);
            s.Float(ref p.SurgeT);
            s.Float(ref p.FrenzyT);
            s.Float(ref p.UltT);
            bool parrying = p.Parrying, firing = p.Firing;  // a client sees the host's Firing as FiringSeen
            s.Bool(ref parrying);
            s.Bool(ref firing);
            s.Float(ref p.ParryHp);
            s.Float(ref p.ParryBroken);
            s.Float(ref p.ShieldHp);
            s.Float(ref p.StunT);
            s.Float(ref p.WallT);
            s.Float(ref p.UnbreakT);
            s.Float(ref p.FuryT);
            s.Float(ref p.WhirlT);
            s.Float(ref p.VanishT);
            s.Float(ref p.IronT);
            s.Float(ref p.PactT);
            s.Float(ref p.RushT);
            s.Byte(ref p.AmmoUsed);
            s.Float(ref p.ReloadT);
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
            int offers = p.Offer != null ? p.Offer.Length : 0;
            s.Byte(ref offers);
            var offer = s.Reading ? (offers > 0 ? new int[offers] : null) : p.Offer;
            for (int k = 0; k < offers; k++)
                s.Byte(ref offer[k]);
            for (int k = 0; k < Upgrades.SlotCount; k++)
            {
                snapCharges[k] = p.Slots[k].Charges;
                snapTimers[k] = p.Slots[k].Timer;
                s.Byte(ref snapCharges[k]);
                s.Float(ref snapTimers[k]);
            }
            SyncMastery(s, p, known);  // Mastery.cs (it may Recalc, which touches health and slots: those come after)
            SyncShop(s, p);            // Shop.cs
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
            if (!s.Reading)
            {
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
                e.Burning = e.BurnT > 0f;
                e.Frozen = e.FrozenT > 0f || e.SlowT > 0f && e.SlowMult <= 0.15f;  // Glacial Tomb, Frost Nova, Cryo freezes
                e.Chilled = e.SlowT > 0f && !e.Frozen;
                e.Bleeding = e.BleedT > 0f;
                status = (byte)((e.Marked ? 1 : 0) | (e.Burning ? 2 : 0) | (e.Chilled ? 4 : 0) | (e.Frozen ? 8 : 0) | (e.Bleeding ? 16 : 0));
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
            var kind = (EnemyType)type;
            if (s.Reading && !Enemies.TryGetValue(id, out e))
            {
                var d = Defs.Get(kind);
                e = new Enemy { Id = id, Type = kind, Def = d, Radius = d.Radius, Pos = pos, Disp = pos, Hp = hp };
                Enemies[id] = e;
            }
            if (s.Reading)
            {
                if (hp < e.Hp)
                    e.Flash = 0.06f;
                e.Pos = pos;
                e.Hp = hp;
                e.MaxHp = maxHp;
                e.Spawning = spawning;
                e.Facing = facing;
                e.Phase = phase;
                e.Elite = elite;
                e.Marked = (status & 1) != 0;
                e.Burning = (status & 2) != 0;
                e.Chilled = (status & 4) != 0;
                e.Frozen = (status & 8) != 0;
                e.Bleeding = (status & 16) != 0;
            }
            if (kind == EnemyType.Boss)
                SyncWarden(s, e);  // Warden.cs
            if (s.Reading)
                e.Radius = Defs.Get(kind).Radius * (elite ? EliteSize : 1f);
            return id;
        }

        /// A minion; returns its id. Reading, a new id brings a new minion.
        int SyncMinion(NetSync s, Minion m)
        {
            int id = 0, kind = 0, owner = 0;
            V2 pos = default(V2);
            float aim = 0f, t = 0f, duration = 0f, guard = 0f, scale = 0f;
            if (!s.Reading)
            {
                id = m.Id;
                kind = (int)m.Kind;
                owner = m.Owner;
                pos = m.Pos;
                aim = m.Aim;
                t = m.T;
                duration = m.Duration;
                m.GuardFrac = m.Def.BlockCapacity > 0f && m.StaggerT <= 0f ? m.BlockHp / (m.Def.BlockCapacity * m.Scale) : 0f;
                guard = m.GuardFrac;
                scale = m.Scale;
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
            }
            return id;
        }

        /// A ground zone (clients rebuild the list from each snapshot).
        void SyncZone(NetSync s, Zone z)
        {
            if (s.Reading)
                z = new Zone();
            else
                z.GuardFrac = z.Def.BlockCapacity > 0f ? z.BlockHp / z.Def.BlockCapacity : 0f;
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
            if (s.Reading)
                Zones[z.Id] = z;
        }

        /// A health orb; returns its id.
        int SyncOrb(NetSync s, Orb o)
        {
            int id = o != null ? o.Id : 0;
            bool big = o != null && o.Big;
            V2 pos = o != null ? o.Pos : default(V2);
            float life = o != null ? o.Life : 0f;
            s.Int(ref id);
            s.Bool(ref big);
            s.Vec(ref pos);
            s.Float(ref life);
            if (s.Reading)
            {
                if (!Orbs.TryGetValue(id, out o))
                {
                    o = new Orb { Id = id, Big = big, Pos = pos, Disp = pos };
                    Orbs[id] = o;
                }
                o.Pos = pos;
                o.Life = life;
            }
            return id;
        }
    }
}
