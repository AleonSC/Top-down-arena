using System;
using System.Collections.Generic;

namespace Arena
{
    /// How well a bot plays (Boss Lab). Casual reacts late and dodges loosely; Expert reads bullets
    /// almost as they appear and threads tight gaps.
    public enum BotSkill : byte { Off, Casual, Good, Expert }

    /// A bot that plays like a person: it dodges by predicting where bullets, enemy bodies, lightning
    /// strikes and the Reapers' tether will be over the next half second, keeps a distance that suits
    /// its role and weapon, leads its shots, revives downed allies and uses abilities when they make sense.
    /// Runs on the bot's own game (host or client) with the same information a player has.
    /// What the smart bot knows when it decides whether to use an ability (each AbilityDef.Bot reads it).
    public struct BotSense
    {
        public Player Me;
        public bool Danger;      // in danger: a risky spot or low health
        public bool AllyHurt;    // you or an ally is hurt, or one is down
        public bool AllyDown;
        public bool HaveTarget;
        public float TargetD;    // distance to its target (9999 without one)
        public int Players;      // players in the match
        public float BotDanger;  // how dangerous its best spot looked (above 2.5: get out)
    }

    public partial class Game
    {
        public BotSkill BotLevel;  // Off = the simple test bot (walks in circles, shoots the nearest enemy)

        /// Human limits: a person reacts late, plans a short way ahead, can only keep track of the nearest
        /// few bullets, changes direction a few times a second, and moves in 8 directions (WASD).
        struct SkillParams
        {
            public float Reaction, Lookahead, Margin, AimJitter, Think, Noise;
            public int Steps, Attention;
        }

        static SkillParams Skill(BotSkill s)
        {
            switch (s)
            {
                case BotSkill.Casual: return new SkillParams { Reaction = 0.35f, Lookahead = 0.3f, Steps = 6, Margin = 2f, AimJitter = 0.12f, Think = 0.22f, Noise = 14f, Attention = 6 };
                case BotSkill.Expert: return new SkillParams { Reaction = 0.15f, Lookahead = 0.5f, Steps = 10, Margin = 6f, AimJitter = 0.03f, Think = 0.08f, Noise = 4f, Attention = 25 };
                default: return new SkillParams { Reaction = 0.24f, Lookahead = 0.4f, Steps = 8, Margin = 4f, AimJitter = 0.07f, Think = 0.14f, Noise = 8f, Attention = 12 };
            }
        }

        // Brain state (one bot per game).
        readonly Dictionary<int, float> botSeen = new Dictionary<int, float>();      // bullet id -> when this bot first saw it
        readonly Dictionary<int, V2> botLastPos = new Dictionary<int, V2>();         // enemy id -> position last frame (for leading)
        readonly Dictionary<int, V2> botEnemyVel = new Dictionary<int, V2>();
        readonly bool[] botHeldLast = new bool[Upgrades.SlotCount];
        V2 botMove;
        float botThinkT, botClock;
        public float BotDanger;  // how dangerous the bot's best option looked (for the lab's readout)

        /// Roughly how fast this weapon's shots fly (for leading). Where it wants to be fought from is
        /// WeaponDef.BotRange.
        static float ShotSpeedFor(WeaponId w)
        {
            switch (w)
            {
                case WeaponId.FlameBurst: return 430f;
                case WeaponId.MendersBow: return 950f;
                case WeaponId.Railgun: return 2000f;
                case WeaponId.Fireball: return 450f;
                case WeaponId.Implosion: return 1e6f;  // instant: aim right at them
                default: return 700f;
            }
        }

