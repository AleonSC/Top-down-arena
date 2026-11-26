using System;
using System.Collections.Generic;

namespace Arena
{
    /// The DPS Hemomancer's Blood Brawler (was the Bone Spear), the Marionette (Docs/BloodBrawlerMarionette.md; mockup Docs/Mockups/bone-spear-rework.html):
    /// the Hemomancer works an enemy's blood from range, landing phantom melee blows from inside its body.
    ///   - Fire needs the cursor on an enemy within the weapon's Reach (the Implosion's targeting: ImplodeTarget, ImplodeReady).
    ///     That enemy is marked (`Player.BsTarget`, replicated, with the thread's time left `BsT`) and takes the next step of a
    ///     four-step string: Pulse, Reel, Heart Lift, Marrow Burst. A press on the same enemy within BsWindow of the last step
    ///     ending moves the string on; another enemy, or a wait, starts over at Pulse. The step shares Player.Combo with the
    ///     Twin Blades and the Blood Lash (low 3 bits: the step, 1-4; a count of steps above).
    ///   - Each move costs BsMoveCost health (Marrow Burst BsBurstCost) and lands after a short wind-up (`bsHits`):
    ///     Pulse shoves BsPulseShove px away, Reel pulls BsReelPull px in, Heart Lift throws it in the air for BsLiftAir s
    ///     (stunned; `Enemy.BsLiftT`), and every Blood Brawler hit on an airborne enemy deals BsAirMult. Marrow Burst hits for
    ///     BsBurstBase + BsBurstPer a hit landed earlier in the string, and bursts into BsShards bone shards (Defs.BloodBrawlerShot,
    ///     Marrow: more) that cut out to BsShardReach. Each extra projectile lands every move on one more enemy near the mark, at SympMult. Heavies and bosses take the hits but aren't moved.
    ///   - Alt (Msg.WeaponDash, WeaponAlt.Sympathy), Sympathy: binds up to SympMax enemies within SympRange of the marked one
    ///     (`Player.SympBound`, `SympSrc`, `SympT`, replicated) for SympTime s; every move landed on it meanwhile also lands on
    ///     each of them at SympMult. SympCooldown s before the next.
    public partial class Game
    {
        public const float BsWindow = 0.9f, BsThread = 1.4f;
        public const float BsPulseTime = 0.26f, BsReelTime = 0.35f, BsLiftTime = 0.45f, BsBurstTime = 0.65f;
        public const float BsPulseHit = 0.06f, BsReelHit = 0.05f, BsLiftHit = 0.1f, BsBurstHit = 0.18f;
        public const float BsReelMult = 15f / 18f, BsLiftMult = 1.5f, BsBurstBase = 2f, BsBurstPer = 12f / 18f, BsShardMult = 24f / 18f;  // of the weapon's Damage (the Pulse's 18)
        public const float BsPulseShove = 24f, BsReelPull = 110f, BsLiftAir = 0.7f, BsAirMult = 1.25f;
        public const int BsShards = 8;
        public const float BsShardSpeed = 600f, BsShardReach = 120f;
        public const float BsMoveCost = 1f, BsBurstCost = 3f;
        public const float SympRange = 170f, SympTime = 4f, SympCooldown = 8f, SympMult = 0.5f;
        public const int SympMax = 4;
        static readonly Rgba BsRed = new Rgba(0.9f, 0.16f, 0.24f);

        class BsHit
        {
            public int Owner, Target, Step;
            public float T, Dmg;
            public bool Echo;
        }

        readonly List<BsHit> bsHits = new List<BsHit>();

        /// The Blood Brawler step in `combo` (1 Pulse, 2 Reel, 3 Heart Lift, 4 Marrow Burst; 0 none yet).
        public static int BsStepOf(int combo) => combo & 7;

        /// How long step `step` takes before the next press can land.
        public static float BsStepTime(int step) => step == 1 ? BsPulseTime : step == 2 ? BsReelTime : step == 3 ? BsLiftTime : BsBurstTime;

