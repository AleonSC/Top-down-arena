using System;
using System.Linq;
using Arena;

// Faction relics (Docs/RelicNodes.md): 20 relics over the 10 boss factions, 1-3 each; a relic node's Rule names its relic; stats apply; saves keep them.
static partial class Program
{
    static void FactionRelicsTest(float dt)
    {
        Check(Relics.PerFaction.Length == Game.BossCycle.Length && Relics.PerFaction.Sum() == Relics.Count - Relics.Classic - 1 && Relics.PerFaction.All(c => c >= 1 && c <= 3), "each boss faction holds 1-3 relics");
        bool ok = true;
        for (int f = 0; f < 10; f++)
            for (int i = 0; i < Relics.PerFaction[f]; i++)
            {
                var r = Relics.OfFaction(f, i);
                ok &= Relics.FactionOf(r) == f && Relics.Name(r).Length > 0 && Relics.Desc(r).Length > 0;
            }
        Check(ok && Relics.FactionOf(RelicId.Backup) < 0 && Relics.FactionOf(RelicId.None) < 0, "every faction relic maps back to its faction and has a name and text");
        var st = new Stats();
        Relics.Apply(RelicId.StoneHeart, st);
        Check(st.BonusHp == 30f, "Stone Heart adds max health");
        st = new Stats(); Relics.Apply(RelicId.DuelGem, st);
        Check(st.DmgMult > 1.1f && st.DamageTaken > 1f, "Duel Gem trades damage taken for damage");
        var node = new MapNode { X = 300, Y = 300, Kind = NodeKind.Relic, Rule = (byte)((int)RelicId.BlackSun + 1) };
        Check(Relics.Held(node) == RelicId.BlackSun && Relics.ForNode(new MapNode { X = 300, Y = 300, Kind = NodeKind.Relic }) < (RelicId)Relics.Classic, "a faction node holds its relic; an old node still rolls a classic one");
        var save = RunSave.Parse(new RunSave { Wave = 6, HasExpansion = true, Seats = { new SeatSave { Relic = RelicId.MawTooth } } }.ToText());
        Check(save != null && save.Seats[0].Relic == RelicId.MawTooth, "a faction relic survives a save");
    }
}
