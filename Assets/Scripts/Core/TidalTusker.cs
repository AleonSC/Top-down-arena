using System;

namespace Arena
{
    /// Tidal Tusker: the Tank Summoner's fourth ultimate (key 3 of the lobby's Ultimate row; the Titan stays the default), from
    /// the mockup (claude.ai artifact "Tidal Tusker").
    ///   - Rise (TkRise s): a puddle opens beside you and a spirit elephant rises out of it.
    ///   - Follow (TkLife s, TkLifeMastered mastered): it plods TkFollow px behind you (behind the way you walk), up to TkSpeed.
    ///     Regular enemies within TkPull px go for it and shoot at it instead of you (TuskerTarget); bosses and mini-bosses don't.
    ///     Enemy shots that reach its body (TkBody) stop there and wear its health (TuskerStops); bodies pressing on it bite it
    ///     every TkContactEvery s and can't walk through it (TuskerContact). Its health: TkHp + TkHpPerWave a wave.
    ///   - The hose: its trunk sprays a steady stream at the nearest enemy within its reach (Zone.Radius, TkReach; Deep Trunk
    ///     farther). The stream stops on the first enemy in its line: TkDps a second (in TkTick steps), wet for TkWet s (Deep
    ///     Trunk longer; slowed, and lightning arcs to it: Tsunami.cs), and pushed back TkPush px a second (not heavies or
    ///     bosses). Mastered, a second stream on the next nearest enemy.
    ///   - Stomp (the last TkStomp s, or as soon as its health runs out or you go down): it rears, and at TkStompLand s in it
    ///     stomps: TkStompDmg to every enemy within TkStompR, every enemy within TkSplashR soaked wet and shoved TkSplashShove px
    ///     out (not heavies or bosses). The wave ending puts it away quietly.
    /// Damage scales with your damage and the ultimate's mastery (Zone.DmgMult). The Rare card Deep Trunk (Stats.TuskReachMult,
    /// TuskWet) makes the stream reach farther and soak longer.
    /// It is one `ZoneKind.Tusker` zone: Pos the elephant, Aim its facing, Radius the stream's reach, BlockHp/SoakMax its health
    /// (replicated as GuardFrac), Origin/Origin2 where its streams end and Side/Rider the enemies they hit (0: that stream is
    /// off), Heading (host) the way you walk, GunCd (host) its next hose tick, TickT (host) its contact timer, Landed (host) the
    /// stomp has landed. Every screen draws it from that (Game/TidalTuskerLook.cs).
    public partial class Game
    {
        public const float TkRise = 0.8f, TkLife = 12f, TkLifeMastered = 16f, TkStomp = 0.7f, TkStompLand = 0.4f;
        public const float TkBody = 34f, TkHead = 40f, TkFollow = 85f, TkSpeed = 220f, TkTurn = 5f;
        public const float TkHp = 600f, TkHpPerWave = 30f, TkPull = 300f, TkContactEvery = 0.5f;
        public const float TkReach = 260f, TkDps = 40f, TkTick = 0.1f, TkWet = 3f, TkPush = 60f;
        public const float TkStompR = 160f, TkStompDmg = 60f, TkSplashR = 220f, TkSplashShove = 70f;
        public const float TkDeepReach = 0.25f, TkDeepWet = 1f;  // Deep Trunk, a stack: the stream this much farther, wet this much longer