        public static float BsHitDelay(int step) => step == 1 ? BsPulseHit : step == 2 ? BsReelHit : step == 3 ? BsLiftHit : BsBurstHit;

        /// How long a move's FxKind.BsBlow plays: its wind-up, then the blow (Heart Lift: the whole time in the air).
        public static float BsFxDur(int step) => BsHitDelay(step) + (step == 1 ? 0.3f : step == 2 ? 0.35f : step == 3 ? BsLiftAir : 0.5f);

        /// Host: one press of fire with the Blood Brawler (from FireBloodWeapon): the next step of the string on the enemy under the cursor.
        void FireBloodBrawler(Player p, float dmg, int extra)
        {
            var e = ImplodeTarget(p, p.AimAt);
            if (e == null)
                return;
            int last = BsStepOf(p.Combo);
            int step = e.Id == p.BsTarget && last >= 1 && last < 4 && p.ComboT > 0f ? last + 1 : 1;
            if (step == 1)
                p.BsHits = 0;
            int count = ((p.Combo >> 3) + 1) & 31;
            p.Combo = (count << 3) | step;
            float dur = BsStepTime(step);
            p.ComboT = dur + BsWindow;
            p.FireCd = dur / FireRate(p);
            p.BsTarget = e.Id;
            p.BsT = BsThread;
            if (!casting)
                PayHealth(p, step == 4 ? BsBurstCost : BsMoveCost, p.Weapon.Name);
            float d = p.Weapon.Damage * dmg;
            QueueBsMove(p, e, step, d, false);
            BroadcastFx(FxKind.BsBlow, e.Pos, new V2(e.Id, p.Id), BsRed, step, BsFxDur(step), step == 4 ? Sfx.SpellArcane : Sfx.SwingLight);
            if (p.SympT > 0f && p.SympSrc == e.Id)
                foreach (int id in p.SympBound)
                {
                    Enemy o;
                    if (TryFoe(id, out o) && Targetable(o))
                    {
                        QueueBsMove(p, o, step, d * SympMult, true);
                        BroadcastFx(FxKind.BsBlow, o.Pos, new V2(o.Id, p.Id), BsRed, step + 10, BsFxDur(step), Sfx.None);
                    }
                }
            if (extra > 0)  // each extra projectile: the move lands on one more enemy near the mark too, at SympMult (as Sympathy, but the nearest each time)
            {
                var near = new List<Enemy>();
                foreach (var o in AllFoes(by: p.Id))
                    if (o != e && Targetable(o) && o.Pos.CloserThan(e.Pos, SympRange + o.Radius) && !(p.SympT > 0f && p.SympSrc == e.Id && p.SympBound.Contains(o.Id)))
                        near.Add(o);
                near.Sort((a, b) => a.Pos.DistanceSq(e.Pos).CompareTo(b.Pos.DistanceSq(e.Pos)));
                for (int i = 0; i < near.Count && i < extra; i++)
                {
                    QueueBsMove(p, near[i], step, d * SympMult, true);
                    BroadcastFx(FxKind.BsBlow, near[i].Pos, new V2(near[i].Id, p.Id), BsRed, step + 10, BsFxDur(step), Sfx.None);
                }
            }
        }

        void QueueBsMove(Player p, Enemy e, int step, float dmg, bool echo) =>
            bsHits.Add(new BsHit { Owner = p.Id, Target = e.Id, Step = step, T = -BsHitDelay(step), Dmg = dmg, Echo = echo });

        /// A Blood Brawler hit: the damage (more on an airborne enemy), credited to the weapon, through the usual on-hit effects.
        void BsHurt(Player p, Enemy e, float dmg, V2 dir)
        {
            if (e.BsLiftT > 0f)
                dmg *= BsAirMult;
            string was = CreditTo("weapon");
            HurtEnemy(e, dmg, p.Id);
            ModHit(e, p.Id, dmg, Defs.BloodBrawlerShot, dir);
            dmgSrc = was;
            p.Run.ShotsFired++;
            p.Run.ShotsHit++;
        }

