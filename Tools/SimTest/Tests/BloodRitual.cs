using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Blood Ritual (BloodRitual.cs): key 3 of the DPS Hemomancer's Ultimate row; the sigil at the cursor clamped to 320 px; the 15% price
// paid over the 1.2 s inscribe, no imps before the flood, broken by going down while drawn; the open sigil slowing what stands on it;
// the burst of 8 then 4 a second, at most 24 out and 40 in all; the other screen seeing the sigil and the imps; an imp clawing (damage,
// a bleed stack, the taunt drawing the enemy's aim, bosses not taunted), being swatted and popping in a splash; the close sending
// every imp home to heal 1% each; mastered (10 s, 50 imps); the imps going with the sigil.
static partial class Program
{
    static void BloodRitualTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var impTarget = typeof(Game).GetMethod("ImpTarget", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby: key 3 of the Ultimate row ----------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.BloodRitual);
        Check(def.Name == "Blood Ritual" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Role == Role.Dps && def.Kit == Kit.Hemomancer && def.Bot != null,
            "Blood Ritual is the DPS Hemomancer's ultimate on key 3, 80 s, with a bot rule");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Hemomancer) == AbilityId.CrimsonHarvest && Upgrades.Ultimate(Role.Dps, Kit.Hemomancer, 1) == AbilityId.BloodRavager
            && Upgrades.Ultimate(Role.Dps, Kit.Hemomancer, 2) == AbilityId.BloodBoil && Upgrades.Ultimate(Role.Dps, Kit.Hemomancer, 3) == AbilityId.BloodRitual,
            "Crimson Harvest stays the default; Blood Ravager key 1, Blood Boil key 2, Blood Ritual key 3");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Hemomancer, 3) == AbilityId.SacrificialRitual,
            "the Tank Hemomancer's key 3 is its own (Sacrificial Ritual), not Blood Ritual");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Ritual", Role.Dps, 0, 0, Kit.Hemomancer, 0, 3);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0, 0, Kit.Hemomancer, 0, 3);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 3 && client.Lobby[1].Ult == 3);
        Check(host.Lobby[1].UltId == AbilityId.BloodRitual && client.Lobby[1].Ult == 3, "the pick reaches the host and the other player");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.BloodRitual && cg.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.BloodRitual,
            "in the run both players' F is Blood Ritual, on both games");

        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        V2 mePos = new V2(500f, 450f), aimAt = new V2(900f, 450f), allyPos = new V2(200f, 200f);
        var mine = new HashSet<int>();
        Func<V2, EnemyType, Enemy> make = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FrozenT = 999f; e.FireT = 999f; mine.Add(e.Id); return e;
        };
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var id in g.Bullets.Values.Where(x => x.Owner != 1).Select(x => x.Id).ToList()) g.Bullets.Remove(id);
                foreach (var id in mine) { Enemy e; if (g.Enemies.TryGetValue(id, out e)) e.FireT = 999f; }
                me.Pos = mePos; ally.Pos = allyPos;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aimAt });
            }
        };
        Action reset = () =>
        {
            foreach (var id in mine.ToList()) g.Enemies.Remove(id);
            mine.Clear();
            g.Bullets.Clear(); g.Effects.Clear(); g.Zones.Clear(); g.Minions.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.InvulnT = 0f; me.DevGod = false;
            me.Slots[Upgrades.UltSlot].Id = AbilityId.BloodRitual; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
            me.Pos = mePos; masteredField.SetValue(g, false);
            g.State = MatchState.Fighting;
        };
        Func<Zone> ritual = () => g.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.BloodRitual);
        Func<Game, List<Minion>> imps = gm => gm.Minions.Values.Where(m => m.Kind == MinionKind.BloodImp).ToList();
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, aimAt, false });

        // --- The inscribe: the sigil at the cursor, clamped; the price; no imps yet --------------------------------------
        reset();
        cast();
        var z = ritual();
        Check(z != null && Math.Abs(z.Pos.X - (mePos.X + Game.RitReach)) < 1f && Math.Abs(z.Pos.Y - mePos.Y) < 1f && z.Radius == Game.RitRadius,
            "the sigil is drawn at the cursor, clamped to 320 px away, 90 px across");
        float hp0 = me.Hp;
        step(Game.RitInscribe - 0.05f);
        float paid = hp0 - me.Hp;
        Check(paid > me.MaxHp * 0.13f && paid < me.MaxHp * 0.16f && imps(g).Count == 0,
            "15% of max health is paid over the 1.2 s inscribe (" + paid.ToString("0.0") + " of " + me.MaxHp.ToString("0") + "), and no imps come out yet");
        step(Game.RitOpen - 0.05f);
        Check(imps(g).Count == 0 && Game.RitOpenNow(ritual()), "the portal opens, still no imps until the flood");

        // --- The open sigil slows what stands on it ------------------------------------------------------------------------
        var onIt = make(z.Pos + new V2(30f, 0f), EnemyType.Grunt);
        var offIt = make(z.Pos + new V2(0f, 260f), EnemyType.Grunt);
        onIt.FrozenT = offIt.FrozenT = 0f;  // (a frozen enemy isn't slowed on top)
        step(0.15f);
        Check(onIt.SlowT > 0f && onIt.SlowMult <= 1f - Game.RitSlow + 0.01f && offIt.SlowT <= 0f, "an enemy on the open sigil is slowed 40%; one off it isn't");

        // --- The flood: a burst of 8, then 4 a second ------------------------------------------------------------------------
        onIt.FrozenT = offIt.FrozenT = 999f;
        Check(imps(g).Count == Game.RitBurst, "8 imps burst out at once (" + imps(g).Count + ")");
        step(1f);
        int out1 = imps(g).Count;
        Check(out1 >= Game.RitBurst + 3 && out1 <= Game.RitBurst + 5, "then 4 a second (" + out1 + " after 1 s)");
        step(0.5f);
        Check(cg.Zones.Values.Any(q => q.Kind == ZoneKind.BloodRitual) && imps(cg).Count > 0, "the other screen sees the sigil and the imps");

        // --- An imp claws: damage, bleed, the taunt -------------------------------------------------------------------------
        float hpOn = onIt.Hp;
        step(1f);
        Check(onIt.Hp < hpOn && onIt.BleedStacks > 0, "the imps claw the enemy they reach (" + (hpOn - onIt.Hp).ToString("0") + " damage) and bleed it");
        Minion tauntBy;
        bool taunted = onIt.ImpTauntT > 0f && g.Minions.TryGetValue(onIt.ImpTauntId, out tauntBy) && tauntBy.Kind == MinionKind.BloodImp;
        V2 aimed = (V2)impTarget.Invoke(g, new object[] { onIt, me.Pos });
        Check(taunted && aimed.DistanceTo(g.Minions[onIt.ImpTauntId].Pos) < 0.5f, "a clawed enemy is taunted: it goes and shoots at the imp, not you");
        var boss = make(z.Pos + new V2(-200f, -150f), EnemyType.Colossus);
        boss.ImpTauntT = 1f; boss.ImpTauntId = onIt.ImpTauntId;
        aimed = (V2)impTarget.Invoke(g, new object[] { boss, me.Pos });
        Check(aimed.DistanceTo(me.Pos) < 0.5f, "a boss ignores the taunt");
        g.Enemies.Remove(boss.Id); mine.Remove(boss.Id);

        // --- Swatted, and popping in a splash --------------------------------------------------------------------------------
        var clinging = imps(g).FirstOrDefault(m => m.Stacks == 2);
        Check(clinging != null && clinging.BlockHp < clinging.Def.BlockCapacity, "an imp clinging to an enemy is swatted (" + (clinging != null ? clinging.BlockHp.ToString("0.0") : "-") + " of 30 left)");
        if (clinging != null)
        {
            var victim = g.Enemies.Values.Where(e => mine.Contains(e.Id)).OrderBy(e => e.Pos.DistanceTo(clinging.Pos)).First();
            float before = victim.Hp;
            int id = clinging.Id;
            clinging.BlockHp = 0.01f;
            step(dt * 2);
            Check(!g.Minions.ContainsKey(id) && victim.Hp <= before - Game.RitPopDamage * 0.9f, "worn out, it pops in a splash that hurts the enemy beside it");
        }

        // --- Never more than 24 out, 40 in all ------------------------------------------------------------------------------
        int most = 0;
        for (int i = 0; i < 60; i++) { step(0.1f); most = Math.Max(most, imps(g).Count); }
        Check(most <= Game.RitAlive && ritual().Side <= Game.RitTotal, "at most 24 out at once (" + most + "), 40 in all (" + ritual().Side + ")");

        // --- The close: every imp flies home and heals you --------------------------------------------------------------------
        z = ritual();
        step(Game.RitCloseAt(z) - Game.RitAge(z) - 0.1f);
        int home = imps(g).Count;
        me.Hp = me.MaxHp * 0.5f;
        float hpClose = me.Hp;
        step(Game.RitClose + 0.3f);
        float healed = me.Hp - hpClose;
        Check(home > 0 && imps(g).Count == 0 && ritual() == null && Math.Abs(healed - home * me.MaxHp * Game.RitPayback) < me.MaxHp * 0.03f,
            "at the close the " + home + " imps left fly home, each healing 1% (" + healed.ToString("0.0") + "), and the sigil is gone");

        // --- Broken by going down while it's drawn ----------------------------------------------------------------------------
        reset();
        cast();
        step(0.4f);
        me.Downed = true;
        step(0.1f);
        me.Downed = false; me.Hp = me.MaxHp;
        step(Game.RitInscribe + Game.RitOpen + 0.3f);
        Check(ritual() == null && imps(g).Count == 0, "going down while the pentagram is drawn breaks it: no imps");

        // --- The imps go with the sigil (the wave's zones cleared) ---------------------------------------------------------
        reset();
        cast();
        step(Game.RitFloodAt + 0.5f);
        int had = imps(g).Count;
        g.Zones.Clear();
        step(dt * 2);
        Check(had > 0 && imps(g).Count == 0, "when the sigil goes (the wave over), its imps go with it");

        // --- Mastered: 10 s, 50 imps --------------------------------------------------------------------------------------------
        reset();
        me.MasteryXp[AbilityId.BloodRitual] = 1e6f;
        cast();
        z = ritual();
        Check(z != null && z.Cap && Math.Abs(z.Duration - (Game.RitInscribe + Game.RitOpen + Game.RitFloodMastered + Game.RitClose)) < 0.01f,
            "mastered, the portal stays open 10 s");
        me.MasteryXp.Remove(AbilityId.BloodRitual);
        reset();

        host.Leave(""); client.Leave("");
    }
}
