using System;
using System.Collections.Generic;

namespace Arena
{
    /// Bone Phalanx, the Tank Summoner's second ultimate (`AbilityId.BonePhalanx`, picked with key 1 of the lobby's Ultimate
    /// row; Titan stays key 0). Design: Docs/Mockups/ultimate-ideas.html (Tank > Summoner, first card); look:
    /// Docs/Mockups/bone-phalanx.html, drawn in Game/BonePhalanxLook.cs.
    ///
    /// A bone gate cracks open in the floor PhalanxAhead px ahead of you, across your aim (PhalanxGate s). Eight skeletons with
    /// tower shields step out of it one after another, middle first (PhalanxStepEvery apart, PhalanxStepTime each), and lock
    /// into a line across the aim, PhalanxSpacing apart. Then the line marches PhalanxMarch px along the aim it was cast with
    /// over PhalanxMarchTime s (it keeps that heading if you turn), holds PhalanxHold s, and crumbles (PhalanxCrumble s).
    ///   - The shields stop every enemy bullet that reaches the line from the front, red aimed shots too (PhalanxStops, from
    ///     the enemy-bullet loop in Combat.cs). Shots from behind pass, so your team's do.
    ///   - Enemies against the line are shoved along ahead of it (ShovePhalanx); every PhalanxSpearEvery s (shortened by
    ///     your fire-rate bonuses, like other minions) each skeleton spears what it is shoving for PhalanxSpear (times your
    ///     damage multiplier at the cast). While it shoves a Heavy enemy the line walks at PhalanxHeavyPace; a boss is never
    ///     shoved and stops the line, and so does an enemy pinned against a wall.
    ///   - Each skeleton has PhalanxHp health and loses it to the shots it blocks and to the enemies pressing on it. A broken
    ///     one leaves a gap (its lane) that bullets and enemies get through.
    ///   - The line stops at walls and props. It crumbles early if you go down or the wave ends.
    /// Mastered: PhalanxCapCount skeletons with PhalanxCapHp health each (a 250 px front).
    ///
    /// It is one `ZoneKind.Phalanx` zone, not eight minions: Pos is the middle of the shield face (it moves), Aim the heading,
    /// T / Duration the clock, and three extras ride the zone's snapshot (GameNet.SyncZone): Marched (px walked), and one byte
    /// of health per skeleton (Bones: 0 = broken). That is about 30 bytes a snapshot against eight full minion records, and
    /// the line's rules (one heading, one stop, lanes) live in one place.
    ///   FxKind.PhalanxJab:   a skeleton's spear thrust (Pos = the spear's butt, Pos2 = where it strikes).
    ///   FxKind.PhalanxSpark: a shot stopped on a shield (Pos = where, Pos2 = the heading as a unit vector).
    ///   FxKind.PhalanxBreak: a skeleton falling apart (Pos = where it stood, Pos2 = the heading; Radius = 0 broken by
    ///                        damage, 1 + delay for the crumble at the end); its bone pile lingers for Dur.
    public partial class Game
    {
        public const int PhalanxCount = 8, PhalanxCapCount = 10;
        public const float PhalanxHp = 120f, PhalanxCapHp = 180f;
        public const float PhalanxAhead = 60f;        // the gate opens this far ahead of you
        public const float PhalanxSpacing = 25f;      // between skeletons: 8 make a 200 px front
        public const float PhalanxGate = 0.5f;        // the gate cracking open
        public const float PhalanxStepEvery = 0.09f, PhalanxStepTime = 0.35f;  // one after another, each takes this long to step into place
        public const float PhalanxMarch = 260f, PhalanxMarchTime = 6f, PhalanxHold = 1f, PhalanxCrumble = 0.8f;
        public const float PhalanxSpear = 15f, PhalanxSpearEvery = 0.5f;
        public const float PhalanxHeavyPace = 0.45f;  // the march's speed while it shoves a Heavy enemy
        public const float PhalanxContactShare = 0.5f;  // of an enemy's contact damage a skeleton takes each spear beat it is pressed
        public const float PhalanxFace = 4f;          // the shields' front, ahead of Pos's line (an enemy is held this far plus its radius out)
        public const float PhalanxBack = 16f;         // the skeletons' backs, behind it (an enemy coming from behind is held there)
        public const float PhalanxBreakFx = 3.5f;     // how long a bone pile lingers
        const float PhalanxSlack = 1.5f;              // an enemy this close to the shield still counts as against it

