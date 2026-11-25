using System;

namespace Arena
{
    /// Vein Tithe (Tank Hemomancer ultimate, replaces Blood Rite), from Docs/Mockups/blood-rite-vein-tithe.html.
    /// A blood tendril grows from you to every enemy within TitheRadius of you (those still spawning or training dummies are skipped); an enemy
    /// that comes inside the radius while it lasts is tethered too (it stays held if it's later out of range) and holds it for TitheLen s (mastered TitheLenMastered): a held enemy can't move or shoot (`Enemy.GraspT`, kept
    /// topped up every tick). You pay TitheCost of your max health a second for each one held (TitheBossMult times that for a boss, mastered
    /// times TitheMasteredCost), and deal TitheOutputMult of your damage while it lasts. The drain is not a hit: it gives no hit grace and
    /// nothing cuts it, and it takes you down if it empties your health. You move at TitheSlow speed while it lasts (`Player.Speed`). It ends when the time runs out, when you go down, or when the
    /// ultimate is swapped; every tendril then lets go (`FxKind.TitheSnap`, one per enemy).
    /// `Player.TitheClock` (seconds since the cast, 0 = off) and `Player.Tithed` (the held enemies' ids, in the order the tendrils reach them)
    /// are replicated, so every screen draws the same tendrils (Game/VeinTitheLook.cs); `Player.UltT` holds the time left.
    public partial class Game
    {
        public const float TitheLen = 15f, TitheLenMastered = 20f, TitheReach = 1.2f, TitheReachStep = 0.05f, TitheReachTime = 0.55f;
        public const float TitheCost = 0.004f, TitheBossMult = 4f, TitheMasteredCost = 0.75f, TitheOutputMult = 0.6f, TitheSlow = 0.65f;
        public const float TitheRadius = 380f;  // max reach around the player: only enemies inside it are tethered
        const float TitheHold = 0.25f;  // how long each tick tops up an enemy's hold

        /// A Vein Tithe is holding enemies for this player.
        public static bool TitheActive(Player p) => p.TitheClock > 0f;

        /// Damage multiplier on everything you deal while it lasts (HurtEnemy).
        public static float TitheOutput(Player p) => TitheActive(p) ? TitheOutputMult : 1f;

        /// How far the tendril to the `k`th held enemy has grown, 0 to 1, `el` seconds after the cast: each starts a little after the one before.
        public static float TitheGrown(float el, int k)
        {
            float x = Math.Max(0f, Math.Min(1f, (el - k * TitheReachStep) / TitheReachTime));
            return x < 0.5f ? 2f * x * x : 1f - (float)Math.Pow(-2f * x + 2f, 2f) / 2f;
        }

        /// What the held enemies cost you per second right now, as a fraction of your max health.
        public float TitheRate(Player p)
        {
            float rate = 0f;
            foreach (int id in p.Tithed)
            {
                Enemy e;
                if (TryFoe(id, out e) && e.Hp > 0f)
                    rate += TitheCost * (e.Def.IsBoss ? TitheBossMult : 1f);
            }
            return rate * (Mastered(p, AbilityId.VeinTithe) ? TitheMasteredCost : 1f);
        }

        void CastTithe(Player p, bool cap)
        {
            p.UltT = cap ? TitheLenMastered : TitheLen;
            p.TitheClock = 0.0001f;
            p.Tithed.Clear();
            var near = new System.Collections.Generic.List<Enemy>();
            foreach (var e in Enemies.Values)
                if (Targetable(e) && e.Pos.DistanceTo(p.Pos) <= TitheRadius)
                    near.Add(e);
            near.Sort((a, b) =>
            {
                int c = a.Pos.DistanceTo(p.Pos).CompareTo(b.Pos.DistanceTo(p.Pos));
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            foreach (var e in near)
            {
                if (p.Tithed.Count >= 255)
                    break;
                p.Tithed.Add(e.Id);
                e.GraspT = Math.Max(e.GraspT, TitheHold);
            }
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.85f, 0.12f, 0.2f), 150f, 0.6f, Sfx.Roar);
        }

        /// Every tendril lets go. `snap` sends the let-go effect (not when the player's slot changed under them).
        void EndTithe(Player p, bool snap = true)
        {
            if (p.TitheClock <= 0f)
                return;
            if (snap)
                foreach (int id in p.Tithed)
                {
                    Enemy e;
                    if (TryFoe(id, out e) && e.Hp > 0f)
                        BroadcastFx(FxKind.TitheSnap, p.Pos, e.Pos, new Rgba(0.85f, 0.12f, 0.2f, e.Def.IsBoss ? 1f : 0.5f), e.Radius, TitheReachTime + 0.2f, Sfx.None);
                }
            p.TitheClock = 0f;
            p.Tithed.Clear();
            p.UltT = 0f;
        }

        /// Host, every tick: keep the held enemies held, drop the dead, charge for the rest, and end it.
        void TickTithe(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.TitheClock <= 0f)
                    continue;
                if (p.Downed || p.UltT <= 0f || p.Slots[Upgrades.UltSlot].Id != AbilityId.VeinTithe)
                {
                    EndTithe(p, !p.Downed || p.UltT > 0f);
                    continue;
                }
                p.TitheClock += dt;
                p.Tithed.RemoveAll(id => { Enemy d; return !TryFoe(id, out d) || d.Hp <= 0f; });
                foreach (var e in Enemies.Values)
                    if (Targetable(e) && p.Tithed.Count < 255 && e.Pos.DistanceTo(p.Pos) <= TitheRadius && !p.Tithed.Contains(e.Id))
                        p.Tithed.Add(e.Id);
                foreach (int id in p.Tithed)
                    Enemies[id].GraspT = Math.Max(Enemies[id].GraspT, TitheHold);
                if (p.TitheClock < TitheReach || p.DevGod)
                    continue;
                float cost = p.MaxHp * TitheRate(p) * dt;
                if (cost <= 0f)
                    continue;
                float paid = Math.Min(cost, Math.Max(0f, p.Hp));
                p.Hp -= paid;
                p.Run.DamageTaken += paid;
                DownIfDead(p, "Vein Tithe");
            }
        }
    }
}
