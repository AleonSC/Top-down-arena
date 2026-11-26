using System;
using System.Collections.Generic;

namespace Arena
{
    /// Iron Armadillo (Tank Summoner, the third ultimate: `AbilityId.IronArmadillo`, key 2 of the lobby's Ultimate row), from
    /// the playable mockup in the session that made it. A giant spirit armadillo wraps round you and curls into an armoured ball
    /// (radius ArmR):
    ///   - the curl takes ArmCurl s (rooted); then you roll for ArmRoll s (mastered ArmRollMastered);
    ///   - rolling, you can't be hurt: enemy shots that reach the shell ricochet off it as your arcane bolts (ArmRicochet x 12),
    ///     red aimed ones just stop;
    ///   - you build speed up to ArmTop, steering toward your cursor (or WASD) with wide turns; hold Fire to brake and aim (the rev,
    ///     full in ArmRevTime), let go to launch at up to ArmBoostTop;
    ///   - each enemy you roll into takes ArmHitMin-ArmHitMax by speed (more past ArmTop), once every ArmHitEvery: regular ones
    ///     are flung (armed like a Lance shove: they hurt what they hit, Knock.cs), Heavies are stunned ArmHeavyStun, and Heavies
    ///     and bosses bounce you off;
    ///   - hitting a wall (or a prop) faster than ArmBashAt of ArmTop: ArmBash within ArmBashR, and you bounce;
    ///   - your weapon, its Alt, your abilities and the Dodge are locked; your minions keep fighting.
    /// It ends on time with the uncurl slam (ArmSlam within ArmSlamR, regulars shoved), or quietly if you go down or the wave
    /// ends. Your own game rolls you (LocalArmRoll) and sends a Msg.WeaponDash (WeaponAlt.ArmBash) for a wall bash; the host times it,
    /// reads your speed from how you move (ArmSpd) and does the hits. `Player.ArmT` (time left, the curl included; 0 = off) and
    /// `ArmAge` are replicated; Game/IronArmadilloLook.cs draws it.
    public partial class Game
    {
        public const float ArmCurl = 0.6f, ArmRoll = 10f, ArmRollMastered = 13f, ArmUncurl = 0.4f;
        public const float ArmR = 70f;                       // the ball's radius (140 px across)
        public const float ArmTop = 520f, ArmBoostTop = 780f, ArmStart = 220f, ArmAccel = 650f, ArmOverDecel = 700f;
        public const float ArmTurnSlow = 2.2f, ArmTurnFast = 6f, ArmRevTurn = 8f;
        public const float ArmBrake = 1100f, ArmRevTime = 0.8f, ArmBoostTime = 0.6f;
        public const float ArmHitMin = 20f, ArmHitMax = 80f, ArmHitEvery = 0.4f, ArmFling = 140f, ArmFlingPerSpeed = 0.3f, ArmHeavyStun = 0.6f;
        public const float ArmRestitution = 0.8f, ArmHeavyBounce = 0.75f;
        public const float ArmBashAt = 0.6f, ArmBash = 50f, ArmBashR = 120f, ArmBashEvery = 0.3f;
        public const float ArmSlam = 90f, ArmSlamMastered = 130f, ArmSlamR = 180f, ArmSlamShove = 110f;
        public const float ArmRicochet = 15f / 12f;          // a ricochet hits for 15 (SendReflects' 12 x this)
        public const float ArmBotRange = 420f;
        // The lobby preview (Showcase.cs): the ball rolls at the dummies in a pen the size of the preview's view, slower, and
        // flings them less far, so it stays in the picture and the view doesn't zoom out after them.
        public const float ArmShowTop = 300f, ArmShowFling = 0.35f, ArmShowHalfW = 160f, ArmShowAbove = 340f, ArmShowBelow = 90f;
        public static readonly Rgba ArmShell = new Rgba(0.79f, 0.71f, 0.54f), ArmSpirit = new Rgba(0.37f, 0.88f, 0.82f),
            ArmGold = new Rgba(1f, 0.8f, 0.35f);