        public static float PhalanxAge(Zone z) => z.Duration - z.T;
        public static int PhalanxSkeletons(Zone z) => z.Bones != null ? z.Bones.Length : PhalanxCount;

        /// The order skeletons step out of the gate: the middle two first, then outward (0 is first).
        public static int PhalanxOrder(int i, int n)
        {
            float off = i - (n - 1) / 2f;
            return 2 * (int)Math.Floor(Math.Abs(off)) + (off < 0f ? 0 : 1);
        }

        /// When skeleton i starts stepping out of the gate, and when the whole line is in place (the march starts).
        public static float PhalanxOutAt(int i, int n) => PhalanxGate + PhalanxOrder(i, n) * PhalanxStepEvery;
        public static float PhalanxMarchAt(int n) => PhalanxGate + (n - 1) * PhalanxStepEvery + PhalanxStepTime;
        public static float PhalanxLife(int n) => PhalanxMarchAt(n) + PhalanxMarchTime + PhalanxHold + PhalanxCrumble;

        /// Crumbling (the end, or cut short): it no longer blocks, shoves or spears.
        public static bool PhalanxCrumbling(Zone z) => z.T <= PhalanxCrumble;

        /// Is skeleton i in its place in the line (stepped out, not broken, not crumbling)?
        public static bool PhalanxStanding(Zone z, int i)
        {
            int n = PhalanxSkeletons(z);
            return i >= 0 && i < n && z.Bones != null && z.Bones[i] > 0 && !PhalanxCrumbling(z) && PhalanxAge(z) >= PhalanxOutAt(i, n) + PhalanxStepTime;
        }

        /// Skeleton i's offset across the line (0 = the middle).
        public static float PhalanxLane(int i, int n) => (i - (n - 1) / 2f) * PhalanxSpacing;

        /// A point in the line's frame: x ahead of the shield face along the heading, y across it.
        public static V2 PhalanxLocal(Zone z, V2 p)
        {
            V2 d = V2.FromAngle(z.Aim), off = p - z.Pos;
            return new V2(off.Dot(d), off.X * -d.Y + off.Y * d.X);
        }

        public static V2 PhalanxWorld(Zone z, float x, float y)
        {
            V2 d = V2.FromAngle(z.Aim);
            return z.Pos + d * x + new V2(-d.Y, d.X) * y;
        }

        /// Where the gate opened (the line's start).
        public static V2 PhalanxGatePos(Zone z) => z.Pos - V2.FromAngle(z.Aim) * z.Marched;

        /// Which skeleton's lane a point across the line (local y) falls in, or -1 off the ends.
        public static int PhalanxLaneAt(Zone z, float y)
        {
            int n = PhalanxSkeletons(z);
            int i = (int)Math.Round(y / PhalanxSpacing + (n - 1) / 2f);
            return i >= 0 && i < n ? i : -1;
        }

        /// Host: the ultimate was cast toward `target`.
        void CastPhalanx(Player p, V2 target, bool mastered)
        {
            V2 off = target - p.Pos;
            float aim = off.LengthSq > 1f ? off.Angle : p.Aim;
            int n = mastered ? PhalanxCapCount : PhalanxCount;
            foreach (var old in Zones.Values)
                if (old.Kind == ZoneKind.Phalanx && old.Owner == p.Id && !PhalanxCrumbling(old))
                    CrumblePhalanx(old);  // (a new line replaces the last; only the Dev tools can cast it this soon)
            var z = AddZone(ZoneKind.Phalanx, p, ClampToArena(p.Pos + V2.FromAngle(aim) * PhalanxAhead, 20f), PhalanxSpacing * n / 2f, PhalanxLife(n));
            z.Aim = aim;
            z.BoneMax = (mastered ? PhalanxCapHp : PhalanxHp) * (casting ? castPower : 1f);
            z.BoneHp = new float[n];
            z.Bones = new byte[n];
            z.SpearT = new float[n];
            for (int i = 0; i < n; i++)
            {
                z.BoneHp[i] = z.BoneMax;
                z.Bones[i] = 255;
                z.SpearT[i] = PhalanxSpearEvery * (0.5f + 0.5f * i / n);  // the first jabs ripple along the line
            }
            BroadcastFx(FxKind.Ring, z.Pos, new Rgba(0.93f, 0.9f, 0.8f), PhalanxSpacing * n / 2f, 0.45f, Sfx.Summon);
        }

