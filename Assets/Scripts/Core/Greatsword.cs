using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Greatsword (DPS Knight; Docs/Mockups/greatsword-combo.html, the Moonlit Crescent look).
    ///   - Chain: the primary button cuts, cuts back, then brings the blade overhead for a rend (the swing is in FireKnightWeapon,
    ///     Knight.cs). The two cuts are quick; the rend winds up for GreatRendWind first and is the hard hit. The chain starts
    ///     over after GreatChainWindow without a swing (Player.GreatStep). Every cut leaves its edge hanging as a crescent that
    ///     stops enemy bullets (SteelWall, Knight.cs); the rend's hangs longest.
    ///   - Alt: hold the weapon-action key (Left Alt) to Guard. A 120 degree arc rises in front of you that stops enemy bullets
    ///     (not red aimed shots); you move at half speed and can't swing. Every bullet it stops adds a point of Temper, up to
    ///     GreatTemperMax. Let go and the Rend fires: a piercing beam for GreatRendBase plus GreatRendPer a point. Six points
    ///     (a full guard) also make the next chain start on the overhead rend. Your game sends Msg.WeaponDash (DashGuardOn when
    ///     you press, DashGuardOff when you let go); the host does the rest.
    public partial class Game
    {
        public const float GreatCutArc = 150f, GreatRendArc = 200f;   // degrees across
        public static readonly float[] GreatMult = { 1f, 1f, 2.25f };       // damage of each step against the weapon's Damage (40, 40, 90)
        public static readonly float[] GreatInterval = { 0.45f, 0.5f, 0.95f };  // seconds from each step to the next press that counts
        public static readonly float[] GreatWall = { 0.35f, 0.35f, 0.9f };     // how long each step's crescent hangs
        public const float GreatRendWind = 0.25f, GreatRendKnock = 40f, GreatWaveDmg = 45f;  // the overhead rend's blade-up time, its shove, a sword wave's damage
        public const float GreatChainWindow = 0.9f;

        public const float GreatGuardRadius = 72f, GreatGuardHalfArc = 60f * MathX.Pi / 180f, GreatGuardBand = 14f;  // the guard arc: distance from you, half its width, how thick it stops
        public const float GreatGuardSlow = 0.5f, GreatGuardLockout = 0.6f;  // your speed while guarding; how soon it can go up again after a Rend
        public const int GreatTemperMax = 6;
        public const float GreatRendBase = 30f, GreatRendPer = 15f, GreatRendReach = 640f, GreatRendWidth = 16f, GreatRendWidthPer = 5f;
        public const float GreatRendFxTime = 0.5f, GreatBlockSoundGap = 0.07f;

        /// What a Rend of `temper` points deals before your damage upgrades.
        public static float GreatRendDamage(int temper) => GreatRendBase + GreatRendPer * temper;

        /// Host: a player pressed (on = true) or let go of the weapon-action key with the Greatsword, aiming at `aimAt`.
        void HostGreatGuard(Player p, bool on, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.Greatsword || State == MatchState.GameOver)
                return;
            if (on)
            {
                if (p.GreatGuard || p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.ParryActive || p.GreatLock > 0f)
                    return;
                p.GreatGuard = true;
                p.GreatTemper = 0;
                BroadcastSound(Sfx.GreatRaise);
                return;
            }
            if (!p.GreatGuard)
                return;
            int n = Math.Min(GreatTemperMax, (int)p.GreatTemper);
            p.GreatGuard = false;
            p.GreatTemper = 0;
            p.GreatLock = GreatGuardLockout;
            if (p.Downed || p.StunT > 0f)
                return;  // (taken out of the guard: no Rend)
            p.FireCd = Math.Max(p.FireCd, 0.3f / FireRate(p));
            if (n >= GreatTemperMax)
                p.GreatPrimed = true;
            V2 toward = aimAt - p.Pos;
            float aim = toward.Length > 1f ? toward.Angle : p.Aim;
            var s = NewSwing(p, aim, GreatRendReach, 0f, GreatRendDamage(n) * p.Stats.DmgMult);
            s.HalfWidth = GreatRendWidth + GreatRendWidthPer * n;
            s.Rend = true;
            s.Power = (float)n / GreatTemperMax;
            s.Heavy = true;
            pendingSwings.Add(s);
            p.SwingCount++;
        }

        /// Host, every tick: the chain forgets its place, the guard stops bullets, everything goes when the sword is put away.
        void TickGreat(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.GreatGap = Math.Min(p.GreatGap + dt, 5f);
                p.GreatLock = Math.Max(0f, p.GreatLock - dt);
                p.GreatBlockT = Math.Max(0f, p.GreatBlockT - dt);
                if (p.Weapon.Id != WeaponId.Greatsword)
                {
                    p.GreatStep = 0;
                    p.GreatTemper = 0;
                    p.GreatGuard = p.GreatPrimed = false;
                    continue;
                }
                if (p.GreatStep != 0 && p.GreatGap > GreatChainWindow)
                    p.GreatStep = 0;
                if (!p.GreatGuard)
                    continue;
                if (p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.ParryActive || State == MatchState.GameOver)
                {
                    p.GreatGuard = false;  // (the guard drops with what it held)
                    p.GreatTemper = 0;
                    continue;
                }
                GreatGuardBlock(p);
            }
        }

        readonly List<int> guardStopped = new List<int>();

        void GreatGuardBlock(Player p)
        {
            guardStopped.Clear();
            CollectShotsInArc(p.Pos, p.Aim, GreatGuardRadius, GreatGuardBand, GreatGuardHalfArc, guardStopped);
            bool full = false;
            foreach (int id in guardStopped)
            {
                Bullet b;
                if (!Bullets.TryGetValue(id, out b))
                    continue;
                AddFx(FxKind.Spark, b.Pos, Defs.BulletColor(Defs.KnightSlash), 8f, 0.2f);
                KillBullet(id);
                if (p.GreatTemper < GreatTemperMax && ++p.GreatTemper == GreatTemperMax)
                    full = true;
            }
            if (full)
            {
                BroadcastSound(Sfx.GreatFull);  // Temper is full: a chime
                p.GreatBlockT = GreatBlockSoundGap;
            }
            else if (guardStopped.Count > 0 && p.GreatBlockT <= 0f)
            {
                BroadcastSound(Sfx.GreatBlock);  // (a stream of shots rings the arc at most this often)
                p.GreatBlockT = GreatBlockSoundGap;
            }
        }

        /// Raising the parry cancels the chain (ResetAttackChains): back to the first cut, a rend still winding up never lands.
        void ResetGreatChain(Player p)
        {
            p.GreatStep = 0;
            p.GreatGap = 5f;
            p.FireCd = 0f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Delay > 0f && s.Heavy && !s.Rend);
            steelWalls.RemoveAll(c => c.Owner == p.Id && c.Wait > 0f);  // (and the crescent that was waiting for it)
        }
    }
}
