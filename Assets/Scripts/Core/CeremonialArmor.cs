using System;

namespace Arena
{
    /// The tank Ninja's Ceremonial Armor (was Iron Body). Each cast summons one lacquered armour
    /// plate (2.5 s cooldown), up to CerePlateMax. Plates stay until they break, in the "Mandala" layout of
    /// Docs/Mockups/ceremonial-armor.html: the first 4 spin fast on an inner ring, the next 6 turn the other way further out.
    /// A plate stops the enemy bullets it touches (losing their damage in health) and pushes enemies off it. With all 10 up
    /// ("full regalia") you take 20% less damage. Capstone: a plate that breaks bursts for 30 near it. Tempered Steel: +50%
    /// plate health a stack. The newest plate drops in over CereSummonTime and blocks nothing until it lands.
    /// Replicated: PlateN, PlateClock, PlateNewT, PlateMax and each plate's health (a byte); drawn by Game/CeremonialArmorLook.cs.
    public partial class Game
    {
        public const int CerePlateMax = 10, CereInnerMax = 4;
        public const float CerePlateHp = 45f, CereSummonTime = 0.55f, CereFullCut = 0.2f;
        public const float CereInnerR = 48f, CereOuterR = 95f, CereInnerSpin = 2.1f, CereOuterSpin = -1f, CereOuterPhase = 0.3f;
        public const float CereHitR = 13f, CereKnock = 26f, CereBurstDamage = 30f, CereBurstRadius = 60f;

        /// The i-th of n plates' angle and ring radius at this clock (the host, and every screen from the replicated clock).
        public static void CereSlot(int i, int n, float clock, out float ang, out float rad)
        {
            int inner = Math.Min(n, CereInnerMax), outer = n - inner;
            if (i < inner)
            {
                ang = clock * CereInnerSpin + i * MathX.Tau / inner;
                rad = CereInnerR;
            }
            else
            {
                ang = clock * CereOuterSpin + (i - inner) * MathX.Tau / Math.Max(1, outer) + CereOuterPhase;
                rad = CereOuterR;
            }
        }

        public static V2 CerePlate(Player p, int i)
        {
            float a, r;
            CereSlot(i, p.PlateN, p.PlateClock, out a, out r);
            return p.Pos + V2.FromAngle(a) * r;
        }

        /// Full regalia: all ten plates up (the newest one landed).
        public static bool CereFull(Player p) => p.PlateN >= CerePlateMax && p.PlateNewT <= 0f;

        /// The full set's damage cut (applied in Damage).
        static float CereCut(Player p) => CereFull(p) ? CereFullCut : 0f;

        /// The cast: one more plate, unless all ten are up (the key does nothing then: Game.cs keeps the cooldown).
        void SummonCerePlate(Player p, float pw, bool cap)
        {
            if (p.PlateN >= CerePlateMax)
                return;
            p.PlateMax = CerePlateHp * p.Stats.PlateHpMult * pw;
            p.PlateHp[p.PlateN] = p.PlateMax;
            p.PlateN++;
            p.PlateNewT = CereSummonTime;
            p.PlateBurst = cap;
            Sounds.Add(Sfx.CereSummon);  // (its click lands with the plate, CereSummonTime in)
        }

        /// Host, every tick: the clock turns, the newest plate lands, plates stop enemy bullets and push enemies away.
        void TickCereArmor(Player p, float dt)
        {
            if (p.PlateN <= 0)
                return;
            if (!CereSlotted(p))  // re-armed without it: the plates go
            {
                p.PlateN = 0;
                p.PlateNewT = 0f;
                return;
            }
            p.PlateClock += dt;
            if (p.PlateNewT > 0f)
            {
                p.PlateNewT = Math.Max(0f, p.PlateNewT - dt);
            }
            int live = p.PlateNewT > 0f ? p.PlateN - 1 : p.PlateN;  // (the one still dropping in blocks nothing)
            for (int i = 0; i < live; i++)
            {
                V2 at = CerePlate(p, i);
                foreach (var b in Bullets.Values)
                {
                    if (!Defs.IsEnemyShot(b.Kind) || b.Pos.FartherThan(at, CereHitR + b.Radius))
                        continue;
                    p.PlateHp[i] -= Math.Max(1f, b.Dmg);
                    BroadcastFx(FxKind.CereBlock, b.Pos, at, new Rgba(0.95f, 0.84f, 0.54f), 1f, 0.25f, Sfx.None);
                    KillBullet(b.Id);
                    break;  // (KillBullet changes Bullets: one a plate a tick is plenty)
                }
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f || e.Pos.FartherThan(at, CereHitR + e.Radius) || e.KnockLeft.Length > 1f)
                        continue;
                    ShoveFrom(e, p.Pos, CereKnock, V2.FromAngle(0f));
                }
                if (p.PlateHp[i] <= 0f)
                {
                    BreakCerePlate(p, i, at);
                    i--;
                    live--;
                }
            }
        }

        /// A plate breaks: it shatters (and bursts, mastered), and the ones after it close the gap.
        void BreakCerePlate(Player p, int i, V2 at)
        {
            float a, r;
            CereSlot(i, p.PlateN, p.PlateClock, out a, out r);
            BroadcastFx(FxKind.CereBreak, at, at + V2.FromAngle(a), new Rgba(0.76f, 0.21f, 0.17f), p.PlateBurst ? CereBurstRadius : 0f, 0.9f, Sfx.CereBreak);
            if (p.PlateBurst)
                foreach (var e in FoesInCircle(at, CereBurstRadius, by: p.Id))
                    HurtEnemy(e, CereBurstDamage * p.Stats.DmgMult, p.Id);
            for (int k = i; k < p.PlateN - 1; k++)
                p.PlateHp[k] = p.PlateHp[k + 1];
            p.PlateN--;
        }

        static bool CereSlotted(Player p)
        {
            foreach (var s in p.Slots)
                if (s.Id == AbilityId.CeremonialArmor)
                    return true;
            return false;
        }

        // --- Network: the player's Ceremonial Armor fields (SyncPlayer) ---

        void SyncCeremonialArmor(NetSync s, Player p)
        {
            s.Byte(ref p.PlateN);  // Ceremonial Armor
            if (p.PlateN > 0)
            {
                s.Float(ref p.PlateClock);
                s.Float(ref p.PlateNewT);
                s.Float(ref p.PlateMax);
                for (int k = 0; k < p.PlateN && k < Game.CerePlateMax; k++)
                {
                    int q = s.Reading ? 0 : (int)Math.Round(MathX.Clamp01(p.PlateHp[k] / Math.Max(1f, p.PlateMax)) * 255f);
                    s.Byte(ref q);
                    if (s.Reading)
                        p.PlateHp[k] = q / 255f * p.PlateMax;
                }
            }
        }
    }
}
