using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The DPS Knight's Space abilities and ultimate: Caught Steel, the Gauntlet, Misericorde and the Drawbridge.
static partial class Program
{
    static void KnightDpsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);

        Check(Upgrades.RoleAbility(Role.Dps, Kit.Knight, 0) == AbilityId.Whirlwind && Upgrades.Ability(AbilityId.Whirlwind).Name == "Misericorde"
            && Upgrades.Ability(AbilityId.Lunge).Name == "The Gauntlet" && Upgrades.Ability(AbilityId.BattleFury).Name == "Caught Steel"
            && Upgrades.Ability(AbilityId.BladeTempest).Name == "Drawbridge" && Upgrades.Ultimate(Role.Dps, Kit.Knight) == AbilityId.BladeTempest,
            "the DPS Knight's abilities keep their slots under their new names");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Dps", Role.Dps, 0, 0, Kit.Knight);
        var client = JoinAs(port, false, "Dps2", Role.Dps, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        var keep = new HashSet<Enemy>();
        Action Clear = () => { foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f; };
        Func<EnemyType, V2, float, Enemy> Mob = (type, at, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.SpawnT = 0f; e.Hp = e.MaxHp = hp; e.FrozenT = 99f; e.Pos = at; keep.Add(e); return e;
        };
        Action<float, V2> Step = (secs, aimAt) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                Thread.Sleep(1);
                host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = aimAt }); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput { AimAt = new V2(800, 450) }); client.Game.Update(dt);
                Clear();
            }
        };
        Action Reset = () =>
        {
            foreach (var e in keep) e.Hp = 0f;
            keep.Clear();
            Clear();
            foreach (var b in g.Bullets.Values.ToList()) g.Bullets.Remove(b.Id);
            g.Zones.Clear();
            Step(0.1f, new V2(1000f, 450f));
            me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
        };
        Func<V2, V2, Bullet> Shot = (at, vel) => (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, at, vel, 0f, 5f, 10f, 0, false, 0f });
        Clear(); Step(0.2f, new V2(1000f, 450f));

        // --- Caught Steel ---
        Reset();
        me.Pos = new V2(600f, 450f);
        me.Slots[0].Id = AbilityId.BattleFury;
        cast.Invoke(g, new object[] { 1, 0, me.Pos, true });
        var skim = Shot(new V2(380f, 465f), new V2(320f, 0f));    // passes 15 px from the knight's centre
        var wide = Shot(new V2(380f, 520f), new V2(320f, 0f));    // passes 70 px away
        Step(1.2f, new V2(1000f, 450f));
        Console.WriteLine("  caught steel: {0} held, window {1:0.0} s, the wide shot {2}", me.Steel, me.SteelT, g.Bullets.ContainsKey(wide.Id) ? "still flying" : "gone");
        Check(me.Steel == 1 && !g.Bullets.ContainsKey(skim.Id), "Caught Steel catches a bullet that skims past");
        Check(g.Bullets.ContainsKey(wide.Id) || wide.Pos.X > 900f, "a bullet passing well clear isn't caught");
        Check(client.Game.Players[1].SteelT > 0f && client.Game.Players[1].Steel == 1, "the other player sees the window and the shard");
        float timer0 = me.Slots[0].Timer;
        Step(0.5f, new V2(1000f, 450f));
        Check(Math.Abs(me.Slots[0].Timer - timer0) < 0.01f, "the cooldown waits while you're catching");
        var target = Mob(EnemyType.Brute, new V2(820f, 450f), 1e6f);
        cast.Invoke(g, new object[] { 1, 0, me.Pos, false });  // pressed again
        Step(0.6f, new V2(820f, 450f));
        Console.WriteLine("  caught steel: release hit the brute for {0:0}", 1e6f - target.Hp);
        Check(me.SteelT <= 0f && me.Steel == 0 && 1e6f - target.Hp >= Game.SteelDamage * 0.9f, "pressing again hurls the shard into the enemy in front");
        Reset();
        me.Slots[0].Id = AbilityId.BattleFury;
        cast.Invoke(g, new object[] { 1, 0, me.Pos, true });
        for (int k = 0; k < 3; k++) Shot(new V2(380f - k * 40f, 465f), new V2(320f, 0f));
        Step(1.4f, new V2(1000f, 450f));
        int held = me.Steel;
        g.DevSetGod(1, false); me.InvulnT = 0f;
        damage.Invoke(g, new object[] { me, 5f, "test" });
        g.DevSetGod(1, true); me.Hp = me.MaxHp;
        Console.WriteLine("  caught steel: {0} held, {1} after a hit", held, me.Steel);
        Check(held == 3 && me.Steel == 2, "a hit knocks a third of the shards loose");
        me.SteelT = 0.01f;
        Step(0.3f, new V2(1000f, 450f));
        Check(me.Steel == 0 && g.Bullets.Values.Count(b => b.Kind == Defs.SteelShard) == 2, "when the window runs out the shards are thrown");

        // --- The Gauntlet ---
        Reset();
        me.Pos = new V2(400f, 450f);
        me.Slots[0].Id = AbilityId.Lunge;
        var foe = Mob(EnemyType.Sniper, new V2(680f, 450f), 300f);
        var pack = Mob(EnemyType.Grunt, new V2(540f, 458f), 300f);
        Step(0.05f, new V2(680f, 450f));
        cast.Invoke(g, new object[] { 1, 0, new V2(700f, 450f), false });  // (not consumed yet: the host spends the charge and starts the cooldown)
        float packHp = pack.Hp;
        Step(0.9f, new V2(680f, 450f));
        Console.WriteLine("  gauntlet: challenge {0:0.0} s, you at ({1:0},{2:0}), the pack took {3:0} and slid to y {4:0}", foe.ChallengeT, me.Pos.X, me.Pos.Y, packHp - pack.Hp, pack.Pos.Y);
        Check(foe.ChallengeT > 3f && foe.ChallengeBy == 1 && client.Game.Enemies[foe.Id].ChallengeT > 3f, "the gauntlet Challenges the first enemy along the aim (and everyone sees the crest)");
        Check(me.Pos.DistanceTo(foe.Pos) < Game.GauntletStopShort + 12f, "you charge to the Challenged enemy");
        Check(packHp - pack.Hp >= Game.GauntletBowlDamage * 0.9f && pack.Pos.Y > 470f, "the charge bowls the enemy in the way aside");
        Check(me.Slots[0].Timer > 5f && me.GauntletDown, "the cooldown runs, and the gauntlet lies where it hit");
        foe.Hp = 0f;
        Step(0.2f, new V2(680f, 450f));
        Check(me.Slots[0].Timer <= 0.01f && !me.GauntletDown, "killing the Challenged enemy brings the gauntlet back and resets the cooldown");
        Reset();
        me.Slots[0].Id = AbilityId.Lunge;
        cast.Invoke(g, new object[] { 1, 0, new V2(700f, 100f), false });
        Step(0.6f, new V2(700f, 100f));
        Check(me.Slots[0].Timer > 5f && !me.GauntletDown, "a miss costs the full cooldown");
        // The client's own game is carried by the host's Msg.Carry.
        Reset();
        var cp = client.Game.Players[2];
        var dp = g.Players[2];
        dp.Pos = cp.Pos = new V2(300f, 250f);
        dp.Slots[0].Id = AbilityId.Lunge;
        var foe2 = Mob(EnemyType.Sniper, new V2(560f, 250f), 300f);
        Step(0.1f, new V2(1000f, 450f));
        cast.Invoke(g, new object[] { 2, 0, new V2(700f, 250f), true });
        Step(1f, new V2(1000f, 450f));
        Console.WriteLine("  gauntlet (client): carried to ({0:0},{1:0})", cp.Pos.X, cp.Pos.Y);
        Check(cp.Pos.DistanceTo(foe2.Pos) < Game.GauntletStopShort + 20f, "a client's charge is carried out on its own game");

        // --- Misericorde ---
        Reset();
        me.Pos = new V2(400f, 450f);
        me.Slots[0].Id = AbilityId.Whirlwind;
        var w1 = Mob(EnemyType.Grunt, new V2(540f, 430f), 100f); w1.Hp = 20f;
        var w2 = Mob(EnemyType.Sniper, new V2(690f, 470f), 100f); w2.Hp = 15f;
        var w3 = Mob(EnemyType.Grunt, new V2(1100f, 470f), 100f); w3.Hp = 10f;  // wounded, but far out of reach
        var hale = Mob(EnemyType.Brute, new V2(470f, 520f), 400f);
        Step(0.05f, new V2(540f, 430f));
        Check(g.MercyFirst(me, new V2(540f, 430f)) == w1, "the first leap goes to the wounded enemy nearest the cursor");
        cast.Invoke(g, new object[] { 1, 0, new V2(540f, 430f), true });
        Step(1.4f, new V2(540f, 430f));
        Console.WriteLine("  misericorde: you at ({0:0},{1:0}); w1 {2:0}, w2 {3:0}, far {4:0}, hale {5:0}", me.Pos.X, me.Pos.Y, w1.Hp, w2.Hp, w3.Hp, hale.Hp);
        Check(w1.Hp <= 0f && w2.Hp <= 0f, "Misericorde executes the wounded enemy and leaps on to the next in reach");
        Check(w3.Hp > 0f && hale.Hp >= 399f, "it leaves the healthy one and the one out of reach alone");
        Check(me.Pos.DistanceTo(new V2(690f, 470f)) < Game.MercyLandShort + 14f, "the leaps carry you to the last enemy executed");
        Reset();
        me.Slots[0].Id = AbilityId.Whirlwind;
        Mob(EnemyType.Grunt, new V2(500f, 450f), 300f);
        Check(g.MercyFirst(me, new V2(500f, 450f)) == null, "with nobody wounded in reach there is nothing to leap to (your own game doesn't spend the press)");

        // --- The Drawbridge ---
        Reset();
        me.Pos = new V2(300f, 450f);
        var crushed = Mob(EnemyType.Brute, new V2(560f, 452f), 1e6f);
        var edge = Mob(EnemyType.Grunt, new V2(640f, 520f), 1e6f);
        var clear = Mob(EnemyType.Grunt, new V2(700f, 250f), 1e6f);
        me.Slots[Upgrades.UltSlot].Id = AbilityId.BladeTempest;
        me.Slots[Upgrades.UltSlot].Charges = 1;
        cast.Invoke(g, new object[] { 1, Upgrades.UltSlot, new V2(900f, 450f), true });
        Step(0.5f, new V2(900f, 450f));
        Check(1e6f - crushed.Hp < 1f && client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.Drawbridge), "the bridge is still falling (everyone sees it)");
        Step(0.8f, new V2(900f, 450f));
        Console.WriteLine("  drawbridge: crushed took {0:0}, edge took {1:0} and slid to y {2:0}, clear took {3:0}", 1e6f - crushed.Hp, 1e6f - edge.Hp, edge.Pos.Y, 1e6f - clear.Hp);
        Check(1e6f - crushed.Hp >= Game.BridgeCrush * 0.9f, "the slam crushes what's in the lane");
        Check(1e6f - edge.Hp >= Game.BridgeEdgeDamage * 0.9f && 1e6f - edge.Hp < Game.BridgeCrush && edge.Pos.Y > 530f, "an enemy at the edge is hurt less and thrown aside");
        Check(1e6f - clear.Hp < 1f, "an enemy well clear isn't touched");
        Check(crushed.Pos.Y > 450f + Game.BridgeHalfWidth || crushed.Pos.Y < 450f - Game.BridgeHalfWidth || crushed.Pos.X > 830f, "a survivor in the lane is put off the bridge");
        clear.Pos = new V2(600f, 460f);
        Step(0.1f, new V2(900f, 450f));
        Check(Math.Abs(clear.Pos.Y - 450f) >= Game.BridgeHalfWidth, "enemies can't step onto the bridge");
        var railShot = Shot(new V2(620f, 300f), new V2(0f, 320f));
        Step(0.7f, new V2(900f, 450f));
        Check(!g.Bullets.ContainsKey(railShot.Id) && railShot.Pos.Y < 450f - Game.BridgeHalfWidth + 12f, "enemy shots stop at the rail");
        Step(6f, new V2(900f, 450f));
        Check(!g.Zones.Values.Any(z => z.Kind == ZoneKind.Drawbridge), "the bridge gives way after its time");
        host.Leave(""); client.Leave("");
    }
}
