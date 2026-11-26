using System;
using System.Collections.Generic;

namespace Arena
{
    /// A Literal Tank (Tank Soldier, the third ultimate: `AbilityId.LiteralTank`, key 2 of the lobby's Ultimate row), from
    /// Docs/Mockups/a-literal-tank.html. A tank drops on you (the first TankDrop s are the look's landing; it shoves enemies off
    /// TankDropShove px) and for TankTime s you drive it:
    ///   - its TankArmor armour (mastered TankArmorMastered) soaks damage before your shields and health (`TankSoak`, from `Damage`);
    ///   - you move at TankSlow speed; the hull turns to where you drive and the turret to your cursor (the look);
    ///   - Fire shoots a big explosive shell every TankRate s (Defs.TankShell, TankShellSpeed): TankHit to what it strikes, and
    ///     60% of that (TankBlast) to everything else within TankBlastR (mastered x TankBlastMastered); a shell that hits nobody
    ///     bursts at your cursor (no farther than TankRange) for TankBlast;
    ///   - small enemies you drive into are crushed (killed outright); heavies and bosses take TankCrush and a shove instead, at
    ///     most once every TankCrushEvery each;
    ///   - your weapon, its Alt, your abilities and the Dodge are locked while you're inside.
    /// It ends when the time runs out, the armour is gone, you go down, or the wave is over: you hop out and the empty tank blows
    /// up where it stands (TankWreck damage within TankWreckR). `Player.TankT` (time left, 0 = off), `TankArmor` and `TankMax`
    /// are replicated; Game/LiteralTankLook.cs draws it.
    public partial class Game
    {
        public const float TankTime = 15f;
        public const float TankDrop = 0.6f;                 // the tank falling on you (drawing; you're inside from the press)
        public const float TankDropR = 120f, TankDropShove = 120f;
        public const float TankArmor = 400f, TankArmorMastered = 550f;
        public const float TankSlow = 0.75f;                // your speed in it
        public const float TankRate = 0.9f;                 // seconds between shells
        public const float TankHit = 75f, TankBlastShare = 0.6f;  // a shell's direct hit; the blast round it does this share (45)
        public const float TankBlast = TankHit * TankBlastShare;
        public const float TankBlastR = 95f, TankBlastMastered = 1.3f;
        public const float TankShellSpeed = 900f, TankRange = 620f, TankMinRange = 120f;
        public const float TankMuzzle = 62f;                // the shell leaves the barrel's end, this far ahead of you
        public const float TankBodyR = 40f;                 // the hull's reach for crushing
        public const float TankCrush = 25f, TankCrushShove = 90f, TankCrushEvery = 0.5f;
        public const float TankWreck = 90f, TankWreckR = 150f;
        public const float TankBotRange = 480f;             // a bot drives one out with an enemy this close
        public static readonly Rgba TankOlive = new Rgba(0.36f, 0.42f, 0.2f), TankFire = new Rgba(1f, 0.62f, 0.25f);