        /// Host, from TickZones: the march, the spears, and the end.
        void TickPhalanx(Zone z, float dt)
        {
            if (z.BoneHp == null || PhalanxCrumbling(z))
                return;
            Player owner;
            if (!Players.TryGetValue(z.Owner, out owner) || owner.Downed || !Battle)
            {
                CrumblePhalanx(z);  // you went down, or the wave is over
                return;
            }
            int n = z.BoneHp.Length;
            float age = PhalanxAge(z), marchAt = PhalanxMarchAt(n);
            bool any = false;
            for (int i = 0; i < n; i++)
                any |= z.BoneHp[i] > 0f;
            if (!any)
            {
                z.T = Math.Min(z.T, PhalanxCrumble);  // every skeleton broken: nothing left to crumble but the gate
                return;
            }
            if (z.T <= PhalanxCrumble + dt * 1.5f)
            {
                CrumblePhalanx(z);
                return;
            }

            // The march: pace along the heading, slowed by a Heavy enemy on the shields, stopped by a boss, a wall or a prop.
            if (age >= marchAt && age < marchAt + PhalanxMarchTime && z.Marched < PhalanxMarch)
            {
                float pace = ShovePhalanx(z, false);
                if (pace > 0f)
                {
                    float step = Math.Min(PhalanxMarch / PhalanxMarchTime * pace * dt, PhalanxMarch - z.Marched);
                    if (!PhalanxWalled(z, step))
                    {
                        z.Pos += V2.FromAngle(z.Aim) * step;
                        z.Marched += step;
                    }
                }
            }
            ShovePhalanx(z, true);

            // The spears, and the enemies pressing on each shield wearing it down.
            float rate = Math.Max(0.1f, owner.Stats.FireRateMult);
            for (int i = 0; i < n; i++)
            {
                if (!PhalanxStanding(z, i))
                    continue;
                z.SpearT[i] -= dt;
                if (z.SpearT[i] > 0f)
                    continue;
                z.SpearT[i] += PhalanxSpearEvery / rate;
                float press = 0f;
                Enemy struck = null;
                foreach (var e in Enemies.Values)
                {
                    if (!PhalanxAgainst(z, e, i))
                        continue;
                    HurtEnemy(e, PhalanxSpear * z.DmgMult, z.Owner);
                    press += EnemyContact(e) * EnemyDamageScale(Wave) * PhalanxContactShare;
                    if (struck == null || e.Pos.DistanceSq(PhalanxWorld(z, 0f, PhalanxLane(i, n))) < struck.Pos.DistanceSq(PhalanxWorld(z, 0f, PhalanxLane(i, n))))
                        struck = e;
                }
                if (struck != null)
                    BroadcastFx(FxKind.PhalanxJab, PhalanxWorld(z, -10f, PhalanxLane(i, n) + 6f), struck.Pos, new Rgba(0.93f, 0.9f, 0.8f), struck.Radius, 0.28f, Sfx.None);
                if (press > 0f)
                    WearBone(z, i, press);
            }
        }

        /// An enemy's contact damage, as TickEnemies works it out for players (before the wave's damage growth).
        static float EnemyContact(Enemy e) => e.Def.IsBoss ? 25f : e.Def.Contact > 0f ? e.Def.Contact : ContactDamage;

        /// Does enemy e (at `local` in the line's frame) overlap skeleton i's shield? It covers its lane plus half the enemy's
        /// radius either side, so a small enemy fits through the gap of one broken skeleton and a big one doesn't.
        static bool PhalanxCovers(Zone z, Enemy e, V2 local, int i) =>
            Math.Abs(local.Y - PhalanxLane(i, PhalanxSkeletons(z))) < PhalanxSpacing / 2f + e.Radius * 0.5f;

