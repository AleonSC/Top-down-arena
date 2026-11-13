using System;
using System.Collections.Generic;

namespace Arena
{
    /// The DPS Knight's Caught Steel (`AbilityId.BattleFury` kept; was Battle Fury), the Forge look of
    /// Docs/Mockups/knight-dps-abilities.html. For SteelTime seconds every enemy bullet that skims past you (its closest
    /// pass inside the graze band, without touching you) is caught: it stops dead and hangs round you as a shard of steel,
    /// up to SteelCap. Press Space again (or let the time run out) and a sweeping cut hurls the whole halo out along your
    /// aim, fanned across SteelFan, one shard after another as the cut passes: each flies at SteelSpeed, cuts any enemy
    /// bullet it touches and stops in the first enemy for SteelDamage. A hit while you hold shards knocks a third loose.
    /// The cooldown waits while you're catching (Game.TickSlots), like Arterial Spray's.
    ///   FxKind.SteelCatch: a bullet caught (Pos = where, Pos2.X = the knight's id).
    ///   FxKind.SteelRelease: the cut (Pos = the knight, Pos2 = (their id, shards thrown), Radius = the aim, Dur = SteelReleaseFx).
    ///   FxKind.SteelHit: a shard striking an enemy or a bullet (Pos = where, Pos2 = its direction, Radius = 1 an enemy, 0 a bullet).
    ///   FxKind.SteelDrop: shards knocked loose by a hit (Pos = the knight, Pos2 = (their id, how many)).
    /// Drawn in Game/CaughtSteelLook.cs.
    public partial class Game
    {
        public const float SteelTime = 6f, SteelSpeed = 1150f, SteelDamage = 22f;
        public const int SteelCap = 12, SteelCapMastered = 18;
        public const float SteelBand = 14f;          // the graze band past the edge of your hit circle
        public const float SteelFan = 1.2f;          // radians the release fans across
        public const float SteelSweep = 0.2f;        // the cut: shards leave one after another over 85% of it
        public const float SteelLaunchFrom = 30f;    // px out along the fan where each shard leaves the blade
        public const float SteelReleaseFx = 0.6f;
        const float SteelLife = 1.1f;

        class SteelLaunch
        {
            public int Owner;
            public float T;      // seconds until it leaves the blade
            public V2 From, Dir;
            public float Dmg;
        }

        readonly List<SteelLaunch> steelLaunches = new List<SteelLaunch>();

        public static int SteelCapOf(Player p, bool mastered) => mastered ? SteelCapMastered : SteelCap;

        /// The graze band's outer edge round a player, for a bullet of this radius (drawn as the dashed ring).
        public static float SteelReach(Player p, float bulletR) => p.Def.HitR + bulletR + SteelBand + p.Stats.SteelReach;

        /// Pressed again while catching: the host throws them.
        public bool SteelRelease(Player p, int slot) => p.SteelT > 0f && p.Slots[slot].Id == AbilityId.BattleFury;

