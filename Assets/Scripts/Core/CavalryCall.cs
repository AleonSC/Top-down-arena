using System;
using System.Collections.Generic;

namespace Arena
{
    /// Cavalry Call (Tank Knight, replaces Unbreakable): two lancers ride in at your sides and charge CavRange px along your aim
    /// (four when mastered, two more outside the first pair). They appear at CavSpawnT, start riding at CavGoT and are gone by CavEndT.
    /// Each enemy a lance meets is hit once per cast: CavDamage, shoved CavKnock px out of the lane, slowed CavSlow for CavSlowTime
    /// (Heavy enemies: 45 px shove and a lighter slow; bosses: no shove). You stay where you are and take CavGuard less damage
    /// (Player.UnbreakT, replicated, kept from Unbreakable) for CavGuardTime while they ride.
    /// The charge is host logic; clients get one FxKind.CavalryCall (Pos = where you stood, Pos2 = the end of the lane, Radius = how many
    /// lancers, colour alpha 1 = mastered) and draw it from its age alone. Mockup: Docs/Mockups/cavalry-call.html. Look: Game/CavalryCallLook.cs.
    public partial class Game
    {
        public const float CavRange = 1100f, CavStart = 30f, CavTip = 70f, CavLane = 38f, CavLaneOuter = 114f, CavHitWidth = 34f, CavTrail = 240f;
        public const float CavSpawnT = 0.45f, CavGoT = 0.85f, CavEndT = 2.75f, CavFadeT = 0.35f;
        public const float CavDamage = 35f, CavKnock = 150f, CavKnockHeavy = 45f, CavSlow = 0.45f, CavSlowHeavy = 0.2f, CavSlowTime = 3f;
        public const float CavGuard = 0.2f, CavGuardTime = 3f, CavGuardTimeMastered = 5f;

        class CavalryCharge
        {
            public int Owner;
            public V2 Origin, Dir;
            public float T, Dmg;
            public int Lancers;
            public HashSet<int> Hit = new HashSet<int>();
        }

        readonly List<CavalryCharge> cavalry = new List<CavalryCharge>();

        /// How far the lancers have ridden after t seconds (the cast is t = 0): nothing until CavGoT, then fast at first, easing out.
        public static float CavReach(float t)
        {
            float u = MathX.Clamp01((t - CavGoT) / (CavEndT - CavGoT));
            return CavRange * (0.45f * u + 0.55f * (u * u * (3f - 2f * u)));
        }

        /// Sideways offset of lancer j (0, 1 inner pair; 2, 3 outer pair when mastered): -, +, -, +.
        public static float CavOffset(int j) => (j % 2 == 0 ? -1f : 1f) * (j < 2 ? CavLane : CavLaneOuter);

        void CastCavalryCall(Player p, V2 target, bool cap, float pw)
        {
            V2 dir = target - p.Pos;
            dir = dir.Length < 1f ? V2.FromAngle(p.Aim) : dir.Normalized();
            cavalry.Add(new CavalryCharge
            {
                Owner = p.Id, Origin = p.Pos, Dir = dir, Lancers = cap ? 4 : 2,
                Dmg = (CavDamage + p.Stats.CavalryDmgBonus) * p.Stats.DmgMult * pw
            });
            p.UnbreakT = cap ? CavGuardTimeMastered : CavGuardTime;
            BroadcastFx(FxKind.CavalryCall, p.Pos, p.Pos + dir * CavRange, new Rgba(1f, 0.82f, 0.36f, cap ? 1f : 0.5f), cap ? 4f : 2f,
                CavEndT + CavFadeT + 0.6f, Sfx.Clang);
        }

        /// Host, every tick: ride the lancers and hit whatever their lances meet.
        void TickCavalry(float dt)
        {
            for (int i = cavalry.Count - 1; i >= 0; i--)
            {
                var c = cavalry[i];
                c.T += dt;
                if (c.T > CavEndT)
                {
                    cavalry.RemoveAt(i);
                    continue;
                }
                if (c.T < CavGoT)
                    continue;
                float tip = CavStart + CavReach(c.T) + CavTip;
                V2 side = c.Dir.Orthogonal();
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f || e.Hp <= 0f || c.Hit.Contains(e.Id))
                        continue;
                    V2 rel = e.Pos - c.Origin;
                    float u = Dot(rel, c.Dir), v = Dot(rel, side);
                    if (u > tip || u < Math.Max(-10f, tip - CavTrail))
                        continue;
                    bool met = false;
                    for (int j = 0; j < c.Lancers && !met; j++)
                        met = Math.Abs(v - CavOffset(j)) < CavHitWidth + e.Radius;
                    if (!met)
                        continue;
                    c.Hit.Add(e.Id);
                    bool heavy = Heavy(e);
                    e.SlowT = Math.Max(e.SlowT, CavSlowTime);
                    e.SlowMult = Math.Min(e.SlowMult, 1f - (heavy ? CavSlowHeavy : CavSlow));
                    if (!e.Def.IsBoss)
                    {
                        V2 out_ = (side * (Math.Abs(v) < 1f ? (e.Id % 2 == 0 ? 1f : -1f) : Math.Sign(v)) * 0.8f + c.Dir * 0.55f).Normalized();
                        KnockEnemy(e, out_ * (heavy ? CavKnockHeavy : CavKnock));  // slides (Knock.cs)
                    }
                    HurtEnemy(e, c.Dmg, c.Owner);
                    castHits++;
                    BroadcastFx(FxKind.Ring, e.Pos, new Rgba(1f, 0.82f, 0.36f), e.Radius + 30f, 0.4f, Sfx.None);
                }
            }
        }
    }
}
