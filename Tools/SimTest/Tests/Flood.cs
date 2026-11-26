using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Flood (Flood.cs), the Tank Elementalist's 4th choice on the lobby's 4th choice: the table (only that class has a 4th), the
// pick reaching the run and being clamped for a class without one, the stamp (10 damage and a 60 px shove for what's touching you,
// heavies only hurt), the pool (190 px where cast, spreading over 0.5 s, 6 s, then draining and doing nothing), the 40% / 20% slow,
// wet while in it and 3 s after, lightning arcing through the wet, mastery (Deluge) and High Tide.
static partial class Program
{
    static void FloodTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var wetArcs = typeof(Game).GetMethod("WetArcs", flags);

        // The table: the Tank Elementalist has four 1st abilities, everyone else three.
        var def = Upgrades.Ability(AbilityId.Flood);
        Check(def.Name == "Flood" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Elementalist && def.Role == Role.Tank && def.Cooldown == 16f,
            "Flood: a 16 s Tank Elementalist 1st ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Tank, Kit.Elementalist) == 4 && Upgrades.FirstAbility(Role.Tank, Kit.Elementalist, 3) == AbilityId.Flood
            && Upgrades.FirstAbility(Role.Tank, Kit.Elementalist, 2) == AbilityId.FlameWall,
            "the Tank Elementalist's 4th key is Flood; its first three are unchanged");
        bool othersThree = true;
        foreach (Role r in new[] { Role.Tank, Role.Healer, Role.Dps })
            for (int k = 0; k < Defs.KitCount; k++)
                if ((Kit)k != Kit.Elementalist && !((Kit)k == Kit.Soldier || r == Role.Tank && (Kit)k == Kit.Summoner || r == Role.Dps && (Kit)k == Kit.Hemomancer || (Kit)k == Kit.Ninja && r != Role.Healer || r == Role.Dps && (Kit)k == Kit.Summoner || r == Role.Healer && (Kit)k == Kit.Knight || r == Role.Tank && (Kit)k == Kit.Knight))  // (every Elementalist role has a 4th: Flood, Water Ward, Gale Wall; the Soldier: Deployable Cover, Decoy Dummy, Deployable Turret; the DPS Hemomancer: Thorn Bloom; the Tank and DPS Ninja: Bamboo Grove, Senbon Seekers; the Summoner: Stampede, Land Shark; the Healer Knight: Sanctum Glass; the Tank Knight: Siege Bell)
                    othersThree &= Upgrades.AbilityLimit(r, (Kit)k) == 3 && Upgrades.FirstAbility(r, (Kit)k, 3) == Upgrades.FirstAbility(r, (Kit)k, 0);
        Check(othersThree && Upgrades.AbilityLimit(Role.Wildcard, Kit.Soldier) == 3, "every non-Elementalist class has three (a 4th pick falls back to the first)");
        Check(Upgrades.All.Any(u => u.ClassAbility && u.Grants == AbilityId.Flood), "Flood is a very rare card for Tank Elementalists who picked another");