        void SmartBot(Player me, float dt, ref V2 move, ref V2 aimAt, ref bool firing, bool[] held)
        {
            var sk = Skill(BotLevel);
            botClock += dt;
            TrackEnemies(dt);
            if (me.Downed)
            {
                move = V2.Zero;
                firing = false;
                for (int i = 0; i < held.Length; i++)
                    held[i] = botHeldLast[i] = false;
                return;
            }

            // --- Target: the boss (or the Reaper that can be hurt), unless something small is right on top of us.
            Enemy target = null, close = null;
            float bestScore = float.MaxValue, closeD = 170f;
            foreach (var e in Enemies.Values)
            {
                if (IsSpawning(e) || SoulBound(e) || e.Hp <= 0f)
                    continue;
                float d = e.Pos.DistanceTo(me.Pos);
                float score = e.Def.IsBoss ? d * 0.25f + e.Hp / Math.Max(1f, e.MaxHp) * 200f : d;
                if (score < bestScore)
                {
                    bestScore = score;
                    target = e;
                }
                if (!e.Def.IsBoss && d < closeD)
                {
                    closeD = d;
                    close = e;
                }
            }
            // Adds that protect the boss come first, like a player would learn to: the wells of a cage
            // closing in, the Lich's phylacteries, the Singularity's corner wells and the Hive Queen's shield drones.
            Enemy key = null;
            bool caged = false;
            foreach (var c in Cages.Values)
                caged |= c.PlayerId == me.Id;
            foreach (var e in Enemies.Values)
            {
                if (IsSpawning(e) || e.Hp <= 0f)
                    continue;
                bool cageWell = e.Type == EnemyType.CageWell;
                if (caged && !cageWell)
                    continue;
                if ((cageWell || e.Type == EnemyType.Phylactery || e.Type == EnemyType.GravityWell || e.Type == EnemyType.Shard)
                    && (key == null || e.Pos.DistanceSq(me.Pos) < key.Pos.DistanceSq(me.Pos)))
                    key = e;
            }
            if (key != null)
                target = key;
            Enemy shootAt = close ?? target;

            // --- Where we'd like to be.
            Player downed = null, lowest = null;
            foreach (var q in Players.Values)
            {
                if (q.Id == me.Id)
                    continue;
                if (q.Downed && (downed == null || q.Pos.DistanceSq(me.Pos) < downed.Pos.DistanceSq(me.Pos)))
                    downed = q;
                if (!q.Downed && (lowest == null || q.Hp / q.MaxHp < lowest.Hp / lowest.MaxHp))
                    lowest = q;
            }
            float pref = me.Weapon.BotRange - (me.Role == Role.Tank ? 40f : 0f);
            if (me.Weapon.Melee)  // in swinging distance of the target's edge
                pref = MeleeReach(me, MeleeRange(me.Weapon.Id)) * 0.7f + (target != null ? target.Radius : 0f);
            V2 anchor = target != null ? target.Pos : new V2(Width / 2f, Height / 2f);
            if (downed != null)
            {
                anchor = downed.Pos;  // go pick them up
                pref = 0f;
            }

            // --- Dodge: score each way we could move over the next fraction of a second.
            botThinkT -= dt;
            if (botThinkT <= 0f)
            {
                botThinkT = sk.Think;
                botMove = ChooseMove(me, sk, anchor, pref, me.Role == Role.Healer && downed == null ? lowest : null, out BotDanger);
            }
            move = botMove;

            // --- Aim: lead the target, with a skill-based wobble.
            if (shootAt != null)
            {
                V2 vel;
                botEnemyVel.TryGetValue(shootAt.Id, out vel);
                float flight = shootAt.Pos.DistanceTo(me.Pos) / ShotSpeedFor(me.Weapon.Id);
                V2 lead = shootAt.Pos + vel * Math.Min(0.8f, flight);
                float wobble = (float)Math.Sin(botClock * 2.3f + me.Id) * sk.AimJitter;
                aimAt = me.Pos + V2.FromAngle((lead - me.Pos).Angle + wobble) * Math.Max(40f, lead.DistanceTo(me.Pos));
                me.Aim = (aimAt - me.Pos).Angle;
            }

            // --- Fire: hold the trigger; the bow draws to full, then lets go.
            firing = shootAt != null;
            if (me.Weapon.Id == WeaponId.MendersBow && bowDraw >= BowFull(me))
                firing = false;

            // --- Abilities.
            for (int i = 0; i < held.Length; i++)
                held[i] = false;
            bool inDanger = BotDanger > 1.5f || me.Hp < me.MaxHp * 0.35f;
            bool allyHurt = me.Hp < me.MaxHp * 0.55f || downed != null || lowest != null && lowest.Hp < lowest.MaxHp * 0.55f;
            float targetD = target != null ? target.Pos.DistanceTo(me.Pos) : 9999f;
            for (int slot = 0; slot < Upgrades.SlotCount; slot++)
            {
                var s = me.Slots[slot];
                if (s.Id == AbilityId.None || s.Charges <= 0 || botHeldLast[slot])
                {
                    botHeldLast[slot] = false;  // release so the next press counts
                    continue;
                }
                if (!WantAbility(s.Id, me, inDanger, allyHurt, downed != null, targetD, target != null))
                    continue;
                held[slot] = botHeldLast[slot] = true;
                if (s.Id == AbilityId.Blink)
                    aimAt = me.Pos + (botMove.IsZero ? V2.FromAngle(me.Aim) : botMove.Normalized()) * 220f;  // blink toward safety
                else if (target != null && s.Id != AbilityId.Mark)
                    aimAt = target.Pos;
                break;  // one ability per frame
            }
        }

