using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Arena;

// The Field Hospital (FieldHospital.cs): the Healer Soldier's key 1 on the lobby's Ultimate row; in a Grid run its pick lists the door
// spots in reach, free ones only where a one-cell room fits (not behind a mystery door, not over a map node); the cast opens that cell as a
// RoomKind.Hospital room on every game, with a shrine (full health and 25% past max, once each), 6 healing orbs that come back and stay
// inside, and a gate that puts enemies out and burns their shots; nothing is built off it and it isn't counted as a room; it keeps its
// minute through the end of the wave; a map reset takes it with the map; at the end everyone inside is put out at its door and the room comes off the map with every other room's parent
// kept right; outside a Grid run it is a tent at the cursor.
static partial class Program
{
    static void FieldHospitalTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        // --- The lobby ---------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.FieldHospital);
        Check(def.Name == "Field Hospital" && def.Cooldown == 150f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Role == Role.Healer && def.Kit == Kit.Soldier && def.Capstone != null,
            "the Field Hospital is the Healer Soldier's ultimate on key 1, 150 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Soldier) == AbilityId.Lifeline && Upgrades.Ultimate(Role.Healer, Kit.Soldier, 1) == AbilityId.FieldHospital,
            "Lifeline stays the default; key 1 is the Field Hospital");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 6 }, port, false, "Medic", Role.Healer, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        host.RequestUlt(1);
        settle();
        Check(host.Lobby[1].UltId == AbilityId.FieldHospital && client.Lobby[1].Ult == 1, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting, noPicks);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.FieldHospital, "in the match the host's ultimate is the Field Hospital");

        // A Grid run: the core.
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Nodes.Clear(); gg.MysteryDoors.Clear(); gg.ExpandOpen = true; }
        Run(s, dt, 0.6f, () => cg.Expanded.Count == 4, noPicks);
        Check(g.HospOnGrid && cg.HospOnGrid, "a Grid run: the hospital opens a cell");
        var me = g.Players[1];
        var ally = g.Players[client.MyId];
        g.DevSetGod(client.MyId, true);
        var mine = new HashSet<int>();
        Action clear = () => { foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id); };
        Action<float, V2, V2> step = (secs, at, allyAt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                clear();
                g.State = MatchState.Fighting;
                me.Pos = at; me.InvulnT = 1f; ally.Pos = allyAt;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = at + new V2(0f, 200f) });
            }
        };

        // --- The pick: free spots and taken ones -------------------------------------------------------------------------
        V2 stand = new V2(1100f, -250f);  // by the core's top wall (the core: -400..2000 x -400..1400)
        me.Pos = stand;
        var spots = g.HospSpots(me);
        Check(spots.Count > 0 && spots.All(q => q.Door.CloserThan(me.Pos, Game.HospReach)), "the pick lists the door spots in reach (" + spots.Count + ")");
        var top = spots.FirstOrDefault(q => q.Door.X == 1100f && q.Door.Y == -400f);
        Check(top.Free && top.X == 800 && top.Y == -1000, "the spot on the wall above is free: a cell straight above it");
        var side = spots.First(q => q.Free && !(q.X == top.X && q.Y == top.Y));
        g.MysteryDoors.Add(new MysteryDoor { X = (short)side.Door.X, Y = (short)side.Door.Y, Vertical = side.Side >= 2, Price = 9 });
        g.Nodes.Add(new MapNode { X = (short)(top.X + 300), Y = (short)(top.Y + 300), Kind = NodeKind.Shrine });
        spots = g.HospSpots(me);
        Check(!spots.First(q => q.X == side.X && q.Y == side.Y).Free, "a spot with a mystery door on it is taken");
        Check(!spots.First(q => q.X == top.X && q.Y == top.Y).Free, "a cell holding a map node is taken");
        g.MysteryDoors.Clear(); g.Nodes.Clear();
        spots = g.HospSpots(me);
        Check(g.HospPickTarget(me, top.Door + new V2(30f, -60f)).Value.X == top.X && !g.HospPickTarget(me, top.Door + new V2(0f, 900f)).HasValue,
            "a click near the door (or in its cell) picks it; one far off picks nothing");

        // --- The cast ----------------------------------------------------------------------------------------------------
        int rooms0 = g.Expanded.Count, bought0 = Expansion.BoughtCount(g.Expanded);
        Action ready = () => { me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; };
        ready();
        me.Pos = stand;
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, top.Door, false });
        var z = g.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.FieldHospital);
        Check(z != null && g.Expanded.Count == rooms0 + 1 && g.Expanded.Last().Kind == RoomKind.Hospital && g.Expanded.Last().X == top.X && g.Expanded.Last().Y == top.Y,
            "the cast opens the cell behind the door as a hospital room");
        Check(z != null && Math.Abs(z.T - Game.HospLife) < 0.01f && z.Pos.DistanceTo(top.Mid) < 1f && Game.HospIsRoom(z), "with its zone over it for a minute");
        Check(Expansion.BoughtCount(g.Expanded) == bought0, "it isn't counted as a built room");
        Run(s, dt, 0.5f, () => cg.Expanded.Any(r => r.Kind == RoomKind.Hospital), noPicks);
        Check(cg.Expanded.Any(r => r.Kind == RoomKind.Hospital) && cg.Zones.Values.Any(q => q.Kind == ZoneKind.FieldHospital), "the other screen gets the room and the zone");
        Check(!Expansion.GridSpots(g.Expanded).Any(q => q.SY == top.Y || ((q.SX == top.X || q.SX == top.X + 600) && q.SY > top.Y && q.SY < top.Y + 600)),
            "nothing can be built off the hospital: it has no door spots");
        V2 inside = z.Pos + new V2(-120f, 120f), inside2 = z.Pos + new V2(120f, 120f);
        step(0.8f, inside, inside2);
        var orbs = g.Orbs.Values.Where(o => o.Hosp == z.Id).ToList();
        Check(orbs.Count == Game.HospOrbs && orbs.All(o => Game.InHospital(z, o.Pos)), "6 healing orbs lie inside");

        // Orbs: a hurt player inside takes one; it comes back; one outside the walls never pulls them out.
        me.Hp = me.MaxHp; ally.Hp = ally.MaxHp;
        g.DevSetGod(client.MyId, false);
        ally.Hp = ally.MaxHp * 0.3f;
        V2 outside = top.Door + new V2(0f, 60f);
        step(1.5f, inside, outside);
        Check(g.Orbs.Values.Count(o => o.Hosp == z.Id) == Game.HospOrbs && ally.Hp < ally.MaxHp * 0.35f, "a hurt player outside the walls doesn't pull its orbs out");
        float hp0 = ally.Hp;
        step(1.0f, inside, z.Pos + new V2(200f, -200f));
        Check(ally.Hp > hp0 && g.Orbs.Values.Count(o => o.Hosp == z.Id) < Game.HospOrbs, "a hurt player inside takes them");
        step(Game.HospOrbEvery + 0.3f, inside, outside);
        ally.Hp = ally.MaxHp;
        Check(g.Orbs.Values.Count(o => o.Hosp == z.Id) > 0, "they come back over time");
        g.DevSetGod(client.MyId, true);

        // The shrine: full health and 25% past max, once each.
        me.Hp = me.MaxHp * 0.5f; me.OverHp = 0f;
        step(0.2f, z.Pos, outside);
        Check(me.Hp >= me.HealCap - 0.5f && Math.Abs(me.OverHp - me.MaxHp * Game.HospOverFrac) < 0.5f && (z.Side & 2) != 0, "the shrine fills you and adds 25% past max");
        me.Hp = me.MaxHp * 0.5f; me.OverHp = 0f;
        step(0.2f, z.Pos, outside);
        Check(me.Hp < me.MaxHp * 0.6f && me.OverHp == 0f, "only once each");
        Run(s, dt, 0.3f, () => false, noPicks);
        Check(cg.Zones.Values.Any(q => q.Kind == ZoneKind.FieldHospital && (q.Side & 2) != 0), "the other screen sees who has used it");

        // The gate: an enemy that walks in is put out; an enemy shot inside burns away.
        var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, z.Pos + new V2(0f, 200f), 0f, 0, false });
        e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FrozenT = 999f; e.Pos = z.Pos + new V2(0f, 200f); mine.Add(e.Id);
        step(0.1f, inside, outside);
        Check(!Game.InHospital(z, e.Pos, e.Radius), "an enemy inside is put back out (" + e.Pos + ")");
        var bad = ((Bullet)typeof(Game).GetMethod("SpawnBullet", flags).Invoke(g, new object[] { Defs.SmallShot, z.Pos + new V2(100f, 0f), new V2(0f, 10f), 0f, 5f, 10f, 0, false, 0f })).Id;
        step(0.05f, inside, outside);
        Check(!g.Bullets.ContainsKey(bad), "an enemy shot inside burns away");

        // A piece built next to it later hangs off the core, never the hospital.
        var pl = Expansion.PlacementsFor(g.Expanded, PieceShape.Mono).Where(q => q.X == top.X - 600 && q.Y == top.Y + 600 - 600).ToList();
        if (pl.Count == 0)
            pl = Expansion.PlacementsFor(g.Expanded, PieceShape.Mono).Where(q => q.Rooms.Any(r => r.X1 == top.X || r.X == top.X + 600)).ToList();
        int hi = Expansion.HospitalAt(g.Expanded, z.Pos);
        if (pl.Count > 0)
        {
            g.Expanded.AddRange(pl[0].Rooms);
            Check(g.Expanded.Skip(hi + 1).All(r => r.Parent != hi + 1), "a piece beside it hangs off the map, not off the hospital");
        }

        // The end: a wave cleared starts the countdown; at 0 everyone inside is put out and the room goes, parents kept right.
        float t0 = z.T;
        g.State = MatchState.Intermission;
        g.FixedStep(dt, new PlayerInput { AimAt = inside });
        Check(z.T > Game.HospEject && z.T > t0 - 0.1f, "the wave ending doesn't cut it short: it keeps its minute");
        var before = g.Expanded.Where((r, i) => i != hi).ToList();
        z.T = 0.01f;
        me.Pos = inside;
        g.FixedStep(dt, new PlayerInput { AimAt = inside });
        Check(!g.Zones.ContainsKey(z.Id) && !g.Expanded.Any(r => r.Kind == RoomKind.Hospital), "time up: the zone and the room are gone");
        Check(!Game.InHospital(z, me.Pos) && me.Pos.DistanceTo(top.Door) < 200f && (bool)typeof(Game).GetMethod("OnFloor", flags).Invoke(g, new object[] { me.Pos }), "you are put out at its door (" + me.Pos + ")");
        Check(g.Orbs.Values.All(o => o.Hosp != z.Id), "its orbs go with it");
        bool parentsOk = true;
        for (int i = 0; i < g.Expanded.Count; i++)
            parentsOk &= g.Expanded[i].Parent <= i && g.Expanded[i].X == before[i].X && g.Expanded[i].Y == before[i].Y && (before[i].Parent <= hi ? g.Expanded[i].Parent == before[i].Parent : g.Expanded[i].Parent == before[i].Parent - 1);
        Check(parentsOk, "every other room keeps its place and its parent");
        Run(s, dt, 0.5f, () => !cg.Expanded.Any(r => r.Kind == RoomKind.Hospital), noPicks);
        Check(!cg.Expanded.Any(r => r.Kind == RoomKind.Hospital), "the other screen loses the room too");

        // A map reset (the map back to the hub) takes it with the map.
        ready();
        me.Pos = stand;
        g.State = MatchState.Fighting;
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, top.Door, false });
        var rz = g.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.FieldHospital);
        Check(rz != null && g.Expanded.Any(r => r.Kind == RoomKind.Hospital), "it opens again");
        g.Expanded.RemoveAll(r => r.Kind == RoomKind.Hospital);
        step(0.1f, stand, outside);
        Check(rz != null && !g.Zones.ContainsKey(rz.Id) && g.Orbs.Values.All(o => o.Hosp != rz.Id), "its room gone (a map reset): it ends at once, orbs and all");

        // --- Outside a Grid run: a tent at the cursor --------------------------------------------------------------------
        g.Diff.Set(DiffKnob.ArenaExpanding, 0f);
        g.State = MatchState.Fighting;
        Check(!g.HospOnGrid, "no Grid run: no door spots");
        me.Pos = new V2(800f, 450f);
        ready();
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, new V2(800f, 300f), false });
        var tz = g.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.FieldHospital);
        Check(tz != null && !Game.HospIsRoom(tz) && tz.Pos.DistanceTo(new V2(800f, 300f)) < 1f && tz.Radius == Game.HospTentRadius, "a tent goes up at the cursor");
        if (tz != null)
        {
            var e2 = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, tz.Pos + new V2(40f, 0f), 0f, 0, false });
            e2.Hp = e2.MaxHp = 1e6f; e2.SpawnT = 0f; e2.FrozenT = 999f; e2.Pos = tz.Pos + new V2(40f, 0f); mine.Add(e2.Id);
            step(0.1f, tz.Pos + new V2(-60f, 0f), outside);
            Check(!Game.InHospital(tz, e2.Pos, e2.Radius - 1f), "its ring keeps enemies out too");
        }
        host.Leave(""); client.Leave("");
    }
}
