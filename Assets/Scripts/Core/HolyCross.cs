using System;
using System.Collections.Generic;

namespace Arena
{
    /// Consecrate (Healer Knight): a Holy Cross leaves you along your aim, flying HolyRange px in HolyTime seconds (fast at
    /// first, slowing) and growing as it goes (arms HolyArmMin to HolyArmMax, bars HolyHalfMin to HolyHalfMax). Everything
    /// the cross shape touches is hit once per cast:
    ///   allies (not you, not the downed): healed HolyHeal and given Swiftness (Player.SwiftT, +30% speed) for HolySwiftTime;
    ///   enemies: HolyDamage, slowed 40% for HolySlowTime and knocked back HolyKnock px away from the cross's centre
    ///   (bosses and Heavy enemies: only a 20% slow, no knockback, as for Tsunami's wet).
    /// Heal and damage scale with the caster's power (and damage with their damage stat). Mastered: two more crosses at
    /// +-HolyFan radians; the three share what they hit, so nobody is hit twice.
    /// The cross is host logic; clients get one FxKind.HolyCross per cross (Pos = where it starts, Pos2 = where it ends) and draw it
    /// from the time alone. Mockup: Docs/Mockups/consecrate-cross-v3.html. Look: Game/HolyCrossLook.cs.
    public partial class Game
    {
        public const float HolyRange = 600f, HolyTime = 1.2f, HolyArmMin = 50f, HolyArmMax = 190f, HolyHalfMin = 10f, HolyHalfMax = 22f;
        public const float HolyHeal = 35f, HolyDamage = 25f, HolySwiftTime = 4f, HolySwiftMult = 1.3f;
        public const float HolySlowTime = 3f, HolySlow = 0.4f, HolySlowHeavy = 0.2f, HolyKnock = 110f, HolyFan = 0.38f;

        class HolyCrossFlight
        {
            public int Owner;
            public V2 Origin, Dir;
            public float T, Range, Heal, Dmg, SwiftFor;
            public HashSet<int> HitEnemies, HitPlayers;  // shared by the crosses of one cast
        }

        readonly List<HolyCrossFlight> holyCrosses = new List<HolyCrossFlight>();

        /// How far along its flight the cross is after t seconds (it eases out: fast, then slower).
        public static float HolyReach(float t) => 1f - (float)Math.Pow(1f - MathX.Clamp01(t / HolyTime), 2.2);

        void CastHolyCross(Player p, V2 target, bool cap, float pw)
        {
            V2 dir = target - p.Pos;
            dir = dir.Length < 1f ? V2.FromAngle(p.Aim) : dir.Normalized();
            float range = HolyRange * p.Stats.HolyRangeMult;
            var enemies = new HashSet<int>();
            var players = new HashSet<int> { p.Id };
            float aim = (float)Math.Atan2(dir.Y, dir.X);
            for (int i = 0; i < (cap ? 3 : 1); i++)
            {
                float turn = i == 0 ? 0f : (i == 1 ? HolyFan : -HolyFan);
                V2 d = V2.FromAngle(aim + turn);
                holyCrosses.Add(new HolyCrossFlight
                {
                    Owner = p.Id, Origin = p.Pos, Dir = d, Range = range, Heal = HolyHeal * pw, Dmg = HolyDamage * p.Stats.DmgMult * pw,
                    SwiftFor = HolySwiftTime + p.Stats.HolySwiftBonus, HitEnemies = enemies, HitPlayers = players
                });
                BroadcastFx(FxKind.HolyCross, p.Pos, p.Pos + d * range, new Rgba(1f, 0.85f, 0.4f), range, HolyTime + 0.4f, i == 0 ? Sfx.SpellHoly : Sfx.None);
            }
        }

        /// Is a circle of radius r at `at` inside the cross centred at `c` pointing along `dir` (arms `arm`, half-thickness `half`)?
        static bool InHolyCross(V2 at, float r, V2 c, V2 dir, float arm, float half)
        {
            V2 rel = at - c;
            float u = Dot(rel, dir), v = Dot(rel, dir.Orthogonal());
            return Math.Abs(u) <= arm + r && Math.Abs(v) <= half + r || Math.Abs(v) <= arm + r && Math.Abs(u) <= half + r;
        }

        /// Host, every tick: fly the crosses and bless or batter whatever they pass through.
        void TickHolyCross(float dt)
        {
            for (int i = holyCrosses.Count - 1; i >= 0; i--)
            {
                var c = holyCrosses[i];
                c.T += dt;
                if (c.T > HolyTime)
                {
                    holyCrosses.RemoveAt(i);
                    continue;
                }
                float reach = HolyReach(c.T);
                V2 centre = c.Origin + c.Dir * (c.Range * reach);
                float arm = HolyArmMin + (HolyArmMax - HolyArmMin) * reach, half = HolyHalfMin + (HolyHalfMax - HolyHalfMin) * reach;
                Players.TryGetValue(c.Owner, out var caster);
                foreach (var q in Players.Values)
                {
                    if (q.Downed || c.HitPlayers.Contains(q.Id) || !InHolyCross(q.Pos, q.Def.BodyR, centre, c.Dir, arm, half))
                        continue;
                    c.HitPlayers.Add(q.Id);
                    Heal(q, c.Heal, caster ?? q);
                    q.SwiftT = Math.Max(q.SwiftT, c.SwiftFor);
                    castHits++;
                    BroadcastFx(FxKind.Ring, q.Pos, new Rgba(0.31f, 0.82f, 0.91f), 46f, 0.5f, Sfx.None);
                }
                foreach (var e in Enemies.Values)
                {
                    if (!Targetable(e) || c.HitEnemies.Contains(e.Id) || !InHolyCross(e.Pos, e.Radius, centre, c.Dir, arm, half))
                        continue;
                    c.HitEnemies.Add(e.Id);
                    bool heavy = Heavy(e);
                    Slow(e, HolySlowTime, 1f - (heavy ? HolySlowHeavy : HolySlow));
                    if (!heavy)
                    {
                        V2 away = e.Pos - centre;
                        KnockEnemy(e, (away.Length < 1f ? c.Dir : away.Normalized()) * HolyKnock);  // slides (Knock.cs)
                    }
                    HurtEnemy(e, c.Dmg, c.Owner);
                    BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.71f, 0.55f, 1f), e.Radius + 24f, 0.4f, Sfx.None);
                }
            }
        }
    }
}