        /// Its entry: key 2 of the Tank Soldier's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] TankUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.LiteralTank, Kit = Kit.Soldier, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "A Literal Tank", Cooldown = 80f, Desc = "Drive an actual tank for 15 s: heavy armour, big explosive shells, and it runs enemies over.",
                Capstone = "550 armour, and the shells blast 30% wider.",
                Bot = b => b.HaveTarget && b.TargetD < TankBotRange,
            },
        };

        /// Inside the tank.
        public static bool TankOn(Player p) => p.TankT > 0f && !p.Downed;

        /// Seconds since the tank landed on you (for the look), from the time left.
        public static float TankSince(Player p) => TankTime - p.TankT;

        readonly Dictionary<long, float> tankCrushed = new Dictionary<long, float>();  // host: (player, enemy) -> seconds until it can be crushed again
        readonly List<long> tankCrushDone = new List<long>();

        /// Host: the tank lands on you.
        void CastTank(Player p, bool mastered)
        {
            p.TankT = TankTime;
            p.TankMax = p.TankArmor = mastered ? TankArmorMastered : TankArmor;
            p.TankMastered = mastered;
            p.TankCd = TankDrop;  // (no shot until it has landed)
            p.UltT = TankTime;
            foreach (var e in Enemies.Values)
                if (Targetable(e) && !Heavy(e) && e.Pos.CloserThan(p.Pos, TankDropR + e.Radius))
                    ShoveFrom(e, p.Pos, TankDropShove);
            BroadcastFx(FxKind.Ring, p.Pos, TankOlive, TankDropR, TankDrop, Sfx.LitTankFall);  // (the landing's crash is played by each screen: LiteralTankLook.cs)
        }

        /// You hop out (time, armour gone, down, or the wave over) and the empty tank blows up.
        void EndTank(Player p)
        {
            if (p.TankT <= 0f && p.TankArmor <= 0f)
                return;
            p.TankT = 0f;
            p.TankArmor = 0f;
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.LiteralTank)
                p.UltT = 0f;
            if (Battle)
                foreach (var e in FoesInCircle(p.Pos, TankWreckR, by: p.Id))
                    HurtEnemy(e, TankWreck * p.Stats.DmgMult, p.Id);
            p.TankWrecks++;
            BroadcastFx(FxKind.Boom, p.Pos, TankFire, TankWreckR, 0.7f, Sfx.BigExplode);
            PropBlast(p.Pos, TankWreckR, p.Id);
        }

        /// Host, every tick: the time, crushing what you drive into, and getting out.
        void TickTanks(float dt)
        {
            if (tankCrushed.Count > 0)
            {
                tankCrushDone.Clear();
                foreach (var kv in tankCrushed)
                    if (kv.Value - dt <= 0f)
                        tankCrushDone.Add(kv.Key);
                foreach (var k in tankCrushDone)
                    tankCrushed.Remove(k);
                tankCrushDone.Clear();
                foreach (var k in new List<long>(tankCrushed.Keys))
                    tankCrushed[k] -= dt;
            }
            foreach (var p in Players.Values)
            {
                if (p.TankT <= 0f)
                    continue;
                p.TankT = Math.Max(0f, p.TankT - dt);
                if (p.TankT <= 0f || p.TankArmor <= 0f || p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.LiteralTank)
                {
                    EndTank(p);
                    continue;
                }
                if (TankSince(p) >= TankDrop)
                    TankCrushes(p);
            }
        }

        readonly List<Enemy> tankUnder = new List<Enemy>();

        void TankCrushes(Player p)
        {
            tankUnder.Clear();
            foreach (var e in Enemies.Values)
                if (Targetable(e) && e.Pos.CloserThan(p.Pos, TankBodyR + e.Radius))
                    tankUnder.Add(e);
            foreach (var e in tankUnder)
            {
                if (!Heavy(e))
                {
                    HurtEnemy(e, Math.Max(e.Hp, 1f) * 50f + 1000f, p.Id);  // run over (enough through any armour)
                    if (e.Hp <= 0f)
                    {
                        p.TankCrushes++;
                        BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, e.Radius + 10f, 0.3f, Sfx.Hit);
                    }
                    continue;
                }
                long key = (long)p.Id << 32 | (uint)e.Id;
                if (tankCrushed.ContainsKey(key))
                    continue;
                tankCrushed[key] = TankCrushEvery;
                HurtEnemy(e, TankCrush * p.Stats.DmgMult, p.Id);
                if (!e.Def.IsBoss)
                    ShoveFrom(e, p.Pos, TankCrushShove);
                BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.8f, 0.8f, 0.75f), e.Radius + 8f, 0.25f, Sfx.Clang);
            }
        }

        /// Host, from TickPlayers: the main gun, in place of your weapon.
        void TickTankGun(Player p, float dt)
        {
            p.TankCd -= dt;
            if (!p.Firing || p.TankCd > 0f || State == MatchState.GameOver)
                return;
            p.TankCd = TankRate;
            V2 dir = V2.FromAngle(p.Aim);
            V2 from = p.Pos + dir * TankMuzzle;
            float range = Math.Max(TankMinRange, Math.Min(TankRange, p.AimAt.IsZero ? TankRange : p.AimAt.DistanceTo(p.Pos)));
            float life = Math.Max(0.05f, (range - TankMuzzle) / TankShellSpeed);
            var b = SpawnBullet(Defs.TankShell, from, dir * TankShellSpeed, 0f, life, TankHit * p.Stats.DmgMult, p.Id);
            b.Splash = TankBlastR * (p.TankMastered ? TankBlastMastered : 1f);
            b.Burst = 1;  // bursts where its flight ends (MoveBullets: TankShellBurst)
            p.TankShells++;
            BroadcastFx(FxKind.Ring, from, TankFire, 26f, 0.14f, Sfx.None);  // the muzzle flash
        }

        /// A shell that reached the end of its flight: it bursts there for the blast's damage.
        void TankShellBurst(Bullet b)
        {
            foreach (var e in FoesInCircle(b.Pos, b.Splash, b.Pos, by: b.Owner))
                HurtEnemy(e, b.Dmg * TankBlastShare, b.Owner);
            BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(Defs.TankShell), b.Splash, 0.45f, Sfx.Explode);
            PropBlast(b.Pos, b.Splash, b.Owner);
            GateLockBlast(b.Pos, b.Splash, b.Owner);
        }

        /// From Damage: the armour takes what it can; returns what is left for shields and health.
        float TankSoak(Player p, float amount)
        {
            if (!TankOn(p) || p.TankArmor <= 0f || amount <= 0f)
                return amount;
            float soak = Math.Min(p.TankArmor, amount);
            p.TankArmor -= soak;
            return amount - soak;
        }

        // --- Network: the player's Literal Tank fields (SyncPlayer) ---

        void SyncLiteralTank(NetSync s, Player p)
        {
            s.Float(ref p.TankT);  // A Literal Tank
            s.Float(ref p.TankArmor);
            s.Float(ref p.TankMax);
        }
    }
}
