using System;
using System.Collections.Generic;

namespace Arena
{
    /// Vortex: the Tank Elementalist's fourth ultimate (lobby key 3 of the Ultimate row; Glacial Tomb stays the default,
    /// Lightning Rod is key 1, Stone Colossus key 2). Docs/Mockups/vortex.html, ending "Fling out", shots "Swept away".
    ///   - Wind-up (VortexWindup s): a ring of dust closes in on you; nothing is moved or hurt yet.
    ///   - Storm (VortexTime s): a VortexRadius px whirlwind round you that follows you. You stand in its calm eye.
    ///     Every enemy inside is swept round it at VortexSwirl px/s and pulled in VortexPull px/s (never closer than
    ///     VortexEye px), and those within VortexFringe px outside its edge are drawn in at VortexFringePull px/s.
    ///     Heavies, bosses and enemies that never move stay put. Everything inside, bosses too, takes VortexTickDamage every
    ///     VortexTick s. Enemy shots that enter it are swept away (gone, with a wisp of wind).
    ///   - Fling: when it ends (time, going down, the wave over) every enemy inside is thrown VortexFling px outward
    ///     (not heavies or bosses) and takes VortexFlingDamage.
    /// All damage scales by your damage. Mastered: VortexRadiusMastered px and VortexTickDamageMastered a tick.
    /// Replicated: Player.VortexT (time left, wind-up included; 0 = off), VortexAge (seconds since the cast) and VortexR (its
    /// radius), drawn by Game/VortexLook.cs; FxKind.VortexWisp is a swept-away shot.
    public partial class Game
    {
        public const float VortexWindup = 0.4f, VortexTime = 5f, VortexRampIn = 0.4f, VortexRampOut = 0.3f;
        public const float VortexRadius = 240f, VortexRadiusMastered = 300f, VortexEye = 60f, VortexFringe = 60f;
        public const float VortexSwirl = 280f, VortexPull = 50f, VortexFringePull = 140f;
        public const float VortexTick = 0.2f, VortexTickDamage = 9f, VortexTickDamageMastered = 11f;  // 45 a second, 55 mastered
        public const float VortexFling = 160f, VortexFlingDamage = 30f;
        public const float VortexHumEvery = 1.1f;     // the swirling hum repeats this often while it blows
        public const int VortexWispsPerTick = 6;      // swept-away shots drawn per tick at most (the rest just go)
        public const float VortexWispTime = 0.5f;
        public static readonly Rgba VortexAir = new Rgba(0.84f, 1f, 0.97f);
        public static readonly Rgba VortexGold = new Rgba(1f, 0.82f, 0.54f);

