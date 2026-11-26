using System;
using System.Collections.Generic;

namespace Arena
{
    /// The DPS Hemomancer's Embolus Spears (the DPS weapon on the lobby's 4th key; mockup Docs/Mockups/embolus-spears.html, look A
    /// Clot Javelin). Arms out, a hardened blood spear in each hand (Player.EsSt, EsPos, EsDir: one slot a hand, 0 left, 1 right).
    ///   Fire with a spear in hand: the left one, then the right, is thrown at the cursor. Each costs EsCost of your max health
    ///     (PayHealth: never below the blood floor, what was paid is kept in EsPaid). It flies at EsSpeed through every enemy (each hit once a
    ///     flight) and sticks in the first wall, pillar or arena edge its tip reaches (or drops in the floor after EsRange).
    ///   Fire with both out: both are recalled. They tear back to your hands at EsBackSpeed, hitting every enemy again on the way,
    ///     and each one caught heals back what it cost. Holding Fire throws both and recalls once both have stuck.
    ///   Alt (Msg.WeaponDash, WeaponAlt.Embolus): every spear out bursts where it is, EsBlastR round it for EsBlastMax down to EsBlastMin
    ///     x the damage, giving back only half its cost (EsBlastHeal); new spears harden in your hands after EsRegrow.
    ///   Extra projectiles add EsExtraDmg damage each; Burst Vessels makes the blast bigger and harder. Going down or putting the
    ///   weapon away crumbles the spears out (their health is lost).
    public partial class Game
    {
        public const int EsInHand = 0, EsFlying = 1, EsStuck = 2, EsReturning = 3, EsHardening = 4;
        public const float EsCost = 0.12f, EsSpeed = 1100f, EsBackSpeed = 1300f, EsRange = 1500f, EsLen = 56f, EsHalfWidth = 7f, EsSink = 10f;
        public const float EsCatch = 22f, EsBlastR = 120f, EsBlastMax = 3f, EsBlastMin = 1.5f, EsBlastHeal = 0.5f, EsRegrow = 0.45f, EsExtraDmg = 0.2f;
        public const float EsHandFwd = 14f, EsHandSide = 11f;  // the hands, held out in front (the model's two-handed hold, CharacterModels.cs)
        public static readonly Rgba EsBlood = new Rgba(0.56f, 0.06f, 0.13f);

        /// Where hand i (0 left, 1 right) holds its spear, for someone at `pos` aiming along `aim`.
        public static V2 EsHand(V2 pos, float aim, int i) => pos + V2.FromAngle(aim) * EsHandFwd + V2.FromAngle(aim + MathX.Pi / 2f) * (i == 0 ? -EsHandSide : EsHandSide);

        /// A spear that is out of your hands and can be recalled or burst.
        public static bool EsOut(int st) => st == EsFlying || st == EsStuck || st == EsReturning;

        float EsDamage(Player p) => p.Weapon.Damage * p.Stats.DmgMult * (1f + EsExtraDmg * ExtraShots(p));

        public float EsBlastRadius(Player p) => EsBlastR * AreaMult(p) * p.Stats.EsBlastMult;

        /// Host, every tick the Embolus Spears are out and you're up: the spears fly, stick, return and harden; then the trigger.
        void TickEmbolus(Player p, float dt)
        {
            for (int i = 0; i < 2; i++)
                StepEmbolus(p, i, dt);
            bool pressed = p.Firing && !p.EsWasFiring;
            p.EsWasFiring = p.Firing;
            if (!p.Firing || p.ParryActive || p.FireCd > 0f || p.StunT > 0f || State == MatchState.GameOver)
                return;
            int hand = p.EsSt[0] == EsInHand ? 0 : p.EsSt[1] == EsInHand ? 1 : -1;
            if (hand >= 0)
            {
                ThrowEmbolus(p, hand);
                p.FireCd = p.Weapon.FireInterval / FireRate(p);
                return;
            }
            bool flying = p.EsSt[0] == EsFlying || p.EsSt[1] == EsFlying, stuck = p.EsSt[0] == EsStuck || p.EsSt[1] == EsStuck;
            if (!flying && !stuck || !pressed && flying)
                return;  // (held down, the recall waits until both have stuck)
            for (int i = 0; i < 2; i++)
                if (p.EsSt[i] == EsFlying || p.EsSt[i] == EsStuck)
                {
                    p.EsSt[i] = EsReturning;
                    p.EsHits[i].Clear();  // they hit everything again on the way back
                }
            p.FireCd = p.Weapon.FireInterval / FireRate(p);
            BroadcastFx(FxKind.Ring, EsHand(p.Pos, p.Aim, 0), EsBlood, 10f, 0.2f, Sfx.Throw);
        }

