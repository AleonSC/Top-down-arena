using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Arena;

// The Rare ability cards of Docs/UpgradePools.md step 5: each Elementalist and Summoner Space ability (and the Soldier's
// Medkit) has one. Every card is checked by casting its ability with no copy, then with copies, and comparing.
static partial class Program
{
    static void AbilityCardsTest(float dt)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var sflags = BindingFlags.NonPublic | BindingFlags.Static;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var recalc = typeof(Game).GetMethod("Recalc", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var mastered = typeof(Game).GetField("castMastered", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Mage", Role.Tank, 0, 0, Kit.Elementalist);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Summoner);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var friend = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        var anchor = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(1700f, 100f), 0f, 0, false });  // keeps the wave from ending
        anchor.Hp = anchor.MaxHp = 1e6f; anchor.SpawnT = 0f;
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        Check(new[] { "Eye of the Storm", "Permafrost", "Inferno", "Monsoon", "Overcharged Ward", "Dawnlight", "Cataclysm", "Whiteout", "Riptide",
            "Basalt Hide", "Deep Tremor", "Kindred Bond", "Sprite Choir", "Rebirth Flame", "Blood Bats", "Venom Glands", "Pack Leader", "Field Surgeon" }
            .All(n => U(n) >= 0 && Upgrades.All[U(n)].Rarity == Rarity.Rare && Upgrades.All[U(n)].MaxStacks == 2), "all 18 ability cards exist, Rare, 2 stacks each");

        // Clear the field, put the ability on Space with `stacks` copies of its card, and cast it at `at`.
        Action<AbilityId, string, int> prep = (ab, card, stacks) =>
        {
            foreach (int id in g.Minions.Values.Where(m => m.Owner == 1 && !m.Companion).Select(m => m.Id).ToList()) g.Minions.Remove(id);
            g.Zones.Clear(); g.Bullets.Clear();
            foreach (int id in g.Enemies.Keys.Where(k => k != anchor.Id).ToList()) g.Enemies.Remove(id);
            me.Pos = new V2(500f, 450f); me.Aim = 0f; me.Hp = me.MaxHp;
            me.Taken.Clear();
            for (int k = 0; k < stacks; k++) me.Taken.Add(U(card));
            recalc.Invoke(g, new object[] { me });
            me.Slots[0].Id = ab;
            mastered.SetValue(g, false);
        };
        Action<AbilityId, string, int, V2> castWith = (ab, card, stacks, at) =>
        {
            prep(ab, card, stacks);
            cast.Invoke(g, new object[] { me, 0, at });
        };
        Func<V2, Enemy> enemyAt = pos =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, pos, 0f, 0, false });
            e.SpawnT = 0f; e.Hp = e.MaxHp = 5000f;
            return e;
        };
        Func<ZoneKind, Zone> zone = k => g.Zones.Values.Where(z => z.Kind == k).OrderBy(z => z.Id).Last();
        Func<MinionKind, List<Minion>> mine = k => g.Minions.Values.Where(m => m.Kind == k && m.Owner == 1).ToList();
        V2 there = new V2(800f, 450f);
        Func<float, float, float, bool> near = (a, b, tol) => Math.Abs(a - b) <= tol;

        // --- Elementalist ---
        castWith(AbilityId.ArcaneAegis, "Eye of the Storm", 0, new V2(500f, 450f));
        float aT = me.AegisT, aR = me.AegisReflectMult;
        castWith(AbilityId.ArcaneAegis, "Eye of the Storm", 2, new V2(500f, 450f));
        Check(near(me.AegisT - aT, 3f, 0.01f) && near(me.AegisReflectMult / aR, 2f, 0.01f), "Eye of the Storm: the vortex lasts 1.5 s longer per card and reflects 50% harder (" + aT + " -> " + me.AegisT + ")");

        prep(AbilityId.FrostNova, "Permafrost", 0);
        var far = enemyAt(new V2(500f + 250f, 450f));  // past the 220 px reach, inside 286
        cast.Invoke(g, new object[] { me, 0, me.Pos });
        bool missed = far.Hp >= far.MaxHp;
        prep(AbilityId.FrostNova, "Permafrost", 1);
        var far2 = enemyAt(new V2(500f + 250f, 450f));
        cast.Invoke(g, new object[] { me, 0, me.Pos });
        Check(missed && far2.Hp < far2.MaxHp, "Permafrost: Frost Nova reaches 30% farther (an enemy 250 px out is missed without it, hit with it)");

        castWith(AbilityId.FlameWall, "Inferno", 0, there);
        float fD = zone(ZoneKind.FlameWall).Duration, fM = zone(ZoneKind.FlameWall).DmgMult;
        castWith(AbilityId.FlameWall, "Inferno", 2, there);
        Check(near(zone(ZoneKind.FlameWall).Duration - fD, 4f, 0.01f) && near(zone(ZoneKind.FlameWall).DmgMult / fM, 2f, 0.01f), "Inferno: Flame Wall stands 2 s longer per card and burns 50% hotter");

        castWith(AbilityId.HealingRain, "Monsoon", 0, there);
        float rR = zone(ZoneKind.HealingRain).Radius, rH = zone(ZoneKind.HealingRain).HealMult;
        castWith(AbilityId.HealingRain, "Monsoon", 2, there);
        Check(near(zone(ZoneKind.HealingRain).Radius / rR, 1.8f, 0.01f) && near(zone(ZoneKind.HealingRain).HealMult / rH, 1.8f, 0.01f), "Monsoon: Healing Rain is 40% wider and heals 40% more per card");

        var payback = typeof(Game).GetMethod("EnergyPayback", flags);
        Func<int, float[]> ward = n =>
        {
            castWith(AbilityId.EnergyWard, "Overcharged Ward", n, me.Pos);
            float soak = me.ShieldHp;
            me.Hp = me.MaxHp - 200f; me.EnergyStore = 100f;
            float hp = me.Hp;
            payback.Invoke(g, new object[] { me });
            return new[] { soak, me.Hp - hp };
        };
        var w0 = ward(0); var w1 = ward(1);
        Check(near(w1[0] / w0[0], 1.5f, 0.01f) && near(w0[1], 50f, 0.5f) && near(w1[1], 75f, 0.5f), "Overcharged Ward: Energy Ward soaks 50% more and gives back 75% instead of 50% (" + w0[1] + " -> " + w1[1] + " HP of 100 soaked)");

        castWith(AbilityId.Sanctify, "Dawnlight", 0, there);
        float lD = zone(ZoneKind.LumenWard).Duration, lH = zone(ZoneKind.LumenWard).HealMult;
        castWith(AbilityId.Sanctify, "Dawnlight", 2, there);
        Check(near(zone(ZoneKind.LumenWard).Duration - lD, 4f, 0.01f) && near(zone(ZoneKind.LumenWard).HealMult / lH, 1.6f, 0.01f), "Dawnlight: Lumen Ward shines 2 s longer per card and heals 30% more");

        castWith(AbilityId.Meteor, "Cataclysm", 0, there);
        float mR = zone(ZoneKind.Meteor).Radius, mD = zone(ZoneKind.Meteor).DmgMult;
        castWith(AbilityId.Meteor, "Cataclysm", 2, there);
        Check(near(zone(ZoneKind.Meteor).Radius / mR, 1.6f, 0.01f) && near(zone(ZoneKind.Meteor).DmgMult / mD, 2f, 0.01f), "Cataclysm: Meteor hits 50% harder and 30% wider per card");

        castWith(AbilityId.Blizzard, "Whiteout", 0, there);
        float bR = zone(ZoneKind.Blizzard).Radius, bD = zone(ZoneKind.Blizzard).Duration;
        castWith(AbilityId.Blizzard, "Whiteout", 2, there);
        Check(near(zone(ZoneKind.Blizzard).Radius / bR, 1.8f, 0.01f) && near(zone(ZoneKind.Blizzard).Duration - bD, 4f, 0.01f), "Whiteout: Blizzard is 40% bigger and lasts 2 s longer per card");

        var wavesF = typeof(Game).GetField("waves", flags);
        Func<object, string, float> fieldOf = (o, n) => (float)o.GetType().GetField(n).GetValue(o);
        castWith(AbilityId.Tsunami, "Riptide", 0, there);
        var wv0 = ((IList)wavesF.GetValue(g))[((IList)wavesF.GetValue(g)).Count - 1];
        float tW = fieldOf(wv0, "HalfWidth"), tWet = fieldOf(wv0, "WetFor");
        ((IList)wavesF.GetValue(g)).Clear();
        castWith(AbilityId.Tsunami, "Riptide", 2, there);
        var wv2 = ((IList)wavesF.GetValue(g))[((IList)wavesF.GetValue(g)).Count - 1];
        Check(near(fieldOf(wv2, "HalfWidth") / tW, 1.8f, 0.01f) && near(fieldOf(wv2, "WetFor") - tWet, 8f, 0.01f), "Riptide: Tsunami's wave is 40% wider and wets 4 s longer per card");
        ((IList)wavesF.GetValue(g)).Clear();

        // --- Summoner ---
        castWith(AbilityId.GolemWall, "Basalt Hide", 0, there);
        float gHp = mine(MinionKind.Wall)[0].BlockHp;
        castWith(AbilityId.GolemWall, "Basalt Hide", 2, there);
        var golems = mine(MinionKind.Wall);
        Check(golems.All(m => near(m.BlockHp / gHp, 2f, 0.01f) && m.MaxHp == m.BlockHp), "Basalt Hide: golems have 50% more health per card (" + gHp + " -> " + golems[0].BlockHp + ")");

        castWith(AbilityId.Earthquake, "Deep Tremor", 0, there);
        var q0 = mine(MinionKind.Burrower).OrderBy(m => m.Id).Last();
        float qD = q0.Duration, qP = q0.Power;
        castWith(AbilityId.Earthquake, "Deep Tremor", 2, there);
        var q2 = mine(MinionKind.Burrower).OrderBy(m => m.Id).Last();
        Check(near(q2.Duration - qD, 3f, 0.01f) && near(q2.Power / qP, 2f, 0.01f), "Deep Tremor: the Burrower tunnels 1.5 s longer and hits 50% harder per card");

        var respawn = typeof(Game).GetMethod("PactRespawn", sflags);
        castWith(AbilityId.GuardianSpirit, "Kindred Bond", 0, new V2(500f, 450f));
        float pr0 = (float)respawn.Invoke(null, new object[] { me });
        castWith(AbilityId.GuardianSpirit, "Kindred Bond", 2, new V2(500f, 450f));
        float pr2 = (float)respawn.Invoke(null, new object[] { me });
        Check(near(pr0, Game.GuardRespawn, 0.01f) && near(pr2, Game.GuardRespawn - 10f, 0.01f), "Kindred Bond: the pact ally comes back 5 s sooner per card (" + pr0 + " -> " + pr2 + " s)");
        var thrallHp = typeof(Game).GetMethod("ThrallMaxHp", sflags);
        castWith(AbilityId.GuardianSpirit, "Kindred Bond", 1, new V2(500f, 450f));
        var victim = enemyAt(new V2(620f, 450f));
        cast.Invoke(g, new object[] { me, 0, victim.Pos });
        Minion thrall = null;
        Run(s, dt, 3f, () => (thrall = g.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.Thrall && m.Owner == 1)) != null);
        float baseHp = thrall != null ? (float)thrallHp.Invoke(null, new object[] { (EnemyType)thrall.Pact, g.Wave }) : 0f;
        Check(thrall != null && near(thrall.MaxHp / baseHp, 1.5f, 0.01f), "Kindred Bond: the pact ally has 50% more health" + (thrall != null ? " (" + thrall.MaxHp + " for " + baseHp + ")" : " (no thrall rose)"));
        foreach (var p in g.Players.Values) { p.PactSet[0] = false; }
        foreach (int id in g.Minions.Values.Where(m => m.Kind == MinionKind.Thrall).Select(m => m.Id).ToList()) g.Minions.Remove(id);

        castWith(AbilityId.SpiritSwarm, "Sprite Choir", 0, there);
        var sp0 = mine(MinionKind.Sprite)[0]; float sD = sp0.Duration, sH = sp0.HealRate;
        castWith(AbilityId.SpiritSwarm, "Sprite Choir", 2, there);
        var sp2 = mine(MinionKind.Sprite)[0];
        Check(near(sp2.Duration - sD, 4f, 0.01f) && near(sp2.HealRate / sH, 1.8f, 0.01f), "Sprite Choir: sprites heal 40% more and last 2 s longer per card");

        castWith(AbilityId.Phoenix, "Rebirth Flame", 0, there);
        float phD = mine(MinionKind.Phoenix)[0].Duration;
        castWith(AbilityId.Phoenix, "Rebirth Flame", 1, there);
        Check(near(mine(MinionKind.Phoenix)[0].Duration - phD, 4f, 0.01f), "Rebirth Flame: the phoenix stays 4 s longer");
        g.DevSetGod(2, false);
        friend.Hp = 0f; friend.Downed = true;
        Run(s, dt, 3f, () => !friend.Downed);
        Check(!friend.Downed && near(friend.Hp / friend.MaxHp, 0.75f, 0.02f), "Rebirth Flame: the phoenix revives with 25% more health (" + Math.Round(friend.Hp / friend.MaxHp * 100f) + "%)");
        g.DevSetGod(2, true);

        castWith(AbilityId.BatSwarm, "Blood Bats", 0, there);
        var bt0 = mine(MinionKind.Bat)[0]; float btD = bt0.Duration, btP = bt0.Power;
        castWith(AbilityId.BatSwarm, "Blood Bats", 2, there);
        var bt2 = mine(MinionKind.Bat)[0];
        Check(near(bt2.Duration - btD, 4f, 0.01f) && near(bt2.Power / btP, 1.8f, 0.01f), "Blood Bats: bats bite 40% harder and hunt 2 s longer per card");

        castWith(AbilityId.Hydra, "Venom Glands", 0, there);
        float hD = mine(MinionKind.Hydra)[0].Duration;
        castWith(AbilityId.Hydra, "Venom Glands", 2, there);
        var hydra = mine(MinionKind.Hydra)[0];
        enemyAt(there + new V2(120f, 0f));
        Bullet venom = null;
        Run(s, dt, 3f, () => (venom = g.Bullets.Values.FirstOrDefault(b => b.Owner == 1 && b.Kind == Defs.MinionShot)) != null);
        Check(near(hydra.Duration - hD, 4f, 0.01f) && venom != null && near(venom.SlowAmt, 0.6f, 0.001f), "Venom Glands: the hydra stays 2 s longer per card and its venom slows (" + (venom != null ? venom.SlowAmt : -1f) + ")");

        var scentStart = typeof(Game).GetMethod("ScentStartFor", sflags);
        castWith(AbilityId.AlphaCall, "Pack Leader", 0, there);
        float sc0 = me.ScentT; int st0 = (int)scentStart.Invoke(null, new object[] { me });
        castWith(AbilityId.AlphaCall, "Pack Leader", 2, there);
        Check(near(me.ScentT - sc0, 6f, 0.01f) && st0 == Game.ScentStart && (int)scentStart.Invoke(null, new object[] { me }) == Game.ScentStart + 2,
            "Pack Leader: Blood Scent lasts 3 s longer and starts 1 stack higher per card");

        // --- Soldier ---
        Func<int, Bullet> medkit = n =>
        {
            castWith(AbilityId.Medkit, "Field Surgeon", n, there);
            return g.Bullets.Values.First(b => b.Kind == Defs.MedkitShot);
        };
        var k0 = medkit(0); int o0 = k0.Burst; float h0 = k0.Dmg;
        var k2 = medkit(2);
        Check(k2.Burst - o0 == 6 && near(k2.Dmg - h0, 10f, 0.01f), "Field Surgeon: the Medkit bursts into 3 more orbs, each healing 5 more, per card (" + o0 + " x " + h0 + " -> " + k2.Burst + " x " + k2.Dmg + ")");

        host.Leave(""); client.Leave("");
    }
}
