using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Mender's Bow's Focus shot and Scatter, its Alt (Docs/Mockups/menders-bow-rework.html).
    ///
    /// Focus: once a draw is full, the Focus bar pops up beside you. A marker sweeps up and down it (BowFocusPeriod for
    /// a round trip) past a gold zone (BowFocusZone of the bar, at a new height each draw). Let go while the marker is in
    /// the zone and the arrow is a Focus arrow (Defs.FocusArrow): it hits much harder, goes through everything, and
    /// carries a gust of wind that shoves every enemy near its path. Let go anywhere else and it's a plain full shot.
    /// The shooter's game runs the bar and decides (LocalBow); the host is told with the release (Msg.Input), like the
    /// draw time itself.
    ///
    /// Scatter (the weapon-action key): five small arrows in a fan (Defs.ScatterArrow), each stopping in the first enemy
    /// it meets. Your game sends Msg.WeaponDash (DashScatter); the host checks the cooldown and looses them.
    public partial class Game
    {
        public const float BowFocusPeriod = 0.8f;  // the marker goes bottom to top and back in this long
        public const float BowFocusZone = 0.2f;    // the gold zone's share of the bar
        public const float BowZoneLow = 0.2f, BowZoneHigh = 0.75f;  // where the zone's bottom can be
        public const float BowFocusDmg = 90f, BowFocusHeal = 30f, BowFocusSpeed = 1150f, BowFocusReach = 1100f;
        public const float BowWindR = 34f;         // the gust shoves enemies this close to the arrow's path (plus their size)
        public const float BowWindShove = 70f;     // this far (heavies half; bosses not at all)
        public const float BowWindHeavy = 0.5f;

        public const int ScatterArrows = 5;
        public const float ScatterFan = 0.7f;      // radians from one edge of the fan to the other (40 degrees)
        public const float ScatterDmg = 12f, ScatterHeal = 6f, ScatterSpeed = 820f, ScatterReach = 400f;
        public const float ScatterCd = 3f;
        const float ScatterHold = 0.25f;           // no shots for this long after the fan
        const byte DashScatter = 57;

        // --- The Focus bar (the shooter's game) ----------------------------------------------------------------

        float bowZone = 0.5f;       // the bottom of this draw's gold zone (0 the bar's bottom, 1 its top)
        bool bowFocusOut;           // the release this frame was in the zone (sent with it)
        readonly Random bowRng = new Random(7177);

        /// Where this draw's gold zone starts (0 bottom .. 1 top); it spans BowFocusZone.
        public float BowZone => bowZone;

        /// The marker on the Focus bar, `sinceFull` seconds after the draw was full: 0 at the bottom, 1 at the top.
        public static float BowMarker(float sinceFull)
        {
            float ph = (float)(sinceFull / BowFocusPeriod - Math.Floor(sinceFull / BowFocusPeriod));
            return ph < 0.5f ? ph * 2f : 2f - ph * 2f;
        }

        /// The Focus bar is up: the Mender's Bow drawn to full.
        public bool BowFocusBar(Player p, float draw) => p.Weapon != null && p.Weapon.Id == WeaponId.MendersBow && draw >= BowFull(p);

        /// Letting go now would be a Focus shot: the marker is in the gold zone.
        public bool BowInZone(Player p, float draw)
        {
            if (!BowFocusBar(p, draw))
                return false;
            float m = BowMarker(draw - BowFull(p));
            return m >= bowZone && m <= bowZone + BowFocusZone;
        }

        /// Your last release, for the bar's look as it goes (Game/BowFocusLook.cs): counted, whether the bar was up, where
        /// the marker stood, and whether it was a Focus shot.
        public int BowReleases { get; private set; }
        public bool BowLastBar { get; private set; }
        public bool BowLastFocus { get; private set; }
        public float BowLastMarker { get; private set; }

        void NoteBowRelease(Player me, float draw)
        {
            if (me.Weapon.Id != WeaponId.MendersBow)
                return;
            BowReleases++;
            BowLastBar = BowFocusBar(me, draw);
            BowLastFocus = bowFocusOut;
            BowLastMarker = BowLastBar ? BowMarker(draw - BowFull(me)) : 0f;
        }

        void NewBowZone() => bowZone = BowZoneLow + (float)bowRng.NextDouble() * (BowZoneHigh - BowZoneLow);

        /// Host: a Focus arrow leaves the bow (the volley's first arrow on a Focus release).
        Bullet FocusArrow(Player p, V2 muzzle, float aim, float dmg)
        {
            float d = BowFocusDmg * dmg;
            var b = SpawnBullet(Defs.FocusArrow, muzzle, V2.FromAngle(aim) * BowFocusSpeed, 0f, BowFocusReach / BowFocusSpeed, d, p.Id);
            b.HealAlly = BowFocusHeal * 9f / d;  // (HealAllyHit scales it by Dmg / 9)
            b.Pierce = 999;                      // through everything
            b.Bolts = true;
            b.Gusted = new HashSet<int>();
            return b;
        }

        /// Host, every tick: each Focus arrow's gust shoves the enemies near its path, once each.
        void TickBowWind(float dt)
        {
            foreach (var b in Bullets.Values)
            {
                if (b.Kind != Defs.FocusArrow || b.Gusted == null)
                    continue;
                V2 fwd = b.Vel.Normalized(), from = b.Pos - b.Vel * dt;
                foreach (var e in Enemies.Values)
                {
                    if (!Targetable(e) || e.Def.IsBoss || b.Gusted.Contains(e.Id))
                        continue;
                    if (SegDistance(e.Pos, from, b.Pos) > BowWindR + e.Radius)
                        continue;
                    b.Gusted.Add(e.Id);
                    V2 rel = e.Pos - b.Pos, across = new V2(-fwd.Y, fwd.X);
                    float side = Math.Sign(rel.X * across.X + rel.Y * across.Y);
                    if (side == 0f) side = e.Id % 2 == 0 ? 1f : -1f;
                    V2 push = (fwd * 0.8f + across * (side * 0.6f)).Normalized();  // on, and away to its side of the path
                    KnockEnemy(e, push * BowWindShove * (e.Def.Heavy ? BowWindHeavy : 1f));  // slides (Knock.cs)
                }
            }
        }

        static float SegDistance(V2 pt, V2 a, V2 b)
        {
            V2 ab = b - a;
            float len2 = ab.X * ab.X + ab.Y * ab.Y;
            float t = len2 > 0f ? MathX.Clamp01(((pt.X - a.X) * ab.X + (pt.Y - a.Y) * ab.Y) / len2) : 0f;
            return pt.DistanceTo(a + ab * t);
        }

        // --- Scatter (the weapon-action key) -----------------------------------------------------------------------

        float scatterLocalCd;

        /// Your own Scatter's cooldown, for the arc under you.
        public float ScatterCooldown => Math.Max(0f, scatterLocalCd);

        /// Your game: the weapon-action key with the Mender's Bow. True when the key belongs to Scatter (pressed or not).
        bool LocalScatter(Player me, bool pressed, V2 aimAt, float dt)
        {
            scatterLocalCd -= dt;
            if (me.Weapon.Id != WeaponId.MendersBow)
                return false;
            if (pressed && scatterLocalCd <= 0f && !me.Downed && me.StunShots <= 0)
            {
                scatterLocalCd = ScatterCd;
                bowDraw = -1f;                       // a draw in progress is dropped
                bowCd = BowRecover / FireRate(me);   // and the next one waits for a fresh arrow
                if (IsHost)
                    HostWeaponDash(MyId, DashScatter, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(DashScatter);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }

        /// Host: a player loosed a Scatter, aiming at `aimAt`.
        void HostScatter(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.MendersBow || p.Downed || p.StunT > 0f || p.ScatterCd > 0f || p.SwallowT > 0f || State == MatchState.GameOver)
                return;
            p.ScatterCd = ScatterCd - 0.1f;  // (a little slack for the round trip)
            p.BowRelease = 0f;
            p.FireCd = Math.Max(p.FireCd, ScatterHold);
            V2 toward = aimAt - p.Pos;
            float aim = toward.Length > 1f ? toward.Angle : p.Aim;
            float d = ScatterDmg * p.Stats.DmgMult;
            for (int k = 0; k < ScatterArrows; k++)
            {
                float a = aim + ScatterFan * (k / (float)(ScatterArrows - 1) - 0.5f);
                var b = SpawnBullet(Defs.ScatterArrow, p.Pos + V2.FromAngle(a) * p.Def.BodyR, V2.FromAngle(a) * ScatterSpeed, 0f, ScatterReach / ScatterSpeed, d, p.Id);
                b.HealAlly = ScatterHeal * 9f / d;
                b.Pierce = 0;
                b.WeaponDmg = true;
            }
            BroadcastFx(FxKind.Spark, p.Pos + V2.FromAngle(aim) * (p.Def.BodyR + 10f), new Rgba(0.5f, 0.9f, 0.85f), 18f, 0.2f, Sfx.Pick);
        }

        /// Host, every tick: Scatter cools down.
        void TickScatter(float dt)
        {
            foreach (var p in Players.Values)
                p.ScatterCd = Math.Max(0f, p.ScatterCd - dt);
        }
    }
}