        // A 4th pick for a class without one is clamped by the host.
        {
            int lp = nextPort++;
            var lh = new Session();
            lh.Host(lp, "Host", Role.Healer, 0, 0, Kit.Knight);
            var lc = new Session();
            lc.Join("127.0.0.1", lp, "Tide", Role.Tank, 0, 3, Kit.Elementalist);
            for (int i = 0; i < 300 && !(lc.Lobby.Count == 2 && lh.Lobby.Count == 2); i++) { Step(lh, dt); Step(lc, dt); System.Threading.Thread.Sleep(2); }
            int cid = lc.MyId;
            Func<int, bool> settle = want => { for (int i = 0; i < 200; i++) { Step(lh, dt); Step(lc, dt); if (lh.Lobby[cid].Ability == want && lc.Lobby[cid].Ability == want) return true; System.Threading.Thread.Sleep(1); } return false; };
            Check(lh.Lobby.ContainsKey(cid) && settle(3), "a Tank Elementalist can pick the 4th key, and everyone sees it");
            lc.RequestKit(Kit.Hemomancer);
            Check(settle(0), "changing to a class with three abilities goes back to the first");
            lc.RequestAbility(3);
            for (int i = 0; i < 20; i++) { Step(lh, dt); Step(lc, dt); }
            Check(lh.Lobby[cid].Ability == 0, "and the 4th key can't be picked there");

        }

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Tide", Role.Tank, 0, 3, Kit.Elementalist);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[0].Id == AbilityId.Flood && client.Game.Players[1].Slots[0].Id == AbilityId.Flood, "the 4th pick reaches the run, on both screens");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        V2 mePos = new V2(500f, 450f);
        var mine = new HashSet<int>();
        var pinned = new Dictionary<int, V2>();
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; mine.Add(e.Id); pinned[e.Id] = at; return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var kv in pinned) { Enemy e; if (g.Enemies.TryGetValue(kv.Key, out e)) { e.Pos = kv.Value; e.KnockLeft = V2.Zero; } }
                g.Bullets.Clear();
                me.Pos = mePos;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) });
            }
        };
        Action reset = () =>
        {
            mine.Clear(); pinned.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); g.Zones.Clear();
            me.Slots[0].Charges = 1; me.Pos = mePos;
            me.MasteryXp.Remove(AbilityId.Flood);
            make(EnemyType.Grunt, new V2(1500f, 120f));  // far off: the wave goes on
        };
        Func<Zone> flood = () => g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.Flood);
        Action cast = () => { me.Slots[0].Charges = 1; hostAbility.Invoke(g, new object[] { 1, 0, mePos, false }); };

        // The stamp: what's touching you is hurt and thrown back; heavies are hurt but stay.
        reset();
        var close = make(EnemyType.Grunt, mePos + new V2(40f, 0f));
        pinned.Remove(close.Id);
        var heavy = make(EnemyType.Thunderhead, mePos + new V2(-50f, 0f));
        pinned.Remove(heavy.Id);
        var mid = make(EnemyType.Grunt, mePos + new V2(0f, 150f));
        float ch = close.Hp, hh = heavy.Hp, mh = mid.Hp;
        V2 heavyAt = heavy.Pos;
        cast();
        var z0 = flood();
        Check(z0 != null && z0.Radius == Game.FloodRadius && z0.Pos.DistanceTo(mePos) < 1f && Math.Abs(Game.FloodLife(z0) - Game.FloodTime) < 0.01f,
            "the pool is 190 px round where you stand, for 6 s");
        Check(ch - close.Hp > 5f && hh - heavy.Hp > 5f && mid.Hp == mh, "the stamp hurts what's touching you, not what's further off");
        Check(Game.FloodReach(z0) < 5f, "the water starts at your feet");
        V2 c0 = close.Pos;
        step(0.3f);
        Console.WriteLine("  flood: close grunt pushed {0:0} px, heavy moved {1:0} px", close.Pos.DistanceTo(c0), heavy.Pos.DistanceTo(heavyAt));
        Check(close.Pos.DistanceTo(mePos) > 40f + Game.FloodSurgePush * 0.8f && heavy.Pos.DistanceTo(heavyAt) < 20f, "it throws a grunt back 60 px; a heavy stays");
        pinned[close.Id] = close.Pos; pinned[heavy.Id] = heavy.Pos;
        step(Game.FloodGrow);
        Check(Math.Abs(Game.FloodReach(z0) - Game.FloodRadius) < 0.5f, "and spreads to its full size over 0.5 s");

        // Wading: slowed and wet.
        Check(mid.WetT > 0f && mid.SlowT > 0f && Math.Abs(mid.SlowMult - (1f - Game.FloodSlow)) < 0.01f, "a grunt in the water is slowed 40% and wet");
        Check(heavy.WadeT > 0f && Math.Abs(heavy.WadeMult - (1f - Game.FloodSlowHeavy)) < 0.01f && heavy.WetT > 0f, "a heavy wades: slowed 20%, and wet");
        var outside = make(EnemyType.Grunt, mePos + new V2(Game.FloodRadius + 60f, 0f));
        step(0.5f);
        Check(outside.WetT <= 0f && outside.SlowT <= 0f, "an enemy outside the water is untouched");
        Check(client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.Flood && z.Pos.DistanceTo(z0.Pos) < 1f) && client.Game.Enemies.Values.Any(e => e.Id == mid.Id && e.Wet),
            "other screens see the pool and the wet enemy");

        // Lightning arcs through the wet ones in it.
        float before = mid.Hp;
        wetArcs.Invoke(g, new object[] { mePos + new V2(30f, 150f), close, 40f, 1 });
        Check(mid.Hp < before, "a lightning hit arcs to a wet enemy in the pool");

        // Stepping out: wet for 3 s more.
        pinned[mid.Id] = mePos + new V2(0f, Game.FloodRadius + 80f);
        step(1f);
        Check(mid.WetT > 1.5f && mid.WetT < Game.FloodWetAfter, "out of the water it stays wet, wearing off");
        step(2.2f);
        Check(mid.WetT <= 0f, "and dries 3 s after leaving");

        // It drains after 6 s and does nothing meanwhile, then it's gone.
        pinned[mid.Id] = mePos + new V2(0f, 60f);
        step(Game.FloodTime - 4.2f + 0.1f);
        Check(z0.T > 0f && !Game.FloodOn(z0) && Game.FloodReach(z0) < Game.FloodRadius, "after 6 s it drains in from the edge");
        mid.WetT = 0f;
        step(0.2f);
        Check(mid.WetT <= 0f, "a draining pool soaks nothing");
        step(Game.FloodDrain);
        Check(flood() == null, "then it's gone");

        // Mastery (Deluge) and High Tide.
        reset();
        me.MasteryXp[AbilityId.Flood] = 1e6f;
        me.Stats.FloodRadiusMult = 1.25f; me.Stats.FloodBonusTime = 2f;
        cast();
        var zm = flood();
        Console.WriteLine("  flood: mastered + High Tide radius {0:0}, life {1:0.0} s", zm?.Radius ?? 0f, zm != null ? Game.FloodLife(zm) : 0f);
        Check(zm != null && Math.Abs(zm.Radius - Game.FloodRadiusMastered * 1.25f) < 0.5f && Game.FloodLife(zm) >= Game.FloodTimeMastered + 2f - 0.01f,
            "mastered it's 30% wider and lasts 9 s; High Tide adds 25% and 2 s");
        me.Stats.FloodRadiusMult = 1f; me.Stats.FloodBonusTime = 0f;
        me.MasteryXp.Remove(AbilityId.Flood);
        Check(Upgrades.All.Any(u => u.Name == "High Tide" && u.RequiresAbility == AbilityId.Flood && u.Rarity == Rarity.Rare), "High Tide is a Rare card for Flood");


    }
}