        /// Fire: spear i leaves its hand for the cursor, paid for in health.
        void ThrowEmbolus(Player p, int i)
        {
            V2 h = EsHand(p.Pos, p.Aim, i), to = p.AimAt - h;
            V2 dir = to.Length > 4f ? to.Normalized() : V2.FromAngle(p.Aim);
            float paid = PayHealth(p, p.MaxHp * EsCost, p.Weapon.Name);
            p.EsPaid[i] = paid;
            if (paid >= 1f)
                BroadcastFx(FxKind.Bleed, p.Pos, new V2(p.Id, 0f), new Rgba(0.85f, 0.12f, 0.2f), paid, 0.55f, Sfx.None);
            p.EsSt[i] = EsFlying;
            p.EsPos[i] = h + dir * (EsLen / 2f);  // (EsPos is the spear's middle)
            p.EsDir[i] = dir;
            p.EsT[i] = 0f;
            p.EsHits[i].Clear();
            p.Run.ShotsFired++;
            BroadcastFx(FxKind.Ring, h, EsBlood, 8f, 0.15f, Sfx.Throw);
        }

        /// Does a spear tip at `at` touch a wall, a pillar or the arena's edge?
        bool EsBlocked(V2 at)
        {
            V2 lo = ArenaMin, hi = ArenaMax;
            if (at.X < lo.X || at.Y < lo.Y || at.X > hi.X || at.Y > hi.Y)
                return true;
            var d = ActiveLayout;
            if (d != null)
                foreach (var s in d.Solids)
                    if (Counts(s) && Layouts.Inside(s, at, 0f))
                        return true;
            return false;
        }

        void StepEmbolus(Player p, int i, float dt)
        {
            int st = p.EsSt[i];
            if (st == EsHardening)
            {
                if ((p.EsT[i] += dt) >= EsRegrow)
                    p.EsSt[i] = EsInHand;
                return;
            }
            if (st == EsStuck)
                p.EsT[i] += dt;
            if (st != EsFlying && st != EsReturning)
                return;
            V2 from = p.EsPos[i];
            if (st == EsFlying)
            {
                V2 dir = p.EsDir[i];
                float left = EsSpeed * dt;
                while (left > 0f)
                {
                    float step = Math.Min(6f, left);
                    left -= step;
                    p.EsPos[i] += dir * step;
                    p.EsT[i] += step;  // (the distance flown)
                    if (EsBlocked(p.EsPos[i] + dir * (EsLen / 2f)) || p.EsT[i] >= EsRange)
                    {
                        if (p.EsT[i] < EsRange)
                            p.EsPos[i] += dir * EsSink;  // the tip sinks into the wall
                        p.EsSt[i] = EsStuck;
                        p.EsT[i] = 0f;
                        break;
                    }
                }
                EsPierce(p, i, from - dir * (EsLen / 2f), p.EsPos[i] + dir * (EsLen / 2f));
                return;
            }
            // Returning: butt first, toward the hand it left (wherever you are now).
            V2 hand = EsHand(p.Pos, p.Aim, i), to = hand - p.EsPos[i];
            float dist = to.Length, move = EsBackSpeed * dt;
            if (dist > 0.5f)
                p.EsDir[i] = -to / dist;
            if (dist <= EsCatch + move)
            {
                EsPierce(p, i, from, hand);
                CatchEmbolus(p, i, p.EsPaid[i]);
                return;
            }
            p.EsPos[i] += to / dist * move;
            EsPierce(p, i, from + p.EsDir[i] * (EsLen / 2f), p.EsPos[i] - p.EsDir[i] * (EsLen / 2f));
        }

