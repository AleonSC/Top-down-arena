using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Ballista (Ballista.cs): key 2 of the DPS Knight's Ultimate row reaching the run; the drop (nothing before it lands, the landing
// slam inside 90 px only); a teammate mounting it over the wire and getting off; one rider at a time; the rider rooted on the seat;
// the bolts (none without Fire, one every 0.9 s, through two enemies in line, the one off the line untouched, knocked along, enemy
// shots in the path knocked down); 30% of the rider's damage worn by the ballista; breaking after the last bolt and setting the
// rider free; folding away when the fight ends; mastered (10 bolts, pinned) and Windlass.
static partial class Program
{
    static void BallistaTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);

        // --- The lobby: key 1 of the DPS Knight's Ultimate row ------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.Ballista);
        Check(def.Name == "Ballista" && def.Cooldown == 75f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Role == Role.Dps && def.Kit == Kit.Knight,
            "Ballista is the DPS Knight's ultimate on key 1, 75 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Knight, 1) == AbilityId.Ballista && Upgrades.Ultimate(Role.Dps, Kit.Knight) == AbilityId.WarhornVolley,
            "key 1 is the Ballista, Warhorn Volley the default");
        Check(Upgrades.ClampUlt(Role.Dps, Kit.Knight, 1) == 1 && Upgrades.Ultimate(Role.Healer, Kit.Knight, 1) != AbilityId.Ballista && Content.UltFallsBack(1),
            "key 1 is the DPS's; a class without a key 1 falls back to the default");
        Check(Upgrades.All.Any(u => u.Name == "Windlass" && u.RequiresAbility == AbilityId.Ballista && u.Rarity == Rarity.Rare),
            "Windlass is a Rare card for it");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Knight", Role.Dps, 0, 0, Kit.Knight, 0, 1);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[1].Ult == 2);
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[cid];
        Check(me.Slots[Upgrades.UltSlot].Id == AbilityId.Ballista && cg.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.Ballista,
            "in the run the Knight's ultimate is the Ballista, on both games");
        g.DevSetGod(1, true); g.DevSetGod(cid, true);

        V2 home = new V2(500f, 450f);
        var mine = new HashSet<int>();
        Func<V2, Enemy> make = at =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; e.Pos = at; mine.Add(e.Id); return e;
        };
        bool fire = false;
        Action<float> step = secs =>
        {
            for (int f = 0; f < Math.Max(1, (int)Math.Round(secs / dt)); f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var b in g.Bullets.Values.Where(b => b.Kind != Defs.BallistaBolt && !Defs.IsEnemyShot(b.Kind)).Select(b => b.Id).ToList()) g.Bullets.Remove(b);
                foreach (var e in g.Enemies.Values) { e.FireT = 99f; e.FrozenT = 99f; }
                if (!g.OnBallista(me)) me.Pos = home;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = home + new V2(400f, 0f), Fire = fire });
            }
        };
        Func<Zone> ballista = () => g.Zones.Values.FirstOrDefault(zz => zz.Kind == ZoneKind.Ballista);
        Action reset = () =>
        {
            foreach (var z in g.Zones.Values.Where(zz => zz.Kind == ZoneKind.Ballista).ToList()) g.Zones.Remove(z.Id);
            foreach (var id in mine) g.Enemies.Remove(id);
            mine.Clear();
            g.Bullets.Clear();
            fire = false;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.UltT = 0f;
            me.Pos = home; me.Aim = 0f; me.Downed = false;
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);  // (the wave never runs out)
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, home + new V2(400f, 0f), false });

        // --- The drop and the landing ----------------------------------------------------------------------------------------
        reset();
        var near = make(home + new V2(Game.BaAhead + 40f, 0f));   // under where it lands
        var far = make(home + new V2(Game.BaAhead + 200f, 120f)); // out of the slam
        cast();
        var z0 = ballista();
        Check(z0 != null && z0.BoltsLeft == Game.BaBolts && z0.Rider == 0 && Math.Abs(z0.Pos.X - (home.X + Game.BaAhead)) < 1f,
            "the cast calls a ballista 70 px ahead along the aim, 8 bolts, nobody on it");
        step(Game.BaFall * 0.5f);
        Check(near.Hp == near.MaxHp, "nothing is hurt while it falls");
        step(Game.BaFall * 0.6f);
        Check(near.Hp < near.MaxHp && far.Hp == far.MaxHp, "the landing slams what's under it, not what's farther");
        Run(s, dt, 1, () => cg.Zones.Values.Any(q => q.Kind == ZoneKind.Ballista && q.BoltsLeft == Game.BaBolts));
        Check(cg.Zones.Values.Any(q => q.Kind == ZoneKind.Ballista && q.BoltsLeft == Game.BaBolts), "the other screen sees it with its bolts");

        // --- A teammate climbs on over the wire, and off -----------------------------------------------------------------------
        ally.Pos = z0.Pos + new V2(0f, 60f);
        cg.Players[cid].Pos = ally.Pos;
        Run(s, dt, 0.3f, null, () => { cg.Players[cid].Pos = ally.Pos = z0.Pos + new V2(0f, 60f); });  // (the client's own position reaches the host)
        client.Game.UseBallista();
        Run(s, dt, 1, () => z0.Rider == cid);
        Check(z0.Rider == cid && g.Seated(ally), "a teammate presses Interact by it: they ride it, seated");
        Run(s, dt, 1, () => cg.Zones.Values.Any(q => q.Kind == ZoneKind.Ballista && q.Rider == cid));
        Check(cg.Seated(cg.Players[cid]), "their own game seats them too");
        me.Pos = z0.Pos + new V2(0f, -60f);
        g.UseBallista();
        Check(z0.Rider == cid, "one rider at a time: the Knight can't climb on while it's taken");
        step(0.1f);
        Check(ally.Pos.DistanceTo(Game.BaSeat(z0)) < 1f, "the rider sits in the seat behind the bow");
        client.Game.UseBallista();
        Run(s, dt, 1, () => z0.Rider == 0);
        Check(z0.Rider == 0 && !g.Seated(ally), "Interact again: they get off");
        ally.Pos = new V2(150f, 820f);

        // --- The bolts ------------------------------------------------------------------------------------------------------------
        foreach (var id in mine) g.Enemies.Remove(id);
        mine.Clear();
        me.Pos = z0.Pos + new V2(-50f, 0f);
        g.UseBallista();
        Check(z0.Rider == 1 && g.OnBallista(me), "the Knight climbs on");
        step(1f);  // (it turns onto the aim)
        var a = make(z0.Pos + new V2(400f, 0f));
        var b2 = make(z0.Pos + new V2(700f, 0f));
        var off = make(z0.Pos + new V2(500f, 200f));
        step(0.5f);
        Check(z0.BoltsLeft == Game.BaBolts && a.Hp == a.MaxHp, "nothing leaves the bow without Fire");
        fire = true;
        int before = g.BallistaBoltsFired;
        float knock = 0f;
        for (int i = 0; i < (int)(0.6f / dt); i++) { step(dt); knock = Math.Max(knock, a.KnockLeft.Length); }
        Check(g.BallistaBoltsFired == before + 1 && z0.BoltsLeft == Game.BaBolts - 1, "holding Fire looses one bolt (the reload holds the next)");
        float hitA = a.MaxHp - a.Hp, hitB = b2.MaxHp - b2.Hp;
        Check(hitA > 150f && hitB > 150f && Math.Abs(hitA - hitB) < 1f, "the bolt goes through both enemies in line, hard (" + hitA + ", " + hitB + ")");
        Check(off.Hp == off.MaxHp, "the enemy off the line is untouched");
        Check(knock > 20f, "what it passes is knocked along (" + knock + ")");
        step(Game.BaReload);
        Check(g.BallistaBoltsFired == before + 2, "the next bolt comes after the reload");
        fire = false;

        // An enemy shot in the bolt's path is knocked out of the air.
        V2 bf = z0.Pos + new V2(300f, 0f);
        var shot = (Bullet)spawnBullet.Invoke(g, new object[] { (byte)10, bf, new V2(-1f, 0f), 0f, 5f, 10f, 0, false, 0f });
        fire = true;
        step(Game.BaReload + 0.1f);
        fire = false;
        Check(!g.Bullets.ContainsKey(shot.Id), "an enemy shot in the bolt's path is knocked down");

        // The rider's hits: 30% worn by the ballista.
        g.DevSetGod(1, false);
        me.Hp = me.MaxHp; me.InvulnT = 0f;
        float hp0 = me.Hp, guard0 = z0.BlockHp;
        damage.Invoke(g, new object[] { me, 100f, "Test" });
        float wore = guard0 - z0.BlockHp, took = hp0 - me.Hp;
        Check(wore > 0f && Math.Abs(wore - Game.BaCut * (wore + took)) < 0.5f,
            "a hit on the rider wears the ballista for 30% of it (" + wore + " on it, " + took + " on you)");
        me.Hp = me.MaxHp;
        g.DevSetGod(1, true);

        // Out of bolts: it breaks and lets the rider go.
        fire = true;
        step(Game.BaReload * (z0.BoltsLeft + 1) + 0.2f);
        fire = false;
        Check(z0.BoltsLeft == 0 && z0.Radius <= 0f && !g.OnBallista(me) && !g.Seated(me), "after the last bolt it breaks and the rider is free");
        step(Game.BaBreakT + 0.2f);
        Check(ballista() == null, "and the wreck is gone");

        // --- The fight ending folds it away ------------------------------------------------------------------------------------
        reset();
        cast();
        step(Game.BaFall + 0.1f);
        g.State = MatchState.Intermission;
        step(0.1f);
        Check(ballista() == null, "it folds away when the fight ends");

        // --- Mastered and Windlass ----------------------------------------------------------------------------------------
        reset();
        me.MasteryXp[AbilityId.Ballista] = 99999f;
        cast();
        Check(ballista().BoltsLeft == Game.BaBoltsMastered && ballista().Cap, "mastered: 10 bolts");
        step(Game.BaFall + 0.1f);
        me.Pos = ballista().Pos + new V2(-50f, 0f);
        g.UseBallista();
        step(1f);
        var p = make(ballista().Pos + new V2(400f, 0f));
        fire = true;
        step(0.5f);
        fire = false;
        Check(p.Hp < p.MaxHp && p.SlowT > 0f, "mastered bolts pin what they hit");
        me.MasteryXp.Remove(AbilityId.Ballista);
        reset();
        me.Stats.BaWindlass = 1;
        cast();
        Check(ballista().BoltsLeft == Game.BaBolts + Game.BaWindlassBolts && ballista().BaReloadT < Game.BaReload, "Windlass: 2 more bolts and a faster reload");
        me.Stats.BaWindlass = 0;
        reset();

        host.Leave(""); client.Leave("");
    }
}
