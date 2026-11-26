using System;
using System.Collections.Generic;

namespace Arena
{
    /// Warhorn Volley, the DPS Knight's default ultimate (key 0 on the lobby's Ultimate row; the Drawbridge moved to the Tank Knight), the Fire Arrows look of
    /// Docs/Mockups/warhorn-volley.html. You blow a warhorn (VolHornT s; you keep moving and fighting) and archers you never see
    /// answer it: burning arrows fall in a front that lands VolBehind px behind you at VolLandT and crosses the field along your
    /// aim at VolSpeed px/s, out to VolRange px ahead, VolHalf px to either side. Every enemy the front reaches is hit once:
    /// VolDamage (three arrows), set burning (VolBurnDps for VolBurnTime s, the strongest fire on it counts) and pinned (slowed
    /// VolPin for VolPinTime s; bosses aren't pinned). Ordinary enemy shots in the front's path are knocked out of the air (red
    /// aimed shots fly on). Mastered: the lane is VolMasterWide wider and the fire burns VolBurnTimeMastered s. The rare card Bodkin Points
    /// (Stats.VolleyDamage, VolleyWide) adds 25% damage and 20% width a stack.
    /// The volley is host logic; clients get one FxKind.WarhornVolley (Pos = where you stood, Pos2 = Pos + the aim * VolRange,
    /// Radius = your id, colour alpha = the lane's half width / 1000 (mastered and Bodkin Points widen it), Dur = VolFxDur) and draw the horn and every arrow from its age alone
    /// (Game/WarhornVolleyLook.cs); its sound is Sfx.Warhorn, the horn and the whole volley in one take (WarhornSoundBank.cs).
    public partial class Game
    {
        public const float VolHornT = 0.9f, VolBlastT = 0.18f, VolLandT = 0.6f, VolFallT = 0.4f;
        public const float VolBehind = 150f, VolRange = 1400f, VolSpeed = 700f, VolHalf = 350f, VolMasterWide = 1.4f;
        public const float VolDamage = 120f, VolBurnDps = 20f, VolBurnTime = 5f, VolBurnTimeMastered = 8f, VolPin = 0.4f, VolPinTime = 3f;
        public const float VolBand = 80f, VolShotBand = 60f, VolLinger = 2.2f;

        /// When the front reaches the far end, and how long the look lasts (the shafts stay in the floor VolLinger s).
        public const float VolEndT = VolLandT + (VolBehind + VolRange) / VolSpeed;
        public const float VolFxDur = VolEndT + VolLinger + 0.4f;

        /// How far along the aim (px from where you stood, negative behind you) the arrows are landing t s after the cast.
        public static float VolFront(float t) => -VolBehind + VolSpeed * (t - VolLandT);

        /// Half the lane's width, mastered or not.
        public static float VolHalfOf(bool mastered) => VolHalf * (mastered ? VolMasterWide : 1f);

        class Volley
        {
            public int Owner;
            public V2 Origin, Dir;
            public float T, Dmg, Burn, BurnTime, Half;
            public HashSet<int> Hit = new HashSet<int>();
        }

        readonly List<Volley> volleys = new List<Volley>();
        readonly List<int> volleyShots = new List<int>();

        void CastWarhornVolley(Player p, V2 target, bool cap, float pw)
        {
            V2 dir = target - p.Pos;
            dir = dir.Length < 1f ? V2.FromAngle(p.Aim) : dir.Normalized();
            float mult = p.Stats.DmgMult * pw;
            volleys.Add(new Volley
            {
                Owner = p.Id, Origin = p.Pos, Dir = dir, Dmg = VolDamage * mult * (1f + p.Stats.VolleyDamage), Burn = VolBurnDps * mult,
                BurnTime = cap ? VolBurnTimeMastered : VolBurnTime, Half = VolHalfOf(cap) * (1f + p.Stats.VolleyWide)
            });
            BroadcastFx(FxKind.WarhornVolley, p.Pos, p.Pos + dir * VolRange, new Rgba(1f, 0.55f, 0.18f, VolHalfOf(cap) * (1f + p.Stats.VolleyWide) / 1000f), p.Id, VolFxDur, Sfx.Warhorn);  // (alpha: the lane's half width / 1000)
        }

        /// Host, every tick: move each volley's front and hit whatever it reaches.
        void TickVolleys(float dt)
        {
            for (int i = volleys.Count - 1; i >= 0; i--)
            {
                var vl = volleys[i];
                vl.T += dt;
                if (vl.T > VolEndT + 0.05f)
                {
                    volleys.RemoveAt(i);
                    continue;
                }
                if (vl.T < VolLandT)
                    continue;
                float front = VolFront(vl.T);
                V2 side = vl.Dir.Orthogonal();
                foreach (var e in AllFoes(by: vl.Owner))
                {
                    if (vl.Hit.Contains(e.Id))
                        continue;
                    V2 rel = e.Pos - vl.Origin;
                    float u = Dot(rel, vl.Dir), v = Dot(rel, side);
                    if (u > front + e.Radius || u < front - VolBand || Math.Abs(v) > vl.Half + e.Radius)
                        continue;
                    vl.Hit.Add(e.Id);
                    e.BurnT = Math.Max(e.BurnT, vl.BurnTime);
                    e.BurnDps = Math.Max(e.BurnDps, vl.Burn);
                    e.BurnBy = vl.Owner;
                    Cauterize(e, vl.Owner);  // fire on a bleed (Cauterize.cs)
                    if (!e.Def.IsBoss)
                        Slow(e, VolPinTime, 1f - VolPin);
                    HurtEnemy(e, vl.Dmg, vl.Owner);
                    castHits++;
                }
                // Ordinary enemy shots in the front's path are knocked down; red aimed shots fly on.
                volleyShots.Clear();
                foreach (var b in Bullets.Values)
                {
                    if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind))
                        continue;
                    V2 rel = b.Pos - vl.Origin;
                    float u = Dot(rel, vl.Dir);
                    if (u <= front + 20f && u >= front - VolShotBand && Math.Abs(Dot(rel, side)) <= vl.Half)
                        volleyShots.Add(b.Id);
                }
                foreach (int id in volleyShots)
                    KillBullet(id);
            }
        }
    }
}