        /// A move's blow landing on `e` (the marked enemy, or one bound to it by Sympathy).
        void LandBsMove(Player p, Enemy e, BsHit h)
        {
            V2 away = AwayFrom(e.Pos, p.Pos, V2.FromAngle(p.Aim));
            bool moves = !Heavy(e);
            switch (h.Step)
            {
                case 1:  // Pulse: a phantom jab from your side
                    BsHurt(p, e, h.Dmg, away);
                    if (moves)
                        KnockEnemy(e, away * BsPulseShove);
                    break;
                case 2:  // Reel: dragged toward you by its own veins (never into you)
                {
                    BsHurt(p, e, h.Dmg * BsReelMult, away * -1f);
                    float room = e.Pos.DistanceTo(p.Pos) - e.Radius - p.Def.BodyR - 20f;
                    if (moves && room > 0f)
                        KnockEnemy(e, away * -Math.Min(BsReelPull, room));
                    break;
                }
                case 3:  // Heart Lift: thrown in the air, stunned while it's up
                    BsHurt(p, e, h.Dmg * BsLiftMult, away);
                    if (moves)
                    {
                        e.BsLiftT = BsLiftAir;
                        Stun(e, BsLiftAir);
                    }
                    break;
                default:  // Marrow Burst: it bursts into bone shards
                {
                    int before = h.Echo ? 0 : p.BsHits;
                    BsHurt(p, e, h.Dmg * (BsBurstBase + BsBurstPer * before), away);
                    int n = BsShards + p.Stats.BsShardBonus;  // (Marrow: more)
                    float off = Rand(0f, MathX.Tau / n);
                    for (int i = 0; i < n; i++)
                    {
                        V2 dir = V2.FromAngle(off + MathX.Tau * i / n);
                        SpawnBullet(Defs.BloodBrawlerShot, e.Pos + dir * (e.Radius + 6f), dir * BsShardSpeed, 0f, BsShardReach / BsShardSpeed, h.Dmg * BsShardMult, p.Id);
                    }
                    if (!h.Echo)
                        BroadcastSound(Sfx.Explode);  // (the burst itself is drawn from the move's FxKind.BsBlow)
                    break;
                }
            }
            if (!h.Echo)
                p.BsHits++;
        }

        void ClearBloodBrawler(Player p)
        {
            p.BsTarget = 0;
            p.BsT = 0f;
            p.BsHits = 0;
            EndSympathy(p);
        }

        void EndSympathy(Player p)
        {
            p.SympT = 0f;
            p.SympSrc = 0;
            p.SympBound.Clear();
        }

        /// Host: Alt with the Blood Brawler: Sympathy binds the enemies near the marked one to it.
        void HostSympathy(Player p)
        {
            Enemy t;
            if (p.Weapon.Id != WeaponId.BloodBrawler || p.Downed || p.SympCd > 0f || State == MatchState.GameOver
                || p.BsTarget == 0 || !TryFoe(p.BsTarget, out t) || !Targetable(t))
                return;
            int max = SympMax + p.Stats.SympBonus;  // (Marrow: one more)
            var near = new List<Enemy>();
            foreach (var o in AllFoes(by: p.Id))
                if (o != t && Targetable(o) && o.Pos.CloserThan(t.Pos, SympRange + o.Radius))
                    near.Add(o);
            if (near.Count == 0)
                return;
            near.Sort((a, b) => a.Pos.DistanceSq(t.Pos).CompareTo(b.Pos.DistanceSq(t.Pos)));
            p.SympBound.Clear();
            for (int i = 0; i < near.Count && i < max; i++)
            {
                p.SympBound.Add(near[i].Id);
                BroadcastFx(FxKind.Drain, t.Pos, near[i].Pos, BsRed, 2f, 0.3f, Sfx.None);
            }
            p.SympSrc = t.Id;
            p.SympT = SympTime;
            p.SympCd = SympCooldown;
            p.BsT = Math.Max(p.BsT, SympTime);  // the mark holds while they're bound
            BroadcastFx(FxKind.Ring, t.Pos, BsRed, SympRange, 0.4f, Sfx.SpellArcane);
        }

