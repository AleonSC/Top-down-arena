using System;
using System.Collections.Generic;

namespace Arena
{
    /// Eye of Calm: the Healer Elementalist's third ultimate (lobby key 2 of the Ultimate row; Sanctum stays the default, Static
    /// Bubble is key 1). Docs/Mockups/eye-of-calm.html.
    ///   - Spin-up (EyeSpin s): the storm wall grows out from the eye to its full EyeWall px; it already works as it grows.
    ///   - Hurricane (EyeTime s, the spin-up included): a hurricane round you that follows you. Its calm eye (EyeRadius px)
    ///     heals every ally standing in it (you too) EyeHeal HP a second; enemies can't walk into it (they're put out at its
    ///     edge) and every enemy shot that reaches it, red aimed shots too, is gone.
    ///   - The wall (EyeRadius to the wall's edge): enemies in it take EyeDps a second (bosses too), are slowed to EyeSlow and
    ///     drift round it at EyeDrift px/s (not heavies, bosses or enemies that never walk). Ordinary enemy shots entering it are
    ///     swept away (gone, with a wisp); red aimed shots fly through to the eye.
    ///   - You move at EyeSelfSpeed and can still shoot.
    ///   - Final gust: when it ends (time, going down, the wave over) the wall blows outward: every enemy in it takes EyeGust
    ///     and is flung EyeFling px out (not heavies or bosses).
    /// Damage scales by your damage. Mastered: the wall reaches EyeWallMastered px and the gust hits for EyeGustMastered.
    /// Replicated: Player.EyeT (time left, 0 = off), EyeAge (seconds since the cast) and EyeWallR (the wall's full reach);
    /// drawn by Game/EyeOfCalmLook.cs; FxKind.EyeGust is the final gust, FxKind.VortexWisp a swept-away shot.
    public partial class Game
    {
        public const float EyeTime = 10f, EyeSpin = 0.8f, EyeRadius = 90f, EyeWall = 280f, EyeWallMastered = 335f;
        public const float EyeHeal = 12f, EyeDps = 22f, EyeTick = 0.25f, EyeSlow = 0.55f, EyeDrift = 120f;
        public const float EyeGust = 40f, EyeGustMastered = 80f, EyeFling = 200f, EyeGustFx = 0.8f, EyeSelfSpeed = 0.8f;
        public const float EyeHumEvery = 1.1f;
        public static readonly Rgba EyeCalm = new Rgba(0.55f, 0.98f, 0.72f), EyeStorm = new Rgba(0.5f, 0.83f, 0.9f), EyeGold = new Rgba(0.94f, 0.74f, 0.33f);

