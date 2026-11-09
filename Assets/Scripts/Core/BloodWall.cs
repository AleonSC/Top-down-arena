using System;
using System.Collections.Generic;

namespace Arena
{
    /// The tank Hemomancer's Blood Wall (`AbilityId.CrimsonPact` kept; was Crimson Pact), look B "Thornvein" of
    /// Docs/Mockups/blood-wall.html. A toggle: press to open your veins and a wall of blood is laid behind you as you walk
    /// (standing still lays nothing), a node every WallSpacing px. The wall stops every enemy bullet (red aimed shots too),
    /// enemies can't walk through it and take WallContactDps while pressed against it; you and your allies walk through,
    /// and your shots fly over it. Each node stands WallLife s, then crumbles.
    /// It drains WallDrain of your max health a second, doubling every WallDouble s (Blood Oath stretches both). The drain
    /// is not a hit: no hit grace, healing still lands, and it can take you down. It ends on a second press or when you go
    /// down; the nodes already laid stand out their time. The cooldown starts when it ends.
    /// Mastered: enemies against it take double and are slowed by WallMasteredSlow.
    /// Each node is a `ZoneKind.BloodWall` zone: Pos the node, Radius half the wall's thickness, Aim the id of the node
    /// before it in the same wall (-1 for the first), so a wall is a chain of capsules; GuardFrac its place along the wall
    /// (0, 1, 2, ...), which the look braids and spaces its thorns by. `Player.BloodWallOn` and
    /// `Player.BloodWallClock` (seconds since it opened) are replicated, for the drain ring (Game/BloodWallLook.cs).
    ///   FxKind.WallSplash: an enemy shot stopping on the wall (Pos = where, Pos2 = back along the shot).
    public partial class Game
    {
        public const float WallSpacing = 12f, WallHalf = 7f, WallLife = 6f, WallLifeUp = 2f;
        public const float WallDrain = 0.01f, WallDouble = 4f, WallDoubleUp = 1f;
        public const float WallContactDps = 12f, WallMasteredSlow = 0.4f;
        const float WallContactTick = 0.25f, WallBreak = 60f;  // contact damage lands in ticks; a jump farther than this starts a new wall

        float wallContactAcc;
        readonly List<Zone> wallNodes = new List<Zone>();

        /// The drain a second, as a share of max health, `clock` seconds after the wall opened.
        public static float WallRate(float clock, float doubling) => WallDrain * (float)Math.Pow(2.0, clock / Math.Max(0.5f, doubling));

        /// The doubling time for this player (Blood Oath slows the ramp).
        public static float WallDoubleOf(Player p) => WallDouble + p.Stats.WallRampBonus;

        /// Pressing Blood Wall again while it's up stops it (no cooldown is spent for the press itself).
        public bool WallRelease(Player p, int slot) => p.BloodWallOn && p.Slots[slot].Id == AbilityId.CrimsonPact;

        /// Host: Blood Wall was cast.
        void OpenBloodWall(Player p, float power, bool mastered)
        {
            p.BloodWallOn = true;
            p.BloodWallClock = 0f;
            p.WallLast = p.Pos;
            p.WallPrev = -1;
            p.WallCount = 0;
            p.WallPower = power;
            p.WallMastered = mastered;
            BroadcastFx(FxKind.Bleed, p.Pos, new V2(p.Id, 0f), new Rgba(0.85f, 0.12f, 0.2f), 6f, 0.5f, Sfx.Ability);
        }

        /// Host, every tick (TickBloodMages): the drain ramps and the wall is laid along your path.
        void TickBloodWall(Player p, float dt)
        {
            if (!p.BloodWallOn)
                return;
            if (p.Downed || State == MatchState.GameOver)
            {
                EndBloodWall(p);
                return;
            }
            p.BloodWallClock += dt;
            if (!p.DevGod)
            {
                float cost = p.MaxHp * WallRate(p.BloodWallClock, WallDoubleOf(p)) * dt;
                float paid = Math.Min(cost, Math.Max(0f, p.Hp));
                p.Hp -= paid;
                p.Run.DamageTaken += paid;
                Recorder?.DamageTaken(p, paid, "Blood Wall");
                DownIfDead(p, "Blood Wall");
                if (p.Downed)
                {
                    EndBloodWall(p);
                    return;
                }
            }
            float d = p.WallLast.DistanceTo(p.Pos);
            if (d > WallBreak)  // carried off (a dash, a blink): the wall starts again where you are
            {
                p.WallLast = p.Pos;
                p.WallPrev = -1;
                p.WallCount = 0;
                return;
            }
            float life = WallLife + p.Stats.WallLifeBonus;
            while (d >= WallSpacing)
            {
                p.WallLast = p.WallLast + (p.Pos - p.WallLast) * (WallSpacing / d);
                var z = AddZone(ZoneKind.BloodWall, p, p.WallLast, WallHalf, life);
                z.Aim = p.WallPrev;
                z.GuardFrac = p.WallCount++;
                z.DmgMult *= p.WallPower;
                z.ExtraSlow = p.WallMastered ? WallMasteredSlow : 0f;
                p.WallPrev = z.Id;
                d -= WallSpacing;
            }
        }