        bool WantAbility(AbilityId id, Player me, bool danger, bool allyHurt, bool allyDown, float targetD, bool haveTarget)
        {
            var rule = Upgrades.Ability(id).Bot;
            if (rule == null)
                return haveTarget && targetD < 520f;  // most abilities hurt the boss: use it when it's in reach
            return rule(new BotSense
            {
                Me = me, Danger = danger, AllyHurt = allyHurt, AllyDown = allyDown, TargetD = targetD, HaveTarget = haveTarget,
                Players = Players.Count, BotDanger = BotDanger,
            });
        }

        /// Enemy velocities, from how far each moved since last frame (smoothed).
        void TrackEnemies(float dt)
        {
            if (dt <= 0f)
                return;
            foreach (var e in Enemies.Values)
            {
                V2 last;
                if (botLastPos.TryGetValue(e.Id, out last))
                {
                    V2 v = (e.Pos - last) / dt;
                    V2 old;
                    botEnemyVel[e.Id] = botEnemyVel.TryGetValue(e.Id, out old) ? V2.Lerp(old, v, 0.2f) : v;
                }
                botLastPos[e.Id] = e.Pos;
            }
            if (botLastPos.Count > Enemies.Count + 50)
            {
                var gone = new List<int>();
                foreach (var id in botLastPos.Keys)
                    if (!Enemies.ContainsKey(id))
                        gone.Add(id);
                foreach (var id in gone)
                {
                    botLastPos.Remove(id);
                    botEnemyVel.Remove(id);
                }
            }
        }

        static readonly List<Bullet> botThreats = new List<Bullet>();

        /// Tries the 8 WASD directions and standing still, and returns the one with
        /// the least predicted danger plus a small cost for being far from where we'd like to be.
        V2 ChooseMove(Player me, SkillParams sk, V2 anchor, float pref, Player stayNear, out float danger)
        {
            float speed = me.Speed * (bowDraw >= 0f ? BowDrawSlow : 1f);
            float T = sk.Lookahead, step = T / sk.Steps;
            float hitR = me.Def.HitR;

            // Bullets we've noticed (a bullet has to be on screen for the reaction time first).
            botThreats.Clear();
            float reach = speed * T + 60f;
            foreach (var b in Bullets.Values)
            {
                if (!Defs.IsEnemyShot(b.Kind))
                    continue;
                float seen;
                if (!botSeen.TryGetValue(b.Id, out seen))
                    botSeen[b.Id] = seen = botClock;
                if (botClock - seen < sk.Reaction)
                    continue;
                if (b.Pos.DistanceTo(me.Pos) < reach + b.Vel.Length * T)
                    botThreats.Add(b);
            }
            if (botThreats.Count > sk.Attention)
            {
                V2 at = me.Pos;
                botThreats.Sort((x, y) => x.Pos.DistanceSq(at).CompareTo(y.Pos.DistanceSq(at)));
                botThreats.RemoveRange(sk.Attention, botThreats.Count - sk.Attention);
            }
            // Misjudging where things are by a few pixels.
            float misjudge = (float)Math.Sin(botClock * 7.1f + me.Id * 3f) * sk.Noise;
            if (botSeen.Count > Bullets.Count + 400)
            {
                var gone = new List<int>();
                foreach (var id in botSeen.Keys)
                    if (!Bullets.ContainsKey(id))
                        gone.Add(id);
                foreach (var id in gone)
                    botSeen.Remove(id);
            }

            // The Singularity's lasers (once they're warned) and cage walls.
            Enemy sing = null;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Singularity && !IsSpawning(e) && (e.Phase == SingLaserWarn || e.Phase == SingLasers))
                    sing = e;

            // The Reapers' tether.
            Enemy twinA = null, twinB = null;
            foreach (var e in Enemies.Values)
                if (IsTwin(e.Type) && !IsSpawning(e) && e.Hp > 0f)
                {
                    if (twinA == null) twinA = e;
                    else twinB = e;
                }

