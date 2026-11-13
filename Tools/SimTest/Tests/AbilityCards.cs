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

        // --- Class cards (Kit; Docs/UpgradePools.md step 6) ---
        var damage = typeof(Game).GetMethod("Damage", flags);
        var fireRate = typeof(Game).GetMethod("FireRate", flags);
        var localDodge = typeof(Game).GetMethod("LocalDodge", flags);
        var pay = typeof(Game).GetMethod("PayHealth", flags);
        Func<float> hurt = () =>
        {
            g.DevSetGod(1, false);
            me.InvulnT = 0f; me.Hp = me.MaxHp;
            me.ShieldHp = 0f; me.ShieldT = 0f; me.EnergyOn = false; me.BarrierT = 0f; me.BarrierHp = 0f;  // (earlier casts' wards and shields would soak it)
            g.Zones.Clear();
            float hp0 = me.Hp;
            damage.Invoke(g, new object[] { me, 100f, "test" });
            g.DevSetGod(1, true);
            return hp0 - me.Hp;
        };
        Check(new[] { "Quartermaster", "Arcane Surge", "Pack Bond", "Plate Armour", "Ghost Step", "Bloodthirst" }
            .All(n => U(n) >= 0 && Upgrades.All[U(n)].Kit.HasValue && Upgrades.All[U(n)].Rarity == Rarity.Rare), "all six class cards exist, each for one class");

        // Quartermaster: orbs heal 30% more; Q/E/Shift recharge 10% faster (Space doesn't).
        Func<int, float[]> quarter = n =>
        {
            prep(AbilityId.Meteor, "Quartermaster", n);
            me.Slots[1].Id = AbilityId.Blink;
            float q = g.Cooldown(me, 1), sp = g.Cooldown(me, 0);
            g.DevSetGod(1, false);
            me.Hp = me.MaxHp - 50f;
            float hp0 = me.Hp;
            g.Orbs.Clear();
            g.Orbs[9999] = new Orb { Id = 9999, Med = true, MedHeal = 10f, Pos = me.Pos, Disp = me.Pos, Life = 5f };
            Run(s, dt, 0.5f, () => !g.Orbs.ContainsKey(9999), () => { me.Pos = new V2(500f, 450f); });
            g.DevSetGod(1, true);
            return new[] { q, sp, me.Hp - hp0 };
        };
        var qm0 = quarter(0); var qm1 = quarter(1);
        Check(near(qm1[0] / qm0[0], 0.9f, 0.001f) && near(qm1[1], qm0[1], 0.001f) && near(qm0[2], 10f, 0.6f) && near(qm1[2], 13f, 0.6f),
            "Quartermaster: Q/E/Shift recharge 10% faster (Space doesn't) and orbs heal 30% more (" + qm0[2] + " -> " + qm1[2] + ")");

        // Arcane Surge: a Space cast fires the weapon 25% faster for 3 s, and the client sees it.
        castWith(AbilityId.Meteor, "Arcane Surge", 0, there);
        float rate0 = (float)fireRate.Invoke(g, new object[] { me });
        bool none = me.SurgeT == 0f;
        castWith(AbilityId.Meteor, "Arcane Surge", 1, there);
        float rate1 = (float)fireRate.Invoke(g, new object[] { me });
        Run(s, dt, 0.3f);
        Check(none && near(rate1 / rate0, 1.25f, 0.001f) && me.SurgeT > 2f && client.Game.Players[1].SurgeT > 2f,
            "Arcane Surge: a Space cast fires the weapon 25% faster for 3 s, on both screens");

        // Pack Bond: minions take 25% more before breaking and hit 15% harder.
        castWith(AbilityId.GolemWall, "Pack Bond", 0, there);
        float pg0 = mine(MinionKind.Wall)[0].BlockHp;
        castWith(AbilityId.BatSwarm, "Pack Bond", 0, there);
        float pb0 = mine(MinionKind.Bat)[0].Power;
        castWith(AbilityId.GolemWall, "Pack Bond", 1, there);
        float pg1 = mine(MinionKind.Wall)[0].BlockHp;
        castWith(AbilityId.BatSwarm, "Pack Bond", 1, there);
        float pb1 = mine(MinionKind.Bat)[0].Power;
        Check(near(pg1 / pg0, 1.25f, 0.001f) && near(pb1 / pb0, 1.15f, 0.001f), "Pack Bond: minions take 25% more before they break and hit 15% harder");

        // Plate Armour: 8% less damage, bullets cut 20% farther out.
        prep(AbilityId.Meteor, "Plate Armour", 0); float pa0 = hurt();
        prep(AbilityId.Meteor, "Plate Armour", 1); float pa1 = hurt();
        Check(near(pa1 / pa0, 0.92f, 0.002f) && near(me.Stats.CutReachMult, 1.2f, 0.001f), "Plate Armour: 8% less damage taken (" + pa0 + " -> " + pa1 + "), swings cut 20% farther out");

        // Ghost Step: +6% speed, 30% less damage for 1 s after a dodge (2 s with two).
        Func<int, float[]> ghost = n =>
        {
            prep(AbilityId.Meteor, "Ghost Step", n);
            float speed = me.Speed;
            float plain = hurt();
            localDodge.Invoke(g, new object[] { me, new V2(1f, 0f), true, 0f });
            float t = me.GhostT;
            float after = hurt();
            Run(s, dt, 1.3f);  // (lets the dodge's dash and cooldown finish)
            me.GhostT = 0f;
            return new[] { speed, plain, after, t };
        };
        var gs0 = ghost(0); var gs2 = ghost(2);
        Check(near(gs2[0] / gs0[0], 1.12f, 0.002f) && near(gs0[2], gs0[1], 0.01f) && near(gs2[2] / gs2[1], 0.7f, 0.002f) && near(gs2[3], 2f, 0.001f),
            "Ghost Step: faster, and 30% less damage for 1 s per card after a dodge (" + gs2[1] + " -> " + gs2[2] + ")");

        // Bloodthirst: abilities cost 20% less health; kills heal 2.
        prep(AbilityId.Meteor, "Bloodthirst", 0);
        me.Hp = me.MaxHp; float paid0 = (float)pay.Invoke(g, new object[] { me, 50f, "test" });
        prep(AbilityId.Meteor, "Bloodthirst", 1);
        me.Hp = me.MaxHp; float paid1 = (float)pay.Invoke(g, new object[] { me, 50f, "test" });
        Check(near(paid0, 50f, 0.01f) && near(paid1, 40f, 0.01f) && near(me.Stats.KillHeal, 2f, 0.001f), "Bloodthirst: abilities cost 20% less health and kills heal 2");

        // --- Knight weapon cards (Weapons; Docs/UpgradePools.md step 7) ---
        var fireKnight = typeof(Game).GetMethod("FireKnightWeapon", flags);
        var swingsF = typeof(Game).GetField("pendingSwings", flags);
        Func<List<Swing>> swings = () => (List<Swing>)swingsF.GetValue(g);
        Action<WeaponId, string, int> arm = (wid, card, n) =>
        {
            prep(AbilityId.Meteor, card, n);
            me.Weapon = Defs.WeaponOf(wid);
            recalc.Invoke(g, new object[] { me });
            me.Weapon = Defs.WeaponOf(wid);
            swings().Clear();
            me.FireCd = 0f; me.Combo = 0; me.ComboT = 0f;
        };
        Func<WeaponId, string, int, List<Swing>> swing = (wid, card, n) =>
        {
            arm(wid, card, n);
            fireKnight.Invoke(g, new object[] { me, 0f, 1f, 0 });
            var got = swings().ToList();
            swings().Clear();
            return got;
        };
        Check(new[] { "Quake Hammer", "Breacher", "Long Haft", "Pilgrim's Staff", "Daybreak", "Thick Incense", "Whirling Sabres", "Tempered Blade", "Duelist's Edge", "Live Wire" }
            .All(n => U(n) >= 0 && Upgrades.All[U(n)].Weapons != null && Upgrades.All[U(n)].Weapons.Length == 1 && Defs.WeaponOf(Upgrades.All[U(n)].Weapons[0]).Kit == Kit.Knight),
            "all ten Knight weapon cards exist, each for one Knight weapon");

        arm(WeaponId.Warhammer, "Quake Hammer", 0); float hr0 = Game.HammerR(me);
        var hs0 = swing(WeaponId.Warhammer, "Quake Hammer", 0);
        arm(WeaponId.Warhammer, "Quake Hammer", 2); float hr2 = Game.HammerR(me);
        var hs2 = swing(WeaponId.Warhammer, "Quake Hammer", 2);
        Check(near(hr2 / hr0, 1.4f, 0.001f) && near(hs2[0].Circle / hs0[0].Circle, 1.4f, 0.001f) && near(hs2[0].Stun - hs0[0].Stun, 0.6f, 0.001f),
            "Quake Hammer: the slam is 20% wider per card (outline too) and stuns 0.3 s longer");

        Func<int, float> breach = n => { arm(WeaponId.Lance, "Breacher", n); me.LancePrimed = true; fireKnight.Invoke(g, new object[] { me, 0f, 1f, 0 }); float k = swings().Max(x => x.Knock); swings().Clear(); return k; };
        var wallHit = typeof(Game).GetMethod("LanceWallImpact", flags);
        Func<int, float> impact = n =>
        {
            arm(WeaponId.Lance, "Breacher", n);
            var e = enemyAt(new V2(900f, 450f));
            e.ImpactBy = me.Id + 1;
            float hp0 = e.Hp;
            wallHit.Invoke(g, new object[] { e, e.Pos });
            return hp0 - e.Hp;
        };
        float bk0 = breach(0), bk1 = breach(1), im0 = impact(0), im1 = impact(1);
        Check(near(bk1 / bk0, 1.3f, 0.001f) && im0 > 0f && near(im1 / im0, 1.5f, 0.01f), "Breacher: Breaching Thrust shoves 30% farther and Impact hits 50% harder (" + im0 + " -> " + im1 + ")");

        var hb0 = swing(WeaponId.Halberd, "Long Haft", 0); var hb2 = swing(WeaponId.Halberd, "Long Haft", 2);
        float sw0 = hb0.Max(x => Math.Abs(x.Aim)), sw2 = hb2.Max(x => Math.Abs(x.Aim));
        Check(near(hb2.Max(x => x.Reach) / hb0.Max(x => x.Reach), 1.3f, 0.01f) && near(sw2 - sw0, 20f * MathX.Pi / 180f, 0.001f),
            "Long Haft: the Halberd reaches 15% farther and sweeps 10° more each way per card");

        var qs0 = swing(WeaponId.HolyMace, "Pilgrim's Staff", 0); int cap0 = g.StaffCap(me);
        var qs1 = swing(WeaponId.HolyMace, "Pilgrim's Staff", 1); int cap1 = g.StaffCap(me);
        Check(near(qs1[0].HealPerHit - qs0[0].HealPerHit, 1f, 0.001f) && cap1 == cap0 + 1, "Pilgrim's Staff: each hit heals 1 more and one more Jab stack fits");

        var lightLanded = typeof(Game).GetMethod("LightLanded", flags);
        var sunlance = typeof(Game).GetMethod("LandSunlance", flags);
        Func<int, float[]> dawn = n =>
        {
            arm(WeaponId.Lightbringer, "Daybreak", n);
            me.Light = 0; me.LightCarry = 0f;
            lightLanded.Invoke(g, new object[] { me, new Swing { LightKind = 1 }, me.Pos, me.Pos, 6 });  // (the gauge holds 12)
            int light = me.Light;
            g.DevSetGod(2, false);
            friend.Hp = friend.MaxHp * 0.3f; float fh = friend.Hp;
            sunlance.Invoke(g, new object[] { me, friend, 5 });
            g.DevSetGod(2, true);
            return new[] { light, friend.Hp - fh };
        };
        var db0 = dawn(0); var db1 = dawn(1);
        Check(db0[0] == 6 && db1[0] == 7 && me.LightCarry > 0.79f && near(db1[1] / db0[1], 1.3f, 0.01f), "Daybreak: the light gauge fills 30% faster and the Sunlance heals 30% more (" + db0[0] + "/" + db1[0] + " light, " + db0[1] + "/" + db1[1] + " HP)");

        var addZone = typeof(Game).GetMethod("AddZone", flags);
        Func<int, Zone> incense = n => { arm(WeaponId.Censer, "Thick Incense", n); return (Zone)addZone.Invoke(g, new object[] { ZoneKind.Incense, me, there, 42f, 2.5f }); };
        var in0 = incense(0); var in2 = incense(2);
        Check(near(in2.Radius / in0.Radius, 1.6f, 0.001f) && near(in2.Duration - in0.Duration, 4f, 0.001f), "Thick Incense: incense clouds are 30% bigger and last 2 s longer per card");

        var fang = typeof(Game).GetMethod("HostTwinFang", flags);
        var runsF = typeof(Game).GetField("fangRuns", flags);
        Func<int, float> sabres = n =>
        {
            arm(WeaponId.TwinBlades, "Whirling Sabres", n);
            me.FangCd = 0f;
            ((IList)runsF.GetValue(g)).Clear();
            fang.Invoke(g, new object[] { me, me.Pos + new V2(1000f, 0f) });
            var run = ((IList)runsF.GetValue(g))[0];
            return ((V2)run.GetType().GetField("To").GetValue(run)).DistanceTo(me.Pos);
        };
        float fr0 = sabres(0), fr1 = sabres(1);
        ((IList)runsF.GetValue(g)).Clear();
        Check(near(fr1 / fr0, 1.25f, 0.01f) && near(me.Stats.FangDmgMult, 1.3f, 0.001f), "Whirling Sabres: the thrown sabres fly 25% farther (" + fr0 + " -> " + fr1 + " px) and hit 30% harder");

        var guard = typeof(Game).GetMethod("HostGreatGuard", flags);
        var block = typeof(Game).GetMethod("GreatGuardBlock", flags);
        Func<int, float[]> great = n =>
        {
            arm(WeaponId.Greatsword, "Tempered Blade", n);
            me.GreatLock = 0f; me.GreatGuard = false; me.TemperCarry = 0f;
            guard.Invoke(g, new object[] { me, true, there });
            g.Bullets.Clear();
            for (int i = 0; i < 4; i++)
                g.Bullets[90000 + i] = new Bullet { Id = 90000 + i, Kind = Defs.SmallShot, Pos = me.Pos + V2.FromAngle(me.Aim + (i - 1.5f) * 0.2f) * Game.GreatGuardRadius, Radius = 4f, Life = 5f };
            block.Invoke(g, new object[] { me });
            float temper = me.GreatTemper;
            me.GreatTemper = 6;
            swings().Clear();
            guard.Invoke(g, new object[] { me, false, there });
            float rend = swings().Where(x => x.Rend).Select(x => x.Dmg).DefaultIfEmpty(0f).Max();
            swings().Clear();
            return new[] { temper, rend };
        };
        var gt0 = great(0); var gt1 = great(1);
        Check(gt0[0] == 4f && gt1[0] == 5f && gt0[1] > 0f && near(gt1[1] / gt0[1], 1.3f, 0.01f), "Tempered Blade: guarding builds Temper 25% faster (4 -> 5 for four bullets) and the Rend hits 30% harder");

        var tickRapier = typeof(Game).GetMethod("TickRapier", flags);
        Func<int, int> tempo = n =>
        {
            arm(WeaponId.Rapier, "Duelist's Edge", n);
            me.RapierTempo = 10; me.RapierTempoF = 10f; me.RapierHitAgo = 1.4f;  // past the plain 1.2 s hold, inside 1.7
            tickRapier.Invoke(g, new object[] { me, 0.1f });
            return me.RapierTempo;
        };
        int tp0 = tempo(0), tp1 = tempo(1);
        Check(tp0 < 10 && tp1 == 10 && me.Stats.SkewerBonus == 1, "Duelist's Edge: Tempo holds 0.5 s longer (" + tp0 + " vs " + tp1 + " stacks after 1.5 s) and skewers one more bullet");

        arm(WeaponId.ArcKnuckles, "Live Wire", 0);
        var far3 = enemyAt(me.Pos + new V2(340f, 0f));  // past the 300 px lock-on, inside 375
        bool noLock = g.KnuckleLockTarget(me.Pos, far3.Pos, me.Stats.KnuckleDashMult) == null;
        arm(WeaponId.ArcKnuckles, "Live Wire", 1);
        var far4 = enemyAt(me.Pos + new V2(340f, 0f));
        Check(noLock && g.KnuckleLockTarget(me.Pos, far4.Pos, me.Stats.KnuckleDashMult) == far4 && near(me.Stats.KnuckleStunBonus, 0.3f, 0.001f),
            "Live Wire: the Rocket Dash locks on 25% farther out and the Twin Hammer stuns 0.3 s longer");

        host.Leave(""); client.Leave("");
    }
}