        /// The standing skeleton nearest enemy e whose shield it is pressed against (at the face, from either side), or -1.
        int PhalanxContact(Zone z, Enemy e, V2 local)
        {
            if (!Targetable(e) || local.X >= PhalanxFace + e.Radius + PhalanxSlack || local.X <= -PhalanxBack - e.Radius - PhalanxSlack)
                return -1;  // (the slack: one held exactly at the shield still counts as pressed against it)
            int n = PhalanxSkeletons(z), best = -1;
            for (int i = 0; i < n; i++)
                if (PhalanxStanding(z, i) && PhalanxCovers(z, e, local, i)
                    && (best < 0 || Math.Abs(local.Y - PhalanxLane(i, n)) < Math.Abs(local.Y - PhalanxLane(best, n))))
                    best = i;
            return best;  // none: a gap (or off the ends), it walks through
        }

        /// Is enemy e pressed against skeleton i's shield? Every shield it overlaps holds it, and spears it. (The spear's
        /// reach is a few px more than the hold.)
        bool PhalanxAgainst(Zone z, Enemy e, int i)
        {
            V2 l = PhalanxLocal(z, e.Pos);
            l.X -= Math.Sign(l.X + 4f) * 3f;
            return PhalanxContact(z, e, l) >= 0 && PhalanxStanding(z, i) && PhalanxCovers(z, e, l, i);
        }

        /// Would the line's shield face, `step` px further on, run into a wall or a prop (or off the arena)? Any standing
        /// skeleton's shield touching one stops the whole line.
        bool PhalanxWalled(Zone z, float step)
        {
            int n = PhalanxSkeletons(z);
            for (int i = 0; i < n; i++)
            {
                if (z.BoneHp[i] <= 0f)
                    continue;
                V2 at = PhalanxWorld(z, step + PhalanxFace, PhalanxLane(i, n));
                if (ClampToArena(at, 6f).DistanceTo(at) > 0.5f || PushOutOfProps(at, 6f).DistanceTo(at) > 0.5f)
                    return true;
            }
            return false;
        }

        /// Enemies against a standing skeleton are held off its shield (ahead of the line, or behind it if they came from
        /// behind). Returns the pace the line can march at: 1, PhalanxHeavyPace while it holds a Heavy enemy, 0 if a boss is
        /// on it or an enemy is pinned against a wall. move: actually put the enemies out (false only asks for the pace).
        float ShovePhalanx(Zone z, bool move)
        {
            if (z.BoneHp == null || PhalanxCrumbling(z))
                return 0f;
            float pace = 1f;
            foreach (var e in Enemies.Values)
            {
                V2 l = PhalanxLocal(z, e.Pos);
                if (PhalanxContact(z, e, l) < 0)
                    continue;
                float front = PhalanxFace + e.Radius, back = -PhalanxBack - e.Radius;
                bool ahead = l.X >= -4f;
                if (e.Def.IsBoss || e.Dummy)
                {
                    pace = 0f;  // a boss (or a balance dummy) is never shoved: the line stops while it is on the shields
                    continue;
                }
                if (ahead && e.Def.Heavy)
                    pace = Math.Min(pace, PhalanxHeavyPace);
                V2 want = PhalanxWorld(z, ahead ? front + (move ? 0f : 2f) : back, l.Y);  // (asking: a step on)
                V2 put = PushOutOfProps(ClampToArena(want, e.Radius), e.Radius);
                if (ahead && put.DistanceTo(want) > 2f)
                    pace = 0f;  // pinned against a wall or a prop: the line can't go on
                if (!move)
                    continue;
                e.Pos = put;
                e.KnockLeft = V2.Zero;
            }
            return pace;
        }

        /// Host, after the enemies move (Game.FixedStep): nobody walks through a standing shield.
        void KeepOffPhalanx()
        {
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.Phalanx)
                    ShovePhalanx(z, true);
        }

