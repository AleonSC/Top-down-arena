using System;
using System.Collections.Generic;

namespace Arena
{
    /// Mammoth Fall: the DPS Summoner's third ultimate (key 2 of the lobby's Ultimate row; Legion stays the default), from the
    /// mockup (claude.ai artifact "Mammoth Fall").
    ///   - Summon (MfSummon s): a rift opens over you and a woolly mammoth forms in it. A gold ring on the ground follows your
    ///     cursor (up to MfRange px away); where it is when the mammoth is whole is where it lands. Going down cancels it.
    ///   - Dive (MfFly s): it launches itself at that spot.
    ///   - Slam: whatever is under it (MfCrush px) takes MfCrushDmg; everything in the shockwave (MfWave px, Ice Age wider) takes
    ///     MfWaveDmg, is shoved MfWaveShove px out (not heavies or bosses) and stunned MfStun s (bosses MfBossStun s).
    ///   - Rampage (MfRampage s, MfRampageMastered mastered), after a MfSettle s pause: it charges the nearest enemy within MfLeash
    ///     px of the crater, turning at MfTurn radians a second (MfSpeed, MfSpeedTurning while turning hard). Every enemy it runs
    ///     into is trampled (MfTrampleDmg, a MfTrampleStun s stun, shoved MfTrampleShove px aside, not heavies or bosses), at most
    ///     once every MfTrampleEvery s; it doesn't charge one it trampled in the last MfPreyRest s, so it turns on the next one.
    ///   - Leave (MfLeave s): it charges off along its heading and fades away, hurting nothing.
    /// Damage scales with your damage and the ultimate's mastery (Zone.DmgMult). The Rare card Ice Age (Stats.MammothWaveMult,
    /// MammothStun) widens the shockwave and lengthens its stun.
    /// It is one `ZoneKind.Mammoth` zone: Pos the mammoth on the ground (your spot while it's summoned, the launch spot during
    /// the dive), Origin where it lands (the aim ring until then), Aim its heading, Radius the shockwave, Side the enemy it's
    /// charging (0 none); every screen draws the rift, ring, dive, crater and rampage from it (Game/MammothFallLook.cs).
    public partial class Game
    {
        public const float MfSummon = 1.6f, MfFly = 0.5f, MfSettle = 0.35f, MfRampage = 3f, MfRampageMastered = 4.5f, MfLeave = 0.8f;
        public const float MfRange = 520f, MfCrush = 70f, MfCrushDmg = 120f, MfWave = 200f, MfWaveDmg = 25f, MfWaveShove = 40f;
        public const float MfStun = 1.5f, MfBossStun = 0.5f;
        public const float MfLeash = 300f, MfTurn = 7f, MfSpeed = 330f, MfSpeedTurning = 170f, MfLeaveSpeed = 420f;
        public const float MfBody = 46f, MfTrampleDmg = 18f, MfTrampleStun = 0.3f, MfTrampleShove = 60f, MfTrampleEvery = 0.4f, MfPreyRest = 1.2f;
        public const float MfIceAgeWave = 0.25f, MfIceAgeStun = 0.5f;  // Ice Age, a stack: the shockwave this much wider, its stun this much longer

