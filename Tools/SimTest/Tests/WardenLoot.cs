using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The Warden's legendaries: the Stun Gun ability and Warden's Guard.
static partial class Program
{
    static void WardenLootTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);

        var own = Enumerable.Range(0, Upgrades.All.Length).Where(i => Upgrades.All[i].Boss == EnemyType.Boss).Select(i => Upgrades.All[i].Name).ToArray();
        Check(U("Warden's Halo") < 0 && U("Iron Volley") < 0 && own.Contains("Warden's Guard") && own.Contains("Stun Gun") && own.Contains("Searchlight") && U("Ringburst") < 0
            && Upgrades.All[U("Stun Gun")].Grants == AbilityId.StunGun, "the Warden's legendaries: Warden's Guard, Stun Gun and Searchlight (" + string.Join(", ", own) + ")");

        var s = StartBuffed(dt, 3, 0);
        var g = s[0].Game;
        g.Bot = s[1].Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var hp = g.Players[2];                // the joining player, as the host sees them
        var cp = s[1].Game.Players[2];        // and as their own game does
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        var keep = new HashSet<Enemy>();
        Action clear = () => { ((List<EnemyType>)queueField.GetValue(g)).Clear(); foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f; };
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; keep.Add(e); return e; };
        // The joining player holds fire (or not) at a point; the host stands still.
        Action<float, bool, V2> drive = (secs, fire, at) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                clear();
                foreach (var e in keep) { e.FireT = 9f; e.Speed = 0f; }
                s[0].Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(800f, 100f) }); g.Update(dt);
                s[1].Poll(); s[1].Game.FixedStep(dt, new PlayerInput { Fire = fire, AimAt = at }); s[1].Game.Update(dt);
                if (f % 2 == 0) Thread.Sleep(1);
            }
        };
        clear(); drive(0.3f, false, new V2(0f, 0f));

        // Stun Gun: it goes on a free key; casting it swaps your weapon for six stun bolts.
        g.GrantUpgrade(2, U("Stun Gun"));
        drive(0.3f, false, new V2(0f, 0f));
        int slot = Array.FindIndex(hp.Slots, k => k.Id == AbilityId.StunGun);
        Check(slot >= 1 && slot <= 3 && cp.Slots[slot].Id == AbilityId.StunGun, "the Stun Gun goes on a Q / E / Shift key");
        cp.Pos = cp.Disp = hp.Pos = new V2(500f, 450f);
        var d = dummy(new V2(800f, 450f));
        // Pressed on the friend's own game, the way a player does (their game tracks its own cooldowns too).
        s[1].Poll(); s[1].Game.FixedStep(dt, new PlayerInput { AbilityQ = slot == 1, AbilityE = slot == 2, AbilityShift = slot == 3, AimAt = d.Pos });
        drive(0.2f, false, d.Pos);
        float held = hp.Slots[slot].Timer;
        drive(1f, false, d.Pos);
        Console.WriteLine("  stun gun: {0} shots out (friend sees {1}); cooldown {2:0.0} s, after a second not firing {3:0.0} s (friend {4:0.0} s)",
            hp.StunShots, cp.StunShots, held, hp.Slots[slot].Timer, cp.Slots[slot].Timer);
        Check(hp.StunShots == Game.StunGunShots && cp.StunShots == Game.StunGunShots && hp.Slots[slot].Charges == 0
            && Math.Abs(hp.Slots[slot].Timer - held) < 0.01f && cp.Slots[slot].Timer > held - 0.2f,
            "casting the Stun Gun draws it with 6 shots, and its cooldown waits while it's out");
        var bolts = new HashSet<int>();
        int otherShots = 0;
        bool stunned = false;
        for (int f = 0; f < 4 * 60 && hp.StunShots > 0; f++)
        {
            drive(1f / 60f, true, d.Pos);
            foreach (var b in g.Bullets.Values)
                if (b.Owner == 2 && b.Kind == Defs.StunBolt) bolts.Add(b.Id);
                else if (b.Owner == 2) otherShots++;
            stunned |= d.FrozenT > 0.5f;
        }
        float cdAfter = hp.Slots[slot].Timer;
        drive(1f, false, d.Pos);
        Console.WriteLine("  stun gun: {0} bolts, {1} other shots meanwhile; the dummy was stunned: {2}; cooldown {3:0.0} s, a second later {4:0.0} s",
            bolts.Count, otherShots, stunned, cdAfter, hp.Slots[slot].Timer);
        Check(bolts.Count == Game.StunGunShots && otherShots == 0 && stunned && hp.StunShots == 0 && cp.StunShots == 0,
            "the Stun Gun fires exactly 6 stun bolts (no weapon shots meanwhile) that stun what they hit");
        Check(hp.Slots[slot].Timer < cdAfter - 0.8f, "once the last bolt is fired, the cooldown starts");
        g.Bullets.Clear();
        drive(0.5f, true, d.Pos);
        Check(g.Bullets.Values.Any(b => b.Owner == 2 && b.Kind != Defs.StunBolt), "then your own weapon is back");
        d.Hp = 0f; keep.Clear();
        drive(0.3f, false, d.Pos);

        // Warden's Guard: a guard walks with you, fights near you, has health, and comes back after it falls.
        g.GrantUpgrade(2, U("Warden's Guard"));
        drive(0.5f, false, new V2(800f, 100f));
        Func<Game, List<Minion>> Guards = gm => gm.Minions.Values.Where(m => m.Owner == 2 && m.Kind == MinionKind.WardenGuard).ToList();
        Check(Guards(g).Count == 1 && Guards(s[1].Game).Count == 1, "Warden's Guard: a guard joins you (and your friend sees it)");
        var guard = Guards(g)[0];
        hp.Pos = cp.Pos = cp.Disp = new V2(300f, 450f);
        drive(2f, false, new V2(800f, 100f));
        Console.WriteLine("  guard: {0:0} px from you while there's nothing to fight", guard.Pos.DistanceTo(hp.Pos));
        Check(guard.Pos.DistanceTo(hp.Pos) < 80f, "the guard keeps to your side");
        var near = dummy(hp.Pos + new V2(150f, 0f));
        var far = dummy(hp.Pos + new V2(900f, 0f));
        drive(4f, false, new V2(800f, 100f));
        Console.WriteLine("  guard: {0:0} damage to the enemy near you, {1:0} to the one far away; it has {2:0} of {3:0} health left after 4 s against a Brute", near.MaxHp - near.Hp, far.MaxHp - far.Hp, guard.Hp, guard.MaxHp);
        Check(near.Hp < near.MaxHp - 20f && far.Hp == far.MaxHp, "it fights enemies near you and doesn't wander off after far ones");
        near.Hp = 0f; keep.Remove(near);  // (the far one stays, so the wave goes on)
        drive(0.3f, false, new V2(800f, 100f));
        guard.Hp = guard.MaxHp;
        float gh = guard.Hp;
        shoot.Invoke(g, new object[] { Defs.SmallShot, guard.Pos - new V2(40f, 0f), new V2(400f, 0f), 0f, 1f, 10f, 0, false, 0f });
        for (int f = 0; f < 18; f++) { s[0].Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(800f, 100f) }); g.Update(dt); s[1].Poll(); s[1].Game.FixedStep(dt, new PlayerInput { AimAt = new V2(800f, 100f) }); s[1].Game.Update(dt); Thread.Sleep(1); }
        Console.WriteLine("  guard: {0:0} of {1:0} health after an enemy shot; it shows {2:0.00} on the friend's screen", guard.Hp, guard.MaxHp, Guards(s[1].Game).Select(m => m.GuardFrac).FirstOrDefault());
        Check(guard.Hp < gh - 5f && Guards(s[1].Game).Count == 1 && Guards(s[1].Game)[0].GuardFrac < 0.99f, "enemy shots hurt the guard (its health shows for everyone)");
        typeof(Game).GetMethod("HurtGuard", flags).Invoke(g, new object[] { guard, 1e4f });
        drive(1f, false, new V2(800f, 100f));
        Console.WriteLine("  guard: fell; next one in {0:0.0} s", hp.GuardCd);
        Check(Guards(g).Count == 0 && Guards(s[1].Game).Count == 0 && hp.GuardCd > Game.GuardRespawn - 2f, "a guard that falls is gone until its respawn timer");
        hp.GuardCd = 0.5f;
        drive(1f, false, new V2(800f, 100f));
        Check(Guards(g).Count == 1, "then a new guard arrives");
        g.GrantUpgrade(2, U("Warden's Guard"));
        drive(1.5f, false, new V2(800f, 100f));
        Check(Guards(g).Count == 2, "a second stack brings a second guard");

        // Searchlight: a light on your cursor; enemies in it take 25% more from everyone, and one held in it 2 s is stunned.
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        g.GrantUpgrade(2, U("Searchlight"));
        var lit = dummy(new V2(800f, 450f));
        var dark = dummy(new V2(800f, 150f));
        drive(1f, false, lit.Pos);
        Console.WriteLine("  searchlight: on the host at {0:0},{1:0}; the friend has it at {2:0},{3:0} (cursor 800,450)", hp.LightPos.X, hp.LightPos.Y, cp.LightDisp.X, cp.LightDisp.Y);
        Check(hp.LightPos.DistanceTo(lit.Pos) < 2f && cp.LightDisp.DistanceTo(lit.Pos) < 2f, "the searchlight follows your cursor (on the host and on your own screen)");
        Func<Enemy, float> Hit = e => { float before = e.Hp; hurt.Invoke(g, new object[] { e, 10f, 1 }); return before - e.Hp; };
        float inLight = Hit(lit), outOfLight = Hit(dark);
        Console.WriteLine("  searchlight: a 10-damage hit from your friend does {0:0.0} in the light, {1:0.0} out of it", inLight, outOfLight);
        Check(Math.Abs(inLight - 12.5f) < 0.01f && Math.Abs(outOfLight - 10f) < 0.01f, "enemies in the light take 25% more damage, from anyone");
        int frames = 0;
        while (frames < 200 && lit.FrozenT <= 0.5f) { drive(1f / 60f, false, lit.Pos); frames++; }
        Console.WriteLine("  searchlight: stunned after {0:0.00} s more in the light", frames / 60f);
        Check(lit.FrozenT > 0.5f && frames < 150 && dark.FrozenT <= 0f, "an enemy held in the light for 2 s is stunned (one outside isn't)");
        g.GrantUpgrade(2, U("Searchlight")); g.GrantUpgrade(2, U("Searchlight"));
        drive(0.2f, false, lit.Pos);
        Check(Game.SearchlightRadius(hp) == Game.SearchWideRadius && Math.Abs(Hit(lit) - 14f) < 0.01f, "three stacks: a wider light and +40%");
        s[0].Leave(""); s[1].Leave("");
    }
}
