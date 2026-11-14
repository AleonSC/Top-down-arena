namespace Arena
{
    /// Step 5 of the enemy behaviour classes (Docs/EnemyBehaviours.md): the lobbers. They keep GrenadierRange away and plant
    /// their feet while winding up (PlantMove, ShootBrains.cs). A LobFire part holds what each one throws: the Gas Grenadier's gas
    /// (WardenFaction.cs), the Honey Lobber's honey (slows), the Rock Hurler's boulder (leads you; a rubble pile:
    /// QuarryFaction.cs), the Bile Spitter's acid (leads you: LarderFaction.cs) and the Dusk Lobber's dark (Penumbral.cs).
    /// The Marshal moves and winds up like them but draws a dueling circle instead (CircleFire, CourtFaction.cs).
    public partial class Game
    {
        /// An overhand lob (GrenadierWind locks the spot as the wind-up starts, GrenadierFire throws): the ring's size and
        /// colour, whether it leads you, and the patch it leaves.
        sealed class LobFire : FirePart
        {
            public float Ring, PatchLife, PatchR;
            public Rgba Color;
            public bool Leads;
            public PatchKind Patch;

            public override void Wind(Game g, Enemy e, V2 tpos, float rateNow) => g.GrenadierWind(e, tpos, rateNow, this);
            public override void Fire(Game g, Enemy e, V2 tpos, float aim) => g.GrenadierFire(e, tpos, this);
        }

        sealed class CircleFire : FirePart
        {
            public override void Fire(Game g, Enemy e, V2 tpos, float aim) => g.MarshalCircle(e, tpos);
        }

        static void LobBrains(Brain[] t)
        {
            var move = new PlantMove(GrenadierRange);
            t[(int)EnemyType.GasGrenadier] = new Brain { Move = move, Fire = new LobFire { Ring = GrenadierGasRadius, Color = GasLobColor, Patch = PatchKind.Gas, PatchLife = GrenadierGasLife, PatchR = GrenadierGasRadius } };
            t[(int)EnemyType.HoneyLobber] = new Brain { Move = move, Fire = new LobFire { Ring = HoneyLobRadius, Color = HoneyLobColor, Patch = PatchKind.Honey, PatchLife = HoneyLobLife, PatchR = HoneyLobRadius } };
            t[(int)EnemyType.RockHurler] = new Brain { Move = move, Fire = new LobFire { Ring = HurlerLandR, Color = HurlerColor, Leads = true, Patch = PatchKind.Rubble, PatchLife = RubbleLife, PatchR = RubbleR } };
            t[(int)EnemyType.BileSpitter] = new Brain { Move = move, Fire = new LobFire { Ring = SpitPoolR, Color = BileColor, Leads = true, Patch = PatchKind.Bile, PatchLife = SpitPoolLife, PatchR = SpitPoolR } };
            t[(int)EnemyType.DuskLobber] = new Brain { Move = move, Fire = new LobFire { Ring = DuskPoolR, Color = DuskColor, Patch = PatchKind.Dusk, PatchLife = DuskLife, PatchR = DuskPoolR } };
            t[(int)EnemyType.Marshal] = new Brain { Move = move, Fire = new CircleFire() };
        }
    }
}
