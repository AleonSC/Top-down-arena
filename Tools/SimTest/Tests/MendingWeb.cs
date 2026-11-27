using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Mending Web (MendingWeb.cs), the Healer Summoner's 1st choice (took Spirit Swarm's place): the table and card, the wall
// snap, the pair rules (another wall, length, an open line), the pick over the network (press, click a wall, click another;
// the key again cancels; nothing spent until the second click; a bad second click does nothing), the web between the two
// anchors on both screens, built only after the spider's run and weave, the heal for allies in the band, the slow and the
// poison for enemies in it (heavies half, the poison staying after), nothing outside it, at most 3 a caster, bots, mastery.
static partial class Program
{
    static void MendingWebTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        var def = Upgrades.Ability(AbilityId.MendingWeb);
        Check(def.Name == "Mending Web" && def.Choice == 0 && def.Kit == Kit.Summoner && def.Role == Role.Healer && def.Cooldown == Game.WebCooldown
            && def.ExtraCharges == Game.WebCharges - 1 && def.Icon == "spiderweb" && Upgrades.FirstAbility(Role.Healer, Kit.Summoner, 0) == AbilityId.MendingWeb,
            "Mending Web: the Healer Summoner's 1st choice, 2 charges of 8 s");
        Check(Upgrades.All.Any(c => c.Name == "Silk Choir" && c.RequiresAbility == AbilityId.MendingWeb && c.Rarity == Rarity.Rare && c.MaxStacks == 2)
            && !Upgrades.All.Any(c => c.Name == "Sprite Choir"), "Silk Choir is its Rare card (Sprite Choir is gone)");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Weaver", Role.Healer, 0, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Summoner);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        var cg = client.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        g.DevSetLayout(LayoutId.Cross);
        var me = g.Players[1];
        var friend = g.Players[2];
        Check(friend.Slots[0].Id == AbilityId.MendingWeb, "the pick reaches the run");