        /// The wall comes down (a second press, or you went down). What's laid stays; the cooldown starts now.
        void EndBloodWall(Player p)
        {
            if (!p.BloodWallOn)
                return;
            p.BloodWallOn = false;
            p.WallPrev = -1;
            for (int i = 0; i < p.Slots.Length; i++)
                if (p.Slots[i].Id == AbilityId.CrimsonPact && p.Slots[i].Charges <= 0)
                    p.Slots[i].Timer = Cooldown(p, i);
        }

        /// The segment a wall node stands for: from the node before it (when it's still there) to this one.
        V2 WallFrom(Zone z)
        {
            Zone prev;
            if (z.Aim >= 0f && Zones.TryGetValue((int)z.Aim, out prev) && prev.Kind == ZoneKind.BloodWall)
                return prev.Pos;
            return z.Pos;
        }

        /// Closest point to `p` on the segment a..b.
        static V2 ClosestOn(V2 a, V2 b, V2 p)
        {
            V2 ab = b - a;
            float l2 = ab.LengthSq;
            if (l2 < 0.0001f)
                return a;
            float u = Math.Max(0f, Math.Min(1f, (p - a).Dot(ab) / l2));
            return a + ab * u;
        }

        void GatherWall()
        {
            wallNodes.Clear();
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.BloodWall)
                    wallNodes.Add(z);
        }

        /// Host, after the enemies move: nobody walks through a blood wall (they're pushed back out the side they came
        /// from), and every WallContactTick those pressed against one bleed for it.
        void KeepOffBloodWalls(float dt)
        {
            GatherWall();
            if (wallNodes.Count == 0)
            {
                wallContactAcc = 0f;
                return;
            }
            wallContactAcc += dt;
            bool tick = wallContactAcc >= WallContactTick;
            if (tick)
                wallContactAcc -= WallContactTick;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                Zone touched = null;
                foreach (var z in wallNodes)
                {
                    float reach = z.Radius + e.Radius;
                    if (Math.Abs(e.Pos.X - z.Pos.X) > reach + WallSpacing + 2f || Math.Abs(e.Pos.Y - z.Pos.Y) > reach + WallSpacing + 2f)
                        continue;
                    V2 c = ClosestOn(WallFrom(z), z.Pos, e.Pos), off = e.Pos - c;
                    float d = off.Length;
                    if (d >= reach + 2f)
                        continue;
                    touched = touched ?? z;
                    if (d >= reach)
                        continue;
                    V2 n = d > 0.01f ? off / d : (z.Pos - WallFrom(z)).Orthogonal().Normalized();
                    if (n.IsZero)
                        n = new V2(1f, 0f);
                    e.Pos = ClampToArena(c + n * (reach + 0.5f), e.Radius);
                    e.KnockLeft = V2.Zero;
                }
                if (touched == null || !tick)
                    continue;
                bool mastered = touched.ExtraSlow > 0f;
                HurtEnemy(e, WallContactDps * WallContactTick * touched.DmgMult * (mastered ? 2f : 1f), touched.Owner);
                if (mastered)
                {
                    e.SlowT = Math.Max(e.SlowT, 0.5f);
                    e.SlowMult = Math.Min(e.SlowMult, 1f - touched.ExtraSlow);
                }
            }
        }

        /// Host, from the enemy-bullet loop (Combat.cs): an enemy shot reaching a blood wall stops there, red aimed shots too.
        bool WallStops(Bullet b)
        {
            if (wallNodes.Count == 0)
                return false;
            foreach (var z in wallNodes)
            {
                float reach = z.Radius + b.Radius;
                if (Math.Abs(b.Pos.X - z.Pos.X) > reach + WallSpacing || Math.Abs(b.Pos.Y - z.Pos.Y) > reach + WallSpacing)
                    continue;
                if (!Zones.ContainsKey(z.Id))
                    continue;
                V2 c = ClosestOn(WallFrom(z), z.Pos, b.Pos);
                if (c.DistanceSq(b.Pos) > reach * reach)
                    continue;
                V2 back = b.Vel.IsZero ? new V2(0f, -1f) : -b.Vel.Normalized();
                BroadcastFx(FxKind.WallSplash, b.Pos, b.Pos + back * 20f, new Rgba(0.85f, 0.12f, 0.2f), b.Radius, 0.45f, Sfx.None);
                return true;
            }
            return false;
        }
    }
}
