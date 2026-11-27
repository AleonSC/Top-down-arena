using System;

namespace Arena
{
    /// Sleepy Cat: the Healer Summoner's fourth ultimate (key 3 of the lobby's Ultimate row; World Tree stays the default), from the
    /// mockup (claude.ai artifact "Sleepy Cat").
    ///   - Prowl (ScProwl s): a cat runs after your cursor (ScSpeed px/s, easing in as it gets there). Fire is put away: each click
    ///     is a meow (ScMeows of them, ScGap s apart), marking every enemy within the meow's reach (Radius) of the cat with a paw.
    ///     The prowl ends when its time is up, or ScLastHold s after the last meow, or when you go down.
    ///   - Nap (ScNap s): the cat curls up where it stands and every marked enemy falls asleep: a Stun (no moving, no shooting) of
    ///     ScNap s, bosses ScBossNap s. Hits don't wake them. The napping cat purrs: allies within ScPurrR px heal ScPurr a second.
    /// It is one `ZoneKind.SleepyCat` zone: Pos the cat, Aim its heading, Radius the meow's reach, Marched its speed; Quiet once it
    /// naps, BoltsLeft the meows left and GunCd the seconds since the last meow (all replicated). The zone's clock restarts when the
    /// nap starts. Each enemy's mark and sleep go out as one byte (Enemy.CatNap). Game/SleepyCatLook.cs draws it.
    public partial class Game
    {
        public const float ScProwl = 5f, ScNap = 10f, ScBossNap = 3f, ScFade = 0.8f, ScCooldown = 85f;
        public const float ScSpeed = 560f, ScEase = 6f, ScTurn = 9f, ScGap = 0.35f, ScLastHold = 0.6f;
        public const float ScRadius = 95f, ScRadiusMastered = 120f, ScPurrR = 140f, ScPurr = 15f;
        public const int ScMeows = 4, ScMeowsMastered = 5;

