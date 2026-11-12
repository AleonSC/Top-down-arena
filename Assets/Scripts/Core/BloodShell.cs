using System;

namespace Arena
{
    /// The Hemomancer tank's Blood Shell: press the ability and a big bubble of blood stands round you (press again to drop it). Enemy bullets stop at
    /// its skin and enemies can't walk in; you and your allies can stand inside. Every bullet it stops costs you health, and
    /// every ally inside gets a little back. It also bleeds you slowly the whole time it stands, and it
    /// stands until you press again or your health falls to a third, when it bursts.
    /// Player.ShellOn is replicated (the shell's wear is just your health); the cancel press goes to the host like any cast. Drawn in Game/BloodShellLook.cs (look B "Heartbeat" of Docs/Mockups/blood-shell.html).
    public partial class Game
    {
        public const float ShellRadius = 96f;
        public const float ShellPopFrac = 1f / 3f;   // it bursts when your health falls to this share of your max
        const float ShellCost = 7f, ShellHeal = 4f;  // health you pay / each ally inside gets, per bullet it stops
        const float ShellDrain = 1.5f;               // health a second it bleeds you just for standing (bullets cost far more)
        const float ShellPopShove = 150f;            // enemies near a burst are flung this far out

        /// Pressing Blood Shell again while it stands takes it down (no cooldown is spent for the press itself).
        public bool ShellRelease(Player p, int slot) => p.ShellOn && p.Slots[slot].Id == AbilityId.BloodShell;

        /// Host: the Blood Shell was cast.
        void RaiseShell(Player p, float power, bool mastered)
        {
            p.ShellOn = true;
            p.ShellT = 0f;
            p.ShellPower = power;
            p.ShellMastered = mastered;
            KeepOutOfShell(0f);  // enemies already inside are put out at the skin
        }

        /// Host, every tick: it bleeds you, and comes down at a third health, or when you go down.
        void TickBloodShell(Player p, float dt)
        {
            if (!p.ShellOn)
                return;
            p.ShellT += dt;
            if (p.Downed || State == MatchState.GameOver)
                EndShell(p, false);
            else if (p.Hp <= p.MaxHp * ShellPopFrac + 0.01f)
                EndShell(p, true);
            else
            {
                PayHealth(p, ShellDrain * dt, "Blood Shell");
                if (p.Hp <= p.MaxHp * ShellPopFrac + 0.01f)
                    EndShell(p, true);
            }
        }

        /// The shell comes down (burst: your health ran out to a third; otherwise you pressed again). Its cooldown starts now.
        void EndShell(Player p, bool burst)
        {
            p.ShellOn = false;
            for (int i = 0; i < p.Slots.Length; i++)
                if (p.Slots[i].Id == AbilityId.BloodShell && p.Slots[i].Charges <= 0)
                    p.Slots[i].Timer = Cooldown(p, i);
            var blood = new Rgba(0.85f, 0.12f, 0.2f);
            BroadcastFx(FxKind.ShellPop, p.Pos, new V2(p.Id, burst ? 1f : 0f), blood, ShellRadius, burst ? 0.7f : 0.35f, burst ? Sfx.Shockwave : Sfx.None);
            if (!burst)
                return;
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || Heavy(e))
                    continue;
                V2 off = e.Pos - p.Pos;
                float d = off.Length;
                if (d > ShellRadius + e.Radius + 70f)
                    continue;
                V2 away = d > 0.01f ? off / d : V2.FromAngle(e.Id * 0.9f);
                e.Pos = ClampToArena(e.Pos + away * ShellPopShove, e.Radius);
            }
            if (p.ShellMastered)
            {
                EraseEnemyBullets(p.Pos, 160f);
                BroadcastFx(FxKind.Ring, p.Pos, blood, 160f, 0.4f, Sfx.None);
            }
        }

        /// A bullet reached the skin: it stops, you pay for it, and every ally inside gets a little back.
        void ShellHit(Player p, Bullet b)
        {
            float cost = ShellCost * (float)Math.Pow(0.75, p.Stats.ShellBonus);
            float heal = ShellHeal * (1f + 0.5f * p.Stats.ShellBonus) * p.ShellPower;
            PayHealth(p, cost, "Blood Shell");
            foreach (var q in Players.Values)
                if (q != p && !q.Downed && q.Pos.DistanceTo(p.Pos) < ShellRadius)
                    Heal(q, heal, p);
            BroadcastFx(FxKind.ShellHit, b.Pos, p.Pos, new Rgba(0.85f, 0.12f, 0.2f), ShellRadius, 0.55f, Sfx.Hit);
        }

        /// Enemies can't be inside a shell: anything that is (at the cast, or pressed in) is moved to its skin and slid round it.
        void KeepOutOfShell(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (!p.ShellOn || p.Downed || p.SwallowT > 0f)
                    continue;
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f)
                        continue;
                    float lim = ShellRadius + e.Radius;
                    V2 off = e.Pos - p.Pos;
                    float d = off.Length;
                    if (d >= lim)
                        continue;
                    float a = d > 0.01f ? (float)Math.Atan2(off.Y, off.X) : (e.Id % 7) * 0.9f;
                    a += (e.Id % 2 == 0 ? 1f : -1f) * 1.6f * dt;
                    e.Pos = ClampToArena(p.Pos + V2.FromAngle(a) * lim, e.Radius);
                }
            }
        }
    }
}
