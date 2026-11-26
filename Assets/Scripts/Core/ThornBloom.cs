using System;
using System.Collections.Generic;

namespace Arena
{
    /// Thorn Bloom, the DPS Hemomancer's 4th choice (`AbilityId.ThornBloom`, `AbilityDef.Fourth`: the lobby's 4th ability
    /// key), from Docs/Mockups/thorn-bloom.html (look B, Needle Volley). Pay 12% of your max health: twelve blood needles
    /// burst out of you, evenly round your aim, 240 px. Each stops on the first enemy (or wall) it meets, for 45 and a
    /// bleed stack. Every needle that lands whips a tendril on to the nearest enemy no tendril has reached yet (180 px), for
    /// 30, and that one chains on: 3 jumps a needle (mastered 5), 20% less each jump. An enemy a needle struck is never a
    /// tendril's target, so the chains spread outward.
    ///
    /// The host works it all out at the cast (`CastThornBloom`) and lands each hit when its needle or tendril arrives
    /// (`tbHits`, ticked by `TickThornBloom`); every screen draws it from three effects (Game/ThornBloomLook.cs):
    /// `FxKind.ThornBloom` (the burst out of you), `FxKind.ThornNeedle` (one a needle) and `FxKind.ThornTendril` (one a jump).
    public partial class Game
    {
        public const int TbNeedles = 12;
        public const float TbReach = 240f, TbSpeed = 1150f;           // needle reach (from your body) and flight speed
        public const float TbTendrilSpeed = 1500f, TbTendrilBase = 0.1f, TbTendrilGap = 0.05f;  // a jump takes base + length / speed, then the next waits a beat
        public const float TbNeedleTail = 0.3f;                        // a needle's burst (or its fade at full reach) after it arrives
        public const float TbTendrilHold = 0.35f, TbTendrilFade = 0.25f;  // a tendril stays strung, then lets go
        public const float TbBurstFx = 0.45f;                          // the burst out of you
        const float TbDamage = 45f, TbBleed = 6f;                      // a needle: the hit, and its bleed a second (one stack)
        const float TbTendrilDamage = 30f, TbTendrilFall = 0.8f;       // the first jump, then 20% less a jump
        const float TbTendrilReach = 180f;
        public const int TbJumps = 3, TbJumpsMastered = 5;
        const float TbWidth = 5f;                                      // how near a needle's line an enemy must be (plus its radius)
        public const float TbCooldown = 8f, TbCost = 0.12f;

        public const float TbNeedleHeal = 0.01f;  // each needle that strikes an enemy heals you this share of your max health
        struct TbHit { public int Owner, Target; public float T, Dmg; public bool Needle; }
        readonly List<TbHit> tbHits = new List<TbHit>();

        /// How many jumps each needle's tendril makes (Thornwork adds one a stack).
        public static int TbJumpsOf(Player p, bool mastered) => (mastered ? TbJumpsMastered : TbJumps) + p.Stats.TbExtraJumps;