            V2 best = V2.Zero;
            float bestCost = float.MaxValue, bestDanger = 0f;
            for (int c = 0; c < 9; c++)
            {
                V2 dir = c == 8 ? V2.Zero : V2.FromAngle(MathX.Tau * c / 8f);  // WASD: 8 directions or standing still
                float d = 0f;
                V2 p = me.Pos;
                for (int k = 1; k <= sk.Steps; k++)
                {
                    float t = k * step;
                    p = me.Pos + dir * speed * t;
                    if (PullStrength > 0f)
                        p += (PullCenter - me.Pos).Normalized() * PullStrength * t;
                    float w = 1.6f - t / T;  // sooner hits matter more
                    foreach (var b in botThreats)
                    {
                        V2 bp = b.AngVel == 0f ? b.Pos + b.Vel * t : b.Pos + b.Vel.Rotated(b.AngVel * t * 0.5f) * t;
                        float r = hitR + b.Radius + sk.Margin + misjudge;
                        float dd = bp.DistanceSq(p);
                        if (dd < r * r)
                            d += w * (1f + (r - (float)Math.Sqrt(dd)) / r);
                    }
                    foreach (var e in Enemies.Values)
                    {
                        if (IsSpawning(e))
                            continue;
                        V2 ev;
                        botEnemyVel.TryGetValue(e.Id, out ev);
                        float r = e.Radius + hitR + 10f + (e.Type == EnemyType.HiveBomber ? BomberBlast : 0f);
                        if ((e.Pos + ev * t).DistanceSq(p) < r * r)
                            d += w * 2f;
                    }
                    foreach (var h in Hazards.Values)
                        if (h.T < t + 0.2f && h.Pos.DistanceTo(p) < h.Radius + hitR + 8f)
                            d += w * 3f;
                    foreach (var pt in Patches.Values)  // a path's fire: burning now or about to (Paths.cs)
                        if (pt.Kind == PatchKind.Fire && pt.Age + t + 0.2f >= pt.Warn && pt.Pos.DistanceTo(p) < pt.Radius + hitR + 8f)
                            d += w * 3f;
                    if (twinB != null && SegmentDistance(p, twinA.Pos, twinB.Pos) < TetherWidth / 2f + hitR + 10f)
                        d += w * 2f;
                    if (sing != null)
                        for (int l = 0; l < LaserCount; l++)
                            if (OnLaser(sing.Pos, sing.Facing + (sing.Phase == SingLasers ? LaserSpin * t : 0f) + MathX.Tau * l / LaserCount, p, LaserWidth / 2f + hitR + sk.Margin + 4f))
                            {
                                d += w * 2f;
                                break;
                            }
                    foreach (var cg in Cages.Values)
                        if (Math.Abs(p.DistanceTo(cg.Center) - Math.Max(0f, cg.Radius - CageShrink * t)) < CageWallWidth / 2f + hitR + sk.Margin + 4f)
                            d += w * 2f;
                }
                // Moving off the arena wraps you around: fine to escape that way, but don't plan on it. Narrow Pass's
                // walls can't be crossed, and hugging them leaves no room to dodge.
                if (Walled)
                {
                    V2 lo = ArenaMin, hi = ArenaMax;
                    float room = Math.Min(Math.Min(p.X - lo.X, hi.X - p.X), Math.Min(p.Y - lo.Y, hi.Y - p.Y));
                    if (room < 50f)
                        d += 0.6f * (50f - room) / 50f;
                }
                else if (p.X < 20f || p.Y < 20f || p.X > Width - 20f || p.Y > Height - 20f)
                    d += 0.2f;
                float dist = p.DistanceTo(anchor);
                float position = 0.35f * Math.Min(4f, Math.Abs(dist - pref) / 200f) * (pref == 0f ? 3f : 1f);
                if (stayNear != null)
                    position += 0.25f * Math.Max(0f, p.DistanceTo(stayNear.Pos) - 220f) / 200f;
                float steady = (dir - botMove).Length * 0.04f;  // don't jitter between equal options
                if (me.Hp < me.MaxHp * 0.7f)  // hurt: Sanctuary's healing pools are worth standing in (Paths.cs)
                    foreach (var pt in Patches.Values)
                        if (pt.Kind == PatchKind.Heal && pt.Pos.DistanceTo(p) < pt.Radius - 10f)
                            position -= 0.3f;
                float cost = d + position + steady;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    best = dir;
                    bestDanger = d;
                }
            }
            danger = bestDanger;
            return best;
        }

        static float SegmentDistance(V2 p, V2 a, V2 b)
        {
            V2 ab = b - a;
            float len2 = ab.LengthSq;
            if (len2 < 1f)
                return p.DistanceTo(a);
            float t = MathX.Clamp01(((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2);
            return p.DistanceTo(a + ab * t);
        }
    }
}
