using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Deployable Cover (DeployableCover.cs), the Tank Soldier's 4th Space ability: the table (two charges, 12 s), the pick reaching the
// match, the bracket planted 64 px ahead along the aim, solid only after 0.4 s, enemy shots stopping on its plates and wearing it,
// shots from behind its arms too, enemies kept off it, a 4th folding away the oldest, breaking at 0 health, replicated (Aim and
// health left), folding when the wave ends, and mastery (600 health, 25% quicker charges).
static partial class Program
{
    static void DeployableCoverTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);

        var def = Upgrades.Ability(AbilityId.DeployableCover);
        Check(def.Name == "Deployable Cover" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Soldier && def.Role == Role.Tank && def.Cooldown == 12f && def.ExtraCharges == 1,
            "Deployable Cover: a 12 s, two-charge Tank Soldier Space ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Tank, Kit.Soldier) == 4 && Upgrades.RoleAbility(Role.Tank, Kit.Soldier, 3) == AbilityId.DeployableCover
            && Upgrades.RoleAbility(Role.Tank, Kit.Soldier, 2) == AbilityId.Bulldoze, "the Tank Soldier's 4th key is Deployable Cover; its first three are unchanged");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Tank", Role.Tank, 0, 3, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[0].Id == AbilityId.DeployableCover && me.Slots[0].MaxCharges == 2 && client.Game.Players[1].Slots[0].Id == AbilityId.DeployableCover,
            "the 4th pick reaches the run on Space, with two charges");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        V2 mePos = new V2(400f, 450f), aimAt = new V2(900f, 450f);
        var spots = new Dictionary<Enemy, V2>();
        Func<EnemyType, V2, bool, Enemy> make = (type, at, pinned) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f;
            if (pinned) spots[e] = at;
            return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var kv in spots) { kv.Key.Pos = kv.Value; kv.Key.FireT = 99f; }
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                me.Pos = mePos;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aimAt });
            }
        };
        Action reset = () =>
        {
            g.Enemies.Clear(); spots.Clear(); g.Bullets.Clear(); g.Effects.Clear(); g.Zones.Clear();
            me.Pos = mePos; me.Slots[0].Charges = 2; me.Slots[0].Timer = 0f;
            me.MasteryXp.Remove(AbilityId.DeployableCover);
            make(EnemyType.Grunt, new V2(1500f, 900f), true);  // out of the way, so the wave goes on
        };
        Func<V2, bool> cast = at => { me.Slots[0].Charges = Math.Max(1, me.Slots[0].Charges); int n = g.Zones.Count; hostAbility.Invoke(g, new object[] { 1, 0, at, false }); return g.Zones.Count > n; };
        Func<List<Zone>> covers = () => g.Zones.Values.Where(z => z.Kind == ZoneKind.Cover && z.T > 0f).OrderBy(z => z.Id).ToList();
        Action<V2, V2, int> volley = (from, vel, n) => { for (int i = 0; i < n; i++) shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(i * 8f, 0f), vel, 0f, 6f, 10f, 0, false, 0f }); };

        // The cast: 64 px ahead along the aim, facing it, 400 health; two charges.
        reset();
        me.Slots[0].Charges = 2;
        hostAbility.Invoke(g, new object[] { 1, 0, aimAt, false });
        var c0 = covers().FirstOrDefault();
        Check(c0 != null && c0.Pos.DistanceTo(mePos + new V2(Game.CoverAhead, 0f)) < 1f && Math.Abs(c0.Aim) < 0.01f && c0.BlockHp == Game.CoverHp,
            "the cover lands 64 px ahead along your aim, facing it, with 400 health");
        Check(me.Slots[0].Charges == 1, "it spends one of two charges");

        // Not solid while it's still being thrown.
        Check(!Game.CoverUp(c0), "it isn't solid the moment it's thrown");
        volley(c0.Pos + new V2(60f, 0f), new V2(-600f, 0f), 1);
        step(0.12f);
        Check(c0.BlockHp == Game.CoverHp, "shots pass while it's still landing");
        step(0.4f);
        Check(Game.CoverUp(c0), "solid after 0.4 s");

        // Enemy shots stop on it and wear it.
        g.Bullets.Clear();
        float scale = g.EnemyDamageScale(Math.Max(1, g.Wave));
        volley(c0.Pos + new V2(120f, 0f), new V2(-400f, 0f), 4);
        step(0.6f);
        Check(!g.Bullets.Values.Any(b => b.Owner == 0), "every enemy shot that reaches the plates stops there");
        Console.WriteLine("  cover: 4 shots of 10, health {0:0.0} of 400 (scale {1:0.00})", c0.BlockHp, scale);
        Check(Math.Abs(Game.CoverHp - c0.BlockHp - 40f * scale) < 0.5f, "each shot wears it by what it would have hit for");
        Check(g.Effects.Any(e => e.Kind == FxKind.CoverHit), "each hit is sent as a look");
        var cc = client.Game.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.Cover);
        Check(cc != null && Math.Abs(cc.Aim - c0.Aim) < 0.01f && Math.Abs(cc.GuardFrac - c0.BlockHp / Game.CoverHp) < 0.01f && Math.Abs(client.Game.CoverFrac(cc) - c0.BlockHp / Game.CoverHp) < 0.01f,
            "other screens see the cover, its facing and its health left");

        // An arm stops a shot from the side; a shot that misses it goes on; your own shots pass.
        g.Bullets.Clear();
        float before = c0.BlockHp;
        volley(c0.Pos + new V2(-18f, -150f), new V2(0f, 500f), 1);  // down onto the top arm
        step(0.5f);
        Check(c0.BlockHp < before, "the arms stop shots too");
        g.Bullets.Clear();
        before = c0.BlockHp;
        volley(c0.Pos + new V2(200f, 120f), new V2(-500f, 0f), 1);  // well below it
        step(0.3f);
        Check(c0.BlockHp == before && g.Bullets.Values.Any(b => b.Owner == 0), "a shot that misses the bracket flies on");
        g.Bullets.Clear();
        shoot.Invoke(g, new object[] { Defs.TankShot, mePos + new V2(20f, 0f), new V2(600f, 0f), 0f, 6f, 10f, 1, false, 0f });
        step(0.2f);
        Check(c0.BlockHp == before && g.Bullets.Values.Any(b => b.Owner == 1 && b.Pos.X > c0.Pos.X + 20f), "your own shots pass through");
        g.Bullets.Clear();

        // Enemies can't walk through it.
        var walker = make(EnemyType.Grunt, c0.Pos + new V2(2f, 10f), false);
        step(0.05f);
        Check(!Game.CoverTouches(c0, walker.Pos, walker.Radius - 1f), "an enemy is pushed out of the plates");

        // Up to three; a fourth folds away the oldest.
        g.Enemies.Remove(walker.Id);
        cast(new V2(900f, 200f));
        cast(new V2(900f, 700f));
        var three = covers();
        Check(three.Count == 3, "three can stand at once");
        cast(new V2(400f, 900f));
        var after = covers();
        Check(after.Count == 3 && !after.Any(z => z.Id == c0.Id) && g.Effects.Any(e => e.Kind == FxKind.CoverFold), "a fourth folds away the oldest");
        step(0.1f);
        Check(!g.Zones.ContainsKey(c0.Id), "the folded one is gone");

        // Breaking at 0 health.
        var tgt = after[0];
        step(0.5f);
        tgt.BlockHp = 15f * scale;
        g.Effects.Clear();
        volley(tgt.Pos + V2.FromAngle(tgt.Aim) * 150f, V2.FromAngle(tgt.Aim) * -500f, 1);
        volley(tgt.Pos + V2.FromAngle(tgt.Aim) * 190f, V2.FromAngle(tgt.Aim) * -500f, 1);
        step(0.6f);
        Check(!g.Zones.ContainsKey(tgt.Id) && g.Effects.Any(e => e.Kind == FxKind.CoverBreak), "at 0 health it breaks apart");

        // Mastered: 600 health and charges back 25% sooner.
        reset();
        me.MasteryXp[AbilityId.DeployableCover] = 1e9f;
        cast(aimAt);
        var m0 = covers().FirstOrDefault();
        Check(m0 != null && m0.BlockHp == Game.CoverHpMastered, "mastered: 600 health");
        float plain = Game.CoverCooldown * me.Stats.CdMult * Game.MasteryCooldown(me, AbilityId.DeployableCover);
        Check(Math.Abs(g.Cooldown(me, 0) - plain * Game.CoverCdMastered) < 0.01f, "mastered: each charge comes back 25% sooner");

        // Reinforced Plating (Rare, two stacks): 40% more health and one more standing.
        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Reinforced Plating");
        Check(card != null && card.Rarity == Rarity.Rare && card.RequiresAbility == AbilityId.DeployableCover && card.MaxStacks == 2, "Reinforced Plating: a Rare card for Deployable Cover, two stacks");
        reset();
        var st = new Stats();
        card.Apply(st);
        me.Stats.CoverHpMult = st.CoverHpMult; me.Stats.CoverExtra = st.CoverExtra;
        for (int k = 0; k < 5; k++) { cast(new V2(900f, 100f + k * 180f)); step(0.1f); }
        var plated = covers();
        Check(plated.Count == 4 && Game.CoverCap(me) == 4, "with it, four stand at once (a fifth folds the oldest)");
        Check(plated.All(z => Math.Abs(z.BlockHp - Game.CoverHp * 1.4f) < 0.5f), "and each takes 560 damage");
        me.Stats.CoverHpMult = 1f; me.Stats.CoverExtra = 0;

        // The fight ends: they fold away.
        me.MasteryXp.Remove(AbilityId.DeployableCover);
        step(0.5f);
        cast(new V2(900f, 200f));
        step(0.5f);
        int standing = covers().Count;
        Check(standing >= 2, "(covers standing before the wave ends)");
        g.Effects.Clear();
        typeof(Game).GetMethod("WaveCleared", flags).Invoke(g, null);
        Check(covers().Count == 0 && g.Effects.Count(e => e.Kind == FxKind.CoverFold) == standing, "they fold away when the wave is cleared");

        host.Leave(""); client.Leave("");
    }
}