        /// A skeleton takes damage; at zero it breaks and leaves a gap (and a pile of bones).
        void WearBone(Zone z, int i, float dmg)
        {
            if (z.BoneHp[i] <= 0f)
                return;
            z.BoneHp[i] -= dmg;
            if (z.BoneHp[i] > 0f)
            {
                z.Bones[i] = (byte)Math.Max(1, Math.Min(255, (int)Math.Ceiling(z.BoneHp[i] / z.BoneMax * 255f)));
                return;
            }
            z.BoneHp[i] = 0f;
            z.Bones[i] = 0;
            BroadcastFx(FxKind.PhalanxBreak, PhalanxWorld(z, -8f, PhalanxLane(i, z.BoneHp.Length)), V2.FromAngle(z.Aim), new Rgba(0.93f, 0.9f, 0.8f), 0f, PhalanxBreakFx, Sfx.Hit);
        }

        /// The end (or cut short): every skeleton left falls apart, one after another from the ends in.
        void CrumblePhalanx(Zone z)
        {
            if (z.BoneHp == null)
                return;
            int n = z.BoneHp.Length;
            float age = PhalanxAge(z);
            for (int i = 0; i < n; i++)
            {
                if (z.BoneHp[i] <= 0f || age < PhalanxOutAt(i, n))
                    continue;  // broken already, or not out of the gate yet
                float delay = (n - 1 - PhalanxOrder(i, n)) * 0.05f;
                BroadcastFx(FxKind.PhalanxBreak, PhalanxWorld(z, -8f, PhalanxLane(i, n)), V2.FromAngle(z.Aim), new Rgba(0.93f, 0.9f, 0.8f), 1f + delay, PhalanxBreakFx + delay, Sfx.None);
                z.BoneHp[i] = 0f;
                z.Bones[i] = 0;
            }
            z.T = Math.Min(z.T, PhalanxCrumble);
            Sounds.Add(Sfx.Explode);
        }

        /// Host, from the enemy-bullet loop (Combat.cs): an enemy shot reaching a standing shield from the front stops there,
        /// red aimed shots too, and wears that skeleton.
        bool PhalanxStops(Bullet b)
        {
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.Phalanx || z.BoneHp == null || PhalanxCrumbling(z))
                    continue;
                V2 l = PhalanxLocal(z, b.Pos), was = PhalanxLocal(z, b.Pos - b.Vel * tickDt);
                if (l.X > PhalanxFace + b.Radius || was.X < -2f || l.X < -PhalanxBack || b.Vel.Dot(V2.FromAngle(z.Aim)) >= 0f)
                    continue;  // not at the face yet, came from behind (or is going the line's way: the line walked into it), or already past
                int lane = PhalanxLaneAt(z, l.Y);
                if (lane < 0 || !PhalanxStanding(z, lane))
                    continue;  // off the ends, or through a gap
                BroadcastFx(FxKind.PhalanxSpark, PhalanxWorld(z, PhalanxFace, l.Y), V2.FromAngle(z.Aim), new Rgba(1f, 0.94f, 0.78f), 0f, 0.3f, b.Dmg >= 25f ? Sfx.Clang : Sfx.None);
                WearBone(z, lane, b.Dmg * EnemyDamageScale(Wave));
                return true;
            }
            return false;
        }

        /// The smart bot's aim for it: the way, within a half turn of where it faces, with the most enemies in a 60-degree
        /// cone out to 480 px (nearer ones count a little more). Falls back to its target.
        V2 PhalanxBotAim(Player me, Enemy target)
        {
            float best = 0f, bestA = target != null ? (target.Pos - me.Pos).Angle : me.Aim;
            for (int k = -8; k <= 8; k++)
            {
                float a = me.Aim + k * (MathX.Pi / 16f), score = 0f;
                foreach (var e in Enemies.Values)
                {
                    if (!Targetable(e))
                        continue;
                    V2 off = e.Pos - me.Pos;
                    float d = off.Length;
                    if (d > 480f || d < 1f)
                        continue;
                    float da = Math.Abs(MathX.Fmod(off.Angle - a + MathX.Pi, MathX.Tau) - MathX.Pi);
                    if (da < 0.52f)
                        score += (1f + (480f - d) / 960f) * (1f - 0.5f * da / 0.52f);  // (nearer the cone's middle counts more, so it centres on the group)
                }
                if (score > best + 0.01f)
                {
                    best = score;
                    bestA = a;
                }
            }
            return me.Pos + V2.FromAngle(bestA) * 300f;
        }
    }
}