        /// Eye of Calm's entry: key 2 of the Healer Elementalist's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] EyeUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.EyeOfCalm, Kit = Kit.Elementalist, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Eye of Calm", Cooldown = 85f,
                Desc = "A hurricane round you for 10 s: its wall grinds enemies and sweeps their shots away, and allies in its calm eye heal 12 a second, safe from bullets. It ends in a gust that flings them out.",
                Capstone = "The wall reaches 335 px instead of 280, and the final gust hits for 80.",
                Bot = b => b.Danger || b.Crowd >= JuggBotCrowd,
            },
        };

        /// The storm is up.
        public static bool EyeOn(Player p) => p.EyeT > 0f && !p.Downed;
        /// The wall's edge for an age: it grows out from the eye over the spin-up.
        public static float EyeReach(float full, float age) => EyeRadius + (full - EyeRadius) * Math.Min(1f, Math.Max(0f, age / EyeSpin));
        /// How hard it blows, 0..1 (the look's strength): rising over the spin-up.
        public static float EyePower(float age) => Math.Min(1f, 0.25f + 0.75f * Math.Max(0f, age / EyeSpin));
        /// Your speed while it blows (Player.Speed).
        public static float EyeSpeed(Player p) => EyeOn(p) ? EyeSelfSpeed : 1f;

        void CastEye(Player p, bool mastered)
        {
            p.EyeT = EyeTime;
            p.EyeAge = 0f;
            p.EyeWallR = mastered ? EyeWallMastered : EyeWall;
            p.EyeTickT = 0f;
            p.EyeHumT = 0f;
            p.EyeMastered = mastered;
            p.EyeUp = true;
            p.UltT = p.EyeT;
            BroadcastFx(FxKind.Ring, p.Pos, EyeStorm, EyeRadius + 30f, 0.4f, Sfx.SpellPullStart);
        }

        /// It ends (time, down, the wave over, the slot changed): `gust` blows the wall outward.
        void EndEye(Player p, bool gust)
        {
            if (!p.EyeUp)
                return;
            p.EyeUp = false;
            p.EyeT = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.EyeOfCalm)
                p.UltT = 0f;
            if (!gust)
                return;
            float R = EyeReach(p.EyeWallR, p.EyeAge);
            float dmg = (p.EyeMastered ? EyeGustMastered : EyeGust) * p.Stats.DmgMult;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (e.SpawnT > 0f || e.Pos.FartherOrAt(p.Pos, R + e.Radius) || DevourUnder(e) || GutwormUnder(e))
                    continue;
                if (VortexMoves(e))
                    ShoveFrom(e, p.Pos, EyeFling, V2.FromAngle(p.Aim));
                HurtEnemy(e, dmg, p.Id);
                p.EyeFlung++;
            }
            BroadcastFx(FxKind.EyeGust, p.Pos, new V2(p.Id, p.EyeMastered ? 1f : 0f), EyeGold, R, EyeGustFx, Sfx.SpellPullStop);
        }

        readonly List<int> eyeGone = new List<int>();

        /// Host, every tick after the enemies have moved (with the other keep-out zones): the clock, the heal, enemies put out
        /// of the eye and drifting round the wall, the damage, the shots.
        void TickEye(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (!p.EyeUp)
                    continue;
                p.EyeT = Math.Max(0f, p.EyeT - dt);
                p.EyeAge += dt;
                if (p.Slots[Upgrades.UltSlot].Id != AbilityId.EyeOfCalm)
                {
                    EndEye(p, false);
                    continue;
                }
                if (p.EyeT <= 0f || p.Downed || !Battle)
                {
                    EndEye(p, true);
                    continue;
                }
                float R = EyeReach(p.EyeWallR, p.EyeAge);
                if ((p.EyeHumT -= dt) <= 0f)
                {
                    p.EyeHumT = EyeHumEvery;
                    BroadcastSound(Sfx.SpellPullHum);
                }
                // the calm eye heals the allies in it
                foreach (var q in Players.Values)
                    if (!q.Downed && q.Pos.CloserThan(p.Pos, EyeRadius))
                        p.EyeHealed += Heal(q, EyeHeal * dt, p, AuraSource);
                // enemies: put out of the eye, drifting round the wall
                foreach (var e in AllFoes(by: p.Id))
                {
                    if (e.SpawnT > 0f || DevourUnder(e) || GutwormUnder(e))
                        continue;
                    V2 off = e.Pos - p.Pos;
                    float d = off.Length;
                    if (d >= R + e.Radius)
                        continue;
                    float lim = EyeRadius + e.Radius;
                    V2 dir = d > 0.01f ? off * (1f / d) : V2.FromAngle(p.Aim);
                    if (d < lim)
                    {
                        e.Pos = ClampToArena(p.Pos + dir * (lim + 1f), e.Radius);
                        d = lim + 1f;
                    }
                    if (VortexMoves(e))
                    {
                        float a = (float)Math.Atan2(dir.Y, dir.X) + EyeDrift / Math.Max(d, 1f) * dt;  // (anticlockwise on screen, as the look turns)
                        e.Pos = ClampToArena(p.Pos + V2.FromAngle(a) * d, e.Radius);
                    }
                    Slow(e, EyeTick + 0.1f, EyeSlow);
                }
                // the damage, every EyeTick
                p.EyeTickT += dt;
                if (p.EyeTickT >= EyeTick)
                {
                    p.EyeTickT -= EyeTick;
                    float dmg = EyeDps * EyeTick * p.Stats.DmgMult;
                    foreach (var e in AllFoes(by: p.Id))
                        if (e.SpawnT <= 0f && e.Pos.CloserThan(p.Pos, R + e.Radius) && !DevourUnder(e) && !GutwormUnder(e))
                        {
                            V2 off = e.Pos - p.Pos;
                            WindHit(e, new V2(-off.Y, off.X));  // wind (Wind.cs: chills the wet, fans the burning)
                            HurtEnemy(e, dmg, p.Id);
                            p.EyeHits++;
                        }
                }
                // shots: the wall sweeps ordinary ones away, the eye takes the rest
                eyeGone.Clear();
                foreach (var b in Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind) && b.Pos.CloserThan(p.Pos, R) && (b.Kind != Defs.PierceShot || b.Pos.CloserThan(p.Pos, EyeRadius + b.Radius)))
                        eyeGone.Add(b.Id);
                int wisps = 0;
                foreach (int id in eyeGone)
                {
                    Bullet b;
                    if (!Bullets.TryGetValue(id, out b))
                        continue;
                    if (wisps++ < VortexWispsPerTick)
                        BroadcastFx(FxKind.VortexWisp, b.Pos, p.Pos, Defs.BulletColor(b.Kind), b.Radius, VortexWispTime, Sfx.None);
                    KillBullet(id);
                    p.EyeSwept++;
                }
            }
        }

        // --- Network: the player's Eye of Calm fields (SyncPlayer) ---

        void SyncEye(NetSync s, Player p)
        {
            s.Float(ref p.EyeT);  // Eye of Calm
            if (p.EyeT > 0f)
            {
                s.Float(ref p.EyeAge);
                s.Float(ref p.EyeWallR);
            }
        }
    }
}
