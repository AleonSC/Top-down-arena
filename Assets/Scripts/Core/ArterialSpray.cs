using System;

namespace Arena
{
    /// The DPS Hemomancer's Arterial Spray (`AbilityId.BloodBoil` kept; was Blood Boil). Press to open a vein: a 60° jet
    /// of blood follows your aim and hurts everything in it (Blood Price makes it hit harder as your health falls), slowing
    /// and bleeding what it soaks and pushing small enemies back. It drains you while it runs; press again to stop, and it
    /// sputters out at 15% health. The cooldown starts when it stops (like Blood Shell).
    /// Player.SprayOn is replicated, and the jet follows the replicated aim: Game/ArterialSprayLook.cs (look A "Jet" of
    /// Docs/Mockups/arterial-spray.html).
    public partial class Game
    {
        public const float SprayReach = 200f;                    // the jet's length...
        public const float SprayHalfArc = 30f * MathX.Pi / 180f; // ...and half its spread
        public const float SprayFloor = 0.15f;                   // it sputters out at this share of your max health
        const float SprayDps = 30f, SprayDrain = 0.04f;          // damage a second to each enemy in it; your max health a second it costs
        const float SprayTickTime = 0.1f;                        // damage lands in ticks this far apart
        const float SprayBleed = 1.5f, SpraySlow = 0.2f, SprayPush = 40f;  // bleed added a tick (stacks up to 5); the slow; px/s small enemies are pushed

        /// Pressing Arterial Spray again while it runs closes the vein (no cooldown is spent for the press itself).
        public bool SprayRelease(Player p, int slot) => p.SprayOn && p.Slots[slot].Id == AbilityId.BloodBoil;

        /// How far a player's jet reaches (High Pressure makes it longer).
        public static float SprayReachOf(Player p) => SprayReach * p.Stats.SprayReach;

        /// Is this point (with this radius) in the jet fired from `from` along `aim`?
        public static bool InSpray(V2 from, float aim, float reach, V2 pos, float radius)
        {
            if (pos.DistanceTo(from) > reach + radius)
                return false;
            return InGraspArc(from, aim, pos, radius + 6f, SprayHalfArc * 2f);  // (the same cone test as Leech Tendrils)
        }

        /// Host: Arterial Spray was cast.
        void OpenSpray(Player p, float power, bool mastered)
        {
            p.SprayOn = true;
            p.SprayT = 0f;
            p.SprayTick = 0f;
            p.SprayPower = power;
            p.SprayMastered = mastered;
            Sounds.Add(Sfx.Ability);
            if (p.Hp <= p.MaxHp * SprayFloor + 0.01f)
                CloseSpray(p, true);  // nothing left to spray
        }

        /// Host, every tick: it drains you, and every SprayTickTime it hurts, slows and bleeds what's in the jet.
        void TickSpray(Player p, float dt)
        {
            if (!p.SprayOn)
                return;
            if (State == MatchState.GameOver)
            {
                CloseSpray(p, false);
                return;
            }
            p.SprayT += dt;
            PayHealth(p, p.MaxHp * SprayDrain * dt);
            if (p.Hp <= p.MaxHp * SprayFloor + 0.01f)
            {
                CloseSpray(p, true);
                return;
            }
            p.SprayTick -= dt;
            if (p.SprayTick > 0f)
                return;
            p.SprayTick += SprayTickTime;
            var s = p.Stats;
            float reach = SprayReachOf(p), dmg = SprayDps * SprayTickTime * s.SprayMult * s.DmgMult * p.SprayPower;
            V2 from = p.Pos, dir = V2.FromAngle(p.Aim);
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || !InSpray(from, p.Aim, reach, e.Pos, e.Radius))
                    continue;
                HurtEnemy(e, dmg, p.Id);
                ApplyBleed(e, SprayBleed * p.SprayPower, p.Id);
                e.SlowT = Math.Max(e.SlowT, 0.3f);
                e.SlowMult = Math.Min(e.SlowMult, 1f - SpraySlow);
                if (!Heavy(e))
                {
                    V2 away = (e.Pos - from).Normalized();
                    e.Pos = ClampToArena(e.Pos + (away.IsZero ? dir : away) * SprayPush * SprayTickTime, e.Radius);
                }
            }
            if (p.SprayMastered)
            {
                // Mastered: the jet washes enemy bullets away.
                var erase = new System.Collections.Generic.List<int>();
                foreach (var b in Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind) && InSpray(from, p.Aim, reach, b.Pos, b.Radius))
                        erase.Add(b.Id);
                foreach (int bid in erase)
                {
                    Bullets.Remove(bid);
                    pendingRemoved.Add(bid);
                }
            }
        }

        /// The vein closes (sputter: your health ran down to the floor; otherwise you pressed again). Its cooldown starts now.
        void CloseSpray(Player p, bool sputter)
        {
            p.SprayOn = false;
            for (int i = 0; i < p.Slots.Length; i++)
                if (p.Slots[i].Id == AbilityId.BloodBoil && p.Slots[i].Charges <= 0)
                    p.Slots[i].Timer = Cooldown(p, i);
            if (sputter)
                BroadcastFx(FxKind.Bleed, p.Pos, new V2(p.Id, 0f), new Rgba(0.85f, 0.12f, 0.2f), 6f, 0.5f, Sfx.None);
        }
    }
}
