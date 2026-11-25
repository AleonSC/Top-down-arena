using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The DPS Hemomancer's Blood Brawler, the Marionette (BloodBrawler.cs; Docs/BloodBrawlerMarionette.md).
static partial class Program
{
    /// Fire with the cursor on an enemy marks it and lands Pulse (shove away), Reel (pull in), Heart Lift (airborne, stunned)
    /// and Marrow Burst (hits harder for the string's hits, bone shards cut a bystander); each move costs 1 health, the Burst 3;
    /// the string starts over after the Burst, on another enemy, or after the window; nothing under the cursor or out of reach
    /// fires nothing; the other screen sees the mark; Alt (Sympathy) binds enemies near the mark, not far ones, and a move on
    /// the mark lands on them at half; Marrow adds shards and a bound enemy.
    static void BloodBrawlerTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Bleeder", Role.Dps, 2, 0, Kit.Hemomancer);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.BloodBrawler, "the Hemomancer DPS's third key is the Blood Brawler (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action<float, Func<V2>, bool, bool> Step = (secs, cursor, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var e in g.Enemies.Values) { e.FireT = 99f; e.Speed = 0f; }
                V2 at = cursor();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = at, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput()); }
            }
        };
        Func<Enemy, Func<V2>> On = e => () => e.Pos;
        float hpBefore = 0f;
        Action<Enemy> Press = e => { me.Hp = me.MaxHp * 0.8f; hpBefore = me.Hp; Step(dt * 2f, On(e), true, false); };
        Action<Enemy> Settle = e => Step(0.5f, On(e), false, false);
        Func<string> Cost = () => (hpBefore - me.Hp).ToString("0.##");
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();

        // 1 Pulse: the mark, 1 health, a hit and a shove away
        var a = Dummy(from + new V2(300f, 0f));
        var by = Dummy(from + new V2(300f, 95f));  // a bystander, out of the shove's way but in the shards' reach
        V2 a0 = a.Pos;
        Press(a);
        Check(Game.BsStepOf(me.Combo) == 1 && me.BsTarget == a.Id, "Fire on an enemy marks it and lands step 1 (step " + Game.BsStepOf(me.Combo) + ")");
        Check(Math.Abs(hpBefore - me.Hp - Game.BsMoveCost) < 0.2f, "a move costs 1 health (" + Cost() + ")");
        Settle(a);
        float pulse = a.MaxHp - a.Hp;
        Check(pulse >= me.Weapon.Damage - 1f && pulse < me.Weapon.Damage * 1.6f && a.Pos.X - a0.X > 15f, "Pulse hits (" + pulse.ToString("0") + ") and shoves it away (" + (a.Pos.X - a0.X).ToString("0") + " px)");
        Step(dt * 3f, On(a), false, false);
        Check(client.Game.Players[1].BsTarget == a.Id, "the other screen sees the mark");

        // 2 Reel: pulled toward you
        V2 a1 = a.Pos;
        Press(a);
        Settle(a);
        Check(Game.BsStepOf(me.Combo) == 2 && a1.X - a.Pos.X > 80f, "step 2, Reel, pulls it in (" + (a1.X - a.Pos.X).ToString("0") + " px)");

        // 3 Heart Lift: airborne and stunned
        Press(a);
        Step(0.2f, On(a), false, false);
        Check(Game.BsStepOf(me.Combo) == 3 && a.BsLiftT > 0f && a.StunT > 0f, "step 3, Heart Lift, throws it in the air, stunned (" + a.BsLiftT.ToString("0.00") + " s)");
        Step(0.25f, On(a), false, false);

        // 4 Marrow Burst: 3 health, a bigger hit for the string's three hits (and airborne), shards cut the bystander
        by.Pos = a.Pos + new V2(0f, 70f);  // (in the shards' reach, wherever the Reel left it)
        float aBefore = a.Hp, byBefore = by.Hp;
        Press(a);
        Check(Math.Abs(hpBefore - me.Hp - Game.BsBurstCost) < 0.2f, "Marrow Burst costs 3 health (" + Cost() + ")");
        bool airborne = a.BsLiftT > 0f;
        Step(0.25f, On(a), false, false);
        float burst = aBefore - a.Hp;
        int shardsSeen = g.Bullets.Values.Count(b => b.Kind == Defs.BloodBrawlerShot);
        Settle(a);
        Check(airborne && burst > me.Weapon.Damage * (Game.BsBurstBase + 3f * Game.BsBurstPer) * 1.2f, "step 4, Marrow Burst, lands on it still airborne for 36 + 12 a hit, +25% (" + burst.ToString("0") + ")");
        Check(by.Hp < byBefore, "its bone shards cut the bystander (" + (byBefore - by.Hp).ToString("0") + "; " + shardsSeen + " shards seen)");

        // the string starts over after the Burst; on another enemy; and after the window
        Press(a);
        Check(Game.BsStepOf(me.Combo) == 1, "after the Burst the next press is Pulse again");
        Settle(a);
        Press(by);
        Check(Game.BsStepOf(me.Combo) == 1 && me.BsTarget == by.Id, "a press on another enemy starts over on it");
        Settle(by);
        Press(by);
        Check(Game.BsStepOf(me.Combo) == 2, "the next press on it moves on");
        Step(Game.BsReelTime + Game.BsWindow + 0.2f, On(by), false, false);
        Press(by);
        Check(Game.BsStepOf(me.Combo) == 1, "after the window it starts over");
        Step(Game.BsThread + 1f, On(by), false, false);
        Check(me.BsTarget == 0, "the mark runs out " + Game.BsThread + " s after the last move");

        // nothing under the cursor, or out of reach: nothing fires
        Press(a);
        Settle(a);  // (a move to reset the cooldown)
        int step0 = me.Combo;
        me.Hp = me.MaxHp * 0.8f; hpBefore = me.Hp;
        Step(0.3f, () => from + new V2(0f, -250f), true, false);
        Check(me.Combo == step0 && hpBefore - me.Hp < 0.1f, "with no enemy under the cursor nothing fires, and it costs nothing");
        var far = Dummy(from + new V2(800f, 0f));
        float farHp = far.Hp;
        Step(0.3f, On(far), true, false);
        Check(far.Hp == farHp && me.BsTarget != far.Id, "an enemy beyond " + me.Weapon.Reach + " px can't be marked");
        g.Enemies.Clear(); g.Bullets.Clear();
        Step(Game.BsThread + 0.5f, () => from, false, false);

        // Alt, Sympathy: binds enemies near the mark (not far ones); a move on the mark lands on them at half
        var t = Dummy(from + new V2(300f, 0f));
        var c1 = Dummy(from + new V2(300f, 120f));
        var c2 = Dummy(from + new V2(400f, -100f));
        var off = Dummy(from + new V2(300f, -300f));
        Step(dt * 4f, On(t), false, true);
        Check(me.SympT == 0f, "Sympathy needs a mark first");
        Press(t);
        Settle(t);
        Step(0.75f, On(t), false, false);  // (past the string's window, so the next press is a clean Pulse, but the mark still holds)
        Step(dt * 4f, On(t), false, true);
        Check(me.SympT > 0f && me.SympBound.Contains(c1.Id) && me.SympBound.Contains(c2.Id) && !me.SympBound.Contains(off.Id) && me.SympSrc == t.Id,
            "Alt binds the enemies within " + Game.SympRange + " px of the mark, not one farther off (" + me.SympBound.Count + " bound)");
        Step(dt * 3f, On(t), false, false);
        Check(client.Game.Players[1].SympBound.Count == me.SympBound.Count, "the other screen sees them bound");
        float tB = t.Hp, c1B = c1.Hp, offB = off.Hp;
        Press(t);
        Settle(t);
        float onT = tB - t.Hp, onC = c1B - c1.Hp;
        Check(Game.BsStepOf(me.Combo) == 1 && onC > 0f && Math.Abs(onC / onT - Game.SympMult) < 0.1f && off.Hp == offB,
            "a Pulse on the mark lands on the bound enemies at half (" + onT.ToString("0") + " / " + onC.ToString("0") + "), not the free one");
        float cd = Game.SympCooldown;
        me.SympT = 0f;
        Step(dt * 4f, On(t), false, true);
        Check(me.SympT == 0f, "Sympathy then waits out its " + cd + " s cooldown");

        // Marrow: 2 more shards, 1 more bound enemy
        var card = Upgrades.All.First(u => u.Name == "Marrow");
        Check(card.Weapons != null && card.Weapons.Contains(WeaponId.BloodBrawler), "Marrow is a Blood Brawler card");
        card.Apply(me.Stats);
        Check(me.Stats.BsShardBonus == 2 && me.Stats.SympBonus == 1, "Marrow adds 2 shards and 1 bound enemy");

        // putting the weapon away drops the mark
        var keep = me.Weapon;
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.Implosion);
        Step(dt * 2f, On(t), false, false);
        Check(me.BsTarget == 0 && me.SympBound.Count == 0, "putting the weapon away drops the mark and Sympathy");
        me.Weapon = keep;
    }
}