        /// Mammoth Fall's entry: key 2 of the DPS Summoner's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] MammothUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.MammothFall, Kit = Kit.Summoner, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Mammoth Fall", Cooldown = 75f,
                Desc = "Summon a woolly mammoth in the sky, aim it, and it slams down, stunning everything around, then rampages through enemies.",
                Capstone = "Rampages 1.5 s longer.",
                Bot = b => b.HaveTarget && b.TargetD < MfRange,
            },
        };

        /// The whole show for a rampage of `rampage` s.
        public static float MfTotal(float rampage) => MfSummon + MfFly + MfSettle + rampage + MfLeave;

        /// Seconds since the cast.
        public static float MfAge(Zone z) => z.Duration - z.T;

        /// When it lands.
        public const float MfImpactAt = MfSummon + MfFly;

        /// When it starts to leave.
        public static float MfLeaveAt(Zone z) => z.Duration - MfLeave;

        /// Is this mammoth the mastered one? Read from its length.
        public static bool MfMastered(Zone z) => z.Duration > MfTotal((MfRampage + MfRampageMastered) * 0.5f);

        /// Where it lands: `aimAt`, kept within MfRange of `from` and inside the arena.
        V2 MfTarget(V2 from, V2 aimAt)
        {
            V2 off = aimAt - from;
            if (off.Length > MfRange)
                aimAt = from + off.Normalized() * MfRange;
            return ClampToArena(aimAt, 30f);
        }

        void CastMammothFall(Player p, V2 target, bool cap)
        {
            float dur = MfTotal(cap ? MfRampageMastered : MfRampage);
            var z = AddZone(ZoneKind.Mammoth, p, p.Pos, MfWave * p.Stats.MammothWaveMult, dur);
            z.Origin = MfTarget(p.Pos, target);
            z.Aim = (z.Origin - p.Pos).LengthSq > 1f ? (z.Origin - p.Pos).Angle : p.Aim;
            z.Leg = -1;  // 0 once it has landed
            z.Side = 0;
            z.HitAt = new Dictionary<int, float>();
            p.UltT = dur;
            BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(ZoneKind.Mammoth).Color, 46f, 0.5f, Sfx.RodentPortal);
        }

        /// Host, every tick, for each mammoth (TickZones).
        void TickMammoth(Zone z, float dt)
        {
            Player owner;
            if (!Battle || !Players.TryGetValue(z.Owner, out owner))
            {
                z.T = Math.Min(z.T, 0.001f);  // the wave is over
                return;
            }
            float age = MfAge(z);
            if (age < MfSummon)
            {
                if (owner.Downed)
                {
                    z.T = Math.Min(z.T, 0.001f);  // the rift closes with nothing in it
                    owner.UltT = 0f;
                    return;
                }
                z.Pos = owner.Pos;  // it hangs over you while it forms, and the ring follows your aim
                z.Origin = MfTarget(owner.Pos, owner.AimAt);
                if ((z.Origin - z.Pos).LengthSq > 1f)
                    z.Aim = (z.Origin - z.Pos).Angle;
                return;
            }
            if (age < MfImpactAt)
                return;  // the dive: Pos (where it left) to Origin, drawn from its age
            if (z.Leg < 0)
            {
                MammothSlam(z, owner);
                return;
            }
            if (age < MfImpactAt + MfSettle)
                return;
            if (age < MfLeaveAt(z))
            {
                MammothRampage(z, dt, age);
                return;
            }
            z.Side = 0;
            z.Pos += V2.FromAngle(z.Aim) * (MfLeaveSpeed * dt);  // off it goes (it can run past the walls: it's fading)
        }

        /// It lands: crush what's under it, then the shockwave's damage, shove and stun.
        void MammothSlam(Zone z, Player owner)
        {
            z.Leg = 0;
            z.Pos = z.Origin;
            float stun = MfStun + owner.Stats.MammothStun;
            foreach (var e in AllFoes(by: z.Owner))
            {
                float d = (e.Pos - z.Pos).Length;
                if (d > z.Radius + e.Radius * 0.5f)
                    continue;
                bool crushed = d <= MfCrush + e.Radius * 0.5f;
                HurtEnemy(e, (crushed ? MfCrushDmg : MfWaveDmg) * z.DmgMult, z.Owner);
                Stun(e, e.Def.IsBoss ? MfBossStun : stun);
                if (!Heavy(e) && d > 1f)
                    KnockEnemy(e, (e.Pos - z.Pos).Normalized() * MfWaveShove);
            }
            BroadcastFx(FxKind.Ring, z.Pos, Kits.Get(ZoneKind.Mammoth).Color, z.Radius, 0.45f, Sfx.BossSlam);
        }

        /// The rampage: charge the nearest enemy near the crater, trampling whatever it runs into.
        void MammothRampage(Zone z, float dt, float age)
        {
            Enemy prey = null;
            float best = float.MaxValue;
            foreach (var e in AllFoes(by: z.Owner))
            {
                float at;
                if (z.HitAt.TryGetValue(e.Id, out at) && age - at < MfPreyRest)
                    continue;  // just trampled: it goes for another one first
                if (!e.Pos.WithinReach(z.Origin, MfLeash))
                    continue;
                float d = (e.Pos - z.Pos).LengthSq;
                if (d < best)
                {
                    best = d;
                    prey = e;
                }
            }
            z.Side = prey != null ? prey.Id : 0;
            float want = prey != null ? (prey.Pos - z.Pos).Angle
                : (z.Origin - z.Pos).Angle + 1.2f;  // nothing in reach: it circles the crater
            float turn = MathX.WrapPi(want - z.Aim), step = MfTurn * dt;
            z.Aim = MathX.WrapPi(z.Aim + Math.Max(-step, Math.Min(step, turn)));
            float ramp = Math.Min(1f, (age - MfImpactAt - MfSettle) / 0.3f);
            float speed = (Math.Abs(turn) > 1.2f ? MfSpeedTurning : MfSpeed) * ramp;
            z.Pos = ClampToArena(z.Pos + V2.FromAngle(z.Aim) * (speed * dt), 40f);
            Player owner;
            Players.TryGetValue(z.Owner, out owner);
            foreach (var e in AllFoes(by: z.Owner))
            {
                if (!e.Pos.WithinReach(z.Pos, MfBody + e.Radius))
                    continue;
                float at;
                if (z.HitAt.TryGetValue(e.Id, out at) && age - at < MfTrampleEvery)
                    continue;
                z.HitAt[e.Id] = age;
                HurtEnemy(e, MfTrampleDmg * z.DmgMult, z.Owner);
                Stun(e, MfTrampleStun);
                if (!Heavy(e))  // aside, the way it was off the mammoth's line
                {
                    V2 fwd = V2.FromAngle(z.Aim), side = new V2(-fwd.Y, fwd.X);
                    V2 rel = e.Pos - z.Pos;
                    float s = rel.X * side.X + rel.Y * side.Y >= 0f ? 1f : -1f;
                    KnockEnemy(e, (fwd * 0.6f + side * s).Normalized() * MfTrampleShove);
                }
                BroadcastFx(FxKind.Spark, e.Pos, e.Pos, new Rgba(0.95f, 0.9f, 0.8f), e.Radius + 4f, 0.25f, Sfx.Hit);
                if (owner != null)
                    owner.MammothTramples++;
            }
        }
    }
}
