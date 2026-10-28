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
                p.Pos = WrapArena(pos);
            p.Aim = aim;
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
            w.Write((byte)State);
            w.Write(Wave);
            w.Write(StateTimer);
            w.Write(Remaining);
            w.Write(Score);
            w.Write(Paused);
            w.Write(PausedBy);
            w.Write(Endless);
            w.Write(PullCenter);
            w.Write(PullStrength);
            w.Write(TwinReviveT);
            w.Write(ShopOpen);
            w.Write(ShopT);
            w.Write((byte)Players.Count);
            foreach (var p in Players.Values)
            {
                w.Write(p.Id);
                w.Write(p.Pos);
                w.Write(p.Aim);
                w.Write(p.Hp);
                w.Write(p.Downed);
                w.Write(p.Revive);
                w.Write(p.BarrierT);
                p.BarrierFrac = p.BarrierMax > 0f ? p.BarrierHp / p.BarrierMax : 1f;  // the host draws from this too
                w.Write(p.BarrierFrac);
                w.Write(p.InvulnT);
                w.Write(p.OverdriveT);
                w.Write(p.TauntT);
                w.Write(p.LinkT);
                w.Write(p.LinkTo);
                w.Write(p.AegisT);
                w.Write(p.SurgeT);
                w.Write(p.FrenzyT);
                w.Write(p.UltT);
                w.Write(p.Parrying);
                w.Write(p.ParryHp);
                w.Write(p.ParryBroken);
                w.Write(p.ShieldHp);
                w.Write(p.WallT);
                w.Write(p.UnbreakT);
                w.Write(p.FuryT);
                w.Write(p.WhirlT);
                w.Write(p.VanishT);
                w.Write(p.IronT);
                w.Write((byte)p.AmmoUsed);
                w.Write(p.ReloadT);
                w.Write(p.DrawT);
                w.Write((short)p.Level);
                w.Write(p.Xp);
                w.Write((byte)p.PendingPicks);
                w.Write((short)p.Rerolls);
                w.Write((byte)p.PendingLegendary);
                w.Write(p.OfferLegendary);
                w.Write((byte)p.PendingMods);
                w.Write(p.OfferMod);
                w.Write((byte)(p.Offer != null ? p.Offer.Length : 0));
                if (p.Offer != null)
                    foreach (int o in p.Offer)
                        w.Write((byte)o);
                foreach (var s in p.Slots)
                {
                    w.Write((byte)s.Charges);
                    w.Write(s.Timer);
                }
                WriteMastery(w, p);
                WriteShop(w, p);
            }
            w.Write((short)Enemies.Count);
            foreach (var e in Enemies.Values)
            {
                w.Write(e.Id);
                w.Write((byte)e.Type);
                w.Write(e.Pos);
                w.Write(e.Hp);
                w.Write(e.MaxHp);
                w.Write(e.SpawnT > 0f);
                w.Write(e.Facing);
                w.Write(e.Phase);
                w.Write(e.Elite);
                e.Marked = e.MarkT > 0f;  // the host draws from these too
                e.Burning = e.BurnT > 0f;
                e.Frozen = e.FrozenT > 0f || e.SlowT > 0f && e.SlowMult <= 0.15f;  // Glacial Tomb, Frost Nova, Cryo freezes
                e.Chilled = e.SlowT > 0f && !e.Frozen;
                w.Write((byte)((e.Marked ? 1 : 0) | (e.Burning ? 2 : 0) | (e.Chilled ? 4 : 0) | (e.Frozen ? 8 : 0)));
            }
            w.Write((byte)Minions.Count);
            foreach (var m in Minions.Values)
            {
                w.Write(m.Id);
                w.Write((byte)m.Kind);
                w.Write(m.Owner);
                w.Write(m.Pos);
                w.Write(m.Aim);
                w.Write(m.T);
                w.Write(m.Duration);
                m.GuardFrac = m.Def.BlockCapacity > 0f && m.StaggerT <= 0f ? m.BlockHp / (m.Def.BlockCapacity * m.Scale) : 0f;
                w.Write(m.GuardFrac);
                w.Write(m.Scale);
            }
            w.Write((byte)Zones.Count);
            foreach (var z in Zones.Values)
            {
                w.Write(z.Id);
                w.Write((byte)z.Kind);
                w.Write(z.Owner);
                w.Write(z.Pos);
                w.Write(z.Radius);
                w.Write(z.T);
                w.Write(z.Duration);
                z.GuardFrac = z.Def.BlockCapacity > 0f ? z.BlockHp / z.Def.BlockCapacity : 0f;
                w.Write(z.GuardFrac);
            }
            WriteHazards(w);
            WriteCages(w);
            w.Write((short)Orbs.Count);
            foreach (var o in Orbs.Values)
            {
                w.Write(o.Id);
                w.Write(o.Big);
                w.Write(o.Pos);
                w.Write(o.Life);
            }
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

        void ReadSnapshot(BinaryReader r)
        {
            State = (MatchState)r.ReadByte();
            Wave = r.ReadInt32();
            StateTimer = r.ReadSingle();
            Remaining = r.ReadInt32();
            Score = r.ReadInt32();
            Paused = r.ReadBoolean();
            PausedBy = r.ReadInt32();
            Endless = r.ReadBoolean();
            PullCenter = r.ReadV2();
            PullStrength = r.ReadSingle();
            TwinReviveT = r.ReadSingle();
            ShopOpen = r.ReadBoolean();
            ShopT = r.ReadSingle();
            int np = r.ReadByte();
            for (int i = 0; i < np; i++)
            {
                int id = r.ReadInt32();
                V2 pos = r.ReadV2();
                float aim = r.ReadSingle(), hp = r.ReadSingle();
                bool downed = r.ReadBoolean();
                float revive = r.ReadSingle(), barrier = r.ReadSingle(), barrierFrac = r.ReadSingle(), invuln = r.ReadSingle(), overdrive = r.ReadSingle();
                float taunt = r.ReadSingle(), link = r.ReadSingle();
                int linkTo = r.ReadInt32();
                float aegis = r.ReadSingle(), surge = r.ReadSingle(), frenzy = r.ReadSingle(), ult = r.ReadSingle();
                bool parrying = r.ReadBoolean();
                float parryHp = r.ReadSingle(), parryBroken = r.ReadSingle(), shield = r.ReadSingle();
                float wallT = r.ReadSingle(), unbreakT = r.ReadSingle(), furyT = r.ReadSingle(), whirlT = r.ReadSingle();
                float vanishT = r.ReadSingle(), ironT = r.ReadSingle();
                int ammoUsed = r.ReadByte();
                float reloadT = r.ReadSingle();
                float drawT = r.ReadSingle();
                int level = r.ReadInt16();
                float xp = r.ReadSingle();
                int pending = r.ReadByte();
                int rerolls = r.ReadInt16();
                int pendingLegendary = r.ReadByte();
                bool offerLegendary = r.ReadBoolean();
                int pendingMods = r.ReadByte();
                bool offerMod = r.ReadBoolean();
                var offer = new int[r.ReadByte()];
                for (int k = 0; k < offer.Length; k++)
                    offer[k] = r.ReadByte();
                var charges = new int[Upgrades.SlotCount];
                var timers = new float[Upgrades.SlotCount];
                for (int k = 0; k < Upgrades.SlotCount; k++)
                {
                    charges[k] = r.ReadByte();
                    timers[k] = r.ReadSingle();
                }
                Player mp;
                Players.TryGetValue(id, out mp);
                ReadMastery(r, mp);
                ReadShop(r, mp);

                Player p;
                if (!Players.TryGetValue(id, out p))
                    continue;
                if (id != MyId)
                {
                    p.Pos = pos;
                    p.Aim = aim;
                    // Your own cooldowns are tracked locally; only copy the ally's.
                    for (int k = 0; k < Upgrades.SlotCount; k++)
                    {
                        p.Slots[k].Charges = charges[k];
                        p.Slots[k].Timer = timers[k];
                    }
                }
                if (hp < p.Hp)
                    p.Flash = 0.1f;
                p.Hp = hp;
                p.Downed = downed;
                p.Revive = revive;
                p.BarrierT = barrier;
                p.BarrierFrac = barrierFrac;
                p.InvulnT = invuln;
                p.OverdriveT = overdrive;
                p.TauntT = taunt;
                p.LinkT = link;
                p.LinkTo = linkTo;
                p.AegisT = aegis;
                p.SurgeT = surge;
                p.FrenzyT = frenzy;
                p.UltT = ult;
                if (id != MyId)
                    p.Parrying = parrying;  // your own comes from your mouse, without the round trip
                p.ParryHp = parryHp;
                p.ParryBroken = parryBroken;
                p.ShieldHp = shield;
                p.WallT = wallT;
                p.UnbreakT = unbreakT;
                p.VanishT = vanishT;
                p.IronT = ironT;
                p.FuryT = furyT;
                p.WhirlT = whirlT;
                p.AmmoUsed = ammoUsed;
                p.ReloadT = reloadT;
                if (id != MyId)
                    p.DrawT = drawT;  // your own draw is measured here
                p.Level = level;
                p.Xp = xp;
                p.PendingPicks = pending;
                p.Rerolls = rerolls;
                p.PendingLegendary = pendingLegendary;
                p.OfferLegendary = offerLegendary;
                p.PendingMods = pendingMods;
                p.OfferMod = offerMod;
                if (id == MyId && RerollPending && (rerolls < rerollsAtRequest || offer.Length == 0))
                    rerollsAtRequest = -1;
                p.Offer = offer.Length == 0 || (id == MyId && awaitingPick) ? null : offer;
            }

            var seen = new HashSet<int>();
            int ne = r.ReadInt16();
            for (int i = 0; i < ne; i++)
            {
                int id = r.ReadInt32();
                var type = (EnemyType)r.ReadByte();
                V2 pos = r.ReadV2();
                float hp = r.ReadSingle(), maxHp = r.ReadSingle();
                bool spawning = r.ReadBoolean();
                float facing = r.ReadSingle();
                byte phase = r.ReadByte();
                bool elite = r.ReadBoolean();
                byte status = r.ReadByte();
                Enemy e;
                if (!Enemies.TryGetValue(id, out e))
                {
                    var d = Defs.Get(type);
                    e = new Enemy { Id = id, Type = type, Def = d, Radius = d.Radius, Pos = pos, Disp = pos, Hp = hp };
                    Enemies[id] = e;
                }
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
                e.Radius = Defs.Get(type).Radius * (elite ? EliteSize : 1f);
                seen.Add(id);
            }
            var gone = new List<Enemy>();
            foreach (var e in Enemies.Values)
                if (!seen.Contains(e.Id))
                    gone.Add(e);
            foreach (var e in gone)
            {
                DeathFx(e, e.Disp);
                Enemies.Remove(e.Id);
            }

            var seenMinions = new HashSet<int>();
            int nm = r.ReadByte();
            for (int i = 0; i < nm; i++)
            {
                int id = r.ReadInt32();
                var kind = (MinionKind)r.ReadByte();
                int owner = r.ReadInt32();
                V2 pos = r.ReadV2();
                float aim = r.ReadSingle(), t = r.ReadSingle(), duration = r.ReadSingle(), guard = r.ReadSingle(), scale = r.ReadSingle();
                Minion m;
                if (!Minions.TryGetValue(id, out m))
                {
                    m = new Minion { Id = id, Kind = kind, Owner = owner, Pos = pos, Disp = pos };
                    Minions[id] = m;
                }
                m.Pos = pos;
                m.Aim = aim;
                m.T = t;
                m.Duration = duration;
                m.GuardFrac = guard;
                m.Scale = scale;
                seenMinions.Add(id);
            }
            var goneMinions = new List<int>();
            foreach (int id in Minions.Keys)
                if (!seenMinions.Contains(id))
                    goneMinions.Add(id);
            foreach (int id in goneMinions)
                Minions.Remove(id);

            Zones.Clear();
            int nz = r.ReadByte();
            for (int i = 0; i < nz; i++)
            {
                var z = new Zone { Id = r.ReadInt32(), Kind = (ZoneKind)r.ReadByte(), Owner = r.ReadInt32(), Pos = r.ReadV2(), Radius = r.ReadSingle(), T = r.ReadSingle(), Duration = r.ReadSingle(), GuardFrac = r.ReadSingle() };
                Zones[z.Id] = z;
            }

            ReadHazards(r);
            ReadCages(r);

            var seenOrbs = new HashSet<int>();
            int no = r.ReadInt16();
            for (int i = 0; i < no; i++)
            {
                int id = r.ReadInt32();
                bool big = r.ReadBoolean();
                V2 pos = r.ReadV2();
                float life = r.ReadSingle();
                Orb o;
                if (!Orbs.TryGetValue(id, out o))
                {
                    o = new Orb { Id = id, Big = big, Pos = pos, Disp = pos };
                    Orbs[id] = o;
                }
                o.Pos = pos;
                o.Life = life;
                seenOrbs.Add(id);
            }
            var expired = new List<int>();
            foreach (int id in Orbs.Keys)
                if (!seenOrbs.Contains(id))
                    expired.Add(id);
            foreach (int id in expired)
                Orbs.Remove(id);
        }
    }
}
