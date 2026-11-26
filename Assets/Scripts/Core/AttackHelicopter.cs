using System;

namespace Arena
{
    /// The Attack Helicopter: the DPS Soldier's second ultimate (`AbilityId.AttackHelicopter`, key 1 of the lobby's Ultimate row;
    /// Artillery Assault stays the default), the Orbit look of Docs/Mockups/attack-helicopter.html.
    /// F calls in a gunship: it flies in over HeliArrive s, then for HeliStation s it circles its caller at HeliOrbitR px and each of
    /// its guns (1, 2 mastered) locks one enemy at a time, the one nearest the caller within HeliLeash px (another gun's target is
    /// skipped), and fires HeliRate rounds a second at it, HeliDamage each times the caller's damage multiplier, until it dies or
    /// strays out of the leash; then the next. A new target takes HeliLock s to lock on, and the barrels take HeliSpin s to spin up
    /// once. The rounds are dealt on the host, not as bullets (the helicopter flies above the walls and can't be hit).
    /// It ends when its time runs out, its caller goes down, or the wave is over. `Player.UltT` is its time left (HeliTime at the cast);
    /// `HeliGuns`, `HeliTgt` and `HeliTgt2` are replicated, so every screen draws the same gunship and tracers
    /// (Game/AttackHelicopterLook.cs, which also flies it in and away).
    public partial class Game
    {
        public const float HeliArrive = 0.9f, HeliStation = 10f, HeliTime = HeliArrive + HeliStation;
        public const float HeliRate = 20f, HeliDamage = 8f;     // rounds a second, and each one's damage (times your damage multiplier)
        public const float HeliLeash = 560f;                     // it only shoots enemies this close to its caller
        public const float HeliLock = 0.25f, HeliSpin = 0.35f;   // a new target's lock-on; the barrels' first spin-up
        public const float HeliOrbitR = 230f, HeliOrbitSquash = 0.8f, HeliOrbitSpeed = 0.55f;  // its circle round the caller (radians a second)

        /// The gunship is out (flying in or on station).
        public static bool HeliOut(Player p) => p.HeliGuns > 0 && p.UltT > 0f;

        /// Seconds since it was called, from the time left.
        public static float HeliSince(Player p) => HeliTime - p.UltT;

        /// Where its circle puts it, `since` seconds after the call, round its caller at `at`.
        public static V2 HeliOrbit(V2 at, float since)
        {
            float a = -2.4f + Math.Max(0f, since - HeliArrive) * HeliOrbitSpeed;
            return at + new V2((float)Math.Cos(a) * HeliOrbitR, (float)Math.Sin(a) * HeliOrbitR * HeliOrbitSquash);
        }

        void CastHeli(Player p, bool mastered)
        {
            p.UltT = HeliTime;
            p.HeliGuns = (byte)(mastered ? 2 : 1);
            p.HeliTgt = p.HeliTgt2 = 0;
            p.HeliLock = p.HeliLock2 = p.HeliCd = p.HeliCd2 = p.HeliSpin = 0f;
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.94f, 0.75f, 0.26f), 200f, 0.8f, Sfx.Ability);
        }

        void EndHeli(Player p)
        {
            p.HeliGuns = 0;
            p.HeliTgt = p.HeliTgt2 = 0;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.AttackHelicopter)
                p.UltT = 0f;
        }

        /// Host, every tick (after the shots have moved and hit).
        void TickHelis(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.HeliGuns == 0)
                    continue;
                if (p.UltT <= 0f || p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.AttackHelicopter)
                {
                    EndHeli(p);
                    continue;
                }
                if (HeliSince(p) < HeliArrive)
                    continue;  // still flying in
                HeliGun(p, dt, ref p.HeliTgt, ref p.HeliLock, ref p.HeliCd, p.HeliTgt2);
                if (p.HeliGuns > 1)
                    HeliGun(p, dt, ref p.HeliTgt2, ref p.HeliLock2, ref p.HeliCd2, p.HeliTgt);
                else
                    p.HeliTgt2 = 0;
                if (p.HeliTgt != 0 || p.HeliTgt2 != 0)
                    p.HeliSpin += dt;
            }
        }

        /// One gun: keep its target or find the next, then fire what this tick holds.
        void HeliGun(Player p, float dt, ref int tgt, ref float lockT, ref float cd, int other)
        {
            Enemy e;
            if (tgt == 0 || !TryFoe(tgt, out e) || !Targetable(e) || !e.Pos.CloserThan(p.Pos, HeliLeash + 40f))
            {
                e = HeliPick(p, other);
                tgt = e != null ? e.Id : 0;
                lockT = 0f;
                cd = 0f;
            }
            if (e == null)
                return;
            lockT += dt;
            if (lockT < HeliLock || p.HeliSpin < HeliSpin)
                return;
            cd -= dt;
            while (cd <= 0f && e.Hp > 0f)
            {
                cd += 1f / HeliRate;
                HurtEnemy(e, HeliDamage * p.Stats.DmgMult, p.Id);
                p.HeliRounds++;
            }
        }

        /// The enemy nearest the caller within the leash, other than the other gun's.
        Enemy HeliPick(Player p, int other)
        {
            Enemy best = null;
            float bestD = HeliLeash * HeliLeash;
            foreach (var e in Enemies.Values)
            {
                if (e.Id == other || !Targetable(e))
                    continue;
                float d = e.Pos.DistanceSq(p.Pos);
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        // --- Network: the player's Attack Helicopter fields (SyncPlayer) ---

        void SyncHelicopter(NetSync s, Player p)
        {
            s.Byte(ref p.HeliGuns);  // the Attack Helicopter: its guns, then their targets while it's out
            if (p.HeliGuns > 0)
            {
                s.Int(ref p.HeliTgt);
                s.Int(ref p.HeliTgt2);
            }
            else if (s.Reading)
                p.HeliTgt = p.HeliTgt2 = 0;
        }
    }
}