        /// Every enemy the spear swept through from a to b (its tip's path and its body), each once a pass.
        void EsPierce(Player p, int i, V2 a, V2 b)
        {
            var snap = SnapFoes(p.Id);
            V2 ab = b - a;
            float len2 = Math.Max(1f, ab.LengthSq);
            foreach (var e in snap)
            {
                if (!Targetable(e) || p.EsHits[i].Contains(e.Id))
                    continue;
                float u = Math.Max(0f, Math.Min(1f, (e.Pos - a).Dot(ab) / len2));
                if (e.Pos.DistanceTo(a + ab * u) > e.Radius + EsHalfWidth)
                    continue;
                p.EsHits[i].Add(e.Id);
                WeaponHurt(e, EsDamage(p), p.Id);
                p.Run.ShotsHit++;
            }
            Free(snap);
        }

        void CatchEmbolus(Player p, int i, float heal)
        {
            p.EsSt[i] = EsInHand;
            p.EsHits[i].Clear();
            p.EsPaid[i] = 0f;
            if (heal > 0f)
                Heal(p, heal, p, p.Weapon.Name);
            BroadcastFx(FxKind.Ring, EsHand(p.Pos, p.Aim, i), new Rgba(1f, 0.7f, 0.74f), 14f, 0.25f, Sfx.None);
        }

        /// Alt: every spear out bursts where it is, giving back half its health; new ones harden in your hands.
        void HostEmbolusBlast(Player p)
        {
            if (p.Weapon.Id != WeaponId.EmbolusSpears || p.Downed || p.StunT > 0f || State == MatchState.GameOver)
                return;
            float r = EsBlastRadius(p), top = EsDamage(p) * EsBlastMax * p.Stats.EsBlastMult, low = EsDamage(p) * EsBlastMin * p.Stats.EsBlastMult;
            for (int i = 0; i < 2; i++)
            {
                if (!EsOut(p.EsSt[i]))
                    continue;
                V2 at = p.EsPos[i];
                var snap = SnapFoes(p.Id);
                foreach (var e in snap)
                {
                    if (!Targetable(e))
                        continue;
                    float d = Math.Max(0f, e.Pos.DistanceTo(at) - e.Radius);
                    if (d > r)
                        continue;
                    WeaponHurt(e, top + (low - top) * (d / r), p.Id);
                    p.Run.ShotsHit++;
                }
                Free(snap);
                BroadcastFx(FxKind.EmbolusBlast, at, new V2(p.Id, i), EsBlood, r, 0.7f, Sfx.Explode);
                float back = p.EsPaid[i] * EsBlastHeal;
                if (back > 0f)
                    Heal(p, back, p, p.Weapon.Name);
                p.EsPaid[i] = 0f;
                p.EsHits[i].Clear();
                p.EsSt[i] = EsHardening;
                p.EsT[i] = 0f;
            }
        }

        /// Off when you go down or put it away: the spears out crumble (their health is lost) and both hands are full again.
        void ClearEmbolus(Player p)
        {
            for (int i = 0; i < 2; i++)
            {
                p.EsSt[i] = EsInHand;
                p.EsPaid[i] = 0f;
                p.EsHits[i].Clear();
            }
        }

        /// Your own game: Alt was pressed with the Embolus Spears (the host checks the rest).
        bool LocalEmbolusBlast(Player me, bool pressed, V2 aimAt)
        {
            if (me.Weapon.Id != WeaponId.EmbolusSpears || !pressed)
                return false;
            if (EsOut(me.EsSt[0]) || EsOut(me.EsSt[1]))
            {
                if (IsHost)
                    HostWeaponDash(MyId, WeaponAlt.Embolus, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(WeaponAlt.Embolus);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }

        // --- Network: the player's Embolus Spears fields (SyncPlayer) ---

        void SyncEmbolus(NetSync s, Player p)
        {
            for (int k = 0; k < 2; k++)  // the Embolus Spears
            {
                s.Byte(ref p.EsSt[k]);
                s.Vec(ref p.EsPos[k]);
                s.Vec(ref p.EsDir[k]);
            }
        }

        /// WeaponDef.BotAlt (BotWeaponAlt.cs): burst the spears where they sit among enemies
        static bool EmbolusSpearsBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            if (late || !(EsOut(me.EsSt[0]) || EsOut(me.EsSt[1])))
                return false;
            foreach (var en in g.Enemies.Values)
                if (!g.IsSpawning(en) && en.Hp > 0f && g.EnemiesNear(en.Pos, 90f) >= 2 && EsNear(me, en.Pos, 90f))
                    return g.Tap(true, 1f);
            return false;
        }
    }
}
