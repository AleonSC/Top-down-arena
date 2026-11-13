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
        public bool AllyThreatened;  // a hurt or downed ally has enemies close on them (Roar draws those enemies off)
        public float DownedD;    // distance to the nearest downed ally (9999 with none)
        public bool TargetMarked;  // its target already carries a Mark for Death
        public int NearEnemies;    // enemies within BotNearReach of it (Bloodbath wants several)
        public int Crowd;        // enemies within Game.JuggBotRange of it (the Juggernaut Suit's rule; only counted for that one)
        public int Shooters;       // how many enemies have shots in flight coming at it (Lightning Rod)
    }

    public partial class Game
    {
        public BotSkill BotLevel;  // Off = the simple test bot (walks in circles, shoots the nearest enemy); F1 "Bot plays for you" uses Good

        /// Human limits: a person reacts late, plans a short way ahead, can only keep track of the nearest
        /// few bullets, changes direction a few times a second, and moves in 8 directions (WASD).
        struct SkillParams
        {
            public float Reaction, Lookahead, Margin, AimJitter, Think, Noise, Turn;  // Turn: how fast the aim swings, radians a second
            public int Steps, Attention;
        }

        static SkillParams Skill(BotSkill s)
        {
            switch (s)
            {
                case BotSkill.Casual: return new SkillParams { Reaction = 0.35f, Lookahead = 0.3f, Steps = 6, Margin = 2f, AimJitter = 0.12f, Think = 0.22f, Noise = 14f, Turn = 4f, Attention = 6 };
                case BotSkill.Expert: return new SkillParams { Reaction = 0.15f, Lookahead = 0.5f, Steps = 10, Margin = 6f, AimJitter = 0.03f, Think = 0.08f, Noise = 4f, Turn = 10f, Attention = 25 };
                default: return new SkillParams { Reaction = 0.24f, Lookahead = 0.4f, Steps = 8, Margin = 4f, AimJitter = 0.07f, Think = 0.14f, Noise = 8f, Turn = 6.5f, Attention = 12 };
            }
        }

        // Brain state (one bot per game).
        readonly Dictionary<int, float> botSeen = new Dictionary<int, float>();      // bullet id -> when this bot first saw it
        readonly Dictionary<int, V2> botLastPos = new Dictionary<int, V2>();         // enemy id -> position last frame (for leading)
        readonly Dictionary<int, V2> botEnemyVel = new Dictionary<int, V2>();
        readonly bool[] botHeldLast = new bool[Upgrades.SlotCount];
        V2 botMove;
        float botThinkT, botClock, botAim;
        bool botAimSet;  // botAim: where the bot's aim really points (Game.cs resets Aim from the input each tick)
        public float BotDanger;  // how dangerous the bot's best option looked (for the lab's readout)

        /// Roughly how fast this weapon's shots fly (for leading). Where it wants to be fought from is
        /// WeaponDef.BotRange.
        static float ShotSpeedFor(WeaponId w)
        {
            switch (w)
            {
                case WeaponId.Flamethrower: return 430f;
                case WeaponId.MendersBow: return 950f;
                case WeaponId.Railgun: return 2000f;
                case WeaponId.Fireball: return 450f;
                case WeaponId.Implosion: return 1e6f;  // instant: aim right at them
                default: return 700f;
            }
        }

        /// Is this enemy close enough to shoot: within the weapon's reach (to its edge), or where the bot likes to stand, and a little over.
        bool InReach(Player me, Enemy e, float pref)
        {
            float reach = Math.Max(WeaponRange(me), 150f) + e.Radius;
            return me.Pos.DistanceTo(e.Pos) <= Math.Max(reach, pref) + 40f;
        }

        const float MarkFocus = 220f;  // how much a marked enemy is preferred as the target (a boss scores 0 to 300)

        /// Swings me.Aim toward `want` by at most `maxStep` radians; true once it's within ~0.25 rad (close enough to fire).
        bool TurnAim(Player me, float want, float maxStep)
        {
            float diff = MathX.Fmod(want - me.Aim + MathX.Pi, MathX.Tau) - MathX.Pi;
            me.Aim += Math.Max(-maxStep, Math.Min(maxStep, diff));
            botAim = me.Aim;
            return Math.Abs(diff) - maxStep < 0.25f;
        }

        /// Is there a clear line between two points, no wall or pillar in the way? (Enemies don't block it.)
        bool BotSees(V2 a, V2 b)
        {
            float d = a.DistanceTo(b);
            for (float t = 20f; t < d; t += 20f)
                if (NearSolid(a + (b - a) * (t / d), 3f))
                    return false;
            return true;
        }

        void SmartBot(Player me, float dt, ref V2 move, ref V2 aimAt, ref bool firing, bool[] held)
        {
            var sk = Skill(BotLevel);
            botClock += dt;
            if (!botAimSet)
            {
                botAim = me.Aim;
                botAimSet = true;
            }
            me.Aim = botAim;
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
            bool inVault = VaultOn && CurrentWorld.AreaAt(me.Pos) == Rooms.Vault;
            foreach (var e in Enemies.Values)
            {
                if (IsSpawning(e) || SoulBound(e) || e.Hp <= 0f)
                    continue;
                if (!inVault && InVault(e))
                    continue;  // the Rooms arena's vault is optional: the bot leaves it alone (Rooms.cs)
                float d = e.Pos.DistanceTo(me.Pos);
                float score = e.Def.IsBoss ? d * 0.25f + e.Hp / Math.Max(1f, e.MaxHp) * 200f : d;
                if (e.Marked || e.MarkT > 0f)
                    score -= MarkFocus;  // the team's Mark for Death: everyone piles onto it
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
                if ((cageWell || e.Type == EnemyType.Phylactery || e.Type == EnemyType.GravityWell || e.Type == EnemyType.Shard || e.Type == EnemyType.SingShard || e.Type == EnemyType.BallLightning || e.Type == EnemyType.Morsel)
                    && (key == null || e.Pos.DistanceSq(me.Pos) < key.Pos.DistanceSq(me.Pos)))
                    key = e;
            }
            if (key != null)
                target = key;
            Enemy shootAt = close ?? target;
            // No shooting through walls: with a wall in the way, take the nearest enemy it can actually see (or none).
            if (shootAt != null && !BotSees(me.Pos, shootAt.Pos))
            {
                Enemy seen = null;
                foreach (var e in Enemies.Values)
                    if (!IsSpawning(e) && !SoulBound(e) && e.Hp > 0f && (seen == null || e.Pos.DistanceSq(me.Pos) < seen.Pos.DistanceSq(me.Pos)) && BotSees(me.Pos, e.Pos))
                        seen = e;
                shootAt = seen;
            }
            bool aimSettled = true;  // false while the aim is still swinging round: hold fire until it's on

            // --- Where we'd like to be.
            Player downed = null, lowest = null;
            foreach (var q in Players.Values)
            {
                if (q.Id == me.Id)
                    continue;
                if (q.Downed && (downed == null || q.Pos.DistanceSq(me.Pos) < downed.Pos.DistanceSq(me.Pos)))
                    downed = q;
                if (!q.Downed && MoreHurt(q, lowest))
                    lowest = q;
            }
            float pref = me.Weapon.BotRange - (me.Role == Role.Tank ? 40f : 0f);
            if (me.Weapon.Melee)  // in swinging distance of the target's edge
                pref = MeleeReach(me, MeleeRange(me.Weapon.Id)) * 0.7f + (target != null ? target.Radius : 0f);
            if (downed != null && RoomsOn)
            {
                int mine = CurrentWorld.AreaAt(me.Pos);
                if (AreaLocked(mine) && CurrentWorld.AreaAt(downed.Pos) != mine)
                    downed = null;  // locked in a room (Rooms.cs): clear it first, the door won't let you out to them
            }
            V2 anchor = target != null ? target.Pos : new V2(Width / 2f, Height / 2f);
            if (downed != null)
            {
                anchor = downed.Pos;  // go pick them up
                pref = 0f;
            }

            // --- Shelter: a Tank's Last Stand stops every shot from the front, so stand in the lee of its plates: just behind
            // the tank, on the side away from where the barrier faces (nobody to revive first). Teammates' shots pass out through it.
            Player stander = null;
            foreach (var q in Players.Values)
                if (q.Id != me.Id && q.StandOn && !q.Downed && (stander == null || q.Pos.DistanceSq(me.Pos) < stander.Pos.DistanceSq(me.Pos)))
                    stander = q;
            if (stander != null && downed == null && !me.StandOn)
            {
                anchor = stander.Pos - V2.FromAngle(stander.StandAim) * (StandRadius * 0.4f);
                pref = 0f;
            }

            // --- The Rooms arena (Rooms.cs): with a wall between us and where we're going, head for the next corner or
            // doorway on the way round instead (the enemies' steering, LayoutRules.cs).
            V2 routeWay = V2.Zero;
            if (RoomsOn && anchor.DistanceTo(me.Pos) > pref + 40f)
            {
                V2 straight = (anchor - me.Pos).Normalized();
                V2 way = SteerDir(-me.Id, me.Pos, botClock, me.Def.BodyR + 4f, anchor, straight);
                if (way.DistanceTo(straight) > 0.01f)
                {
                    anchor = me.Pos + way * 140f;
                    pref = 0f;
                    routeWay = way;
                }
            }

            // --- Dodge: score each way we could move over the next fraction of a second.
            botThinkT -= dt;
            if (botThinkT <= 0f)
            {
                botThinkT = sk.Think;
                botMove = ChooseMove(me, sk, anchor, pref, me.Role == Role.Healer && downed == null ? lowest : null, out BotDanger);
            }
            move = botMove;
            if (!routeWay.IsZero && BotDanger < 1.2f)
                move = routeWay;  // on the way round a wall and nothing to dodge: walk the route (the dodge scoring would rub on door jambs)
            move = AroundProps(me, move);

            // --- Aim: lead the target, with a skill-based wobble.
            if (shootAt != null)
            {
                V2 vel;
                botEnemyVel.TryGetValue(shootAt.Id, out vel);
                float flight = shootAt.Pos.DistanceTo(me.Pos) / ShotSpeedFor(me.Weapon.Id);
                V2 lead = shootAt.Pos + vel * Math.Min(0.8f, flight);
                float wobble = (float)Math.Sin(botClock * 2.3f + me.Id) * sk.AimJitter;
                float dist = Math.Max(40f, lead.DistanceTo(me.Pos));
                aimSettled = TurnAim(me, (lead - me.Pos).Angle + wobble, sk.Turn * dt);
                aimAt = me.Pos + V2.FromAngle(me.Aim) * dist;
            }

            // --- Reach: don't shoot from across the arena. Only fire once the target is within the weapon's reach
            // (or where the bot likes to stand with it); until then keep closing in, and use anything already in reach.
            if (shootAt != null && !InReach(me, shootAt, pref))
            {
                Enemy inReach = null;
                foreach (var e in Enemies.Values)
                    if (!IsSpawning(e) && !SoulBound(e) && e.Hp > 0f && InReach(me, e, pref)
                        && (inReach == null || e.Pos.DistanceSq(me.Pos) < inReach.Pos.DistanceSq(me.Pos)))
                        inReach = e;
                if (inReach != null)
                {
                    shootAt = inReach;
                    V2 vel;
                    botEnemyVel.TryGetValue(shootAt.Id, out vel);
                    V2 lead = shootAt.Pos + vel * Math.Min(0.8f, shootAt.Pos.DistanceTo(me.Pos) / ShotSpeedFor(me.Weapon.Id));
                    aimSettled = TurnAim(me, (lead - me.Pos).Angle, sk.Turn * dt);
                    aimAt = me.Pos + V2.FromAngle(me.Aim) * Math.Max(40f, lead.DistanceTo(me.Pos));
                }
            }

            // --- Fire: hold the trigger; the bow draws to full, then lets go. Not into a hungry Devourer, or its Maw.
            firing = shootAt != null && InReach(me, shootAt, pref) && !(shootAt.Type == EnemyType.Devourer && (Hungry(shootAt) || DevourImmune(shootAt)
                || shootAt.Devour != null && shootAt.Devour.Stage == DevourStage.Ravenous && InMaw(shootAt, me.Pos)));
            // --- Healing shots: an arrow heals the ally it passes and a dart heals the one it reaches, so a healer lines
            // the shot up on a hurt teammate close by (and sends it on toward the boss from there).
            if (me.Role == Role.Healer && (me.Weapon.Id == WeaponId.MendersBow || me.Weapon.Id == WeaponId.TransfusionDarts)
                && (me.Weapon.Id == WeaponId.MendersBow || me.Hp > me.MaxHp * 0.4f))  // a dart costs the healer health
            {
                Player healTo = null;
                foreach (var q in Players.Values)
                    if (q.Id != me.Id && !q.Downed && q.Hp < q.MaxHp * 0.7f && q.Pos.DistanceTo(me.Pos) < 420f
                        && MoreHurt(q, healTo))
                        healTo = q;
                if (healTo != null)
                {
                    bool on = BotSees(me.Pos, healTo.Pos) && TurnAim(me, (healTo.Pos - me.Pos).Angle, sk.Turn * dt);
                    aimAt = me.Pos + V2.FromAngle(me.Aim) * Math.Max(40f, healTo.Pos.DistanceTo(me.Pos));
                    firing = on;
                    aimSettled = true;
                }
            }
            if (!aimSettled)
                firing = false;
            if (ChargeWeapon(me.Weapon.Id) && bowDraw >= BowFull(me))
                firing = false;
            else if (me.Weapon.Id == WeaponId.ClotBolt && bowDraw >= BowMinDraw
                && ClotDistance(BowCharge(me, bowDraw)) * me.Stats.ShotSpeedMult >= aimAt.DistanceTo(me.Pos))
                firing = false;  // the Clot Cannon: let go once the clot would land on what it's aimed at

            // --- Abilities.
            for (int i = 0; i < held.Length; i++)
                held[i] = false;
            bool inDanger = BotDanger > 1.5f || me.Hp < me.MaxHp * 0.35f;
            bool allyHurt = me.Hp < me.MaxHp * 0.55f || downed != null || lowest != null && lowest.Hp < lowest.MaxHp * 0.55f;
            float targetD = target != null ? target.Pos.DistanceTo(me.Pos) : 9999f;
            float downedD = downed != null ? downed.Pos.DistanceTo(me.Pos) : 9999f;
            bool targetMarked = target != null && (target.Marked || target.MarkT > 0f);
            bool allyThreatened = AllyInTrouble(me);
            Player healSpot = downed ?? (lowest != null && lowest.Hp < lowest.MaxHp * 0.85f ? lowest : null);
            for (int slot = 0; slot < Upgrades.SlotCount; slot++)
            {
                var s = me.Slots[slot];
                if (s.Id == AbilityId.LastStand && me.StandOn)
                {
                    held[slot] = botHeldLast[slot] = false;
                    bool guarding = downed != null && downed.Pos.DistanceTo(me.Pos) < StandRadius;  // someone is being stood back up in its lee
                    if (me.StandT > 3f && BotDanger < 0.2f && !guarding && !botHeldLast[slot])  // nothing coming for a moment: step out of it
                        held[slot] = botHeldLast[slot] = true;
                    continue;
                }
                Zone bath;
                if (s.Id == AbilityId.Bloodbath && (bath = ActiveBath(me)) != null)
                {
                    held[slot] = botHeldLast[slot] = false;
                    if (BathAge(bath) > 1.5f && (BathWading(bath) == 0 || me.Hp < me.MaxHp * 0.25f))  // nothing wading, or bled enough: stop the channel
                        held[slot] = botHeldLast[slot] = true;
                    continue;
                }
                if (s.Id == AbilityId.BloodBoil && me.SprayOn)
                {
                    held[slot] = botHeldLast[slot] = false;
                    bool inReach = target != null && targetD < Game.SprayReachOf(me) + target.Radius;
                    if (me.SprayT > 0.5f && (!inReach || me.Hp < me.MaxHp * 0.3f))  // nothing in the jet, or bled enough: close the vein
                        held[slot] = botHeldLast[slot] = true;
                    continue;
                }
                if (s.Id == AbilityId.CrimsonPact && me.BloodWallOn)
                {
                    held[slot] = botHeldLast[slot] = false;
                    if (me.BloodWallClock > 1f && (me.Hp < me.MaxHp * 0.4f || BotDanger < 0.05f && target == null))  // bled enough, or nothing left to wall off: stop
                        held[slot] = botHeldLast[slot] = true;
                    continue;
                }
                if (s.Id == AbilityId.BloodShell && me.ShellOn)
                {
                    held[slot] = botHeldLast[slot] = false;
                    if (BotDanger < 0.1f && me.ShellT > 1.5f)  // nothing coming: press again to drop it (it bleeds you)
                        held[slot] = botHeldLast[slot] = true;
                    continue;
                }
                if (s.Id == AbilityId.None || s.Charges <= 0 || botHeldLast[slot])
                {
                    botHeldLast[slot] = false;  // release so the next press counts
                    continue;
                }
                if (!WantAbility(s.Id, me, inDanger, allyHurt, downed != null, targetD, target != null, allyThreatened, downedD, targetMarked))
                    continue;
                held[slot] = botHeldLast[slot] = true;
                if (s.Id == AbilityId.Blink)
                    aimAt = me.Pos + (botMove.IsZero ? V2.FromAngle(me.Aim) : botMove.Normalized()) * 220f;  // blink toward safety
                else if (s.Id == AbilityId.Adrenaline)
                    aimAt = (lowest != null && lowest != me ? lowest : me).Pos;  // the hurt ally, or itself
                else if (s.Id == AbilityId.BonePhalanx)
                    aimAt = PhalanxBotAim(me, target);  // the line marches at the biggest group in front (BonePhalanx.cs)
                else if (HealsWhereAimed(s.Id) && healSpot != null)
                    aimAt = healSpot.Pos;  // a medkit, rain, lantern, cross or cone lands where the hurt ally is
                else if (target != null)
                    aimAt = target.Pos;  // (a Mark goes on the target too: the boss, not the add that happens to be nearest)
                break;  // one ability per frame
            }
        }

        bool WantAbility(AbilityId id, Player me, bool danger, bool allyHurt, bool allyDown, float targetD, bool haveTarget,
            bool allyThreatened = false, float downedD = 9999f, bool targetMarked = false)
        {
            var rule = Upgrades.Ability(id).Bot;
            if (rule == null)
                return haveTarget && targetD < 520f;  // most abilities hurt the boss: use it when it's in reach
            return rule(new BotSense
            {
                Me = me, Danger = danger, AllyHurt = allyHurt, AllyDown = allyDown, TargetD = targetD, HaveTarget = haveTarget,
                Players = Players.Count, BotDanger = BotDanger,
                AllyThreatened = allyThreatened, DownedD = downedD, TargetMarked = targetMarked, NearEnemies = EnemiesNear(me.Pos, BotNearReach),
                Crowd = id == AbilityId.JuggernautSuit ? CrowdNear(me) : 0,
                Shooters = id == AbilityId.LightningRod ? ShootersAt(me) : 0,  // (only worked out for the one ability that asks)
            });
        }

        public const float BotNearReach = 170f;

        /// Enemies (up and fighting) within reach of pos.
        int EnemiesNear(V2 pos, float reach)
        {
            int n = 0;
            foreach (var e in Enemies.Values)
                if (Targetable(e) && !e.Dummy && e.Pos.DistanceTo(pos) < reach + e.Radius)
                    n++;
            return n;
        }

        /// Abilities that heal where you aim them: a bot casts these at the ally who needs it, not at the boss.
        static bool HealsWhereAimed(AbilityId id)
        {
            switch (id)
            {
                case AbilityId.Medkit:
                case AbilityId.HealingRain:
                case AbilityId.SoulLantern:
                case AbilityId.Consecrate:
                case AbilityId.Sanctify:
                    return true;
            }
            return false;
        }

        /// Is a teammate hurt or down with enemies right on top of them? (Roar draws those enemies off.)
        bool AllyInTrouble(Player me)
        {
            foreach (var q in Players.Values)
            {
                if (q.Id == me.Id || !q.Downed && q.Hp >= q.MaxHp * 0.5f)
                    continue;
                foreach (var e in Enemies.Values)
                    if (e.Hp > 0f && !IsSpawning(e) && e.Pos.DistanceSq(q.Pos) < 220f * 220f)
                        return true;
            }
            return false;
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
        static readonly List<StompRing> stompRings = new List<StompRing>();
        static readonly List<ColCrack> quarryCracks = new List<ColCrack>();

        /// Where a stomp ring's band will be in `t` s covers `p` (outside a gap): the Colossus's and a Stomper's.
        static bool RingDanger(StompRing r, V2 p, float t, float pad)
        {
            float front = r.Radius + RingSpeed * Math.Max(0f, t - r.Warn), back = front - (GateRows - 1) * GateRowGap;
            float dc = p.DistanceTo(r.Center);
            return dc > back - pad && dc < front + pad && !InRingGap(r, p, pad);
        }

        /// Tries the 8 WASD directions and standing still, and returns the one with
        /// the least predicted danger plus a small cost for being far from where we'd like to be.
        /// A bot walking into a crate, barrel or chest turns to the nearest way past it (enemies steer round props in
        /// PropDir; players would just walk round). Only when one is right in the way.
        V2 AroundProps(Player me, V2 move)
        {
            if (move.IsZero || Props.Count == 0)
                return move;
            float r = me.Def.BodyR + 4f;
            Func<V2, bool> blocked = dir =>
            {
                V2 ahead = me.Pos + dir * 28f;
                foreach (var pr in Props.Values)
                    if (Solid(pr) && pr.Hp > 0f && PropCovers(pr, ahead, r))
                        return true;
                return false;
            };
            V2 want = move.Normalized();
            if (!blocked(want))
                return move;
            foreach (float turn in new[] { 0.5f, -0.5f, 1f, -1f, 1.5f, -1.5f })
            {
                V2 d = V2.FromAngle(want.Angle + turn);
                if (!blocked(d))
                    return d * move.Length;
            }
            return move;
        }

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
            StitchLinks(Bullets.Values, botThreads);  // Stitcher threads (Variety.cs): treated as standing still
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

            // The Singularity's lasers (once they're warned), its accretion disk and cage walls.
            Enemy sing = null, disk = null;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Singularity && !IsSpawning(e))
                {
                    if (e.Phase == SingLaserWarn || e.Phase == SingLasers)
                        sing = e;
                    if (DiskUp(e))
                        disk = e;
                }
            int lasers = sing != null ? SingLaserCount(sing) : 0;
            float laserSpin = sing != null ? SingLaserSpin(sing) : 0f;

            // The Colossus's stomp rings, landings, fissures and charge (Colossus.cs).
            var col = TheColossus()?.Colossus;
            Enemy colossus = col != null ? TheColossus() : null;
            stompRings.Clear();
            quarryCracks.Clear();
            foreach (var se in Enemies.Values)
            {
                if (se.Stomp != null && se.Type == EnemyType.Stomper)
                    stompRings.Add(se.Stomp);  // Stompers' small rings (QuarryFaction.cs), read like the Colossus's
                if (se.Crack != null)
                    quarryCracks.Add(se.Crack);  // Drillers' and Geomancers' cracks, like its fissures
            }

            // The Reapers' tether.
            Enemy twinA = null, twinB = null;
            foreach (var e in Enemies.Values)
                if (IsTwin(e.Type) && !IsSpawning(e) && e.Hp > 0f)
                {
                    if (twinA == null) twinA = e;
                    else twinB = e;
                }

            // The Storm Herald: its rods' arcs, ball lightning, the spokes, the Conductor and the wind (Herald.cs).
            Enemy herald = null;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.StormHerald && e.Herald != null && !IsSpawning(e) && e.Herald.TransT <= 0f)
                    herald = e;
            var hs = herald?.Herald;
            botBalls.Clear();
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.BallLightning && !IsSpawning(e))
                    botBalls.Add(e);
            V2 windDrift = HeraldWind(WindPush) + DevourPull(me.Pos);
            // The Devourer: its lunge lines, the inhale cone, the eruption, its Maw and the last stand's teeth (Devourer.cs).
            Enemy dev = null;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Devourer && e.Devour != null && !IsSpawning(e) && e.Devour.TransT <= 0f)
                    dev = e;
            var ds = dev?.Devour;
            Player conductor = null;
            if (hs != null && hs.CondId != 0)
                Players.TryGetValue(hs.CondId, out conductor);

            // The Eclipse: flares, closing rings, the echoes' beams and disk, the flash (hide in the moon's shadow),
            // the Umbra's sunspots and Last Light's burning edge (Eclipse.cs).
            Enemy eclipse = TheEclipse();
            var es = eclipse != null && eclipse.Eclipse.TransT <= 0f ? eclipse.Eclipse : null;
            V2 shadowSpot = V2.Zero, sunspot = V2.Zero;
            bool hide = es != null && es.FlashT > 0f && es.MoonD > 0f, seekLight = es != null && es.Stage == EclipseStage.Umbra && es.Spots.Count > 0;
            if (hide)
            {
                // Deeper in the shadow than the moon, a little apart from the others; where it'll be when the flash goes.
                V2 sp = eclipse.Pos + V2.FromAngle(es.MoonA) * (es.MoonD + 110f + (me.Id % 3) * 50f);
                float m = Math.Max(40f, es.Inset + 30f);
                shadowSpot = new V2(Math.Max(m, Math.Min(Width - m, sp.X)), Math.Max(m, Math.Min(Height - m, sp.Y)));
            }
            if (seekLight)
            {
                sunspot = es.Spots[0].Pos;
                foreach (var spot in es.Spots)
                    if (spot.Pos.DistanceSq(me.Pos) < sunspot.DistanceSq(me.Pos))
                        sunspot = spot.Pos;
            }

            // Hurt: healing orbs and the team's healing zones (rain, tree, pools, wards, ...) are worth walking to.
            bool lure = false;
            V2 lurePos = V2.Zero;
            float lureR = 0f, lureD = 450f;
            if (me.Hp < me.MaxHp * 0.7f)
            {
                foreach (var o in Orbs.Values)
                {
                    float od = o.Pos.DistanceTo(me.Pos) - 30f;
                    if (od < lureD) { lureD = od; lurePos = o.Pos; lureR = 30f; lure = true; }
                }
                foreach (var z in Zones.Values)
                {
                    if (z.Kind == ZoneKind.LumenWard || z.Def.HealRate <= 0f && z.Kind != ZoneKind.Sanctum)
                        continue;
                    float zd = z.Pos.DistanceTo(me.Pos) - (z.Radius - 10f);
                    if (zd < lureD) { lureD = zd; lurePos = z.Pos; lureR = z.Radius - 10f; lure = true; }
                }
            }

            V2 best = V2.Zero;
            float bestCost = float.MaxValue, bestDanger = 0f;
            int lockedArea = RoomsOn && AreaLocked(CurrentWorld.AreaAt(me.Pos)) ? CurrentWorld.AreaAt(me.Pos) : -1;
            for (int c = 0; c < 9; c++)
            {
                V2 dir = c == 8 ? V2.Zero : V2.FromAngle(MathX.Tau * c / 8f);  // WASD: 8 directions or standing still
                float d = 0f;
                V2 p = me.Pos;
                for (int k = 1; k <= sk.Steps; k++)
                {
                    float t = k * step;
                    p = me.Pos + dir * speed * t;
                    if (PullStrength != 0f)
                        p += (PullCenter - me.Pos).Normalized() * PullStrength * t;
                    p += windDrift * t;
                    float w = 1.6f - t / T;  // sooner hits matter more
                    foreach (var b in botThreats)
                    {
                        V2 bp = b.Kind == Defs.PauseShot ? b.Pos + b.Vel * PauseTravel(b, t)  // slowing to a stop, then hanging (Variety.cs)
                            : b.AngVel == 0f ? b.Pos + b.Vel * t : b.Pos + b.Vel.Rotated(b.AngVel * t * 0.5f) * t;
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
                    foreach (var pt in Patches.Values)  // a path's fire, the Colossus's lava the Devourer's bile, a Scorcher's embers or tear gas: burning now or about to (Paths.cs)
                        if ((pt.Kind == PatchKind.Fire || pt.Kind == PatchKind.Lava || pt.Kind == PatchKind.Bile || pt.Kind == PatchKind.Embers || pt.Kind == PatchKind.Gas || pt.Kind == PatchKind.Acid || pt.Kind == PatchKind.Rubble && !pt.Active) && pt.Age + t + 0.2f >= pt.Warn && pt.Pos.DistanceTo(p) < pt.Radius + hitR + 8f)
                            d += w * 3f;
                    foreach (var th in botThreads)
                        if (SegmentDistance(p, th.A, th.B) < StitchWidth / 2f + hitR + sk.Margin + 4f)
                            d += w * 2.5f;
                    foreach (var e in Enemies.Values)  // a Beam Sentry's beam, a Reaver's rake (Variety.cs)
                    {
                        // The enemies that don't shoot (NonShooters.cs): a lit fuse's blast, the Hookman's line, pods and their lasers.
                        if (e.Type == EnemyType.FuseRunner && e.Phase == 1 && e.Pos.DistanceTo(p) < FuseBlast + hitR + 10f)
                            d += w * 3f;
                        else if (e.Type == EnemyType.Hookman && e.Phase == 1 && SegmentDistance(p, e.Pos, e.Pos + V2.FromAngle(e.Facing) * HookReach) < hitR + 22f)
                            d += w * 2.5f;
                        else if (e.Type == EnemyType.SapperMine && e.Phase >= 1)
                        {
                            if (e.Pos.DistanceTo(p) < (e.Phase == 2 ? PodBlast : PodTrigger) + hitR + 8f)
                                d += w * 3f;
                            if (e.Phase == 1)
                                foreach (var o in Enemies.Values)
                                    if (o.Type == EnemyType.SapperMine && o.Phase == 1 && o.Id > e.Id && o.Pos.DistanceTo(e.Pos) < PodWire && SegmentDistance(p, e.Pos, o.Pos) < hitR + 8f)
                                        d += w * 2.5f;
                        }
                        if (e.Type == EnemyType.BeamSentry && (e.Phase == 3 || e.Phase == 2 && e.PhaseT < t + 0.3f))
                        {
                            // It sweeps a quarter turn either way from where it points: keep clear of that fan near its line.
                            float ang = (p - e.Pos).Angle, off = Math.Abs(MathX.Fmod(ang - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
                            if (p.DistanceTo(e.Pos) < SentryBeamLen && off < (e.Phase == 3 ? 0.35f : 0.6f))
                                d += w * 2.5f;
                        }
                        else if (PulsarLocked(e) && PulsarBeamDistance(e, p) < hitR + 40f)
                            d += w * 2.5f;  // a Pulsar's beams, flickering or sweeping (Infall.cs)
                        else if (e.Kin == EnemyType.RiotShield && (RiotState(e) == RiotWinding || RiotState(e) == RiotLunging))
                        {
                            // A Riot Shield about to bash: get out from in front of the shield (WardenFaction.cs).
                            V2 rel = p - e.Pos;
                            float off = Math.Abs(MathX.Fmod(rel.Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
                            if (rel.Length < e.Radius + hitR + 110f && off < 1.2f)
                                d += w * 3f;
                        }
                        else if (IsSwordsman(e.Type) && e.Phase == 1 && e.PhaseT < t + 0.35f)
                        {
                            // A Swordsman about to cut: stay out of its cone (Swordsman.cs).
                            V2 rel = p - e.Pos;
                            float off = Math.Abs(MathX.Fmod(rel.Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
                            if (rel.Length < SwordReachOf(e.Type) + hitR + 10f && off < SwordHalfOf(e.Type) + 0.25f)
                                d += w * 3f;
                        }
                        else if (e.Type == EnemyType.Reaver && e.Phase == 1)
                        {
                            V2 rel = p - e.Pos;
                            float off = Math.Abs(MathX.Fmod(rel.Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
                            if (rel.Length < ReaverSwipeR + e.Radius + hitR + 15f && off < ReaverCone + 0.2f)
                                d += w * 3f;
                        }
                    }
                    foreach (var r in stompRings)
                        if (RingDanger(r, p, t, GateBulletR + hitR + sk.Margin + 6f))
                            d += w * 2.5f;
                    foreach (var ck in quarryCracks)
                        if (SegmentDistance(p, ck.From, ck.From + V2.FromAngle(ck.Ang) * ck.Max) < hitR + CrackHalfW + 4f)
                            d += w * 1.5f;
                    if (col != null)
                    {
                        foreach (var r in col.Rings)  // where each ring's band will be by then, unless you're in a gap
                            if (RingDanger(r, p, t, GateBulletR + hitR + sk.Margin + 6f))
                                d += w * 2.5f;
                        foreach (var dr in col.Drops)
                            if (dr.T + t + 0.2f >= dr.Dur && dr.Pos.DistanceTo(p) < dr.Radius + hitR + 8f)
                                d += w * 3f;
                        foreach (var ck in col.Cracks)
                            if (SegmentDistance(p, ck.From, ck.From + V2.FromAngle(ck.Ang) * ck.Max) < hitR + 18f)
                                d += w * 1.5f;
                        if (col.ChargeWarn > 0f && colossus != null && SegmentDistance(p, colossus.Pos, colossus.Pos + V2.FromAngle(col.ChargeDir) * ColChargeLength) < colossus.Radius + hitR + 10f)
                            d += w * 3f;
                    }
                    if (twinB != null && SegmentDistance(p, twinA.Pos, twinB.Pos) < TetherWidth / 2f + hitR + 10f)
                        d += w * 2f;
                    if (sing != null)
                        for (int l = 0; l < lasers; l++)
                            if (OnLaser(sing.Pos, sing.Facing + (sing.Phase == SingLasers ? laserSpin * t : 0f) + MathX.Tau * l / lasers, p, LaserWidth / 2f + hitR + sk.Margin + 4f))
                            {
                                d += w * 2f;
                                break;
                            }
                    if (disk != null)  // where the disk's orbs will be by then
                    {
                        float orbReach = DiskOrbR + hitR + sk.Margin + 4f, dc = p.DistanceTo(disk.Pos);
                        if (Math.Abs(dc - DiskInnerR) < orbReach || Math.Abs(dc - DiskOuterR) < orbReach)
                        {
                            disk.Sing.DiskT += t;
                            for (int ring = 0; ring < 2; ring++)
                                for (int i = 0; i < (ring == 0 ? DiskInnerN : DiskOuterN); i++)
                                {
                                    V2 at;
                                    if (DiskOrb(disk.Sing, disk.Pos, ring, i, out at) && at.DistanceTo(p) < orbReach)
                                        d += w * 2f;
                                }
                            disk.Sing.DiskT -= t;
                        }
                    }
                    if (ds != null)
                    {
                        float pad = hitR + sk.Margin + 8f;
                        if (ds.Lunging && SegmentDistance(p, ds.LungeFrom, ds.LungeFrom + V2.FromAngle(ds.LungeAng) * (ds.LungeLen + BiteReach * 0.6f)) < dev.Radius + pad)
                            d += w * 3f;
                        V2 off = p - dev.Pos;
                        if ((ds.InhaleT > 0f || ds.Turn == DevourTurn.Inhale) && off.Length < InhaleReach + 40f
                            && Math.Abs(MathX.Fmod(off.Angle - dev.Facing + MathX.Pi, MathX.Tau) - MathX.Pi) < InhaleHalf + 0.15f)
                            d += w * 2.5f;
                        if (ds.BurrowT > 0f && off.Length < EruptRadius + pad + (ds.BurrowT < BurrowChase ? 60f : 0f))
                            d += w * 3f;
                        if (ds.Stage == DevourStage.Ravenous && InMaw(dev, p))
                            d += w * 0.5f;  // rather be on its back
                        if (ds.Stage == DevourStage.LastStand)
                            for (int tk = 0; tk < 12; tk++)
                                if (tk != 0 && tk != 6 && p.DistanceTo(ToothPos(dev.Pos, ds.Spin + TeethSpin * t, tk)) < ToothHit + pad)
                                {
                                    d += w * 2.5f;
                                    break;
                                }
                    }
                    if (hs != null)
                    {
                        float arcReach = RodArcWidth / 2f + hitR + sk.Margin + 6f;
                        foreach (var a in hs.Arcs)
                            if (a.A < hs.Rods.Count && a.B < hs.Rods.Count && a.T + t + 0.25f >= a.Warn && a.T + t <= a.Warn + ArcLive
                                && SegmentDistance(p, hs.Rods[a.A].Pos, hs.Rods[a.B].Pos) < arcReach)
                                d += w * 3f;
                        if (hs.Stage == HeraldStage.LastStand)
                            for (int sp = 0; sp < 4; sp++)
                                if (SegmentDistance(p, herald.Pos, herald.Pos + V2.FromAngle(hs.Spin + SpokeSpin * t + sp * MathX.Pi / 2f) * SpokeLength) < SpokeWidth / 2f + hitR + sk.Margin + 6f)
                                {
                                    d += w * 2f;
                                    break;
                                }
                        if (conductor != null && hs.CondT < t + 1.5f)  // the charge is about to jump: keep apart
                        {
                            if (conductor == me)
                            {
                                foreach (var q in Players.Values)
                                    if (q != me && !q.Downed && q.Pos.DistanceTo(p) < ConductorRadius + 30f)
                                        d += w * 2f;
                            }
                            else if (conductor.Pos.DistanceTo(p) < ConductorRadius + 30f)
                                d += w * 2f;
                        }
                    }
                    if (es != null)
                    {
                        float eReach = hitR + sk.Margin + 6f;
                        foreach (var f in es.Flares)
                            if (f.T + t + 0.2f >= FlareWarn && f.T + t <= FlareWarn + FlareLive
                                && SegmentDistance(p, f.From, f.From + V2.FromAngle(f.Ang) * FlareLen) < FlareWidth / 2f + eReach)
                                d += w * 3f;
                        foreach (var r in es.Rings)
                            if (Math.Abs(p.DistanceTo(r.Center) - (r.R - UmbraRingSpeed * t)) < UmbraRingWidth / 2f + eReach && !InUmbraGap(r, p))
                                d += w * 3f;
                        if (es.SweepOn > 0)
                        {
                            float a = es.SweepA + EchoSweepSpin * (es.SweepOn == 2 ? t : Math.Max(0f, t - (EchoSweepWarn - es.SweepT)));
                            for (int k2 = 0; k2 < 2; k2++)
                                if (SegmentDistance(p, eclipse.Pos, eclipse.Pos + V2.FromAngle(a + k2 * MathX.Pi) * EchoSweepLen) < EchoSweepWidth / 2f + eReach)
                                    d += w * 2f;
                        }
                        if (es.DiskT >= 0f)
                            for (int k2 = 0; k2 < DiskOrbs; k2++)
                                if (DiskOrbAt(eclipse.Pos, es.DiskT + t, k2).DistanceTo(p) < EchoDiskOrbR + eReach)
                                    d += w * 2f;
                        if (es.FlashT > 0f && es.FlashT < t + 0.3f && !InMoonShadow(es, eclipse.Pos, p, hitR))
                            d += w * 4f;
                        if (seekLight && !InSunlight(es, p, hitR))
                            d += w * 0.3f;
                    }
                    for (int i = 0; i < botBalls.Count; i++)
                        for (int j = i + 1; j < botBalls.Count; j++)
                            if (botBalls[i].Pos.DistanceTo(botBalls[j].Pos) < BallArcRange && SegmentDistance(p, botBalls[i].Pos, botBalls[j].Pos) < BallArcWidth / 2f + hitR + sk.Margin + 6f)
                                d += w * 2f;
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
                else if (es != null && es.Inset > 0f)
                {
                    // Last Light's burning edge can't be crossed either.
                    float room = Math.Min(Math.Min(p.X, Width - p.X), Math.Min(p.Y, Height - p.Y)) - es.Inset;
                    if (room < 60f)
                        d += 0.8f * (60f - room) / 60f;
                }
                else if (RoomsOn)
                {
                    // The Rooms arena (Rooms.cs): a step into a wall goes nowhere, a locked door holds you in, and a corner of
                    // a room is a bad place to be caught (a person kites round the room instead).
                    if (lockedArea >= 0 && CurrentWorld.AreaAt(p) != lockedArea)
                        d += 2f;
                    if (NearSolid(p, hitR + 6f))
                        d += 1f;
                    var ar = CurrentWorld.Areas[CurrentWorld.AreaAt(p)];
                    float edge = Math.Min(Math.Min(p.X - ar.Min.X, ar.Max.X - p.X), Math.Min(p.Y - ar.Min.Y, ar.Max.Y - p.Y));
                    if (edge < 50f && !ar.Hall)
                        d += 0.3f * (50f - edge) / 50f;
                }
                else if (p.X < 20f || p.Y < 20f || p.X > Width - 20f || p.Y > Height - 20f)
                    d += 0.2f;
                float dist = p.DistanceTo(anchor);
                float position = 0.35f * Math.Min(4f, Math.Abs(dist - pref) / 200f) * (pref == 0f ? 3f : 1f);
                if (stayNear != null)
                    position += 0.25f * Math.Max(0f, p.DistanceTo(stayNear.Pos) - 220f) / 200f;
                if (hide)
                    position += 1.2f * Math.Min(4f, p.DistanceTo(shadowSpot) / 150f);  // get into the moon's shadow
                else if (seekLight && !InSunlight(es, p, hitR + 15f))
                    position += 0.6f * Math.Min(3f, p.DistanceTo(sunspot) / 150f);  // back into the light
                if (lure)
                    position += 0.3f * Math.Min(3f, Math.Max(0f, p.DistanceTo(lurePos) - lureR) / 150f);
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

        readonly List<Enemy> botBalls = new List<Enemy>();

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