        /// Its entry: key 2 of the Tank Summoner's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] ArmUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.IronArmadillo, Kit = Kit.Summoner, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Iron Armadillo", Cooldown = 75f,
                Desc = "Curl into a giant armoured ball for 10 s: roll over enemies, bounce their shots back, slam down as you uncurl.",
                Capstone = "Rolls 13 s and the uncurl slam hits for 130.",
                Bot = b => b.HaveTarget && b.TargetD < ArmBotRange,
            },
        };

        /// Curled up (the curl included).
        public static bool ArmOn(Player p) => p.ArmT > 0f && !p.Downed;
        /// Past the curl: rolling.
        public static bool ArmRolling(Player p) => ArmOn(p) && p.ArmAge >= ArmCurl;
        /// A roll hit's damage at `speed` px/s (before your damage bonus): ArmHitMin at rest to ArmHitMax at ArmTop, and on up to
        /// a quarter more at the launch's top speed.
        public static float ArmHit(float speed)
        {
            float k = Math.Max(0f, Math.Min(speed, ArmTop)) / ArmTop;
            float over = Math.Max(0f, Math.Min(speed, ArmBoostTop) - ArmTop) / (ArmBoostTop - ArmTop);
            return (ArmHitMin + (ArmHitMax - ArmHitMin) * k) * (1f + 0.25f * over);
        }

        readonly Dictionary<long, float> armHitAt = new Dictionary<long, float>();  // host: (player, enemy) -> ArmAge it was last hit
        readonly List<Enemy> armTouch = new List<Enemy>();

        /// Host: the armadillo wraps round you.
        void CastArmadillo(Player p, bool mastered)
        {
            p.ArmMastered = mastered;
            p.ArmT = ArmCurl + (mastered ? ArmRollMastered : ArmRoll);
            p.ArmAge = 0f;
            p.ArmSpd = 0f;
            p.ArmLast = p.Pos;
            p.ArmBashCd = 0f;
            p.UltT = p.ArmT;
            BroadcastFx(FxKind.Ring, p.Pos, ArmSpirit, ArmR + 40f, ArmCurl, Sfx.Summon);
        }

        /// It's over: on time the uncurl slam, otherwise (down, wave over, the ultimate swapped) quietly.
        void EndArmadillo(Player p, bool slam)
        {
            if (p.ArmT <= 0f && p.ArmAge <= 0f)
                return;
            p.ArmT = 0f;
            p.ArmAge = 0f;
            p.ArmSpd = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.IronArmadillo)
                p.UltT = 0f;
            if (!slam || !Battle)
                return;
            float dmg = (p.ArmMastered ? ArmSlamMastered : ArmSlam) * p.Stats.DmgMult;
            foreach (var e in FoesInCircle(p.Pos, ArmSlamR, by: p.Id))
            {
                HurtEnemy(e, dmg, p.Id);
                if (!Heavy(e))
                    ShoveFrom(e, p.Pos, ArmSlamShove);
            }
            p.ArmSlams++;
            BroadcastFx(FxKind.Boom, p.Pos, ArmGold, ArmSlamR, 0.55f, Sfx.BossSlam);
            PropBlast(p.Pos, ArmSlamR, p.Id);
        }

        /// Host, every tick: the time, your speed, the hits, and uncurling.
        void TickArmadillo(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.ArmT <= 0f)
                    continue;
                if (p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.IronArmadillo)
                {
                    EndArmadillo(p, false);
                    continue;
                }
                p.ArmT = Math.Max(0f, p.ArmT - dt);
                p.ArmAge += dt;
                p.ArmBashCd -= dt;
                if (p.ArmT <= 0f)
                {
                    EndArmadillo(p, true);
                    continue;
                }
                // How fast you're rolling, from how you move (a client's position comes in with its input): eased, so the
                // ticks a message lands on don't read as spikes.
                if (dt > 0f)
                {
                    float sample = (p.Pos - p.ArmLast).Length / dt;
                    if (sample < ArmBoostTop * 2.5f)  // (a teleport or a portal isn't speed)
                        p.ArmSpd += (Math.Min(sample, ArmBoostTop) - p.ArmSpd) * Math.Min(1f, dt / 0.12f);
                }
                p.ArmLast = p.Pos;
                if (p.ArmAge >= ArmCurl)
                    ArmHits(p);
            }
        }

        /// Host: whatever the ball touches.
        void ArmHits(Player p)
        {
            armTouch.Clear();
            foreach (var e in FoesInCircle(p.Pos, ArmR, by: p.Id))
                armTouch.Add(e);
            foreach (var e in armTouch)
            {
                long key = (long)p.Id << 32 | (uint)e.Id;
                if (armHitAt.TryGetValue(key, out float at) && p.ArmAge - at < ArmHitEvery && p.ArmAge >= at)
                    continue;
                armHitAt[key] = p.ArmAge;
                HurtEnemy(e, ArmHit(p.ArmSpd) * p.Stats.DmgMult, p.Id);
                p.ArmHits++;
                V2 away = AwayFrom(e.Pos, p.Pos, V2.FromAngle(p.Aim));
                if (!Heavy(e))
                {
                    // Bowled over: flung away, faster the faster you roll, armed to hurt what it meets.
                    KnockEnemy(e, away * ((ArmFling + p.ArmSpd * ArmFlingPerSpeed) * (ShowcaseMode ? ArmShowFling : 1f)));
                    e.ImpactBy = p.Id + 1;
                    e.ImpactLast = -1;
                    BroadcastFx(FxKind.Ring, e.Pos, ArmGold, e.Radius + 12f, 0.25f, Sfx.BruteBash);
                }
                else
                {
                    if (!e.Def.IsBoss)
                        Stun(e, ArmHeavyStun);
                    BroadcastFx(FxKind.Ring, e.Pos, ArmGold, e.Radius + 24f, 0.3f, Sfx.Clang);
                }
            }
            if (armHitAt.Count > 256)
                armHitAt.Clear();  // (old pairs: a fresh roll starts from ArmAge 0 anyway)
        }

        /// Host, from HostWeaponDash: your game says you hit a wall at `at`, fast.
        void HostArmBash(Player p, V2 at)
        {
            if (!ArmRolling(p) || p.ArmBashCd > 0f || at.FartherThan(p.Pos, ArmR + 60f))
                return;
            p.ArmBashCd = ArmBashEvery;
            p.ArmBashes++;
            foreach (var e in FoesInCircle(at, ArmBashR, by: p.Id))
                HurtEnemy(e, ArmBash * p.Stats.DmgMult, p.Id);
            BroadcastFx(FxKind.Boom, at, ArmGold, ArmBashR, 0.4f, Sfx.StoneSlam);
            PropBlast(at, ArmBashR, p.Id);
        }

        /// From ShieldsStop: a shot that reaches the shell ricochets off it (true: it's dealt with).
        bool ArmStops(Player p, Bullet b)
        {
            if (!ArmOn(p) || !Enters(b, p.Pos, ArmR))
                return false;
            scratchIds.Add(b.Id);
            if (Defs.Unblockable(b.Kind))
                return true;  // a red aimed shot just stops on the shell
            V2 n = AwayFrom(b.Pos, p.Pos, -b.Vel.Normalized());
            V2 v = b.Vel;
            float vn = v.Dot(n);
            if (vn < 0f)
                v -= n * (2f * vn);
            // SendReflects sends a reflection back against its Vel: so store it reversed.
            reflects.Add(new Bullet { Pos = p.Pos + n * (ArmR + 4f), Vel = -v, Owner = p.Id, Dmg = ArmRicochet, Kind = Defs.ArcaneShot });
            p.ArmRicochets++;
            return true;
        }

        // --- Your own game ------------------------------------------------------------------------------------------

        V2 armVel;                  // your ball's velocity
        float armRev, armBoost;     // the rev's charge (0-1) while you hold Fire, and the launch's time left
        bool armRevving, armRolled; // holding Fire to rev; rolling started (the first push given)
        public float ArmRevNow => armRevving ? armRev : 0f;   // (the look draws your launch arrow)
        public float ArmHeading;                               // where your ball is heading (the look, and the launch's aim)
        public int ArmBashesSent;                              // (tests)

        /// From the movement step: rolls you while you're curled up (in place of walking). `firing` revs.
        void LocalArmRoll(Player me, V2 move, V2 aimAt, bool firing, float dt)
        {
            if (!ArmOn(me) || me.ArmAge < ArmCurl)
            {
                armVel = V2.Zero;
                armRev = armBoost = 0f;
                armRevving = armRolled = false;
                ArmHeading = me.Aim;
                return;
            }
            V2 toward = !move.IsZero && !ShowcaseMode ? move : aimAt - me.Pos;  // (the preview strolls you round: the ball goes for the dummies instead)
            if (!armRolled)
            {
                armRolled = true;
                ArmHeading = toward.Length > 1f ? (float)Math.Atan2(toward.Y, toward.X) : me.Aim;
                armVel = V2.FromAngle(ArmHeading) * ArmStart;
            }
            float speed = armVel.Length;
            float heading = speed > 5f ? (float)Math.Atan2(armVel.Y, armVel.X) : ArmHeading;
            bool rev = firing && !Bot && me.StunT <= 0f;
            if (toward.Length > 20f)
            {
                float want = (float)Math.Atan2(toward.Y, toward.X);
                float d = want - heading;
                while (d > MathX.Pi) d -= MathX.Tau;
                while (d < -MathX.Pi) d += MathX.Tau;
                float turn = rev ? ArmRevTurn : ArmTurnFast - (ArmTurnFast - ArmTurnSlow) * Math.Min(1f, speed / ArmTop);
                heading += Math.Max(-turn * dt, Math.Min(turn * dt, d));
            }
            armBoost = Math.Max(0f, armBoost - dt);
            if (rev)
            {
                armRevving = true;
                armRev = Math.Min(1f, armRev + dt / ArmRevTime);
                speed = Math.Max(0f, speed - ArmBrake * dt);
            }
            else
            {
                if (armRevving)
                {
                    // Let go: launched along your aim, faster the longer you revved.
                    speed = ArmTop + (ArmBoostTop - ArmTop) * armRev;
                    armBoost = ArmBoostTime * armRev + 0.1f;
                    armRevving = false;
                    armRev = 0f;
                }
                float cap = (armBoost > 0f ? ArmBoostTop : ArmTop) * (ShowcaseMode ? ArmShowTop / ArmTop : 1f);
                speed = speed < cap ? Math.Min(cap, speed + ArmAccel * dt) : Math.Max(cap, speed - ArmOverDecel * dt);
            }
            ArmHeading = heading;
            armVel = V2.FromAngle(heading) * speed;

            // Heavies and bosses: you bounce off them.
            foreach (var e in Enemies.Values)
            {
                if (!Heavy(e) || IsSpawning(e) || !e.Pos.CloserThan(me.Pos, ArmR + e.Radius))
                    continue;
                V2 n = AwayFrom(e.Pos, me.Pos, V2.FromAngle(heading));
                float vn = armVel.Dot(n);
                if (vn > 0f)
                    armVel = (armVel - n * (1.7f * vn)) * ArmHeavyBounce;
                me.Pos = e.Pos - n * (ArmR + e.Radius);
            }

            V2 want2 = me.Pos + armVel * dt;
            V2 kept = KeepInArena(want2, ArmR);
            if (ShowcaseMode)
            {
                V2 pen = new V2(Math.Max(Showcase.Home.X - ArmShowHalfW + ArmR, Math.Min(Showcase.Home.X + ArmShowHalfW - ArmR, kept.X)),
                    Math.Max(Showcase.Home.Y - ArmShowAbove + ArmR, Math.Min(Showcase.Home.Y + ArmShowBelow - ArmR, kept.Y)));
                V2 lost = pen - kept;
                if (lost.Length > 0.01f)
                {
                    V2 n = lost.Normalized();
                    float vn = armVel.Dot(n);
                    if (vn < 0f)
                        armVel -= n * ((1f + ArmRestitution) * vn);  // (bounces off the pen's edge, no bash)
                    kept = pen;
                }
            }
            if (Walled)
                ArmWallBounce(me, want2, ref kept);
            if (Props.Count > 0 || LayoutOn)
            {
                V2 free = kept;
                kept = PushOutOfProps(kept, ArmR * 0.8f);
                ArmWallBounce(me, free, ref kept);
            }
            me.Pos = kept;
            if (armVel.Length > 5f)
                ArmHeading = (float)Math.Atan2(armVel.Y, armVel.X);
            iceVel = armVel;  // (on ice you carry on as you uncurl)
        }

        /// The ball was held back from `want` to `kept` (a wall, a prop): bounce, and a hard hit is a bash.
        void ArmWallBounce(Player me, V2 want, ref V2 kept)
        {
            V2 lost = kept - want;
            if (lost.Length < 0.01f)
                return;
            V2 n = lost.Normalized();  // (into the arena, away from what you hit)
            float vn = armVel.Dot(n);
            if (vn >= 0f)
                return;
            armVel -= n * ((1f + ArmRestitution) * vn);
            if (-vn < ArmTop * ArmBashAt)
                return;
            V2 at = kept - n * ArmR;
            ArmBashesSent++;
            if (IsHost)
                HostArmBash(me, at);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.ArmBash);
                w.Write(at);
                session.SendToHost(Proto.End(w));
            }
        }

        // --- Network: the player's Iron Armadillo fields (SyncPlayer) ---

        void SyncArmadillo(NetSync s, Player p)
        {
            s.Float(ref p.ArmT);  // Iron Armadillo
            s.Float(ref p.ArmAge);
        }
    }
}