        /// Tidal Tusker's entry: key 3 of the Tank Summoner's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] TuskerUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.TidalTusker, Kit = Kit.Summoner, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Tidal Tusker", Cooldown = 75f,
                Desc = "A spirit elephant rises beside you and follows you for 12 s. Enemies near it go for it instead of you, and its trunk hoses them with a steady stream that soaks and pushes them back. It ends with a splashing stomp.",
                Capstone = "Stays 16 s, and sprays two streams at once.",
                Bot = b => b.HaveTarget && b.TargetD < 420f,
            },
        };

        /// The whole show for a stay of `life` s (the stomp is its last TkStomp s).
        public static float TkTotal(float life) => TkRise + life + TkStomp;

        /// Seconds since the cast.
        public static float TkAge(Zone z) => z.Duration - z.T;

        /// Is it rearing for (or into) its stomp?
        public static bool TkStomping(Zone z) => z.T <= TkStomp;

        /// Is it up: risen, not stomping yet, drawing enemies and taking shots?
        public static bool TuskerUp(Zone z) => z.Kind == ZoneKind.Tusker && z.T > TkStomp && TkAge(z) >= TkRise;

        /// Is this elephant the mastered one? Read from its length.
        public static bool TkMastered(Zone z) => z.Duration > TkTotal((TkLife + TkLifeMastered) * 0.5f);

        /// Its full health on this wave.
        public static float TkHealth(int wave) => TkHp + TkHpPerWave * Math.Max(0, wave - 1);

        /// Its health left, 0-1 (the host's own numbers, or the snapshot's GuardFrac on a client).
        public float TkFrac(Zone z) => IsHost && z.SoakMax > 0f ? Math.Max(0f, z.BlockHp / z.SoakMax) : z.GuardFrac;

        /// Where its trunk starts.
        public static V2 TkTrunk(Zone z) => z.Pos + V2.FromAngle(z.Aim) * TkHead;

        void CastTusker(Player p, bool cap)
        {
            float dur = TkTotal(cap ? TkLifeMastered : TkLife);
            V2 at = ClampToArena(p.Pos + V2.FromAngle(p.Aim + MathX.Pi * 0.75f) * 70f, TkBody);
            var z = AddZone(ZoneKind.Tusker, p, at, TkReach * p.Stats.TuskReachMult, dur);
            z.Aim = p.Aim;
            z.Heading = p.Aim;
            z.SoakMax = z.BlockHp = TkHealth(Wave);
            z.GuardFrac = 1f;
            z.Cap = cap;
            z.Origin = z.Origin2 = at;
            z.OwnerAt = p.Pos;
            z.Side = z.Rider = 0;
            p.UltT = dur;
            BroadcastFx(FxKind.Ring, at, Kits.Get(ZoneKind.Tusker).Color, 70f, 0.8f, Sfx.WwCast);
        }

        /// Host, every tick, for each elephant (TickZones).
        void TickTusker(Zone z, float dt)
        {
            Player owner;
            if (!Battle || !Players.TryGetValue(z.Owner, out owner))
            {
                if (z.T > 0f)
                    BroadcastFx(FxKind.Ring, z.Pos, Kits.Get(ZoneKind.Tusker).Color, 50f, 0.5f, Sfx.None);
                z.T = 0f;  // the wave is over: it fades away quietly
                z.Side = z.Rider = 0;
                return;
            }
            z.TickT = Math.Max(0f, z.TickT - dt);
            if (owner.Downed && z.T > TkStomp)
                z.T = TkStomp;  // you went down: it stomps at once
            float age = TkAge(z);
            if (age < TkRise)
                return;  // rising out of its puddle
            if (TkStomping(z))
            {
                z.Side = z.Rider = 0;
                if (!z.Landed && z.T <= TkStomp - TkStompLand)
                    TuskerStomp(z, owner);
                return;
            }
            // Follow: behind you, the way you walk.
            V2 moved = owner.Pos - z.OwnerAt;
            if (moved.LengthSq > 0.25f)
                z.Heading = MathX.WrapPi(z.Heading + MathX.WrapPi(moved.Angle - z.Heading) * Math.Min(1f, dt * 6f));
            z.OwnerAt = owner.Pos;
            V2 spot = owner.Pos - V2.FromAngle(z.Heading) * TkFollow, to = spot - z.Pos;
            float gap = to.Length, walk = 0f;
            if (gap > 6f)
            {
                walk = Math.Min(TkSpeed, gap * 2.5f) * dt;
                z.Pos = ClampToArena(z.Pos + to * (Math.Min(walk, gap) / gap), TkBody);
            }
            // The hose.
            Enemy first = null, second = null;
            float d1 = float.MaxValue, d2 = float.MaxValue;
            V2 trunk = TkTrunk(z);
            foreach (var e in AllFoes(by: z.Owner))
            {
                if (!Targetable(e))
                    continue;
                float d = (e.Pos - trunk).Length - e.Radius;
                if (d > z.Radius)
                    continue;
                if (d < d1)
                {
                    second = first; d2 = d1;
                    first = e; d1 = d;
                }
                else if (d < d2)
                {
                    second = e; d2 = d;
                }
            }
            float want = first != null ? (first.Pos - z.Pos).Angle : walk > 0.5f ? to.Angle : z.Aim;
            float turn = MathX.WrapPi(want - z.Aim), step = TkTurn * dt;
            z.Aim = MathX.WrapPi(z.Aim + Math.Max(-step, Math.Min(step, turn)));
            z.GunCd -= dt;
            bool tick = z.GunCd <= 0f;
            if (tick)
                z.GunCd += TkTick;
            if (z.GunCd < 0f)
                z.GunCd = 0f;
            z.Side = Hose(z, owner, first, tick, ref z.Origin);
            z.Rider = z.Cap && second != null ? Hose(z, owner, second, tick, ref z.Origin2) : 0;
        }

        /// One stream from the trunk at `target`: it stops on the first enemy in its line (returned, 0 for none: off), which
        /// takes its share of the damage, the wet and the push on each tick. `end` is where the stream stops.
        int Hose(Zone z, Player owner, Enemy target, bool tick, ref V2 end)
        {
            if (target == null)
                return 0;
            V2 trunk = TkTrunk(z), dir = (target.Pos - trunk).Normalized();
            if (dir.IsZero)
                dir = V2.FromAngle(z.Aim);
            Enemy hit = null;
            float best = z.Radius + target.Radius;
            foreach (var e in AllFoes(by: z.Owner))
            {
                if (!Targetable(e))
                    continue;
                V2 rel = e.Pos - trunk;
                float along = rel.X * dir.X + rel.Y * dir.Y, off = Math.Abs(rel.X * dir.Y - rel.Y * dir.X);
                if (along < -e.Radius || off >= e.Radius)
                    continue;
                float at = Math.Max(0f, along - (float)Math.Sqrt(e.Radius * e.Radius - off * off));
                if (at < best)
                {
                    best = at;
                    hit = e;
                }
            }
            if (hit == null)
                return 0;
            end = trunk + dir * best;
            if (tick)
            {
                HurtEnemy(hit, TkDps * TkTick * z.DmgMult, z.Owner);
                hit.WetT = Math.Max(hit.WetT, TkWet + owner.Stats.TuskWet);
                if (!Heavy(hit))
                    KnockEnemy(hit, dir * (TkPush * TkTick));
            }
            return hit.Id;
        }

        /// The stomp lands: the hurt close in, and the splash soaking and shoving everything round it.
        void TuskerStomp(Zone z, Player owner)
        {
            z.Landed = true;
            foreach (var e in AllFoes(by: z.Owner))
            {
                if (!Targetable(e))
                    continue;
                float d = (e.Pos - z.Pos).Length;
                if (d > TkSplashR + e.Radius * 0.5f)
                    continue;
                if (d <= TkStompR + e.Radius * 0.5f)
                    HurtEnemy(e, TkStompDmg * z.DmgMult, z.Owner);
                e.WetT = Math.Max(e.WetT, TkWet + owner.Stats.TuskWet);
                if (!Heavy(e))
                    ShoveFrom(e, z.Pos, TkSplashShove, V2.FromAngle(z.Aim));
            }
            BroadcastFx(FxKind.Ring, z.Pos, Kits.Get(ZoneKind.Tusker).Color, TkSplashR, 0.6f, Sfx.BossSlam);
            BroadcastFx(FxKind.Ring, z.Pos, new Rgba(0.91f, 0.97f, 1f), TkStompR, 0.4f, Sfx.WwSplash);  // (the foam of the stomp)
        }

        /// Wears a standing elephant (a shot or a bite); at 0 it rears for its stomp.
        void WearTusker(Zone z, float dmg, V2 at)
        {
            z.BlockHp -= dmg;
            z.GuardFrac = Math.Max(0f, z.BlockHp / Math.Max(1f, z.SoakMax));
            BroadcastFx(FxKind.Spark, at, at, Kits.Get(ZoneKind.Tusker).Color, 8f, 0.25f, Sfx.None);
            if (z.BlockHp <= 0f && z.T > TkStomp)
                z.T = TkStomp;
        }

        /// Host, from the enemy-bullet loop in Combat.cs: does an elephant stop this shot? It wears it by what it would have hit for.
        bool TuskerStops(Bullet b)
        {
            foreach (var z in Zones.Values)
            {
                if (!TuskerUp(z) || b.Pos.FartherThan(z.Pos, TkBody + b.Radius))
                    continue;
                WearTusker(z, b.Dmg * EnemyDamageScale(Math.Max(1, Wave)), b.Pos);
                return true;
            }
            return false;
        }

        /// Host, from TickEnemies: a regular enemy within TkPull of an elephant goes for (and aims at) the nearest one.
        V2 TuskerTarget(Enemy e, V2 tpos)
        {
            if (e.Def.IsBoss || e.Def.Mini)
                return tpos;
            Zone best = null;
            float bestD = TkPull * TkPull;
            foreach (var z in Zones.Values)
            {
                if (!TuskerUp(z))
                    continue;
                float d = z.Pos.DistanceSq(e.Pos);
                if (d < bestD)
                {
                    bestD = d;
                    best = z;
                }
            }
            return best != null ? best.Pos : tpos;
        }

        /// Host, from TickEnemies: a regular enemy's body against an elephant bites it (every TkContactEvery s) and stays outside it.
        void TuskerContact(Enemy e, float contact)
        {
            foreach (var z in Zones.Values)
            {
                if (!TuskerUp(z))
                    continue;
                V2 d = e.Pos - z.Pos;
                float reach = TkBody + e.Radius;
                if (d.LengthSq >= reach * reach)
                    continue;
                e.Pos = ClampToArena(z.Pos + (d.LengthSq > 0.01f ? d.Normalized() : V2.FromAngle(z.Aim)) * reach, e.Radius);
                if (z.TickT <= 0f && contact > 0f)
                {
                    z.TickT = TkContactEvery;
                    WearTusker(z, contact * EnemyDamageScale(Math.Max(1, Wave)), z.Pos + d.Normalized() * TkBody);
                }
            }
        }
    }
}