        /// Host: Caught Steel was cast.
        void CastCaughtSteel(Player p, float power, bool mastered)
        {
            p.SteelT = SteelTime;
            p.Steel = 0;
            p.SteelPower = power;
            p.SteelMastered = mastered;
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.69f, 0.31f), 34f, 0.3f, Sfx.Clang);
        }

        /// Host, from the enemy-bullet loop (Combat.cs): does this bullet skim past the knight and get caught?
        bool SteelGraze(Player p, Bullet b)
        {
            if (p.SteelT <= 0f || p.Steel >= SteelCapOf(p, p.SteelMastered) || b.Vel.LengthSq < 900f)
                return false;
            V2 off = b.Pos - p.Pos;
            float d = off.Length, hit = p.Def.HitR + b.Radius;
            if (d <= hit || d > SteelReach(p, b.Radius))
                return false;
            if (off.X * b.Vel.X + off.Y * b.Vel.Y < 0f)
                return false;  // still closing in: wait for its closest pass
            p.Steel++;
            BroadcastFx(FxKind.SteelCatch, b.Pos, new V2(p.Id, 0f), new Rgba(1f, 0.89f, 0.67f), 0f, 0.4f, Sfx.None);
            Sounds.Add(Sfx.Clang);
            return true;
        }

        /// Host, from Damage: a hit knocks a third of the shards loose.
        void SteelHurt(Player p)
        {
            if (p.Steel == 0)
                return;
            int lost = (p.Steel + 2) / 3;
            p.Steel = (byte)(p.Steel - lost);
            BroadcastFx(FxKind.SteelDrop, p.Pos, new V2(p.Id, lost), new Rgba(0.52f, 0.55f, 0.6f), 0f, 0.8f, Sfx.None);
        }

        /// Host: the cut that throws the shards (pressed again, or the catching time ran out).
        void ReleaseSteel(Player p)
        {
            p.SteelT = 0f;
            int n = p.Steel;
            p.Steel = 0;
            if (n == 0 || p.Downed)
                return;
            float dmg = (SteelDamage + p.Stats.SteelDamage) * p.Stats.DmgMult * p.SteelPower;
            int cap = SteelCapOf(p, p.SteelMastered);
            for (int i = 0; i < n; i++)
            {
                int fi = i + (cap - n + 1) / 2;  // the shards held take the middle of the fan
                float a = p.Aim - SteelFan / 2f + SteelFan * fi / (cap - 1);
                V2 dir = V2.FromAngle(a);
                steelLaunches.Add(new SteelLaunch { Owner = p.Id, T = 0.03f + SteelSweep * 0.85f * (n > 1 ? i / (float)(n - 1) : 0f), From = p.Pos + dir * SteelLaunchFrom, Dir = dir, Dmg = dmg });
            }
            BroadcastFx(FxKind.SteelRelease, p.Pos, new V2(p.Id, n), new Rgba(1f, 0.59f, 0.24f), p.Aim, SteelReleaseFx, Sfx.SwingHeavy);
        }

        /// Host, every tick: the catching time running out, shards leaving the blade, flying shards cutting enemy bullets.
        void TickSteel(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.SteelT <= 0f)
                    continue;
                p.SteelT -= dt;
                if (p.Downed)
                {
                    p.SteelT = 0f;
                    p.Steel = 0;
                }
                else if (p.SteelT <= 0f)
                    ReleaseSteel(p);
            }
            for (int i = steelLaunches.Count - 1; i >= 0; i--)
            {
                var l = steelLaunches[i];
                l.T -= dt;
                if (l.T > 0f)
                    continue;
                steelLaunches.RemoveAt(i);
                var b = SpawnBullet(Defs.SteelShard, l.From, l.Dir * SteelSpeed, 0f, SteelLife, l.Dmg, l.Owner);
                b.Pierce = 0;
            }
            List<int> cut = null;
            foreach (var s in Bullets.Values)
            {
                if (s.Kind != Defs.SteelShard)
                    continue;
                foreach (var b in Bullets.Values)
                {
                    if (!Defs.IsEnemyShot(b.Kind) || b.Pos.DistanceSq(s.Pos) > 144f)
                        continue;
                    if (cut == null)
                        cut = new List<int>();
                    if (!cut.Contains(b.Id))
                    {
                        cut.Add(b.Id);
                        BroadcastFx(FxKind.SteelHit, b.Pos, s.Vel.Normalized(), new Rgba(1f, 0.55f, 0.6f), 0f, 0.3f, Sfx.None);
                    }
                }
            }
            if (cut != null)
                foreach (int id in cut)
                {
                    KillBullet(id);
                }
        }

        /// Host, a shard striking an enemy (KnightKit.OnShotHit): sparks where it stuck.
        void SteelShardHit(Bullet b, Enemy e)
        {
            if (b.Kind == Defs.SteelShard)
                BroadcastFx(FxKind.SteelHit, b.Pos, b.Vel.Normalized(), new Rgba(1f, 0.59f, 0.24f), 1f, 0.35f, Sfx.None);
        }
    }
}