        /// Sleepy Cat's entry: key 3 of the Healer Summoner's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] CatUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.SleepyCat, Kit = Kit.Summoner, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Sleepy Cat", Cooldown = ScCooldown,
                Desc = "A cat chases your cursor for 5 s: click to meow and mark enemies. Then it naps, and every marked enemy sleeps for 10 s while it purrs, healing allies.",
                Capstone = "Five meows, and each reaches 25% wider.",
                Bot = b => b.HaveTarget && b.TargetD < 500f,
            },
        };

        /// Seconds since the cast (prowling) or since the nap started.
        public static float ScAge(Zone z) => z.Duration - z.T;

        /// Is the cat napping?
        public static bool ScNapping(Zone z) => z.Kind == ZoneKind.SleepyCat && z.Quiet;

        /// Is `p`'s cat prowling (Fire is put away: it meows)?
        public bool CatOn(Player p) => CatOf(p) != null;

        /// `p`'s prowling cat, or null.
        Zone CatOf(Player p)
        {
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.SleepyCat && z.Owner == p.Id && !z.Quiet && z.T > 0f)
                    return z;
            return null;
        }

        /// Enemy.CatNap, the replicated byte: 0 nothing, 1 marked by a prowling cat, 2 + tenths of a second left asleep.
        public static bool CatMarked(Enemy e) => e.CatNap == 1;
        public static bool CatAsleep(Enemy e) => e.CatNap >= 2;
        public static float CatSleepLeft(Enemy e) => e.CatNap >= 2 ? (e.CatNap - 2) / 10f : 0f;

        /// Host, before sending an enemy: its mark (a cat still prowling) and its sleep, as Enemy.CatNap.
        byte CatNapByte(Enemy e)
        {
            if (e.CatNapT > 0f)
                return (byte)(2 + Math.Min(250, (int)Math.Ceiling(e.CatNapT * 10f)));
            Zone z;
            return e.CatMark != 0 && Zones.TryGetValue(e.CatMark, out z) && z.Kind == ZoneKind.SleepyCat && !z.Quiet ? (byte)1 : (byte)0;
        }

        void CastSleepyCat(Player p, bool cap)
        {
            foreach (var old in Zones.Values)
                if (old.Kind == ZoneKind.SleepyCat && old.Owner == p.Id && !old.Quiet)
                    StartCatNap(old);  // (a second cast while one prowls: that one naps now)
            V2 at = ClampToArena(p.Pos + V2.FromAngle(p.Aim) * 30f, 20f);
            var z = AddZone(ZoneKind.SleepyCat, p, at, cap ? ScRadiusMastered : ScRadius, ScProwl + ScNap + ScFade);
            z.Aim = p.Aim;
            z.Origin = default(V2);  // the cat's velocity (host)
            z.Marched = 0f;          // its speed (replicated, for the run cycle)
            z.BoltsLeft = cap ? ScMeowsMastered : ScMeows;
            z.GunCd = 99f;           // seconds since the last meow
            z.Quiet = false;
            z.Landed = p.Firing;     // Fire held last tick (a click is a fresh press): the cast's own press doesn't meow
            z.Cap = false;           // a click waiting for the gap
            p.UltT = ScProwl;
            BroadcastFx(FxKind.Ring, at, Kits.Get(ZoneKind.SleepyCat).Color, 40f, 0.45f, Sfx.Summon);
        }

        /// Host, every tick, for each Sleepy Cat (TickZones): the prowl (the run, the meows) and the nap (the purr).
        void TickSleepyCat(Zone z, float dt)
        {
            Player owner;
            Players.TryGetValue(z.Owner, out owner);
            z.GunCd += dt;
            if (z.Quiet)
            {
                if (ScAge(z) < ScNap)
                    foreach (var q in Players.Values)
                        if (!q.Downed && q.Pos.WithinReach(z.Pos, ScPurrR))
                            Heal(q, ScPurr * z.HealMult * dt, owner);
                return;
            }
            if (owner == null || owner.Downed)
            {
                StartCatNap(z);
                return;
            }
            // The run: steer the velocity toward the cursor, slowing as it gets there.
            V2 to = ClampToArena(owner.AimAt, 20f) - z.Pos;
            float d = to.Length, want = Math.Min(ScSpeed, d * ScEase);
            V2 goal = d > 1f ? to / d * want : default(V2);
            z.Origin = z.Origin + (goal - z.Origin) * Math.Min(1f, ScTurn * dt);
            z.Pos = ClampToArena(z.Pos + z.Origin * dt, 20f);
            z.Marched = z.Origin.Length;
            if (z.Marched > 20f)
                z.Aim = z.Origin.Angle;
            // The meows: a fresh click (kept until the gap allows it); a bot meows when an unmarked enemy is in reach.
            bool fire = owner.Firing;
            if (fire && !z.Landed)
                z.Cap = true;
            z.Landed = fire;
            if ((owner.IsBot || ShowcaseMode) && CatCanMark(z))  // (the loadout preview meows by itself)
                z.Cap = true;
            if (z.Cap && z.BoltsLeft > 0 && z.GunCd >= ScGap)
            {
                z.Cap = false;
                Meow(z);
            }
            if (ScAge(z) >= ScProwl || z.BoltsLeft <= 0 && z.GunCd >= ScLastHold)
                StartCatNap(z);
        }

        /// Is an unmarked enemy within the meow's reach (a bot's cue)?
        bool CatCanMark(Zone z)
        {
            foreach (var e in Enemies.Values)
                if (Targetable(e) && e.CatMark != z.Id && e.Pos.WithinReach(z.Pos, z.Radius + e.Radius))
                    return true;
            return false;
        }

        /// A meow: every enemy in reach is marked by this cat.
        void Meow(Zone z)
        {
            z.BoltsLeft--;
            z.GunCd = 0f;
            foreach (var e in Enemies.Values)
                if (Targetable(e) && e.Pos.WithinReach(z.Pos, z.Radius + e.Radius))
                    e.CatMark = z.Id;
            BroadcastFx(FxKind.Ring, z.Pos, Kits.Get(ZoneKind.SleepyCat).Color, z.Radius, 0.5f, Sfx.Meow);
        }

        /// The prowl is over: the cat curls up and every enemy it marked falls asleep (bosses only doze).
        void StartCatNap(Zone z)
        {
            z.Quiet = true;
            z.T = z.Duration = ScNap + ScFade;
            z.Origin = default(V2);
            z.Marched = 0f;
            z.GunCd = 99f;
            Player owner;
            if (Players.TryGetValue(z.Owner, out owner))
                owner.UltT = Math.Max(owner.UltT, ScNap);
            foreach (var e in Enemies.Values)
            {
                if (e.CatMark != z.Id)
                    continue;
                e.CatMark = 0;
                if (!Targetable(e))
                    continue;
                float t = e.Def.IsBoss ? ScBossNap : ScNap;
                Stun(e, t);
                e.CatNapT = Math.Max(e.CatNapT, t);
            }
            BroadcastFx(FxKind.Ring, z.Pos, new Rgba(0.71f, 0.55f, 1f), ScPurrR, 0.9f, Sfx.CatNap);
        }

        /// Each tick, for each enemy (EnemyAI, beside TickDaze): the sleep runs down, and ends if something else lifted the stun.
        static void TickCatNap(Enemy e, float dt)
        {
            if (e.CatNapT <= 0f)
                return;
            e.CatNapT = e.StunT > 0f ? Math.Max(0f, e.CatNapT - dt) : 0f;
        }
    }
}
