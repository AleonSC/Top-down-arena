using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Censer (Healer Knight): the Thurible Rite (Docs/Mockups/censer-rework.html; the motion is CenserMotion.cs).
    ///   - Chain: Sweep, Return Sweep, Benediction, then it starts over. The next press counts once a step is Next of the way
    ///     through; CenserChainWindow after a step ends without one, the chain starts over. The sweeps heal allies near you and
    ///     leave a puff of incense where the head ends up; the Benediction lets the chain out for a full turn, heals further and
    ///     more for each enemy it hit, and leaves an incense cloud at your feet. The step is Player.Combo's low bits, as the
    ///     Twin Blades keep theirs (the high bits count steps).
    ///   - Embers (Player.Embers, replicated): each enemy a step hits lights one, up to EmberBase plus one per extra projectile.
    ///     They never fade.
    ///   - Alt (the weapon-action key): Censer Cast. The head is thrown out to the cursor (up to CastReach), bursts where it
    ///     lands for CastBurstPerEmber a spent Ember, and leaves an incense cloud that grows and heals more with each Ember. It
    ///     spends every Ember and the head is reeled back on its own; no swings until it is. Your game sends Msg.WeaponDash
    ///     (DashCast, aimed at the cursor); the host does the rest.
    /// Each step is one effect (FxKind.CenserStep) and each throw one (FxKind.CenserCast), drawn from their age (Game/CenserLook.cs).
    public partial class Game
    {
        public const float CenserChainWindow = 0.6f;
        public static readonly float[] CenserMult = { 1f, 1f, 18f / 13f };   // damage of each step against the weapon's Damage (13, 13, 18)
        public static readonly float[] CenserHeal = { 3f, 3f, 6f };          // what each step heals allies near you (+1 each per extra projectile)
        public static readonly float[] CenserHealR = { 140f, 140f, 170f };   // and how far
        public const float CenserArc = 200f, BenedictionReach = 30f, BenedictionBonus = 4f;  // the sweeps' arc; the Benediction reaches this much further and heals +1 per enemy hit, up to this
        public const float PuffRadius = 42f, PuffTime = 2.5f, BenedictionCloudR = 75f, BenedictionCloudT = 4f;
        public const int EmberBase = 5;
        public const float CastReach = 280f, CastCooldown = 6f, CastBurstR = 50f, CastBurstPerEmber = 8f, CastCloudBase = 60f, CastCloudPerEmber = 6f, CastCloudTime = 4f;
        public const float CastHealBase = 2f, CastHealPerEmber = 1f;  // the cloud's healing a second
        public static readonly Rgba CenserGold = new Rgba(1f, 0.81f, 0.43f);

        class CastRun
        {
            public int Owner, Embers;
            public V2 To;
            public float T;
            public bool Landed;
        }

        readonly List<CastRun> castRuns = new List<CastRun>();
        float censerLocalCd;   // your own game: until the key casts again (the host checks its own)
        public float CenserCastCd => censerLocalCd;

        /// How many Embers the Censer can hold: five, and one more for each extra projectile.
        public int EmberCap(Player p) => EmberBase + ExtraShots(p);

        /// Host: one press of the primary with the Censer (from FireKnightWeapon): the next step of the rite.
        void FireCenser(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            int last = CenserMotion.StepOf(p.Combo);
            int step = last >= 1 && last < 3 && p.ComboT > 0f ? last + 1 : 1;
            int count = ((p.Combo >> 3) + 1) & 31;
            p.Combo = (count << 3) | step;
            var d = CenserMotion.Steps[step - 1];
            p.ComboT = d.Dur + CenserChainWindow;
            p.FireCd = d.Dur * d.Next / FireRate(p);
            bool fin = step == 3;
            float reach = w.Reach + w.Grow * extra + (fin ? BenedictionReach : 0f);
            var s = NewSwing(p, aim, reach, fin ? 360f : CenserArc, w.Damage * dmg * CenserMult[step - 1]);
            s.Delay = d.Dur * d.Land;
            s.NoFx = true;  // the step's own effect draws it
            s.CenserStep = (byte)step;
            s.Heavy = fin;
            s.Knock = fin ? 20f : 12f;
            s.Sound = fin ? Sfx.SwingHeavy : Sfx.SwingLight;
            s.HealSpin = CenserHeal[step - 1] + extra;
            s.HealRange = CenserHealR[step - 1];
            pendingSwings.Add(s);
            // The whole move, drawn from its age: Radius carries who and which step.
            BroadcastFx(FxKind.CenserStep, p.Pos, p.Pos + V2.FromAngle(aim) * s.Reach, CenserGold, p.Id * 10 + step, d.Dur + CenserMotion.Recover, Sfx.None);
        }

        /// Host: a step of the rite landed on `hits` enemies (from the swing resolution, Knight.cs), before its heal goes out:
        /// Embers for each, the Benediction's bonus heal, and the step's incense.
        void CenserLanded(Player p, Swing s, int hits)
        {
            if (hits > 0)
                p.Embers = (byte)Math.Min(EmberCap(p), p.Embers + hits);
            if (s.CenserStep == 3)
            {
                s.HealSpin += Math.Min(BenedictionBonus, hits);
                AddZone(ZoneKind.Incense, p, p.Pos, BenedictionCloudR, BenedictionCloudT);
                BroadcastFx(FxKind.Ring, p.Pos, CenserGold, s.HealRange, 0.45f, Sfx.SpellHoly);
            }
            else
            {
                var d = CenserMotion.Steps[s.CenserStep - 1];
                V2 at = ClampToArena(CenserMotion.Head(p.Pos, s.Aim, CenserMotion.At(d, d.Dur), s.Reach), 10f);
                AddZone(ZoneKind.Incense, p, at, PuffRadius, PuffTime);
            }
        }

        /// Host, every tick: the rite forgets its place, cast heads fly and land.
        void TickCenser(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.CenserCd = Math.Max(0f, p.CenserCd - dt);
                if (p.Weapon.Id != WeaponId.Censer)
                    continue;
                if (CenserMotion.StepOf(p.Combo) != 0 && ((p.ComboT -= dt) <= 0f || p.Downed))
                    p.Combo &= ~7;  // back to the Sweep (the swing count stays)
            }
            for (int i = castRuns.Count - 1; i >= 0; i--)
            {
                var r = castRuns[i];
                Player p;
                r.T += dt;
                if (!Players.TryGetValue(r.Owner, out p) || r.T >= CenserMotion.CastTotal)
                {
                    castRuns.RemoveAt(i);
                    continue;
                }
                if (!r.Landed && r.T >= CenserMotion.CastLand)
                {
                    r.Landed = true;
                    CastLanded(p, r);
                }
            }
        }

        /// The cast head lands: a burst for every Ember spent, then a cloud of incense that heals.
        void CastLanded(Player p, CastRun r)
        {
            int n = Math.Max(1, r.Embers);
            float dmg = CastBurstPerEmber * n * p.Stats.DmgMult;
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f || e.Pos.DistanceTo(r.To) > e.Radius + CastBurstR)
                    continue;
                V2 dir = e.Pos - r.To;
                HurtEnemy(e, dmg, p.Id);
                OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir.IsZero ? new V2(1f, 0f) : dir.Normalized(), Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, e);
                if (!Heavy(e))
                    KnockEnemy(e, (dir.IsZero ? new V2(1f, 0f) : dir.Normalized()) * 16f);
            }
            float radius = CastCloudBase + CastCloudPerEmber * n, heal = CastHealBase + CastHealPerEmber * n;
            var z = AddZone(ZoneKind.Incense, p, r.To, radius, CastCloudTime);
            z.HealMult = heal / z.Def.HealRate;
            foreach (var q in Players.Values)
                if (!q.Downed && q.Pos.DistanceTo(r.To) <= radius)
                    Heal(q, heal, p);
            BroadcastFx(FxKind.Ring, r.To, CenserGold, CastBurstR, 0.35f, Sfx.Explode);
        }

        /// Host: the weapon-action key with the Censer, aimed at `aimAt`: Censer Cast, if it is ready and there is an Ember.
        void HostCenserCast(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.Censer || p.CenserCd > 0f || p.Embers == 0 || p.Downed || p.StunT > 0f || p.ParryActive || State == MatchState.GameOver)
                return;
            V2 to = aimAt - p.Pos;
            float len = to.Length;
            V2 dir = len > 1f ? to / len : V2.FromAngle(p.Aim);
            V2 target = ClampToArena(p.Pos + dir * Math.Min(CastReach, Math.Max(60f, len)), 10f);
            int n = p.Embers;
            p.Embers = 0;
            p.CenserCd = CastCooldown;
            p.FireCd = Math.Max(p.FireCd, CenserMotion.CastTotal);  // no swings until the head is back
            p.Combo &= ~7;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.CenserStep > 0 && s.Delay > 0f);  // a step still winding up never lands
            castRuns.Add(new CastRun { Owner = p.Id, To = target, Embers = n });
            BroadcastFx(FxKind.CenserCast, p.Pos, target, CenserGold, p.Id * 10 + Math.Min(9, n), CenserMotion.CastTotal, Sfx.SwingHeavy);
        }

        /// Raising the parry cancels the rite (ResetAttackChains): back to the Sweep, a step still winding up never lands.
        void ResetCenserChain(Player p)
        {
            p.Combo &= ~7;
            p.ComboT = 0f;
            p.FireCd = 0f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.CenserStep > 0 && s.Delay > 0f);
        }

        /// Your own game: the cast's cooldown, for the key and the ring under your feet.
        void LocalCenserTick(float dt) => censerLocalCd = Math.Max(0f, censerLocalCd - dt);
    }
}
