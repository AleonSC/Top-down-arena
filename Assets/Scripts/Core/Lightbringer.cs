using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Lightbringer (Healer Knight): Dawn, Mercy, Noon (Docs/LightbringerRework.md; mockup Docs/Mockups/lightbringer-rework.html;
    /// the motion is LightbringerMotion.cs, the look Game/LightbringerLook.cs).
    ///   - Chain: Dawn Cut (its crescent heals the allies it passes), Mercy Cut (its crescent wards them: a shield of MercyWard for
    ///     MercyTime), Noon Sunburst (the sword planted: a ring that shoves enemies back and heals every ally in it, you a little).
    ///     The next press counts once a step is Next of the way through; LightChainWindow after a step ends without one, the chain
    ///     starts over. The step and a count of steps share Player.Combo with the Twin Blades (StepOf), and each step moves you
    ///     forward a little on your own game (LocalLightStep).
    ///   - Light gauge (Player.Light, 0 to LightMax): every enemy a cut hits adds one, every enemy the Sunburst hits two. Full,
    ///     the next Noon is Radiant: a bigger ring, LightRadiant times the healing and a ward on everyone in it; it spends the gauge.
    ///   - Alt (the weapon-action key): the Sunlance. With LanceNeed light or more, the ally nearest your cursor (up to
    ///     LanceRange away, downed or not) gets a lance of light: LancePerPip healing a pip (a downed ally gets LanceRevive of
    ///     their revive instead), and every enemy on the line takes LanceBase + LanceDmgPip a pip. It spends all the light.
    public partial class Game
    {
        public const float LightChainWindow = 0.8f, LightArc = 100f, CrescentMult = 12f / 17f, CrescentHeal = 6f, NoonMult = 22f / 17f;
        public const float NoonRadius = 140f, NoonRadiantRadius = 220f, NoonGrow = 30f, NoonAt = 26f, NoonKnock = 28f, NoonHeal = 10f, NoonSelfHeal = 5f;
        public const float MercyWard = 12f, MercyTime = 3f;
        public const int LanceNeed = 4;
        public const float LanceRange = 450f, LancePerPip = 4f, LanceBase = 10f, LanceDmgPip = 2f, SunlanceHalfWidth = 12f, LanceCooldown = 1.5f, LanceRevive = 0.25f;
        public static readonly Rgba LightGold = new Rgba(1f, 0.84f, 0.47f);

        class LanceRun
        {
            public int Owner, Target, Pips;
            public float T;
        }

        readonly List<LanceRun> lanceRuns = new List<LanceRun>();
        float lanceLocalCd;  // your own game: until the key throws again (the host checks its own)
        public float SunlanceCd => lanceLocalCd;

        /// Host: one press of the primary with the Lightbringer (from FireKnightWeapon): the next step of the chain.
        void FireLightbringer(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            int last = TwinBladesMotion.StepOf(p.Combo);
            int step = last >= 1 && last < 3 && p.ComboT > 0f ? last + 1 : 1;
            int count = ((p.Combo >> 3) + 1) & 31;
            p.Combo = (count << 3) | step;
            var d = LightbringerMotion.Steps[step - 1];
            p.ComboT = d.Dur + LightChainWindow;
            p.FireCd = d.Dur * d.Next / FireRate(p);
            bool radiant = false;
            float reach = MeleeReach(p, w.Reach);
            Swing s;
            if (step < 3)
            {
                s = NewSwing(p, aim, w.Reach, LightArc, w.Damage * dmg);
                s.Delay = d.Dur * LightbringerMotion.CutHit;
                s.LightWaves = 1 + extra;
                s.LightWaveDmg = w.Damage * dmg * CrescentMult;
                s.Sound = step == 1 ? Sfx.LightCut1 : Sfx.LightCut2;  // (LightSoundBank.cs)
            }
            else
            {
                radiant = p.Light >= LightMax;
                if (radiant)
                    p.Light = 0;  // spent as it is raised: the gauge starts filling again from the ring's own hits
                reach = (radiant ? NoonRadiantRadius : NoonRadius) + NoonGrow * extra;
                s = NewSwing(p, aim, 0f, 360f, w.Damage * dmg * NoonMult);
                s.Circle = reach;
                s.CircleAt = NoonAt;
                s.Knock = NoonKnock;
                s.Heavy = true;
                s.Delay = d.Dur * LightbringerMotion.NoonHit;
                s.Silent = true;  // its sound plays from the click: the swell up to the blow, then the chord
            }
            s.NoFx = true;  // the step's own effect draws it
            s.LightKind = (byte)step;
            s.LightRadiant = radiant;
            pendingSwings.Add(s);
            // The whole move, drawn from its age: Radius carries who and which step (4: a Radiant Noon); Pos2 is out along the
            // aim at the cut's reach, or at the ring's radius for Noon.
            BroadcastFx(FxKind.LightStep, p.Pos, p.Pos + V2.FromAngle(aim) * reach, LightGold, p.Id * 10 + (radiant ? 4 : step),
                d.Dur + LightbringerMotion.Recover + (step == 3 ? 0.15f : 0f), step < 3 ? Sfx.None : radiant ? Sfx.LightRadiant : Sfx.LightNoon);  // (Noon's light outlasts the swing)
        }

        /// Host, from LandSwing: a Lightbringer step landed and hit `hits` enemies. The gauge fills, Dawn and Mercy throw their
        /// crescents, Noon heals (and a Radiant Noon wards) everyone in its ring.
        void LightLanded(Player p, Swing s, V2 origin, V2 circleAt, int hits)
        {
            if (!s.LightRadiant)
                p.Light = Math.Min(LightMax, p.Light + hits * (s.LightKind == 3 ? 2 : 1));
            if (s.LightKind < 3)
            {
                if (hits > 0)
                    BroadcastSound(Sfx.LightHit);
                for (int i = 0; i < s.LightWaves; i++)
                {
                    float f = s.LightWaves == 1 ? 0f : i * 2f / (s.LightWaves - 1) - 1f;
                    var b = SpawnBullet(Defs.KnightWave, origin + V2.FromAngle(s.Aim) * p.Def.BodyR, V2.FromAngle(s.Aim + f * 0.2f * (s.LightWaves - 1)) * 600f, 0f, 0.45f, s.LightWaveDmg, p.Id);
                    b.Pierce += 2;
                    if (s.LightKind == 2)
                        b.WardAlly = MercyWard;
                    else
                        b.HealAlly = CrescentHeal * 9f / Math.Max(1f, s.LightWaveDmg);  // HealAllyHit scales it by Dmg / 9 (so damage upgrades heal more too)
                }
                return;
            }
            float mult = s.LightRadiant ? LightRadiant : 1f;
            foreach (var q in Players.Values)
            {
                if (q.Downed || q.Pos.DistanceTo(circleAt) > s.Circle + q.Def.BodyR)
                    continue;
                Heal(q, (q.Id == p.Id ? NoonSelfHeal : NoonHeal) * mult, p);
                if (s.LightRadiant)
                    Ward(q, MercyWard);
            }
        }

        /// Mercy's ward on a player: a shield that soaks the next MercyWard damage, for MercyTime (the larger of it and any
        /// shield already up; they don't stack). Drawn while Player.MercyT runs (Game/LightbringerLook.cs).
        void Ward(Player q, float amount)
        {
            q.ShieldHp = Math.Max(q.ShieldHp, amount);
            q.ShieldT = Math.Max(q.ShieldT, MercyTime);
            q.MercyT = MercyTime;
        }

        /// Host, every tick: wards fade, Sunlances fly. (The chain forgets its place in TickTwinBlades, which it shares.)
        void TickLightbringer(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.LanceCd = Math.Max(0f, p.LanceCd - dt);
                if (p.MercyT > 0f && ((p.MercyT -= dt) <= 0f || p.ShieldHp <= 0f || p.Downed))
                    p.MercyT = 0f;
            }
            for (int i = lanceRuns.Count - 1; i >= 0; i--)
            {
                var r = lanceRuns[i];
                r.T += dt;
                if (r.T < LightbringerMotion.LanceRelease)
                    continue;
                lanceRuns.RemoveAt(i);
                Player p, q;
                if (!Players.TryGetValue(r.Owner, out p) || p.Downed || !Players.TryGetValue(r.Target, out q))
                    continue;
                LandSunlance(p, q, r.Pips);
            }
        }

        void LandSunlance(Player p, Player q, int pips)
        {
            BroadcastSound(Sfx.LanceRelease);
            if (q.Downed)
                q.Revive = Math.Min(1f, q.Revive + LanceRevive);
            else
                Heal(q, LancePerPip * pips, p);
            V2 a = p.Pos, b = q.Pos, d = b - a;
            float len = d.Length;
            if (len < 1f)
                return;
            V2 dir = d / len, side = dir.Orthogonal();
            float dmg = (LanceBase + LanceDmgPip * pips) * p.Stats.DmgMult;
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f)
                    continue;
                V2 rel = e.Pos - a;
                float along = Dot(rel, dir), across = Math.Abs(Dot(rel, side));
                if (along < 0f || along > len || across > SunlanceHalfWidth + e.Radius)
                    continue;
                HurtEnemy(e, dmg, p.Id);
                OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, e);
            }
        }

        /// The ally a Sunlance from `p` aimed at `aimAt` goes to: the one nearest the aim point, within LanceRange of `p`
        /// (downed ones too). Null when there's none. Both games use it: yours to keep the key's cooldown, the host to throw.
        public Player SunlanceTarget(Player p, V2 aimAt)
        {
            Player best = null;
            float bd = float.MaxValue;
            foreach (var q in Players.Values)
            {
                if (q.Id == p.Id || q.Pos.DistanceTo(p.Pos) > LanceRange)
                    continue;
                float d = q.Pos.DistanceSq(aimAt);
                if (d < bd)
                {
                    bd = d;
                    best = q;
                }
            }
            return best;
        }

        /// Host: the weapon-action key with the Lightbringer, aimed at `aimAt`: the Sunlance, if it is ready.
        void HostSunlance(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.Lightbringer || p.LanceCd > 0f || p.Light < LanceNeed || p.Downed || p.StunT > 0f || p.ParryActive || State == MatchState.GameOver)
                return;
            var q = SunlanceTarget(p, aimAt);
            if (q == null)
                return;
            int pips = p.Light;
            p.Light = 0;
            p.LanceCd = LanceCooldown;
            ResetLightChain(p);
            p.FireCd = Math.Max(p.FireCd, LightbringerMotion.Lance.Dur * 0.85f);
            lanceRuns.Add(new LanceRun { Owner = p.Id, Target = q.Id, Pips = pips });
            // The thrust and the lance, drawn from its age: Radius carries the thrower's id * 1000 + the ally's, Pos2 where the ally stood.
            BroadcastFx(FxKind.Sunlance, p.Pos, q.Pos, LightGold, p.Id * 1000 + q.Id, LightbringerMotion.Lance.Dur + LightbringerMotion.Recover, Sfx.LanceGather);
        }

        /// Raising the parry (or the Sunlance) cancels the chain: back to Dawn, a step still winding up never lands.
        void ResetLightChain(Player p)
        {
            p.Combo &= ~7;
            p.ComboT = 0f;
            p.FireCd = 0f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.LightKind > 0 && s.Delay > 0f);
        }

        // --- Your own game ------------------------------------------------------------------------------------------

        int lightCountSeen = -1;

        /// Each step moves you forward a little as it plays (the host counts the steps; a change in the count is a new one).
        void LocalLightStep(Player me, float dt)
        {
            lanceLocalCd = Math.Max(0f, lanceLocalCd - dt);
            int count = me.Combo >> 3;
            if (lightCountSeen < 0 || me.Weapon.Id != WeaponId.Lightbringer)
            {
                lightCountSeen = count;
                return;
            }
            if (count == lightCountSeen)
                return;
            lightCountSeen = count;
            int step = TwinBladesMotion.StepOf(me.Combo);
            if (step < 1 || step > 3 || me.Downed || me.StunT > 0f || DashT > 0f || me.Parrying || KatanaStriking)
                return;
            var d = LightbringerMotion.Steps[step - 1];
            V2 dir = V2.FromAngle(me.Aim);
            V2 to = ClampToArena(me.Pos + dir * d.Fwd, me.Def.BodyR);
            float time = d.Dur * 0.55f;
            DashDir = dir;
            dashSpeed = d.Fwd / time;
            DashT = to.DistanceTo(me.Pos) / dashSpeed;
        }

        /// Your own game: the weapon-action key with the Lightbringer. True when it threw (or tried to) and the key is used up.
        bool LocalSunlance(Player me, V2 aimAt, bool pressed)
        {
            if (me.Weapon.Id != WeaponId.Lightbringer || !pressed || lanceLocalCd > 0f || me.Downed || me.StunT > 0f)
                return false;
            if (me.Light < LanceNeed || SunlanceTarget(me, aimAt) == null)
            {
                AddFx(FxKind.Ring, me.Pos, new Rgba(1f, 0.6f, 0.5f, 0.6f), 22f, 0.25f);  // not enough light, or no ally in reach
                Sounds.Add(Sfx.LanceFizzle);  // (your own screen only)
                return true;
            }
            lanceLocalCd = LanceCooldown;
            if (IsHost)
                HostWeaponDash(MyId, DashLance, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(DashLance);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }
    }
}
