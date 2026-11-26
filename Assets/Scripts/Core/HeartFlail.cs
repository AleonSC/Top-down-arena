using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Healer Hemomancer's fourth weapon (the lobby's 4th key), the Heart Flail: a living heart on a chain of veins
    /// (mockup: the "Lifeline Heart Flail" artifact). The heart beats at Player.HfBpm (HfBpmMin-HfBpmMax); every enemy hit
    /// quickens it (HfBpmHit, HfBpmSpinHit while spinning) and it slows by HfBpmDecay a second.
    ///   Fire, clicked (let go within HfHoldToSpin): a Swing, a whip-crack arc HfSwingHalf either side of your aim, HfSwingDur
    ///     long, alternating sides. The heart hits each enemy it passes once for the weapon's damage, makes it bleed HfBleed a
    ///     second and shoves it HfKnock. Each hit squeezes out HfDrops blood drops (+1 an extra projectile) that fly to the most
    ///     hurt player near the heart (you too) and heal HfDropHeal each (FxKind.HeartDrops; host: heartDrops).
    ///   Fire, held: the Spin. The heart whirls round you at HfSpinR; it hits each enemy it passes for HfSpinMult x damage, at
    ///     most once per HfSpinEvery, and every heartbeat heals the allies within HfPulseR of you HfPulseHeal (FxKind.HeartPulse).
    ///   Alt (Msg.WeaponDash WeaponAlt.HeartThrow): Open Heart. The heart flies to your cursor (up to HfThrowRange) in
    ///     HfThrowFlight, lands for HfLandMult x damage within HfLandR (bleed and drops as a swing hit), then roots there for
    ///     HfRootTime: a vein runs from it to every ally within HfVeinR, healing HfVeinHps each. Then it reels back in HfReelTime.
    ///     HfThrowCd cooldown from the throw (Player.HfCd). No Fire while the heart is out.
    ///   Cardiac Burst: once the beat reaches HfBpmMax it holds there and the next beat heals everyone within HfBurstR of you HfBurstHeal (you too), shoves regular
    ///     enemies HfBurstKnock and sets the heart back to HfBpmMin (FxKind.HeartBurst).
    ///   At or under HfLowHp of your health every heal of the flail is HfLowMult stronger (the Hemomancer gives its blood away).
    ///   Pacemaker (Rare card): the heart quickens 50% faster per hit and its pulses and bursts heal 50% more.
    /// Replicated (Player.HfMode, HfT, HfAng, HfSide, HfPos, HfBpm, HfBeats, HfCd): Game/HeartFlailLook.cs draws the heart and
    /// chain from them.
    public partial class Game
    {
        public const byte HfRest = 0, HfSwing = 1, HfSpin = 2, HfFly = 3, HfRoot = 4, HfReel = 5;
        public const float HfSwingDur = 0.3f, HfSwingHalf = 75f * MathX.Pi / 180f, HfHeartR = 20f, HfHoldToSpin = 0.22f;
        public const float HfSpinR = 115f, HfSpinOmega = 11f, HfSpinEvery = 0.4f, HfSpinMult = 0.5f;
        public const float HfBpmMin = 60f, HfBpmMax = 180f, HfBpmHit = 10f, HfBpmSpinHit = 5f, HfBpmDecay = 15f, HfBpmSpinDecay = 6f;
        public const float HfBleed = 4f, HfKnock = 40f;
        public const int HfDrops = 3;
        public const float HfDropHeal = 4f, HfDropRange = 320f, HfDropFlight = 0.45f;
        public const float HfPulseHeal = 5f, HfPulseR = 150f, HfBurstHeal = 30f, HfBurstR = 220f, HfBurstKnock = 90f;
        public const float HfThrowRange = 360f, HfThrowFlight = 0.25f, HfLandMult = 1.3f, HfLandR = 70f, HfRootTime = 3f, HfVeinR = 200f, HfVeinHps = 8f;
        public const float HfReelTime = 0.3f, HfThrowCd = 12f, HfLowHp = 0.3f, HfLowMult = 1.5f, HfExtraReach = 0.1f;
        public static readonly Rgba HfBlood = new Rgba(0.88f, 0.2f, 0.29f), HfDeep = new Rgba(0.48f, 0.05f, 0.12f),
            HfPink = new Rgba(1f, 0.54f, 0.59f), HfGreen = new Rgba(0.35f, 0.94f, 0.5f), HfGold = Rgba.Gold;

        public static float HfEase(float x) { x = Math.Max(0f, Math.Min(1f, x)); return 1f - (1f - x) * (1f - x) * (1f - x); }

        /// How far the heart swings and spins out (extra projectiles: 10% farther each).
        public float HfReachOf(Player p) => p.Weapon.Reach * (1f + HfExtraReach * ExtraShots(p));
        public float HfSpinROf(Player p) => HfSpinR * (1f + HfExtraReach * ExtraShots(p));

        /// The swing's angle `k` (0-1) of the way through, from the aim it started on and its side (+1 or -1).
        public static float HfSwingAng(float aim0, int side, float k) => aim0 + side * (-HfSwingHalf + 2f * HfSwingHalf * HfEase(k));

        /// Where the heart hangs at rest: beside and a little behind your hand.
        public static V2 HfRestPos(Player p) => p.Pos + V2.FromAngle(p.Aim + 2.3f) * 24f;

        /// How strongly this player's flail heals now: Pacemaker isn't here (pulses only), the low-health bonus is.
        static float HfHealMult(Player p) => p.MaxHp > 0f && p.Hp <= p.MaxHp * HfLowHp ? HfLowMult : 1f;

        class HfHost
        {
            public float Hold, BpmF = HfBpmMin, Beat;
            public bool WasFiring, FireNow, Primed;  // Primed: the beat reached HfBpmMax, the next one bursts
            public V2 From, To;
            public readonly List<int> Hit = new List<int>();                    // this swing's enemies
            public readonly Dictionary<int, float> SpinCd = new Dictionary<int, float>();  // the spin: until each enemy can be hit again
        }
        readonly Dictionary<int, HfHost> hfHost = new Dictionary<int, HfHost>();

        HfHost HfOf(Player p)
        {
            HfHost h;
            if (!hfHost.TryGetValue(p.Id, out h))
                hfHost[p.Id] = h = new HfHost();
            return h;
        }

        /// Host, from the trigger chain (Game.cs) while the Heart Flail is out and you can attack: Fire this tick.
        void HfTrigger(Player p, float dt)
        {
            var h = HfOf(p);
            h.FireNow = p.Firing && !p.ParryActive && State != MatchState.GameOver;
        }

        /// Host, every tick for every player: the trigger (a click swings, holding spins), the heart's motion and hits, the beat.
        void TickHeartFlail(Player p, float dt)
        {
            p.HfCd = Math.Max(0f, p.HfCd - dt);
            if (p.Weapon.Id != WeaponId.HeartFlail || p.Downed)
            {
                if (p.HfMode != HfRest || p.HfBpm != (byte)HfBpmMin)
                    ClearHeartFlail(p);
                return;
            }
            var h = HfOf(p);
            bool fire = h.FireNow;
            h.FireNow = false;
            p.HfT += dt;
            bool heartOut = p.HfMode >= HfFly;
            // the trigger
            if (!heartOut)
            {
                if (fire)
                {
                    h.Hold += dt;
                    if (h.Hold >= HfHoldToSpin && p.HfMode != HfSpin && p.HfMode != HfSwing)
                        HfStartSpin(p, h);
                }
                else
                {
                    if (h.WasFiring && h.Hold < HfHoldToSpin && p.HfMode != HfSwing && p.FireCd <= 0f)
                        HfStartSwing(p, h);
                    if (p.HfMode == HfSpin)
                        HfSetMode(p, HfRest);
                    h.Hold = 0f;
                }
                if (p.HfMode == HfSwing && p.HfT >= HfSwingDur)
                    HfSetMode(p, fire && h.Hold >= HfHoldToSpin ? HfSpin : HfRest);
            }
            else
                h.Hold = 0f;
            h.WasFiring = fire;
            // the heart's motion and hits
            float dmg = p.Weapon.Damage * p.Stats.DmgMult;
            switch (p.HfMode)
            {
                case HfSwing:
                {
                    p.HfAng = HfSwingAng(p.HfAim0, p.HfSide, p.HfT / HfSwingDur);
                    p.HfPos = p.Pos + V2.FromAngle(p.HfAng) * HfReachOf(p);
                    foreach (var e in FoesInCircle(p.HfPos, HfHeartR, by: p.Id))
                    {
                        if (h.Hit.Contains(e.Id))
                            continue;
                        h.Hit.Add(e.Id);
                        HfHitEnemy(p, h, e, dmg, HfBpmHit, true);
                    }
                    break;
                }
                case HfSpin:
                {
                    p.HfAng = MathX.Fmod(p.HfAng + HfSpinOmega * dt, MathX.Tau);
                    p.HfPos = p.Pos + V2.FromAngle(p.HfAng) * HfSpinROf(p);
                    HfTickSpinCds(h, dt);
                    foreach (var e in FoesInCircle(p.HfPos, HfHeartR, by: p.Id))
                    {
                        if (h.SpinCd.ContainsKey(e.Id))
                            continue;
                        h.SpinCd[e.Id] = HfSpinEvery;
                        HfHitEnemy(p, h, e, dmg * HfSpinMult, HfBpmSpinHit, false);
                    }
                    break;
                }
                case HfFly:
                {
                    p.HfPos = V2.Lerp(h.From, h.To, HfEase(p.HfT / HfThrowFlight));
                    if (p.HfT >= HfThrowFlight)
                        HfLand(p, h, dmg);
                    break;
                }
                case HfRoot:
                {
                    p.HfPos = h.To;
                    float vr = HfVeinR * AreaMult(p), heal = HfVeinHps * dt * HfHealMult(p);
                    foreach (var q in Players.Values)
                        if (q.Id != p.Id && !q.Downed && !q.Pos.FartherThan(h.To, vr))
                            Heal(q, heal, p);
                    if (p.HfT >= HfRootTime || p.Pos.FartherThan(h.To, HfThrowRange * 1.6f))
                    {
                        HfSetMode(p, HfReel);
                        BroadcastSound(Sfx.LanceHookOut);
                    }
                    break;
                }
                case HfReel:
                {
                    p.HfPos = V2.Lerp(h.To, HfRestPos(p), HfEase(p.HfT / HfReelTime));
                    if (p.HfT >= HfReelTime)
                        HfSetMode(p, HfRest);
                    break;
                }
                default:
                {
                    V2 rest = HfRestPos(p);
                    p.HfPos = p.HfPos.IsZero ? rest : V2.Lerp(p.HfPos, rest, Math.Min(1f, dt * 14f));
                    p.HfAng = (p.HfPos - p.Pos).Angle;
                    break;
                }
            }
            // the beat
            if (h.BpmF >= HfBpmMax - 0.01f)
                h.Primed = true;  // (held at full until the burst)
            if (!h.Primed)
                h.BpmF = Math.Max(HfBpmMin, h.BpmF - (p.HfMode == HfSpin ? HfBpmSpinDecay : HfBpmDecay) * dt);
            h.Beat += dt * h.BpmF / 60f;
            if (h.Beat >= 1f)
            {
                h.Beat -= 1f;
                HfBeat(p, h);
            }
            p.HfBpm = (byte)Math.Round(h.BpmF);
        }

        void HfTickSpinCds(HfHost h, float dt)
        {
            if (h.SpinCd.Count == 0)
                return;
            hfKeys.Clear();
            hfKeys.AddRange(h.SpinCd.Keys);
            foreach (int k in hfKeys)
            {
                float left = h.SpinCd[k] - dt;
                if (left <= 0f)
                    h.SpinCd.Remove(k);
                else
                    h.SpinCd[k] = left;
            }
        }
        readonly List<int> hfKeys = new List<int>();

        void HfSetMode(Player p, byte mode)
        {
            p.HfMode = mode;
            p.HfT = 0f;
        }

        void HfStartSwing(Player p, HfHost h)
        {
            p.HfSide = p.HfSide == 1 ? -1 : 1;
            p.HfAim0 = p.Aim;
            HfSetMode(p, HfSwing);
            h.Hit.Clear();
            p.FireCd = p.Weapon.FireInterval / FireRate(p);
            p.Run.ShotsFired++;
            BroadcastSound(Sfx.SwingHeavy);
        }

        void HfStartSpin(Player p, HfHost h)
        {
            if (p.HfMode == HfRest)
                p.HfAng = (p.HfPos - p.Pos).IsZero ? p.Aim : (p.HfPos - p.Pos).Angle;
            HfSetMode(p, HfSpin);
            h.SpinCd.Clear();
            BroadcastSound(Sfx.SwingLight);
        }

        /// One enemy struck by the heart: damage, bleed and shove (a swing or a landing), the beat quickens, blood drops.
        void HfHitEnemy(Player p, HfHost h, Enemy e, float dmg, float bpm, bool heavy)
        {
            V2 dir = e.Pos - p.HfPos;
            dir = dir.IsZero ? V2.FromAngle(p.Aim) : dir.Normalized();
            WeaponHurt(e, dmg, p.Id);
            if (heavy)
            {
                ApplyBleed(e, HfBleed, p.Id);
                if (e.Hp > 0f && !Heavy(e))
                    KnockEnemy(e, dir * HfKnock);
            }
            if (p.Run.ShotsHit < p.Run.ShotsFired)
                p.Run.ShotsHit++;
            h.BpmF = Math.Min(HfBpmMax, h.BpmF + bpm * p.Stats.HfBpmMult);
            p.HfBpm = (byte)Math.Round(h.BpmF);
            if (heavy)
                HfSqueeze(p, p.HfPos, HfDrops + ExtraShots(p));  // (the spin's glancing hits squeeze none: its beats heal instead)
            Sounds.Add(Sfx.Hit);
        }

        /// The heart lands where it was thrown: it hits round it, then roots.
        void HfLand(Player p, HfHost h, float dmg)
        {
            p.HfPos = h.To;
            h.Hit.Clear();
            foreach (var e in FoesInCircle(h.To, HfLandR, by: p.Id))
                if (!h.Hit.Contains(e.Id))
                {
                    h.Hit.Add(e.Id);
                    HfHitEnemy(p, h, e, dmg * HfLandMult, HfBpmHit, true);
                }
            HfSetMode(p, HfRoot);
            BroadcastFx(FxKind.HeartPulse, h.To, new V2(p.Id, 1f), HfBlood, HfLandR, 0.45f, Sfx.BossSplat);
        }

        /// A heartbeat: a Cardiac Burst at full speed, a healing pulse while spinning, else just the beat (the look counts HfBeats).
        void HfBeat(Player p, HfHost h)
        {
            p.HfBeats = (byte)(p.HfBeats + 1);
            if (State != MatchState.Fighting && State != MatchState.Intermission)
                return;
            if (h.Primed)
            {
                h.Primed = false;
                float r = HfBurstR * AreaMult(p), heal = HfBurstHeal * HfHealMult(p) * p.Stats.HfPulseMult;
                foreach (var q in Players.Values)
                    if (!q.Downed && !q.Pos.FartherThan(p.Pos, r))
                        Heal(q, heal, p);
                foreach (var e in FoesInCircle(p.Pos, r, by: p.Id))
                {
                    if (Heavy(e) || e.Hp <= 0f)
                        continue;
                    V2 d = e.Pos - p.Pos;
                    KnockEnemy(e, (d.IsZero ? V2.FromAngle(p.Aim) : d.Normalized()) * HfBurstKnock);
                }
                h.BpmF = HfBpmMin;
                p.HfBpm = (byte)HfBpmMin;
                BroadcastFx(FxKind.HeartBurst, p.Pos, new V2(p.Id, 0f), HfGold, r, 0.7f, Sfx.Shockwave);
                return;
            }
            if (p.HfMode == HfSpin)
            {
                float r = HfPulseR * AreaMult(p), heal = HfPulseHeal * HfHealMult(p) * p.Stats.HfPulseMult;
                foreach (var q in Players.Values)
                    if (q.Id != p.Id && !q.Downed && !q.Pos.FartherThan(p.Pos, r))
                        Heal(q, heal, p);
                BroadcastFx(FxKind.HeartPulse, p.Pos, new V2(p.Id, 0f), HfGreen, r, 0.5f, Sfx.StepThump);
            }
        }

        class HfDropRun { public int Owner, Target, Count; public float T; }
        readonly List<HfDropRun> heartDrops = new List<HfDropRun>();

        /// Blood squeezed out of the heart: `n` drops fly to the most hurt player near it (you too) and heal as they land;
        /// with nobody hurt they spatter and fade.
        void HfSqueeze(Player p, V2 at, int n)
        {
            Player best = null;
            float worst = 0.999f;
            foreach (var q in Players.Values)
            {
                if (q.Downed || q.MaxHp <= 0f || q.Pos.FartherThan(at, HfDropRange))
                    continue;
                float f = q.Hp / q.MaxHp;
                if (f < worst - 0.001f || f < worst + 0.001f && best != null && best.Id == p.Id && q.Id != p.Id)
                {
                    worst = f;
                    best = q;
                }
            }
            int target = best != null ? best.Id : -1;
            if (best != null)
                heartDrops.Add(new HfDropRun { Owner = p.Id, Target = target, Count = n, T = HfDropFlight });
            BroadcastFx(FxKind.HeartDrops, at, new V2(target, n), HfPink, p.Id, HfDropFlight + 0.15f, Sfx.None);
        }

        /// Host, every tick: drops in flight land on whoever they were flying to.
        void TickHeartDrops(float dt)
        {
            for (int i = heartDrops.Count - 1; i >= 0; i--)
            {
                var d = heartDrops[i];
                d.T -= dt;
                if (d.T > 0f)
                    continue;
                heartDrops.RemoveAt(i);
                Player from, to;
                if (!Players.TryGetValue(d.Owner, out from) || !Players.TryGetValue(d.Target, out to) || to.Downed)
                    continue;
                Heal(to, HfDropHeal * d.Count * HfHealMult(from), from);
            }
        }

        /// Put away or down: the heart hangs back at your side and stops racing.
        void ClearHeartFlail(Player p)
        {
            p.HfMode = HfRest;
            p.HfT = 0f;
            p.HfBpm = (byte)HfBpmMin;
            HfHost h;
            if (hfHost.TryGetValue(p.Id, out h))
            {
                h.BpmF = HfBpmMin;
                h.Hold = 0f;
                h.WasFiring = h.FireNow = h.Primed = false;
            }
        }

        // ---- Open Heart (Alt, a tap)

        /// Your own game: Alt with the Heart Flail throws it (the host checks the rest). True when the key is theirs.
        bool LocalHeartThrow(Player me, bool pressed, V2 aimAt)
        {
            if (me.Weapon.Id != WeaponId.HeartFlail)
                return false;
            if (!pressed || me.Downed || me.StunT > 0f || me.HfCd > 0f || me.HfMode >= HfFly)
                return true;
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.HeartThrow, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.HeartThrow);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        /// From HostWeaponDash: Open Heart thrown at `target`.
        bool HostHeartDash(Player p, int kind, V2 target)
        {
            if (kind != WeaponAlt.HeartThrow)
                return false;
            if (p.Weapon.Id != WeaponId.HeartFlail || p.Downed || p.StunT > 0f || p.HfCd > 0f || p.HfMode >= HfFly || State == MatchState.GameOver)
                return true;
            var h = HfOf(p);
            V2 to = target - p.Pos;
            float d = to.Length;
            if (d > HfThrowRange)
                to = to * (HfThrowRange / d);
            h.From = p.HfPos.IsZero ? p.Pos : p.HfPos;
            h.To = ClampToArena(p.Pos + to, HfHeartR);
            h.Hold = 0f;
            HfSetMode(p, HfFly);
            p.HfCd = HfThrowCd;
            p.Run.ShotsFired++;
            BroadcastSound(Sfx.Throw);
            return true;
        }

        // --- Network: the player's Heart Flail fields (SyncPlayer) ---

        void SyncHeartFlail(NetSync s, Player p)
        {
            s.Byte(ref p.HfMode);
            s.Byte(ref p.HfBpm);
            s.Byte(ref p.HfBeats);
            s.Float(ref p.HfT);
            s.Float(ref p.HfAng);
            s.Float(ref p.HfCd);
            s.SByte(ref p.HfSide);
            s.Float(ref p.HfAim0);
            s.Vec(ref p.HfPos);
        }

        /// WeaponDef.BotAlt (BotWeaponAlt.cs): Open Heart beside a hurt teammate, else into a pack
        static bool HeartFlailBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            var e = v.ShootAt;
            if (late || me.HfCd > 0f || me.HfMode >= HfFly)
                return false;
            Player hurt = null;
            foreach (var q in g.Players.Values)
                if (q.Id != me.Id && !q.Downed && q.Hp < q.MaxHp * 0.6f && !q.Pos.FartherThan(me.Pos, HfThrowRange) && (hurt == null || q.Hp / q.MaxHp < hurt.Hp / hurt.MaxHp))
                    hurt = q;
            if (hurt != null)
            {
                if (!g.Tap(true, 1f))
                    return false;
                aimAt = hurt.Pos;
                return true;
            }
            if (e == null || v.ShootD > HfThrowRange || !g.Tap(g.EnemiesNear(e.Pos, 100f) >= 3, 1f))
                return false;
            aimAt = e.Pos;
            return true;
        }
    }
}
