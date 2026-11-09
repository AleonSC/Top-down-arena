using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Katana's two stances (Docs/KatanaCombo.md; mockup Docs/Mockups/katana-stances.html). The weapon-action key switches
    /// between them: the blade turns from one guard to the other and a small burst pushes nearby enemies back (KatanaSwitchCd
    /// between switches). The switch does nothing else.
    ///
    /// Hayate (0): every attack is a dash-cut along your aim (KatanaDashLen, untouchable), cutting everything on the line. A
    /// dash that kills comes back at once and adds a level of Flow (longer, harder dashes; it fades while you're in Hayate
    /// without a kill). Every enemy a dash hits fills the Kage Chain's meter; full, holding fire hops you through the nearest
    /// enemies, cutting each.
    ///
    /// Senbon (1): swipes that mark every enemy they don't kill (up to KatanaMarkMax). Every enemy a swipe hits fills Zanshin's
    /// meter; full, holding fire cuts through every marked enemy, brings you back to where you stood, and as you sheathe each
    /// takes a cut per mark (and enemy shots near you are cut). Both meters are kept when you switch.
    ///
    /// As with other weapon moves, your own game moves you (the dash when KatanaDashes changes, the hops of a
    /// Kage Chain or Zanshin: LocalKatana) and tells the host (Msg.WeaponDash); the host checks the meter, picks its own
    /// targets (the same rule, from where it sees you) and lands the cuts on time (TickKatanaRuns).
    public partial class Game
    {
        public static readonly Rgba KatanaRose = new Rgba(1f, 0.56f, 0.66f);   // Senbon, Zanshin
        public static readonly Rgba KatanaSteel = new Rgba(0.745f, 0.87f, 1f);  // Hayate, the Kage Chain

        class KatanaRun
        {
            public int Owner;
            public bool Zan;               // Zanshin (else the Kage Chain)
            public List<int> Targets = new List<int>();
            public List<V2> Hops = new List<V2>();  // where each hop ends (Zanshin: then back to Start)
            public V2 Start;
            public float T, Total;
            public int Done;               // hops landed
            public bool Sheathed;          // Zanshin: the sheathe's sound raised
        }

        readonly List<KatanaRun> katanaRuns = new List<KatanaRun>();
        static readonly Sfx[] KatanaHopSfx = { Sfx.KatanaChain1, Sfx.KatanaChain2, Sfx.KatanaChain3, Sfx.KatanaChain4, Sfx.KatanaChain5 };

        // Local (your own screen): the Kage Chain or Zanshin you are in the middle of, the hold that spends a meter, the switch's cooldown.
        class LocalRun
        {
            public bool Zan;
            public List<V2> Hops;
            public int Next;
            public float T, Total;
        }

        LocalRun katanaLocal;
        float katanaHoldT, katanaSwitchLocalCd;
        byte katanaDashesSeen;
        bool katanaFireHeld;         // fire held this tick (set by LocalControl)
        bool katanaFullSeen;         // the stance's meter was full last frame (a fill plays KatanaReady)
        public bool KatanaStriking => katanaLocal != null;  // a Kage Chain or Zanshin under way on your own screen: you can't move or swing
        public float KatanaHoldProgress => katanaHoldT / KatanaHoldFire;  // how far the hold that spends the meter has got (the look)
        public bool KatanaZanshinLocal => katanaLocal != null && katanaLocal.Zan;  // your own Zanshin: the look dims the arena and letterboxes the screen
        public float KatanaRunT => katanaLocal != null ? katanaLocal.T : 0f;
        public float KatanaRunTotal => katanaLocal != null ? katanaLocal.Total : 0f;

        public static bool KatanaMeterFull(Player p) => p.KatanaStance == 0 ? p.KatanaKage >= KatanaKageNeed : p.KatanaZan >= KatanaZanNeed;

        /// How far Hayate's dash-cut goes: longer with Flow and Piercing Rounds.
        public static float KatanaDashReach(Player p) => MeleeReach(p, KatanaDashLen) * (1f + KatanaFlowLen * p.KatanaFlow);

        bool KatanaBusy(Player p)
        {
            foreach (var r in katanaRuns)
                if (r.Owner == p.Id)
                    return true;
            return false;
        }

        // --- The attacks (host) -----------------------------------------------------------------------------------------

        /// Hayate's dash-cut or Senbon's swipe. Each extra projectile strikes once more at KatanaExtraMult.
        void FireKatana(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            if (p.KatanaStance == 0)
            {
                // A line from where you stand to where the dash ends; your own game moves you along it (LocalKatana).
                V2 from = p.Pos, to = ClampToArena(from + V2.FromAngle(aim) * KatanaDashReach(p), p.Def.BodyR);
                float len = Math.Max(20f, to.DistanceTo(from));
                p.FireCd = KatanaDashCd / FireRate(p);
                p.InvulnT = Math.Max(p.InvulnT, KatanaDashTime + 0.05f);
                p.KatanaDashes++;
                float d = w.Damage * (KatanaDashMult + KatanaFlowDmg * p.KatanaFlow) * dmg;
                for (int k = 0; k <= extra; k++)
                {
                    var s = NewSwing(p, aim, len, 20f, k == 0 ? d : d * KatanaExtraMult);
                    s.Reach = len;
                    s.HalfWidth = KatanaDashWidth;
                    s.FromSet = true;
                    s.From = from;
                    s.DrawCut = true;
                    s.Heavy = true;
                    s.KatanaMove = 1;
                    s.Delay = KatanaDashTime * 0.5f + 0.05f * k;
                    if (k == 0)
                        s.Sound = Sfx.KatanaLunge;
                    else
                    {
                        s.NoFx = true;  // the extra strike rides the same line
                        s.Silent = true;
                    }
                    pendingSwings.Add(s);
                }
                return;
            }
            int step = p.KatanaStep;
            p.KatanaStep = (byte)(step ^ 1);  // the look alternates the swipes
            p.FireCd = KatanaSwipeTime / FireRate(p);
            for (int k = 0; k <= extra; k++)
            {
                var s = NewSwing(p, aim + (k == 0 ? 0f : (k % 2 == 1 ? 0.2f : -0.2f)), w.Reach, KatanaSwipeArc, w.Damage * KatanaSwipeMult * dmg * (k == 0 ? 1f : KatanaExtraMult));
                s.KatanaMove = 2;
                if (k == 0)
                    s.Sound = step == 0 ? Sfx.KatanaSwipe1 : Sfx.KatanaSwipe2;
                else
                {
                    s.Delay = 0.05f * k;
                    s.Silent = true;
                }
                pendingSwings.Add(s);
            }
        }

        /// A dash-cut or swipe landed (LandSwing): its hits fill the stance's meter; a dash that killed comes back at once and adds
        /// Flow; a swipe marks what it didn't kill.
        void KatanaLanded(Player p, Swing s, List<Enemy> hits)
        {
            if (hits.Count == 0)
                return;
            if (s.KatanaMove == 1)
            {
                p.KatanaKage = (byte)Math.Min(KatanaKageNeed, p.KatanaKage + hits.Count);
                bool killed = false;
                foreach (var e in hits)
                    killed |= e.Hp <= 0f;
                if (killed)
                {
                    p.FireCd = 0f;  // a killing dash comes back at once
                    p.TapQueued = false;
                    if (!s.NoFx)  // (the extra strike doesn't add Flow again)
                    {
                        p.KatanaFlow = (byte)Math.Min(KatanaFlowMax, p.KatanaFlow + 1);
                        p.KatanaFlowT = 0f;
                    }
                }
            }
            else
            {
                p.KatanaZan = (byte)Math.Min(KatanaZanNeed, p.KatanaZan + hits.Count);
                foreach (var e in hits)
                    if (e.Hp > 0f)
                        e.KatanaMarks = (byte)Math.Min(KatanaMarkMax, e.KatanaMarks + 1);
            }
            if (!s.Silent)
                KatanaHitCue(p, s.KatanaMove == 1, hits.Count);
        }

        /// Host: a cut landed. Sent as an effect that draws nothing: it carries the cut's sound, and the striker's own game holds
        /// the picture for a moment (Game/Juice.cs; harder for a dash).
        void KatanaHitCue(Player p, bool dash, int hits)
        {
            BroadcastFx(FxKind.KatanaHitCue, p.Pos, new V2(p.Id, 0f), KatanaRose.WithAlpha(dash ? 1f : 0.5f), hits, 0.2f, Sfx.KatanaHit);
        }

        /// Every tick, for every player: Flow fading in Hayate, the switch's cooldown.
        void TickKatana(Player p, float dt)
        {
            p.KatanaSwitchCd = Math.Max(0f, p.KatanaSwitchCd - dt);
            if (p.Downed)
            {
                p.KatanaFlow = 0;
                return;
            }
            if (p.KatanaStance != 0 || p.KatanaFlow == 0)
                return;  // (Flow only fades while you're in Hayate)
            p.KatanaFlowT += dt;
            if (p.KatanaFlowT >= KatanaFlowKeep)
            {
                p.KatanaFlow--;
                p.KatanaFlowT = KatanaFlowKeep - KatanaFlowFall;
            }
        }

        /// The Katana and Bo Staff fire on their own timing: a press during a step is remembered and starts the next step the
        /// moment this one ends. With the Katana's stance meter full only a press attacks (holding fire spends the meter: your
        /// own game, LocalKatana), and nothing does during a Kage Chain or Zanshin.
        void TickKatanaFire(Player p)
        {
            bool pressed = p.Firing && !p.WasFiring;
            p.WasFiring = p.Firing;
            if (pressed)
                p.TapQueued = true;
            bool katana = p.Weapon.Id == WeaponId.Katana;
            if (p.ParryActive || State == MatchState.GameOver || katana && KatanaBusy(p))
            {
                p.TapQueued = false;
                return;
            }
            bool held = p.Firing && !(katana && KatanaMeterFull(p));
            if ((held || p.TapQueued) && p.FireCd <= 0f)
            {
                p.TapQueued = false;
                p.FireCd = p.Weapon.FireInterval / FireRate(p);  // (the step sets its own, in FireNinjaWeapon)
                FirePlayer(p);
            }
        }

        /// Raising the parry is an animation cancel for weapons that attack in a fixed order: the chain goes back to its first
        /// step, the wait for the next step is cleared, and an attack that hasn't landed yet never does. Builds you have earned
        /// (the Katana's meters and Flow, the Rapier's streak) are left alone. Called on the host.
        void ResetAttackChains(Player p)
        {
            if (p.Weapon.Id == WeaponId.BoStaff)
            {
                p.BoStep = 0;  // back to the slash
                p.FireCd = 0f;
                p.TapQueued = false;
                if (boShoves.RemoveAll(sh => sh.Owner == p.Id) > 0)  // a shove still drawing back never lands (and costs no cooldown)
                    p.ShoveCd = 0f;
                return;
            }
            if (p.Weapon.Id == WeaponId.Kusarigama)
            {
                p.KusariStep = 0;  // back to the sickle cut (a throw already away still lands)
                p.FireCd = 0f;
                return;
            }
            if (p.Weapon.Id == WeaponId.Greatsword)
            {
                ResetGreatChain(p);  // Greatsword.cs
                return;
            }
            if (p.Weapon.Id == WeaponId.TwinBlades)
            {
                ResetTwinChain(p);  // TwinBlades.cs
                return;
            }
            if (p.Weapon.Id == WeaponId.Lightbringer)
            {
                ResetLightChain(p);  // Lightbringer.cs
                return;
            }
            if (p.Weapon.Id == WeaponId.Censer)
            {
                ResetCenserChain(p);  // Censer.cs
                return;
            }
            if (p.Weapon.Id == WeaponId.Lance)
            {
                ResetLanceChain(p);  // Lance.cs
                return;
            }
            if (p.Weapon.Id != WeaponId.Katana)
                return;
            p.KatanaStep = 0;  // the next swipe is the first again, and the wait for it is gone (a dash already thrown still lands)
            p.FireCd = 0f;
            p.TapQueued = false;
        }

        // --- The Kage Chain and Zanshin: who they cut, and where they take you ----------------------------------------------

        /// The enemies a Kage Chain (the `max` nearest, each from the last) or Zanshin (every marked enemy, in the same order)
        /// cuts, from `from`.
        List<Enemy> KatanaTargets(V2 from, bool zan, int max)
        {
            var pool = new List<Enemy>();
            foreach (var e in Enemies.Values)
                if (!IsSpawning(e) && e.Hp > 0f && (!zan || e.KatanaMarks > 0))
                    pool.Add(e);
            var list = new List<Enemy>();
            V2 at = from;
            while (pool.Count > 0 && list.Count < max)
            {
                int best = 0;
                for (int i = 1; i < pool.Count; i++)
                    if (pool[i].Pos.DistanceSq(at) < pool[best].Pos.DistanceSq(at))
                        best = i;
                var e = pool[best];
                pool.RemoveAt(best);
                list.Add(e);
                at = e.Pos;
            }
            return list;
        }

        /// Where each hop ends: just past each enemy, on the far side from where the hop began.
        List<V2> KatanaHopPoints(V2 from, List<Enemy> targets, float bodyR)
        {
            var hops = new List<V2>();
            V2 at = from;
            foreach (var e in targets)
            {
                V2 d = e.Pos - at;
                V2 dir = d.IsZero ? new V2(1f, 0f) : d.Normalized();
                at = ClampToArena(e.Pos + dir * (e.Radius + KatanaHopPast), bodyR);
                hops.Add(at);
            }
            return hops;
        }

        int KatanaKageMax(Player p) => KatanaKageHops + ExtraShots(p);

        // --- Your own screen ---------------------------------------------------------------------------------------------

        /// Your own game, every tick: your dashes, the switch on the weapon-action key, the hold that spends a full meter, and
        /// the hops of a Kage Chain or Zanshin. Returns true while the Katana is out (nothing else uses the key then).
        bool LocalKatana(Player me, V2 aimAt, bool pressed, bool fireHeld, float dt)
        {
            katanaSwitchLocalCd = Math.Max(0f, katanaSwitchLocalCd - dt);
            bool katana = me.Weapon.Id == WeaponId.Katana;
            KatanaLocalSounds(me, katana);
            if (!katana)
                katanaDashesSeen = me.KatanaDashes;  // (picking the Katana up never replays an old count)
            // A dash thrown (the host counts them): you dash along it, on your own screen like every move.
            if (me.KatanaDashes != katanaDashesSeen)
            {
                katanaDashesSeen = me.KatanaDashes;
                if (katana && !me.Downed && me.StunT <= 0f && katanaLocal == null)
                {
                    V2 dir = V2.FromAngle(me.Aim);
                    V2 to = ClampToArena(me.Pos + dir * KatanaDashReach(me), me.Def.BodyR);
                    dashSpeed = KatanaDashReach(me) / KatanaDashTime;
                    DashDir = dir;
                    DashT = to.DistanceTo(me.Pos) / dashSpeed;
                }
            }
            if (me.Downed || me.StunT > 0f)
            {
                katanaHoldT = 0f;
                if (katanaLocal != null)
                {
                    katanaLocal = null;
                    DashT = 0f;
                }
                return katana;
            }
            if (katanaLocal != null)
            {
                TickLocalKatanaRun(me, dt);
                return true;
            }
            if (!katana)
            {
                katanaHoldT = 0f;
                return false;
            }
            if (fireHeld && KatanaMeterFull(me) && !me.Parrying)
            {
                katanaHoldT += dt;
                if (katanaHoldT >= KatanaHoldFire && DashT <= 0f)
                {
                    katanaHoldT = 0f;
                    StartLocalKatanaRun(me);
                    return true;
                }
            }
            else
                katanaHoldT = 0f;
            if (pressed && katanaSwitchLocalCd <= 0f && DashT <= 0f)
            {
                katanaSwitchLocalCd = KatanaSwitchCd;
                SendKatanaDash(DashKatanaStance, aimAt);
            }
            return true;
        }

        void SendKatanaDash(byte kind, V2 at)
        {
            if (IsHost)
                HostWeaponDash(MyId, kind, at);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(kind);
                w.Write(at);
                session.SendToHost(Proto.End(w));
            }
        }

        void StartLocalKatanaRun(Player me)
        {
            bool zan = me.KatanaStance == 1;
            var targets = KatanaTargets(me.Pos, zan, zan ? int.MaxValue : KatanaKageMax(me));
            if (targets.Count == 0)
                return;  // nothing to cut: the meter waits
            var hops = KatanaHopPoints(me.Pos, targets, me.Def.BodyR);
            if (zan)
                hops.Add(me.Pos);  // and back to where you stood
            katanaLocal = new LocalRun { Zan = zan, Hops = hops, Total = zan ? KatanaZanTotal(targets.Count) : targets.Count * KatanaKageHop };
            DashT = 0f;
            SendKatanaDash(zan ? DashKatanaZan : DashKatanaKage, me.Pos);
        }

        /// One hop at a time, each a dash to its point; Zanshin then holds you still through the sheathe.
        void TickLocalKatanaRun(Player me, float dt)
        {
            var r = katanaLocal;
            r.T += dt;
            if (DashT > 0f)
                return;
            if (r.Next < r.Hops.Count)
            {
                bool back = r.Zan && r.Next == r.Hops.Count - 1;
                float hop = r.Zan ? (back ? KatanaZanBack : KatanaZanHop) : KatanaKageHop;
                V2 d = r.Hops[r.Next] - me.Pos;
                float len = d.Length;
                DashDir = len > 1f ? d / len : V2.Zero;
                DashT = hop;
                dashSpeed = len / hop;
                r.Next++;
                return;
            }
            if (!r.Zan || r.T >= r.Total)
                katanaLocal = null;
        }

        /// Your own screen only, never sent: the stance's meter filling.
        void KatanaLocalSounds(Player me, bool katana)
        {
            bool full = katana && !me.Downed && KatanaMeterFull(me);
            if (full && !katanaFullSeen)
                Sounds.Add(me.KatanaStance == 0 ? Sfx.KatanaReady : Sfx.KatanaEmpReady);
            katanaFullSeen = full;
        }

        // --- The host: the switch, the Kage Chain and Zanshin -------------------------------------------------------------

        /// A Katana move asked for on the weapon-action key or by holding fire (Msg.WeaponDash).
        void HostKatanaDash(Player p, int kind)
        {
            if (p.Weapon.Id != WeaponId.Katana || p.Downed || p.StunT > 0f || KatanaBusy(p))
                return;
            if (kind == DashKatanaStance)
            {
                HostKatanaSwitch(p);
                return;
            }
            bool zan = kind == DashKatanaZan;
            if (zan != (p.KatanaStance == 1) || !KatanaMeterFull(p))
                return;
            var targets = KatanaTargets(p.Pos, zan, zan ? int.MaxValue : KatanaKageMax(p));
            if (targets.Count == 0)
                return;
            var run = new KatanaRun { Owner = p.Id, Zan = zan, Start = p.Pos, Hops = KatanaHopPoints(p.Pos, targets, p.Def.BodyR) };
            foreach (var e in targets)
                run.Targets.Add(e.Id);
            run.Total = zan ? KatanaZanTotal(targets.Count) : targets.Count * KatanaKageHop;
            if (zan)
            {
                p.KatanaZan = 0;
                foreach (var e in targets)
                    if (!e.Def.IsBoss)
                        e.FrozenT = Math.Max(e.FrozenT, run.Total);  // the marked hold still until the cut falls
            }
            else
                p.KatanaKage = 0;
            p.InvulnT = Math.Max(p.InvulnT, run.Total + 0.15f);
            katanaRuns.Add(run);
            BroadcastSound(Sfx.KatanaStance);
        }

        /// The switch: the stance turns over and a small burst pushes nearby enemies back. Nothing else.
        void HostKatanaSwitch(Player p)
        {
            if (p.KatanaSwitchCd > 0f)
                return;
            p.KatanaStance = (byte)(p.KatanaStance ^ 1);
            p.KatanaSwitchCd = KatanaSwitchCd - 0.1f;  // (a little slack for the trip from a guest's game, which keeps the real cooldown)
            p.FireCd = Math.Max(p.FireCd, KatanaSwitchTime);
            p.TapQueued = false;
            p.KatanaFlowT = Math.Min(p.KatanaFlowT, KatanaFlowKeep - KatanaFlowFall);
            foreach (var e in Enemies.Values)
            {
                if (IsSpawning(e) || e.Hp <= 0f || Heavy(e))
                    continue;
                V2 d = e.Pos - p.Pos;
                float dist = d.Length;
                if (dist > KatanaSwitchRadius + e.Radius)
                    continue;
                KnockEnemy(e, (dist > 1f ? d / dist : V2.FromAngle(p.Aim)) * KatanaSwitchPush);
            }
            // Pos2: who switched and to which stance; Radius: the burst's reach (Game/KatanaLook.cs).
            BroadcastFx(FxKind.KatanaStanceFx, p.Pos, new V2(p.Id, p.KatanaStance), p.KatanaStance == 0 ? KatanaSteel : KatanaRose, KatanaSwitchRadius, 0.5f, Sfx.KatanaStanceSwap);
        }

        /// Host: the Kage Chain's cuts land one hop at a time; Zanshin's all fall as the blade is sheathed.
        void TickKatanaRuns(float dt)
        {
            for (int i = katanaRuns.Count - 1; i >= 0; i--)
            {
                var r = katanaRuns[i];
                r.T += dt;
                Player p;
                if (!Players.TryGetValue(r.Owner, out p) || p.Downed)
                {
                    katanaRuns.RemoveAt(i);
                    continue;
                }
                float hop = r.Zan ? KatanaZanHop : KatanaKageHop;
                while (r.Done < r.Targets.Count && r.T >= (r.Done + 1) * hop)
                {
                    V2 from = r.Done == 0 ? r.Start : r.Hops[r.Done - 1], to = r.Hops[r.Done];
                    // Each hop's streak: the Kage Chain's fades quickly, Zanshin's hang in the air until the cuts fall.
                    float left = r.Zan ? r.Total - r.T + 0.45f : 0.6f;
                    BroadcastFx(FxKind.KatanaHop, from, to, (r.Zan ? KatanaRose : KatanaSteel).WithAlpha(r.Zan ? 1f : 0.5f), p.Id, left,
                        KatanaHopSfx[Math.Min(KatanaHopSfx.Length - 1, r.Done)]);
                    if (!r.Zan)
                        KatanaCutEnemy(p, r.Targets[r.Done], (to - from).Normalized(), w => w.Damage * KatanaKageMult, KatanaSteel, true);
                    r.Done++;
                }
                if (!r.Zan)
                {
                    if (r.Done >= r.Targets.Count)
                        katanaRuns.RemoveAt(i);
                    continue;
                }
                if (!r.Sheathed && r.T >= r.Total - KatanaZanSheathe)
                {
                    r.Sheathed = true;
                    BroadcastSound(Sfx.KatanaSheathe);
                }
                if (r.T < r.Total)
                    continue;
                // The click: every marked enemy takes a cut per mark, and the shots near you fall.
                katanaRuns.RemoveAt(i);
                for (int k = 0; k < r.Targets.Count; k++)
                {
                    Enemy e;
                    if (!Enemies.TryGetValue(r.Targets[k], out e))
                        continue;
                    int marks = Math.Max(1, (int)e.KatanaMarks);
                    e.KatanaMarks = 0;
                    e.FrozenT = 0f;
                    V2 from = k == 0 ? r.Start : r.Hops[k - 1];
                    KatanaCutEnemy(p, e.Id, (r.Hops[k] - from).Normalized(), w => w.Damage * KatanaZanMult * marks, KatanaRose, k == 0);
                }
                CutBulletsInCircle(p.Pos, KatanaZanCutRange, true);
                BroadcastSound(Sfx.KatanaEmpStrikeCut);
            }
        }

        /// One cut from a Kage Chain or Zanshin: its damage (through the usual on-hit pipeline) and its KatanaCut effect.
        void KatanaCutEnemy(Player p, int id, V2 dir, Func<WeaponDef, float> baseDmg, Rgba col, bool blowSound)
        {
            Enemy e;
            if (!Enemies.TryGetValue(id, out e) || IsSpawning(e) || e.Hp <= 0f)
                return;
            float hit = baseDmg(p.Weapon) * p.Stats.DmgMult;
            hit *= NinjaHitMult(p.Id, Defs.KnightSlash, e);  // Vanish's ambush (Ninja.cs)
            HurtEnemy(e, hit, p.Id);
            OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = hit, Kind = Defs.KnightSlash }, e);
            BroadcastFx(FxKind.KatanaCut, e.Pos, dir, col.WithAlpha(e.Hp <= 0f ? 1f : 0.5f), e.Radius, 0.9f, blowSound ? Sfx.KatanaBlow : Sfx.None);
        }
    }
}
