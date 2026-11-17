using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Ninja DPS's fourth weapon, the Nunchaku: Twin Storm (Nunchaku.cs).
static partial class Program
{
    /// Fire runs Snap, Backhand, Double Figure-8 (four hits) and the Twin Crush (a stunning shockwave), then starts over; Alt raises the
    /// Twin Windmill guard (slower, seen by the other screen); its discs swat shots back and count them; Fire in the guard is the Twin
    /// Counter (20 + 6 a swat, which it uses up); Alt drops it; the parry resets the combo; Iron Links widens the discs.
    static void NunchakuTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Chucks", Role.Dps, 3, 0, Kit.Ninja);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.Nunchaku && Defs.WeaponCount(Role.Dps, Kit.Ninja) == 4, "the Ninja DPS's fourth key is the Nunchaku (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f), far = from + new V2(400f, 0f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action<float, bool, bool> Step = (secs, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = far, Fire = fire && f < 2, WeaponAction = alt && f < 3 } : new PlayerInput()); }
            }
        };
        float mult = me.Stats.DmgMult;
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();

        // the combo: each press the next step, its hits landing as the chucks come round
        var d1 = Dummy(from + new V2(70f, 0f));
        float[] want = { 20f, 22f, 48f, 68f };
        for (int k = 0; k < 4; k++)
        {
            float before = d1.Hp;
            d1.Pos = from + new V2(70f, 0f);  // (each step shoves it a little)
            me.FireCd = 0f;
            Step(dt * 2f, true, false);
            Check(Game.NunStepOf(me.Combo) == k + 1, "press " + (k + 1) + " is step " + (k + 1) + ", " + NunchakuMotion.Steps[k].Name + " (" + Game.NunStepOf(me.Combo) + ")");
            Step(NunchakuMotion.Steps[k].Dur + 0.25f, false, false);
            float took = before - d1.Hp;
            Check(Math.Abs(took - want[k] * mult) < 1f, NunchakuMotion.Steps[k].Name + " deals " + want[k] + " to an enemy 70 px ahead (" + took.ToString("0.#") + ")");
        }
        Check(g.Effects.Any(e => e.Kind == FxKind.NunStep && Math.Round(e.Radius) == 14), "each step is sent as FxKind.NunStep (the Crush: id 1, step 4)");
        me.FireCd = 0f;
        Step(dt * 2f, true, false);
        Check(Game.NunStepOf(me.Combo) == 1, "after the Twin Crush the combo starts over at the Snap");
        Step(Game.NunChainWindow + 1f, false, false);
        me.FireCd = 0f;
        Step(dt * 2f, true, false);
        Check(Game.NunStepOf(me.Combo) == 1, "and after a second without a step, too");
        Step(0.5f, false, false);

        // the stun: the Crush's shockwave holds what it hits
        g.Enemies.Clear(); me.Combo = 0;
        var d2 = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, from + new V2(90f, 50f), 0f, 0, false });
        d2.MaxHp = d2.Hp = 1e6f; d2.Speed = 0f; d2.FireT = 99f; d2.SpawnT = 0f;
        for (int k = 0; k < 3; k++) { me.FireCd = 0f; Step(dt * 2f, true, false); Step(NunchakuMotion.Steps[k].Dur * NunchakuMotion.Steps[k].Next, false, false); d2.Pos = from + new V2(90f, 50f); d2.StunT = 0f; }
        me.FireCd = 0f; Step(dt * 2f, true, false);
        Step(NunchakuMotion.Steps[3].Dur * Game.NunCrushAt + 0.05f, false, false);
        Check(d2.StunT > 0f, "the Twin Crush stuns an enemy off to the side, inside its 100 px ring (" + d2.StunT.ToString("0.00") + ")");
        Step(1f, false, false);

        // the parry takes the combo back to the Snap
        me.FireCd = 0f; Step(dt * 2f, true, false); Step(0.3f, false, false);
        typeof(Game).GetMethod("ResetNunChain", flags).Invoke(g, new object[] { me });
        Check(Game.NunStepOf(me.Combo) == 0, "raising the parry resets the combo");

        // Alt: the guard goes up, slows you, and the other screen sees it
        float speed = me.Speed;
        Step(dt * 4f, false, true);
        Check(me.NunGuard, "Alt raises the Twin Windmill guard");
        Check(Math.Abs(me.Speed - speed * Game.NunGuardSlow) < 0.5f, "you move at 75% in it (" + me.Speed.ToString("0") + " of " + speed.ToString("0") + ")");
        Step(dt * 3f, false, false);
        Check(client.Game.Players[1].NunGuard, "the other screen sees the guard");

        // the discs swat shots back, one count each
        g.Enemies.Clear(); g.Bullets.Clear();
        Dummy(from + new V2(300f, 0f));  // (something for swatted shots to fly at)
        V2 disc = Game.NunDiscAt(from, me.Aim, true);
        var b = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, disc + new V2(30f, 0f), new V2(-300f, 0f), 0f, 2f, 5f, 0, false, 0f });
        Step(dt * 3f, false, false);
        Check(!g.Bullets.ContainsKey(b.Id) && me.NunSwats == 1, "a small shot hitting a disc is swatted (swats " + me.NunSwats + ")");
        Check(g.Bullets.Values.Any(x => x.Owner == 1), "and flies back as the Ninja's own shot");
        for (int k = 0; k < 3; k++)
        {
            shoot.Invoke(g, new object[] { Defs.SmallShot, Game.NunDiscAt(from, me.Aim, k % 2 == 0) + new V2(30f, 0f), new V2(-300f, 0f), 0f, 2f, 5f, 0, false, 0f });
            Step(dt * 3f, false, false);
        }
        Check(me.NunSwats == 4, "each shot counts (" + me.NunSwats + ")");
        Step(dt * 3f, false, false);
        Check(client.Game.Players[1].NunSwats == 4, "the other screen sees the count");

        // Fire in the guard: the Twin Counter, 20 + 6 a swat, along a long line; the swats are used up
        g.Enemies.Clear(); g.Bullets.Clear();
        var line = Dummy(from + new V2(150f, 0f));
        me.FireCd = 0f;
        Step(dt * 2f, true, false);
        Check(me.NunSwats == 0 && me.NunGuard && Game.NunStepOf(me.Combo) == 0, "Fire in the guard is the Twin Counter: it uses the swats up, the guard stays, no combo step");
        Step(0.5f, false, false);
        float hit = line.MaxHp - line.Hp;
        Check(Math.Abs(hit - (20f + 6f * 4f) * mult) < 1f, "it reaches 150 px for 20 + 6 a swat (44: " + hit.ToString("0.#") + ")");

        // while the counter lashes out the discs are down (nothing swatted); once it's back in the windmill they swat again
        me.FireCd = 0f;
        Step(dt * 2f, true, false);
        var mid = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, Game.NunDiscAt(from, me.Aim, true) + new V2(30f, 0f), new V2(-300f, 0f), 0f, 2f, 5f, 0, false, 0f });
        Step(dt * 3f, false, false);
        Check(g.Bullets.ContainsKey(mid.Id) && me.NunSwats == 0, "mid-counter the windmill is down: a shot reaching a disc isn't swatted");
        g.Bullets.Clear();
        Step(NunchakuMotion.Counter.Dur, false, false);
        shoot.Invoke(g, new object[] { Defs.SmallShot, Game.NunDiscAt(from, me.Aim, true) + new V2(30f, 0f), new V2(-300f, 0f), 0f, 2f, 5f, 0, false, 0f });
        Step(dt * 3f, false, false);
        Check(me.NunSwats == 1, "after it the windmill is back up and swats again (" + me.NunSwats + ")");

        // Alt again drops it
        Step(0.4f, false, false);
        Step(dt * 4f, false, true);
        Check(!me.NunGuard && Math.Abs(me.Speed - speed) < 0.5f, "Alt again drops the guard, full speed");

        // Iron Links: wider discs, more per swat
        var links = Upgrades.All.First(u => u.Name == "Iron Links");
        Check(links.Weapons != null && links.Weapons.Contains(WeaponId.Nunchaku), "Iron Links is a Nunchaku card");
        float r0 = Game.NunDisc(me);
        links.Apply(me.Stats);
        me.NunSwats = 2;
        Check(Math.Abs(Game.NunDisc(me) - r0 * 1.25f) < 0.5f && Math.Abs(Game.NunCounterDamage(me) - me.Weapon.Damage * (20f + 8f * 2f) / 20f) < 0.1f,
            "with Iron Links the discs are 25% wider and each swat adds 8");
        me.NunSwats = 0;
    }
}
