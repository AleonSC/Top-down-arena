using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The DPS Hemomancer's fourth weapon, the Embolus Spears (EmbolusSpears.cs).
static partial class Program
{
    /// Fire throws the left spear, then the right, for 20% max health each; they pierce every enemy and stick at the arena's edge; a
    /// third Fire recalls both, piercing again, and the catch gives the health back. Alt bursts the spears out for half the health
    /// back and new ones harden; holding Fire throws and recalls on its own; the other screen sees the spears; Burst Vessels widens
    /// the burst; putting the weapon away crumbles them.
    static void EmbolusTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Bleeder", Role.Dps, 3, 0, Kit.Hemomancer);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.EmbolusSpears && Defs.WeaponCount(Role.Dps, Kit.Hemomancer) == 4, "the Hemomancer DPS's fourth key is the Embolus Spears (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f), far = new V2(1500f, 450f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action<float, V2, bool, bool> Step = (secs, cursor, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var e in g.Enemies.Values) { e.FireT = 99f; e.Speed = 0f; }
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = cursor, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput()); }
            }
        };
        Action Click = () => { Step(dt * 2f, far, true, false); Step(0.3f, far, false, false); };
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        me.Hp = me.MaxHp;
        float full = me.Hp, cost = me.MaxHp * Game.EsCost;

        // two clicks: the left spear, then the right, each for 20% max health
        var a = Dummy(from + new V2(150f, 0f));
        var b = Dummy(from + new V2(320f, 0f));
        Step(dt * 2f, far, true, false);
        Check(me.EsSt[0] == Game.EsFlying && me.EsSt[1] == Game.EsInHand, "the first click throws the left spear");
        Check(Math.Abs(me.Hp - (full - cost)) < 0.5f, "it costs 20% of max health (" + (full - me.Hp).ToString("0.#") + " of " + me.MaxHp.ToString("0") + ")");
        Step(0.3f, far, false, false);
        Click();
        Check(me.EsSt[1] != Game.EsInHand && Math.Abs(me.EsPaid[1] - cost) < 0.5f && me.Hp < full - 2f * cost + 6f, "the second throws the right one, another 20% (hits steal a little back: " + me.Hp.ToString("0.#") + ")");
        float aOut = a.MaxHp - a.Hp, bOut = b.MaxHp - b.Hp;
        Check(aOut > 50f && bOut > 50f && aOut < 2.2f * 30f * 1.5f, "both spears pierce both enemies on the line, once each (" + aOut.ToString("0") + ", " + bOut.ToString("0") + ")");
        Step(1.5f, far, false, false);
        Check(me.EsSt[0] == Game.EsStuck && me.EsSt[1] == Game.EsStuck, "both stick in the wall");
        float edge = g.ArenaMax.X;
        Check(me.EsPos.All(p => p.X + Game.EsLen / 2f >= edge && p.X + Game.EsLen / 2f <= edge + Game.EsSink + 7f), "with their tips just into the arena's right edge (" + (me.EsPos[0].X + Game.EsLen / 2f).ToString("0") + " vs " + edge.ToString("0") + ")");
        Step(dt * 3f, far, false, false);
        var other = client.Game.Players[1];
        Check(other.EsSt[0] == Game.EsStuck && other.EsSt[1] == Game.EsStuck && other.EsPos[0].DistanceTo(me.EsPos[0]) < 1f, "the other screen sees them stuck where they are");

        // the third click recalls both: they pierce again on the way back, and the catch heals what they cost
        float aBefore = a.Hp;
        Click();
        Check(me.EsSt.All(x => x == Game.EsReturning || x == Game.EsInHand), "the third click recalls both");
        Step(1.5f, far, false, false);
        Check(me.EsSt.All(x => x == Game.EsInHand), "both are caught back in the hands");
        Check(a.Hp < aBefore, "they hit again on the way back (" + (aBefore - a.Hp).ToString("0") + ")");
        Check(Math.Abs(me.Hp - full) < 0.5f, "and the health they cost is back (" + me.Hp.ToString("0.#") + " of " + full.ToString("0.#") + ")");

        // Alt: the spears out burst where they are, for half the health back; new ones harden in the hands
        g.Enemies.Clear();
        var wall = Dummy(new V2(edge - 70f, 470f));
        var safe = Dummy(new V2(edge - 400f, 700f));
        Click(); Click();
        Step(1.3f, far, false, false);
        float wallBefore = wall.Hp, hpBefore = me.Hp;
        Step(dt * 4f, far, false, true);
        Check(g.Effects.Any(e => e.Kind == FxKind.EmbolusBlast), "Alt bursts them (FxKind.EmbolusBlast)");
        Check(wall.Hp < wallBefore - 2f * 30f && safe.Hp == safe.MaxHp, "the burst hurts an enemy by the wall hard, not one far off (" + (wallBefore - wall.Hp).ToString("0") + ")");
        Check(me.Hp - hpBefore >= cost - 0.5f && me.Hp - hpBefore < 1.5f * cost, "and gives back only half the health (+" + (me.Hp - hpBefore).ToString("0.#") + " for 2 x " + cost.ToString("0") + " spent)");
        Check(me.EsSt.All(x => x == Game.EsHardening), "new spears harden in the hands");
        Step(Game.EsRegrow + 0.05f, far, false, false);
        Check(me.EsSt.All(x => x == Game.EsInHand), "and are ready after " + Game.EsRegrow + " s");
        Step(dt * 4f, far, false, true);
        Check(me.EsSt.All(x => x == Game.EsInHand), "Alt with nothing out does nothing");

        // held down: throws both, waits for them to stick, recalls, and goes again
        me.Hp = me.MaxHp;
        g.Enemies.Clear();
        int recalls = 0, last = 0;
        for (int f = 0; f < 4f / dt; f++)
        {
            Step(dt, far, true, false);
            if (me.EsSt[0] == Game.EsReturning && last != Game.EsReturning) recalls++;
            last = me.EsSt[0];
        }
        Check(recalls >= 2, "holding Fire throws and recalls on its own (" + recalls + " recalls in 4 s)");
        Step(1.5f, far, false, false);

        // Burst Vessels; putting the weapon away crumbles the spears out
        float r0 = g.EsBlastRadius(me);
        var card = Upgrades.All.First(u => u.Name == "Burst Vessels");
        Check(card.Weapons != null && card.Weapons.Contains(WeaponId.EmbolusSpears), "Burst Vessels is an Embolus Spears card");
        card.Apply(me.Stats);
        Check(Math.Abs(g.EsBlastRadius(me) - r0 * 1.3f) < 0.5f, "and widens the burst by 30% (" + r0.ToString("0") + " -> " + g.EsBlastRadius(me).ToString("0") + ")");
        Click();
        var keep = me.Weapon;
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Implosion);
        Step(dt * 2f, far, false, false);
        Check(me.EsSt.All(x => x == Game.EsInHand), "putting the weapon away crumbles the spears out");
        me.Weapon = keep;
    }
}