        /// Vortex's entry: key 3 of the tank Elementalist's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] VortexUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.Vortex, Kit = Kit.Elementalist, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Vortex", Cooldown = 80f,
                Desc = "A huge whirlwind round you drags enemies with it, shreds them and sweeps their shots away, then flings them out.",
                Capstone = "300 px across instead of 240, and 55 damage a second.",
                Bot = b => b.Crowd >= JuggBotCrowd,
            },
        };

        /// The storm is up (wind-up included).
        public static bool VortexOn(Player p) => p.VortexT > 0f && !p.Downed;

        /// How hard it blows, 0..1: 0 through the wind-up, easing in after it and out over its last moment.
        public static float VortexPower(float age, float left)
        {
            if (age < VortexWindup || left <= 0f)
                return 0f;
            float k = Math.Min(1f, (age - VortexWindup) / VortexRampIn);
            return Math.Min(k, Math.Min(1f, left / VortexRampOut));
        }

        void CastVortex(Player p, bool mastered)
        {
            p.VortexT = VortexWindup + VortexTime;
            p.VortexAge = 0f;
            p.VortexR = mastered ? VortexRadiusMastered : VortexRadius;
            p.VortexTickT = 0f;
            p.VortexHumT = VortexWindup;
            p.VortexMastered = mastered;
            p.VortexUp = true;
            p.UltT = p.VortexT;
            BroadcastFx(FxKind.Ring, p.Pos, VortexAir, p.VortexR, VortexWindup, Sfx.SpellPullStart);
        }

        /// It dies down (time, down, the wave over, the slot changed): everyone inside is flung out.
        void EndVortex(Player p, bool fling)
        {
            if (!p.VortexUp)
                return;
            p.VortexUp = false;
            p.VortexT = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.Vortex)
                p.UltT = 0f;
            if (!fling)
                return;
            float dmg = VortexFlingDamage * p.Stats.DmgMult;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (e.Pos.FartherOrAt(p.Pos, p.VortexR + e.Radius) || DevourUnder(e) || GutwormUnder(e))
                    continue;
                if (VortexMoves(e))
                    ShoveFrom(e, p.Pos, VortexFling, V2.FromAngle(p.Aim));
                HurtEnemy(e, dmg, p.Id);
                p.VortexFlung++;
            }
            BroadcastFx(FxKind.Ring, p.Pos, VortexGold, p.VortexR * 1.6f, 0.45f, Sfx.SpellPullStop);
        }

        /// The wind moves it: not a heavy, a boss or something that never walks.
        static bool VortexMoves(Enemy e) => !Heavy(e) && e.Def.Speed > 0f;

        /// Host, every tick after the enemies have moved (with the other keep-out zones): the clock, the swirl, the damage,
        /// the shots swept away.
        void TickVortex(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (!p.VortexUp)
                    continue;
                p.VortexT = Math.Max(0f, p.VortexT - dt);
                p.VortexAge += dt;
                if (p.Slots[Upgrades.UltSlot].Id != AbilityId.Vortex)
                {
                    EndVortex(p, false);
                    continue;
                }
                if (p.VortexT <= 0f || p.Downed || !Battle)
                {
                    EndVortex(p, true);
                    continue;
                }
                float k = VortexPower(p.VortexAge, p.VortexT);
                if (k <= 0f)
                    continue;
                float R = p.VortexR;
                if ((p.VortexHumT -= dt) <= 0f)
                {
                    p.VortexHumT = VortexHumEvery;
                    BroadcastSound(Sfx.SpellPullHum);
                }
                // the swirl and the pull
                foreach (var e in AllFoes(by: p.Id))
                {
                    if (!VortexMoves(e) || DevourUnder(e) || GutwormUnder(e))
                        continue;
                    V2 off = e.Pos - p.Pos;
                    float d = off.Length;
                    if (d >= R + VortexFringe + e.Radius)
                        continue;
                    if (d < R + e.Radius)
                    {
                        float a = d > 0.01f ? (float)Math.Atan2(off.Y, off.X) : (e.Id % 7) * 0.9f;
                        float rr = Math.Max(d, 1f);
                        a += VortexSwirl * k / rr * dt;  // (anticlockwise on screen, as the look turns)
                        float nd = Math.Max(VortexEye + e.Radius, d - VortexPull * k * dt);
                        e.Pos = ClampToArena(p.Pos + V2.FromAngle(a) * nd, e.Radius);
                    }
                    else
                        e.Pos = ClampToArena(e.Pos - off * (VortexFringePull * k * dt / d), e.Radius);
                }
                // the damage, every VortexTick
                p.VortexTickT += dt;
                if (p.VortexTickT >= VortexTick)
                {
                    p.VortexTickT -= VortexTick;
                    float dmg = (p.VortexMastered ? VortexTickDamageMastered : VortexTickDamage) * p.Stats.DmgMult;
                    foreach (var e in AllFoes(by: p.Id))
                        if (e.Pos.CloserThan(p.Pos, R + e.Radius) && !DevourUnder(e) && !GutwormUnder(e))
                        {
                            V2 off = e.Pos - p.Pos;
                            WindHit(e, new V2(-off.Y, off.X));  // wind: the swirl's way round (Wind.cs: chills the wet, fans the burning)
                            HurtEnemy(e, dmg, p.Id);
                            p.VortexHits++;
                        }
                }
                SweepShots(p, R, k);
            }
        }

        readonly List<int> vortexSwept = new List<int>();

        /// Enemy shots inside the storm are swept away: gone, with a wisp drawn curling off round it.
        void SweepShots(Player p, float R, float k)
        {
            if (k < 0.3f)
                return;
            vortexSwept.Clear();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && b.Pos.CloserThan(p.Pos, R))
                    vortexSwept.Add(b.Id);
            int wisps = 0;
            foreach (int id in vortexSwept)
            {
                Bullet b;
                if (!Bullets.TryGetValue(id, out b))
                    continue;
                if (wisps++ < VortexWispsPerTick)
                    BroadcastFx(FxKind.VortexWisp, b.Pos, p.Pos, Defs.BulletColor(b.Kind), b.Radius, VortexWispTime, Sfx.None);
                KillBullet(id);
                p.VortexSwept++;
            }
        }

        // --- Network: the player's Vortex fields (SyncPlayer) ---

        void SyncVortex(NetSync s, Player p)
        {
            s.Float(ref p.VortexT);  // Vortex
            s.Float(ref p.VortexAge);
            s.Float(ref p.VortexR);
        }
    }
}
