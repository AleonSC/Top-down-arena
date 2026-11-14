using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Devourer's faction, The Larder (Docs/Approaches.md "The Larder", mockup Docs/Mockups/devourer-faction.html).
    /// Step 1 is three enemies that share their Warden counterpart's behaviour through HiveKin's table: the Gnawling plays
    /// like a Rioter, the Butcher like a Guard and the Gulper like a Thug (a Brute). Each adds one trick that teaches a turn
    /// of the Devourer fight:
    ///   Butcher (its Snap bite): within ChopTrigger of its target it stops and raises its cleaver for ChopWind, then chops
    ///     a half-circle ChopReach in front (ChopDamage), then rests ChopEvery.
    ///   Gulper (its Inhale): within GulpRange it stops, its cheeks swell for GulpWind, then it breathes in for GulpTime: a
    ///     cone (GulpHalf either side, GulpReach long) pulls you toward its mouth at GulpPull (DevourPull, on your own game).
    ///     Reach the mouth and it bites (GulpBite) and the breath ends. Step out of the cone sideways.
    ///
    /// Step 2: the Carcass Bearer plays like a Riot Shield; when its slab (a side of meat) breaks, it drops in front as a
    /// morsel (the Devourer's EnemyType.Morsel, 60 HP): shoot it or walk over it. A Gnawling within GnawSniff of a morsel
    /// goes for it (GnawlingTarget) and eats it (GnawlingEat): +50% health, and it's drawn bigger (LarderStage 1 on a
    /// Gnawling: fed). The Bile Spitter plays like a Gas Grenadier but leads you like a Rock Hurler and lobs bile: a
    /// PatchKind.Bile pool (SpitPoolR, SpitPoolLife) that burns SpitDps (TickLarderHost; the Devourer's fight ticks its own).
    /// With no Devourer about, players also squash morsels by walking over them (StepOnMorsels, Devourer.cs).
    ///
    ///
    /// Step 3: the Gutworm (its own moves) walks at you, then dives (LarderStage 1, can't be hurt or touched): a ripple
    /// chases you for GutChase s at its speed, stops and marks GutMarkR for GutMark s (stage 2, still underground), then
    /// erupts: a bite (GutBite) on whoever's in the mark and a ring of GutRing shots; it rests GutEvery s on the surface.
    /// The Hook Thrower is the Hookman (NonShooters.cs, IsHook) with a meat hook: a longer line (HookThrowAim), a slower
    /// hook (HookThrowSpeed), a shorter reel (HookThrowYank) and HookThrowDamage on a hit. The Maw Priest moves and charges
    /// like a Taser Guard but fires a fan of three (LarderFire); it turns at PriestTurn (PriestFace), and a shot into the
    /// maw in front (PriestMawHalf either side) heals it for half while one in its back does PriestBackMult
    /// (LarderBlocks notes the side, PriestHit applies it), the Devourer's Ravenous Maw in small.
    ///
    /// The state is Enemy.LarderStage (0 idle, 1 winding up, 2 acting) and LarderT (seconds left in it), replicated by
    /// SyncLarder; the facing is locked while it's busy (LarderFaceLocked), so the cone and the chop show where they hit.
    public static class LarderKin
    {
        public static bool Is(EnemyType t) => t >= EnemyType.Gnawling && t <= EnemyType.BileSpitter || t >= EnemyType.Gutworm && t <= EnemyType.MawPriest;  // (the Eclipse's faction sits between the two steps)
    }

    public partial class Enemy
    {
        public byte LarderStage;  // a Butcher's or Gulper's attack: 0 idle, 1 winding up, 2 chopping / breathing in (replicated)
        public float LarderT;     // seconds left in the stage (replicated)
        public float LarderCd;    // host: until it may attack again
        public byte MawSide;      // host: the side the last shot at a Maw Priest came from (1 its maw, 2 elsewhere)
    }

    public partial class Game
    {
        public const float ChopTrigger = 70f, ChopReach = 110f, ChopWind = 0.4f, ChopFlash = 0.15f, ChopEvery = 1.6f, ChopDamage = 14f;
        public const float GulpRange = 220f, GulpWind = 0.6f, GulpTime = 1f, GulpEvery = 4f, GulpReach = 200f, GulpHalf = 0.61f, GulpPull = 120f, GulpBite = 20f;
        public const byte LarderIdle = 0, LarderWinding = 1, LarderActing = 2, GnawFed = 1;
        public const float SpitPoolR = 35f, SpitPoolLife = 4f, SpitDps = 10f, GnawSniff = 320f, GnawFeed = 1.5f;
        public const float GutChase = 1.8f, GutMark = 0.6f, GutMarkR = 50f, GutBite = 16f, GutEvery = 3f, GutRingSpeed = 170f;
        public const int GutRing = 8;
        public const float HookThrowAim = 0.7f, HookThrowSpeed = 600f, HookThrowYank = 90f, HookThrowDamage = 8f;
        public const float PriestTurn = 0.52f, PriestMawHalf = MathX.Pi / 4f, PriestBackMult = 1.25f, PriestFan = 0.3f, MawShotSpeed = 200f;
        public static readonly Rgba BileColor = new Rgba(0.72f, 0.88f, 0.29f);

        static bool LarderAttacks(EnemyType t) => t == EnemyType.Butcher || t == EnemyType.Gulper;

        /// Standing still: winding up, chopping or breathing in.
        public static bool LarderPlanted(Enemy e) => LarderAttacks(e.Type) && e.LarderStage != LarderIdle || e.Type == EnemyType.Gutworm && e.LarderStage == LarderActing;

        /// A Gutworm underground (chasing or marked): it can't be hurt or touched.
        public static bool GutwormUnder(Enemy e) => e.Type == EnemyType.Gutworm && e.LarderStage != LarderIdle;

        /// Is `from` in front of a Maw Priest, in its maw?
        public static bool InPriestMaw(Enemy e, V2 from) => Math.Abs(MathX.Fmod((from - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi) < PriestMawHalf;

        /// Its facing stays put while it's busy, so the chop and the cone land where they showed.
        public static bool LarderFaceLocked(Enemy e) => LarderPlanted(e);

        /// A Gulper breathing in.
        public static bool Gulping(Enemy e) => e.Type == EnemyType.Gulper && e.LarderStage == LarderActing && e.Hp > 0f;

        /// How far through the wind-up, 0 to 1 (drawing: the cleaver going up, the cheeks swelling).
        public static float LarderWind(Enemy e) => e.LarderStage == LarderWinding
            ? MathX.Clamp01(1f - e.LarderT / (e.Type == EnemyType.Gulper ? GulpWind : ChopWind)) : 0f;

        /// Is `p` in a cone `half` either side of `face`, within `reach` of `from`?
        static bool InCone(V2 from, float face, float half, float reach, V2 p, float pad)
        {
            V2 to = p - from;
            float d = to.Length;
            if (d > reach + pad)
                return false;
            if (d < pad)
                return true;
            return Math.Abs(MathX.Fmod(to.Angle - face + MathX.Pi, MathX.Tau) - MathX.Pi) < half;
        }

        /// Host, in Special: starts, runs and ends a Butcher's chop or a Gulper's breath.
        void LarderSpecial(Enemy e, V2 tpos, float dt)
        {
            bool gulper = e.Type == EnemyType.Gulper;
            e.LarderCd -= dt;
            if (e.LarderStage == LarderIdle)
            {
                if (e.LarderCd > 0f || e.SpawnT > 0f || e.Pos.DistanceTo(tpos) > (gulper ? GulpRange : ChopTrigger + e.Radius))
                    return;
                e.Facing = (tpos - e.Pos).Angle;
                e.LarderStage = LarderWinding;
                e.LarderT = gulper ? GulpWind : ChopWind;
                return;
            }
            e.LarderT -= dt;
            if (e.LarderStage == LarderWinding)
            {
                if (e.LarderT > 0f)
                    return;
                e.LarderStage = LarderActing;
                e.LarderT = gulper ? GulpTime : ChopFlash;
                if (!gulper)
                    Chop(e);
                else
                    BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * e.Radius, new Rgba(1f, 0.75f, 0.8f), 20f, 0.25f, Sfx.BossWhoosh);
                return;
            }
            if (gulper && GulpBit(e))
                e.LarderT = 0f;  // it bit: the breath ends
            if (e.LarderT > 0f)
                return;
            e.LarderStage = LarderIdle;
            e.LarderCd = gulper ? GulpEvery - GulpWind - GulpTime : ChopEvery;
        }

        /// Host: the Butcher's chop lands on everyone in the half-circle in front.
        void Chop(Enemy e)
        {
            foreach (var p in Players.Values)
                if (!p.Downed && p.InvulnT <= 0f && p.SwallowT <= 0f && InCone(e.Pos, e.Facing, MathX.Pi / 2f, ChopReach, p.Pos, p.Def.HitR))
                    Damage(p, ChopDamage, "Butcher");
            BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 14f), new Rgba(1f, 0.92f, 0.92f), 26f, 0.2f, Sfx.SwingHeavy);
        }

        /// Host: whoever reaches a breathing Gulper's mouth is bitten. True when it bit someone.
        bool GulpBit(Enemy e)
        {
            V2 mouth = e.Pos + V2.FromAngle(e.Facing) * e.Radius;
            foreach (var p in Players.Values)
                if (!p.Downed && p.InvulnT <= 0f && p.SwallowT <= 0f && p.Pos.DistanceTo(mouth) < p.Def.HitR + 12f)
                {
                    Damage(p, GulpBite, "Gulper");
                    BroadcastFx(FxKind.Spark, mouth, new Rgba(1f, 0.35f, 0.42f), 26f, 0.25f, Sfx.BossChomp);
                    return true;
                }
            return false;
        }

        /// Your own game (from DevourPull): a breathing Gulper's cone pulls you toward its mouth.
        V2 GulpPullAt(V2 at)
        {
            foreach (var e in Enemies.Values)
            {
                if (!Gulping(e))
                    continue;
                V2 to = e.Pos - at;
                float d = to.Length;
                if (d < e.Radius || !InCone(e.Pos, e.Facing, GulpHalf, GulpReach, at, 0f))
                    continue;
                return to / d * GulpPull;
            }
            return V2.Zero;
        }

        /// Host: a broken carcass slab falls in front of its bearer as a morsel.
        void DropCarcass(Enemy e)
        {
            V2 at = ClampToArena(e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 16f), 30f);
            var m = SpawnEnemyAt(EnemyType.Morsel, at, 0.15f, e.Id);
            m.FireT = 99f;
        }

        /// Host: the nearest morsel within `reach` of `from` (null for none).
        Enemy MorselNear(V2 from, float reach)
        {
            Enemy best = null;
            foreach (var m in Enemies.Values)
                if (m.Type == EnemyType.Morsel && m.Hp > 0f && !IsSpawning(m) && m.Pos.DistanceTo(from) < reach && (best == null || m.Pos.DistanceSq(from) < best.Pos.DistanceSq(from)))
                    best = m;
            return best;
        }

        /// Host, in TickEnemies: a hungry Gnawling goes for a morsel near it instead of you.
        V2 GnawlingTarget(Enemy e, V2 tpos)
        {
            if (e.Type != EnemyType.Gnawling || e.LarderStage == GnawFed || !anyMorsel)
                return tpos;
            var m = MorselNear(e.Pos, GnawSniff);
            return m != null ? m.Pos : tpos;
        }

        /// Host, in Special: a hungry Gnawling that reaches a morsel eats it and grows.
        void GnawlingEat(Enemy e)
        {
            if (e.LarderStage == GnawFed || !anyMorsel)
                return;
            var m = MorselNear(e.Pos, e.Radius + 18f);
            if (m == null)
                return;
            Enemies.Remove(m.Id);  // eaten (no kill, no reward)
            float add = e.MaxHp * (GnawFeed - 1f);
            e.MaxHp += add;
            e.Hp += add;
            e.LarderStage = GnawFed;
            BroadcastFx(FxKind.Ring, m.Pos, new Rgba(1f, 0.69f, 0.44f), 34f, 0.35f, Sfx.BossChomp);
        }

        bool anyMorsel;
        float larderBileT;

        /// Host, every tick: whether any morsel is out; with no Devourer about, bile pools burn and you squash morsels by
        /// walking over them (the Devourer's fight does both itself).
        void TickLarderHost(float dt)
        {
            anyMorsel = false;
            bool anyBile = false;
            foreach (var e in Enemies.Values)
                anyMorsel |= e.Type == EnemyType.Morsel;
            foreach (var pt in Patches.Values)
                anyBile |= pt.Kind == PatchKind.Bile;
            if (!anyMorsel && !anyBile || FindDevourer() != null)
                return;
            if (anyMorsel)
                StepOnMorsels();
            larderBileT -= dt;
            if (!anyBile || larderBileT > 0f)
                return;
            larderBileT = 0.25f;
            foreach (var pt in Patches.Values)
            {
                if (pt.Kind != PatchKind.Bile || !pt.Active)
                    continue;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && p.Pos.DistanceTo(pt.Pos) < pt.Radius + p.Def.HitR)
                        DamageOverTime(p, SpitDps * 0.25f, "Bile Spitter (bile)");
            }
        }

        /// Host, in Special: a Gutworm dives, chases, marks the ground and erupts.
        void GutwormSpecial(Enemy e, V2 tpos, float dt)
        {
            if (e.LarderStage == LarderIdle)
            {
                e.LarderCd -= dt;
                if (e.LarderCd > 0f || e.SpawnT > 0f || e.Pos.DistanceTo(tpos) > 520f)
                    return;
                e.LarderStage = LarderWinding;
                e.LarderT = GutChase;
                BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.6f, 0.38f, 0.36f), e.Radius + 16f, 0.3f, Sfx.BossRumble);  // it dives
                return;
            }
            e.LarderT -= dt;
            if (e.LarderT > 0f)
                return;
            if (e.LarderStage == LarderWinding)
            {
                e.LarderStage = LarderActing;  // the ripple stops and marks the ground
                e.LarderT = GutMark;
                return;
            }
            foreach (var p in Players.Values)
                if (!p.Downed && p.InvulnT <= 0f && p.SwallowT <= 0f && p.Pos.DistanceTo(e.Pos) < GutMarkR + p.Def.HitR)
                    Damage(p, GutBite, "Gutworm");
            float off = Rand(0f, MathX.Tau);
            for (int i = 0; i < GutRing; i++)
                EnemyShot(e, off + MathX.Tau * i / GutRing, GutRingSpeed, Defs.SmallShot);
            BroadcastFx(FxKind.Boom, e.Pos, new Rgba(0.85f, 0.64f, 0.5f), GutMarkR + 14f, 0.4f, Sfx.BossSlam);  // it erupts
            e.LarderStage = LarderIdle;
            e.LarderCd = GutEvery;
        }

        /// Host, in Face: a Maw Priest turns slowly, so you can get round its maw.
        void PriestFace(Enemy e, float want, float dt)
        {
            float diff = MathX.Fmod(want - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi;
            e.Facing += Math.Max(-PriestTurn * dt, Math.Min(PriestTurn * dt, diff));
        }

        /// Host, in EnemyFire: a Maw Priest's fan of three (it charges like a Taser Guard). False for anyone else.
        bool LarderFire(Enemy e, V2 tpos, float aim)
        {
            if (e.Type != EnemyType.MawPriest)
                return false;
            e.FireT = TaserEvery;
            for (int k = -1; k <= 1; k++)
                EnemyShot(e, e.Facing + k * PriestFan, MawShotSpeed, Defs.SmallShot);  // (from its maw, where it faces)
            BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 10f), new Rgba(1f, 0.35f, 0.42f), 22f, 0.2f, Sfx.BossChomp);
            return true;
        }

        /// BlocksShot: a Gutworm underground lets shots pass; a Maw Priest notes which side the hit came from.
        bool LarderBlocks(Enemy e, V2 from)
        {
            if (GutwormUnder(e))
                return true;
            if (e.Type == EnemyType.MawPriest)
                e.MawSide = (byte)(InPriestMaw(e, from) ? 1 : 2);
            return false;
        }

        /// HurtEnemy: a shot into a Maw Priest's maw heals it for half (false: nothing more to do); in its back it does more.
        bool PriestHit(Enemy e, ref float dmg)
        {
            if (e.Type != EnemyType.MawPriest)
                return true;
            byte side = e.MawSide;
            e.MawSide = 0;
            if (side == 1)
            {
                e.Hp = Math.Min(e.MaxHp, e.Hp + dmg * 0.5f);
                return false;
            }
            if (side == 2)
                dmg *= PriestBackMult;
            return true;
        }

        void SyncLarder(NetSync s, Enemy e)
        {
            s.Byte(ref e.LarderStage);
            if (e.LarderStage != LarderIdle && e.Type != EnemyType.Gnawling)
                s.Float(ref e.LarderT);
        }
    }
}