        var mine = new HashSet<int>();
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; mine.Add(e.Id); return e;
        };
        var pinned = new Dictionary<Enemy, V2>();
        V2 friendPos = new V2(0f, 0f), mePos = new V2(0f, 0f);
        Action<float, PlayerInput> stepIn = (secs, input) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                g.Bullets.Clear();
                foreach (var kv in pinned) { kv.Key.Pos = kv.Value; kv.Key.FireT = 99f; kv.Key.KnockLeft = V2.Zero; }
                me.Pos = mePos; me.InvulnT = 1f; friend.InvulnT = 1f; cg.Me.Pos = friendPos;
                hostS.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = mePos }); g.Update(dt);
                client.Poll(); cg.FixedStep(dt, input); cg.Update(dt);
            }
        };
        Action<float> step = secs => stepIn(secs, new PlayerInput { AimAt = friendPos });
        Func<List<Zone>> webs = () => g.Zones.Values.Where(z => z.Kind == ZoneKind.MendingWeb).OrderBy(z => z.Id).ToList();
        make(EnemyType.Grunt, new V2(60f, 60f));  // far off: the wave goes on
        step(1.5f);  // (the layout's walls reach the client)

        // Find two facing walls in the Cross layout with an open line between them.
        V2 a = V2.Zero, b = V2.Zero;
        WebAnchor wa = default(WebAnchor), wb = default(WebAnchor);
        bool found = false;
        for (float x = g.ArenaMin.X + 60f; x < g.ArenaMax.X - 60f && !found; x += 40f)
            for (float y = g.ArenaMin.Y + 60f; y < g.ArenaMax.Y - 60f && !found; y += 40f)
                if (g.WebAuto(new V2(x, y), V2.Zero, 300f, out a, out b) && g.WebSnap(a, 6f, out wa) && g.WebSnap(b, 6f, out wb)
                    && g.WebPair(wa, wb, Game.WebMaxLen) == null && cg.WebSnap(a, 6f, out _) && cg.WebSnap(b, 6f, out _))
                    found = true;
        Check(found, "the Cross layout has two walls a web can join (" + a + " to " + b + ")");
        V2 mid = (a + b) * 0.5f, u = (b - a).Normalized(), n = new V2(-u.Y, u.X);
        friendPos = mid + n * 120f;  // close by, off the web
        mePos = mid + n * 220f;

        // The rules: a wall face snaps, open floor doesn't; the same wall, too far and a blocked line are refused.
        WebAnchor t0, t1;
        Check(g.WebSnap(a + wa.Normal * 20f, Game.WebSnapR, out t0) && t0.At.DistanceTo(a) < 1f && t0.Wall == wa.Wall
            && !g.WebSnap(mid, 5f, out t1), "a click near a wall snaps to its face; open floor doesn't");
        Check(g.WebPair(wa, wa, 999f) != null && g.WebPair(wa, wb, a.DistanceTo(b) - 10f) == "too far" && g.WebPair(wa, wb, 999f) == null,
            "a pair needs another wall and to fit the length");

        // The pick, on the client: press (a pick, nothing spent), click open floor (nothing), click wall 1, click wall 1 again
        // (refused), the key again cancels; press, click wall 1, click wall 2: the cast.
        var cme = cg.Me;
        friend.Slots[0].Charges = 2; friend.Slots[0].Timer = 0f;
        step(0.2f);
        stepIn(dt, new PlayerInput { AimAt = friendPos, Ability = true });
        step(0.05f);
        Check(cg.WebPickSlot == 0 && !cg.WebFirst.HasValue && cme.Slots[0].Charges == 2 && webs().Count == 0, "pressing it starts the pick and spends nothing");
        stepIn(dt, new PlayerInput { AimAt = mid, Fire = true }); step(0.05f);
        Check(!cg.WebFirst.HasValue && g.Bullets.Count == 0, "a click on open floor sets nothing (and fires nothing)");
        stepIn(dt, new PlayerInput { AimAt = a + wa.Normal * 12f, Fire = true }); step(0.05f);
        Check(cg.WebFirst.HasValue && cg.WebFirst.Value.At.DistanceTo(a) < 1f, "a click near a wall sets the first anchor on its face");
        stepIn(dt, new PlayerInput { AimAt = a + wa.Normal * 12f, Fire = true }); step(0.1f);
        Check(webs().Count == 0 && cg.WebPickSlot == 0, "a second click on the same wall does nothing");
        stepIn(dt, new PlayerInput { AimAt = friendPos, Ability = true }); step(0.1f);
        Check(cg.WebPickSlot < 0 && webs().Count == 0 && cme.Slots[0].Charges == 2, "the key again cancels, nothing spent");
        stepIn(dt, new PlayerInput { AimAt = friendPos, Ability = true }); step(0.05f);
        stepIn(dt, new PlayerInput { AimAt = a + wa.Normal * 12f, Fire = true }); step(0.05f);
        stepIn(dt, new PlayerInput { AimAt = b + wb.Normal * 12f, Fire = true }); step(0.2f);
        var w = webs().FirstOrDefault();
        V2 ea = V2.Zero, eb = V2.Zero;
        if (w != null) Game.WebEnds(w, out ea, out eb);
        Check(w != null && w.Owner == 2 && ea.DistanceTo(a) < 1.5f && eb.DistanceTo(b) < 1.5f && cg.WebPickSlot < 0,
            "the second click casts: a web from the first wall to the second (" + ea + " to " + eb + ")");
        Check(w != null && cg.Zones.Values.Any(z => z.Kind == ZoneKind.MendingWeb && z.Pos.DistanceTo(w.Pos) < 1f && Math.Abs(z.Aim - w.Aim) < 0.01f && Math.Abs(z.Radius - w.Radius) < 0.5f),
            "the web is on the client's screen too");

        // Building: nothing until the spider's done; then the band heals allies and slows and poisons enemies.
        var grunt = make(EnemyType.Grunt, mid + n * 5f);
        var brute = make(EnemyType.Brute, mid + u * 40f - n * 8f);
        var outside = make(EnemyType.Grunt, mid + n * 90f);
        pinned[grunt] = grunt.Pos; pinned[brute] = brute.Pos; pinned[outside] = outside.Pos;
        float build = w != null ? Game.WebBuildT(w.Radius) : 1f;
        Func<float> rate = () => { me.Hp = me.MaxHp * 0.5f; float h0 = me.Hp; step(1f); return me.Hp - h0; };
        mePos = mid - u * 30f + n * 55f;
        float aura = rate();  // (the Healer's own aura heals too: measured just off the web, by the same enemies)
        mePos = mid - u * 30f + n * 6f;  // the host player stands in the web
        Check(w != null && w.Duration - w.T < build && grunt.PoisonT <= 0f, "nothing before the spider has woven it (" + build.ToString("0.00") + " s)");
        step(Math.Max(0f, build - (w.Duration - w.T)) + 0.1f);
        float healed = rate() - aura;
        Check(healed > Game.WebHeal * 0.8f && healed < Game.WebHeal * 1.2f, "an ally in the web heals 7 HP a second (" + healed.ToString("0.0") + ")");
        Check(grunt.PoisonT > 0f && grunt.SlowMult < 0.6f && brute.PoisonT > 0f && (brute.Def.Heavy ? brute.SlowMult > 0.7f && brute.SlowMult < 0.85f : brute.SlowMult < 0.6f)
            && outside.PoisonT <= 0f && outside.SlowT <= 0f, "enemies in it are poisoned and slowed 45% (heavies half); one outside isn't touched ("
            + grunt.SlowMult.ToString("0.00") + ", " + brute.SlowMult.ToString("0.00") + (brute.Def.Heavy ? " heavy" : "") + ", " + outside.PoisonT.ToString("0.0") + "/" + outside.SlowT.ToString("0.0") + ")");
        float hpG = grunt.Hp;
        pinned[grunt] = mid + n * 150f;
        step(1f);
        Check(grunt.PoisonT > 0f && grunt.Hp < hpG - Game.WebPoisonDps * 0.5f, "the poison keeps ticking after it leaves");

        // At most three a caster: a fourth unravels the oldest. Bots cast with no pick (a web is found round the target).
        int first = w != null ? w.Id : -1;
        for (int i = 0; i < 3; i++) { friend.Slots[0].Charges = 1; hostAbility.Invoke(g, new object[] { 2, 0, mid, false }); step(0.05f); }
        Check(webs().Count(z => z.Owner == 2) == Game.WebMax && !g.Zones.ContainsKey(first), "a 4th web unravels the oldest");
        foreach (var z in webs()) g.Zones.Remove(z.Id);
        me.Slots[0].Charges = 1;
        hostAbility.Invoke(g, new object[] { 1, 0, mid + u * 5f, false });
        var botWeb = webs().FirstOrDefault(z => z.Owner == 1);
        Check(botWeb != null && botWeb.Radius * 2f <= Game.WebMaxLen + 0.5f, "with no anchors (a bot), the host strings one round the target");

    }
}