        /// Your own game: Alt was pressed with the Blood Brawler (the host checks the rest).
        bool LocalSympathy(Player me, bool pressed, V2 aimAt)
        {
            if (me.Weapon.Id != WeaponId.BloodBrawler)
                return false;
            if (!pressed)
                return true;
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.Sympathy, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.Sympathy);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        /// Host, every tick: the string's window and the mark running out, Sympathy fading, airborne enemies coming down,
        /// and the moves' blows landing.
        void TickBloodBrawler(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.SympCd = Math.Max(0f, p.SympCd - dt);
                if (p.Weapon.Id != WeaponId.BloodBrawler || p.Downed)
                {
                    if (p.BsTarget != 0 || p.SympT > 0f)
                        ClearBloodBrawler(p);
                    continue;
                }
                if (BsStepOf(p.Combo) != 0 && (p.ComboT -= dt) <= 0f)
                    p.Combo &= ~7;  // back to the Pulse (the step count stays)
                Enemy t;
                if (p.BsTarget != 0 && ((p.BsT -= dt) <= 0f || !TryFoe(p.BsTarget, out t) || t.Hp <= 0f))
                {
                    p.BsTarget = 0;
                    p.BsT = 0f;
                    p.Combo &= ~7;
                }
                if (p.SympT > 0f)
                {
                    p.SympBound.RemoveAll(id => { Enemy o; return !TryFoe(id, out o) || o.Hp <= 0f; });
                    if ((p.SympT -= dt) <= 0f || p.SympBound.Count == 0 || !TryFoe(p.SympSrc, out t) || t.Hp <= 0f)
                        EndSympathy(p);
                }
            }
            foreach (var e in Enemies.Values)
                if (e.BsLiftT > 0f)
                    e.BsLiftT -= dt;
            for (int i = bsHits.Count - 1; i >= 0; i--)
            {
                var h = bsHits[i];
                if ((h.T += dt) < 0f)
                    continue;
                bsHits.RemoveAt(i);
                Player p;
                Enemy e;
                if (Players.TryGetValue(h.Owner, out p) && !p.Downed && TryFoe(h.Target, out e) && Targetable(e))
                    LandBsMove(p, e, h);
            }
        }

        // --- Network: the player's Blood Brawler fields (SyncPlayer) ---

        void SyncBloodBrawler(NetSync s, Player p)
        {
            s.Int(ref p.BsTarget);  // the Blood Brawler's mark, thread and Sympathy
            s.Float(ref p.BsT);
            s.Float(ref p.SympT);
            s.Int(ref p.SympSrc);
            int bound = Math.Min(255, p.SympBound.Count);
            s.Byte(ref bound);
            if (s.Reading)
                p.SympBound.Clear();
            for (int k = 0; k < bound; k++)
            {
                int boundId = s.Reading ? 0 : p.SympBound[k];
                s.Int(ref boundId);
                if (s.Reading)
                    p.SympBound.Add(boundId);
            }
        }

        /// WeaponDef.BotAlt (BotWeaponAlt.cs): Sympathy when the mark has company
        static bool BloodBrawlerBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            if (late || me.BsTarget == 0 || me.SympT > 0f || !g.Enemies.TryGetValue(me.BsTarget, out var mark))
                return false;
            return g.Tap(g.EnemiesNear(mark.Pos, 170f) >= 2, 8.5f);
        }
    }
}
