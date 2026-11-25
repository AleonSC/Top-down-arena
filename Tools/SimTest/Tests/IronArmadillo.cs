using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Iron Armadillo (IronArmadillo.cs): the Tank Summoner's key 2 on the lobby's Ultimate row, the pick reaching the run; the curl
// (rooted), then your own game rolling you toward the cursor, speed building; nothing hurts you; abilities locked; an enemy you
// roll into takes damage by speed and is flung, a Heavy is stunned; an enemy shot ricochets off the shell as yours; a fast wall
// hit bashes; it ends on time with the uncurl slam, mastered it rolls longer; it ends quietly when you go down or the wave ends.
static partial class Program
{
    static void IronArmadilloTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var tick = typeof(Game).GetMethod("TickArmadillo", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);

        // --- The lobby ---------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.IronArmadillo);
        Check(def.Name == "Iron Armadillo" && def.Cooldown == 75f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Role == Role.Tank && def.Kit == Kit.Summoner && def.Capstone != null,
            "Iron Armadillo is the Tank Summoner's ultimate on key 2, 75 s");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Summoner) == AbilityId.Titan && Upgrades.Ultimate(Role.Tank, Kit.Summoner, 1) == AbilityId.BonePhalanx
            && Upgrades.Ultimate(Role.Tank, Kit.Summoner, 2) == AbilityId.IronArmadillo && Upgrades.ClampUlt(Role.Tank, Kit.Summoner, 2) == 2,
            "Titan stays the default and Bone Phalanx key 1; key 2 is the armadillo");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9 }, port, false, "Roller", Role.Tank, 0, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Thread.Sleep(1); } };
        host.RequestUlt(2);
        settle();
        Check(host.Lobby[1].Ult == 2 && client.Lobby[1].Ult == 2 && host.Lobby[1].UltId == AbilityId.IronArmadillo, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        int cid = client.MyId;
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.IronArmadillo && client.Game.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.IronArmadillo,
            "in the match the host's Ultimate is the armadillo, on every game");

        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        g.Props.Clear();
        V2 start = new V2(400f, 450f);
        V2 aim = new V2(1400f, 450f);
        bool fire = false;
        var mine = new HashSet<int>();
        Func<EnemyType, V2, float, Enemy> make = (type, at, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.FrozenT = 999f; e.FireT = 999f; e.Pos = at; mine.Add(e.Id); return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                ally.Pos = new V2(200f, 800f);
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim, Fire = fire });
            }
        };
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            tick.Invoke(g, new object[] { 0f });
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.ArmT = 0f; me.ArmAge = 0f;
            me.ArmHits = me.ArmBashes = me.ArmRicochets = me.ArmSlams = 0;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = start;
            fire = false; aim = new V2(1400f, 450f);
            g.State = MatchState.Fighting;
            step(dt);  // (your game sees it off)
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, me.Pos, false });
        EnemyType heavy = Enum.GetValues(typeof(EnemyType)).Cast<EnemyType>().First(t => (int)t < Defs.Enemies.Length && Defs.Get(t) != null && Defs.Get(t).Heavy && !Defs.Get(t).IsBoss);
        float dm = me.Stats.DmgMult;

        // The curl: rooted, can't be hurt, abilities locked.
        reset();
        cast();
        Check(me.ArmT == Game.ArmCurl + Game.ArmRoll && me.UltT == me.ArmT && Game.ArmOn(me) && !Game.ArmRolling(me), "the armadillo curls round you: 0.6 s, then 10 s rolling");
        step(0.3f);
        Check(me.Pos.DistanceTo(start) < 1f, "rooted while it curls");
        Check(client.Game.Players[1].ArmT > 0f, "the other screen sees it");
        float hp0 = me.Hp;
        me.InvulnT = 0f;
        damage.Invoke(g, new object[] { me, 100f, "test" });
        Check(me.Hp == hp0, "nothing hurts you inside the shell");
        float slot0 = me.Slots[0].Timer;
        int charges0 = me.Slots[0].Charges;
        hostAbility.Invoke(g, new object[] { 1, 0, me.Pos, false });
        Check(me.Slots[0].Charges == charges0 && me.Slots[0].Timer == slot0, "no abilities while curled up");

        // Rolling toward the cursor, speed building.
        step(0.3f + 0.6f);
        float rolled = me.Pos.X - start.X;
        Console.WriteLine("  armadillo: rolled {0:0} px in 0.6 s, host reads {1:0} px/s", rolled, me.ArmSpd);
        Check(Game.ArmRolling(me) && rolled > 150f && Math.Abs(me.Pos.Y - start.Y) < 20f, "it rolls toward the cursor");
        Check(me.ArmSpd > 400f && me.ArmSpd <= Game.ArmTop + 5f, "the host reads its speed, up to 520 px/s");

        // An enemy in the way: hit by speed and flung; a Heavy is stunned.
        var grunt = make(EnemyType.Grunt, me.Pos + new V2(160f, 0f), 1e6f);
        V2 gruntAt = grunt.Pos;
        step(0.3f);
        float lost = 1e6f - grunt.Hp;
        Console.WriteLine("  armadillo: grunt lost {0:0.0} (dmg mult {1:0.00}), moved {2:0} px", lost, dm, grunt.Pos.DistanceTo(gruntAt));
        Check(me.ArmHits >= 1 && lost >= Game.ArmHitMin * dm && lost <= Game.ArmHitMax * 1.25f * dm * 2f + 1f, "an enemy you roll into takes 20-80 by speed");
        Check(grunt.Pos.DistanceTo(gruntAt) > 80f, "and is flung");
        Check(Math.Abs(Game.ArmHit(0f) - 20f) < 0.01f && Math.Abs(Game.ArmHit(Game.ArmTop) - 80f) < 0.01f && Math.Abs(Game.ArmHit(Game.ArmBoostTop) - 100f) < 0.01f,
            "20 at rest, 80 at top speed, 100 at the launch's");
        g.Enemies.Remove(grunt.Id); mine.Clear();
        var big = make(heavy, me.Pos + new V2(150f, 0f), 1e6f);
        big.FrozenT = 0f;
        float x0 = me.Pos.X;
        step(0.35f);
        Check(big.Hp < 1e6f && big.StunT > 0f, "a Heavy is hit and stunned");
        Check(me.Pos.X < big.Pos.X - Game.ArmR + 5f, "and you bounce off it");
        g.Enemies.Remove(big.Id); mine.Clear();

        // A shot ricochets off the shell.
        g.Bullets.Clear();
        var shot = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, me.Pos + new V2(0f, -Game.ArmR - 6f), new V2(0f, 600f), 0f, 2f, 10f, 0, false, 0f });
        hp0 = me.Hp;
        step(0.15f);
        Check(me.ArmRicochets == 1 && !g.Bullets.ContainsKey(shot.Id) && g.Bullets.Values.Any(b => b.Owner == 1 && b.Kind == Defs.ArcaneShot), "an enemy shot ricochets off the shell as yours");
        Check(me.Hp == hp0, "and doesn't hurt you");

        // A fast wall hit bashes (an arena with walls: Solid Walls; a plain arena wraps round).
        g.Mods |= RunMod.SolidWalls;
        me.Pos = new V2(1000f, 450f);
        aim = new V2(3000f, 450f);
        step(1f);
        var nearWall = make(EnemyType.Grunt, new V2(Game.Width - 20f, me.Pos.Y), 1e6f);
        for (int i = 0; i < 30 && me.ArmBashes == 0; i++) step(0.1f);
        Check(me.ArmBashes >= 1 && g.ArmBashesSent >= 1, "rolling hard into the wall bashes (" + me.ArmBashes + ")");
        Check(1e6f - nearWall.Hp >= Game.ArmBash * dm - 1f, "50 round where it hit (" + (1e6f - nearWall.Hp).ToString("0.0") + ")");
        Check(me.Pos.X <= Game.Width - Game.ArmR + 1f, "and the ball bounces off, never into the wall");
        g.Mods &= ~RunMod.SolidWalls;
        g.Enemies.Remove(nearWall.Id); mine.Clear();

        // It ends on time with the uncurl slam.
        aim = new V2(800f, 450f);
        step(me.ArmT - 3f * dt);
        var bystander = make(EnemyType.Grunt, me.Pos + new V2(0f, 150f), 1e6f);
        step(0.2f);
        Check(me.ArmT == 0f && !Game.ArmOn(me) && me.UltT == 0f && me.ArmSlams == 1, "you uncurl when the time runs out");
        Check(1e6f - bystander.Hp >= Game.ArmSlam * dm - 1f, "with a slam of 90 round you (" + (1e6f - bystander.Hp).ToString("0.0") + ")");
        Run(s, dt, 1, () => client.Game.Players[1].ArmT == 0f);
        Check(client.Game.Players[1].ArmT == 0f, "the other screen sees it go");
        float walkFrom = me.Pos.X;
        aim = me.Pos + new V2(-400f, 0f);
        step(0.3f);
        Check(Math.Abs(me.Pos.X - walkFrom) < 5f, "back on your feet: Fire's aim no longer rolls you");

        // Mastered: rolls 13 s.
        reset();
        me.MasteryXp[AbilityId.IronArmadillo] = 1e6f;
        cast();
        Check(me.ArmT == Game.ArmCurl + Game.ArmRollMastered && me.ArmMastered, "mastered: 13 s rolling");
        me.MasteryXp.Remove(AbilityId.IronArmadillo);

        // Going down, the wave ending.
        reset();
        cast();
        me.Downed = true;
        tick.Invoke(g, new object[] { dt });
        Check(me.ArmT == 0f && me.ArmSlams == 0, "it ends quietly if you go down");
        reset();
        cast();
        g.State = MatchState.Intermission;
        tick.Invoke(g, new object[] { dt });
        Check(me.ArmT == 0f && me.UltT == 0f && me.ArmSlams == 0, "and when the wave is over");
        g.State = MatchState.Fighting;
        host.Leave(""); client.Leave("");

        // The lobby preview: the ball stays in the picture (its pen) and the dummies aren't flung far enough to zoom the view out.
        var sc = new Showcase();
        sc.Show(Role.Tank, Kit.Summoner, 0, Upgrades.FirstAbility(Role.Tank, Kit.Summoner, 0), true, true, 2);
        float worstOut = 0f, maxExtent = 0f;
        bool rolled2 = false;
        for (int f = 0; f < 12f / dt; f++)
        {
            sc.Step(dt);
            var pm = sc.Me;
            if (pm == null) continue;
            rolled2 |= Game.ArmRolling(pm);
            V2 d = pm.Pos - Showcase.Home;
            float outX = Math.Abs(d.X) - (Game.ArmShowHalfW - Game.ArmR), outY = Math.Max(-d.Y - (Game.ArmShowAbove - Game.ArmR), d.Y - (Game.ArmShowBelow - Game.ArmR));
            if (Game.ArmRolling(pm)) worstOut = Math.Max(worstOut, Math.Max(outX, outY));
            maxExtent = Math.Max(maxExtent, sc.Extent);
        }
        sc.Stop();
        Console.WriteLine("  preview: past the pen by {0:0.0} px at most, view extent up to {1:0}", worstOut, maxExtent);
        Check(rolled2 && worstOut <= 8f, "the lobby preview's ball stays in the picture (within a few px of its pen)");
        Check(maxExtent < 330f, "and the dummies stay close enough that the view doesn't zoom out after them");
    }
}