        /// Host: Thorn Bloom cast (its health is already paid).
        void CastThornBloom(Player p, float power, bool mastered)
        {
            var s = p.Stats;
            float body = p.Def.BodyR, needleDmg = TbDamage * s.DmgMult * s.TbDmgMult * power;
            var blood = new Rgba(0.85f, 0.12f, 0.2f);
            BroadcastFx(FxKind.ThornBloom, p.Pos, new V2(p.Id, p.Aim), blood, TbNeedles, TbBurstFx, Sfx.BlastWhirl);
            var landed = new List<KeyValuePair<float, Enemy>>();  // (when the needle lands, the enemy) for the tendrils
            for (int i = 0; i < TbNeedles; i++)
            {
                V2 dir = V2.FromAngle(p.Aim + i * MathX.Tau / TbNeedles), from = p.Pos + dir * body;
                float len = TbReach;
                for (float d = 8f; d <= TbReach; d += 8f)  // a wall or the arena's edge stops it
                    if (EsBlocked(from + dir * d))
                    {
                        len = Math.Max(6f, d - 6f);
                        break;
                    }
                Enemy hit = null;
                foreach (var e in Enemies.Values)
                {
                    if (!Targetable(e))
                        continue;
                    float rx = e.Pos.X - from.X, ry = e.Pos.Y - from.Y, along = rx * dir.X + ry * dir.Y;
                    float perp = Math.Abs(rx * dir.Y - ry * dir.X), r = e.Radius + TbWidth;
                    if (perp >= r || along + r < 0f)
                        continue;
                    float entry = Math.Max(0f, along - (float)Math.Sqrt(r * r - perp * perp));
                    if (entry < len)
                    {
                        len = Math.Max(6f, entry);
                        hit = e;
                    }
                }
                float flight = len / TbSpeed;
                BroadcastFx(FxKind.ThornNeedle, from, from + dir * len, blood, hit != null ? 1f : 0f, flight + TbNeedleTail, Sfx.None);
                if (hit == null)
                    continue;
                tbHits.Add(new TbHit { Owner = p.Id, Target = hit.Id, T = -flight, Dmg = needleDmg, Needle = true });
                landed.Add(new KeyValuePair<float, Enemy>(flight, hit));
            }
            // The tendrils: from each needle's enemy, in the order they're struck, on to the nearest enemy no tendril has reached.
            landed.Sort((a, b) => a.Key.CompareTo(b.Key));
            var reached = new HashSet<int>();  // (an enemy a needle struck counts as reached: the chains spread outward, never back)
            foreach (var l in landed)
                reached.Add(l.Value.Id);
            int jumps = TbJumpsOf(p, mastered);
            foreach (var l in landed)
            {
                Enemy at = l.Value;
                float t = l.Key + TbTendrilGap, dmg = TbTendrilDamage * s.DmgMult * s.TbDmgMult * power;
                for (int j = 0; j < jumps; j++)
                {
                    Enemy next = null;
                    float best = TbTendrilReach;
                    foreach (var e in Enemies.Values)
                    {
                        if (e == at || !Targetable(e) || reached.Contains(e.Id))
                            continue;
                        float d = e.Pos.DistanceTo(at.Pos) - e.Radius;
                        if (d < best)
                        {
                            best = d;
                            next = e;
                        }
                    }
                    if (next == null)
                        break;
                    reached.Add(next.Id);
                    float travel = TbTendrilBase + at.Pos.DistanceTo(next.Pos) / TbTendrilSpeed;
                    BroadcastFx(FxKind.ThornTendril, at.Pos, next.Pos, new Rgba(blood.R, blood.G, blood.B, Math.Min(0.99f, t / 4f)), j, t + travel + TbTendrilHold + TbTendrilFade, Sfx.None);
                    tbHits.Add(new TbHit { Owner = p.Id, Target = next.Id, T = -(t + travel), Dmg = dmg });
                    t += travel + TbTendrilGap;
                    dmg *= TbTendrilFall;
                    at = next;
                }
            }
            castHits += landed.Count;
        }

        /// When a tendril effect starts, from its colour's alpha (the delay / 4 s; Game/ThornBloomLook.cs).
        public static float TbTendrilDelay(Effect fx) => fx.Color.A * 4f;

        /// Host, every tick: needles and tendrils land.
        void TickThornBloom(float dt)
        {
            for (int i = tbHits.Count - 1; i >= 0; i--)
            {
                var h = tbHits[i];
                if ((h.T += dt) < 0f)
                {
                    tbHits[i] = h;
                    continue;
                }
                tbHits.RemoveAt(i);
                Player p;
                Enemy e;
                if (!Players.TryGetValue(h.Owner, out p) || !TryFoe(h.Target, out e) || !Targetable(e))
                    continue;
                HurtEnemy(e, h.Dmg, p.Id);
                if (h.Needle)
                {
                    ApplyBleed(e, TbBleed, p.Id);
                    if (!p.Downed)
                        Heal(p, p.MaxHp * TbNeedleHeal, p);  // each needle that strikes gives some of the cost back
                }
            }
        }
    }
}
