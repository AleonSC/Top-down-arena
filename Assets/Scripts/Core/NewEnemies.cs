using System.Collections.Generic;

namespace Arena
{
    /// Which regular enemy types a wave brings for the first time, and a one-line note on what to do about each, for the
    /// new-enemy chips under the between-waves box (Game/NewEnemyChips.cs, Docs/EnemyReadability.md section 4).
    public partial class Game
    {
        /// The melee rushers' unlock waves (their own list in BuildWave; the shooters and non-shooters are in WavePool).
        static readonly (EnemyType Type, int From)[] MeleeUnlocks =
        {
            (EnemyType.Swarmer, 1), (EnemyType.Spiker, 2), (EnemyType.Brute, 3), (EnemyType.Lunger, 5), (EnemyType.Buzzsaw, 5), (EnemyType.SpikeBlock, 5),
            (EnemyType.Swordsman, SwordsmanFrom), (EnemyType.ShieldSwordsman, ShieldSwordsmanFrom),
        };

        /// The regular types that join on wave n: the ones its pool has and the last wave's didn't, plus the rushers that unlock then.
        public static List<EnemyType> NewTypesAt(int n)
        {
            var res = new List<EnemyType>();
            var before = n > 1 ? WavePool(n - 1) : new List<EnemyType>();
            foreach (var t in WavePool(n))
                if (!before.Contains(t))
                    res.Add(t);
            foreach (var m in MeleeUnlocks)
                if (m.From == n)
                    res.Add(m.Type);
            return res;
        }

        /// One line on what the enemy does and how to answer it; "" for a type without one. Short enough for two lines of a chip.
        public static string EnemyNote(EnemyType t)
        {
            switch (t)
            {
                case EnemyType.Worker: return "Small and quick, weaving at you in pairs. Don't get surrounded.";
                case EnemyType.Soldier: return "Armoured and straight: no weaving. Sidestep the rush.";
                case EnemyType.CombShield: return "Wax-cell shield stops shots. Break its cells, or flank it.";
                case EnemyType.HoneyLobber: return "Lobs honey at the red ring. Leave the spot; the puddle slows you.";
                case EnemyType.Stinger: return "Charges a venom lance, then fires one dart that stuns. Strafe across the line.";
                case EnemyType.Needler: return "Locks on, then fires needles straight at you. Keep moving, rush it as it reloads.";
                case EnemyType.Bumble: return "A huge, slow rusher with a heavy landing. Back off and keep firing.";
                case EnemyType.Hornet: return "Circles, shows a red lane, then darts along it. Leave the lane.";
                case EnemyType.Grunt: return "Shoots a three-shot fan. Weave between the shots.";
                case EnemyType.Swarmer: return "Runs at you in packs and bites. Don't get surrounded.";
                case EnemyType.Wisp: return "Soul flames that drift at you in threes. Thin them early.";
                case EnemyType.Acolyte: return "Raises a sickle over a pale cone. Step back out of it.";
                case EnemyType.BoundWraith: return "A pair joined by a tether that hurts. Kill both, or one comes back.";
                case EnemyType.Lamplighter: return "Gold lines show its volley: three lines, then a cross. Step off them.";
                case EnemyType.Mourner: return "Weeps a twin spiral, then rests. Push in while it rests.";
                case EnemyType.Burster: return "Fires a ring of shots. Find the gap and step through.";
                case EnemyType.Spiker: return "Rushes in spiked pairs. Keep moving, don't stand still.";
                case EnemyType.Sniper: return "One fast aimed shot. Watch its aim line, then move.";
                case EnemyType.SecurityGuard: return "Locks on, then fires tracers straight at you. Keep moving, rush it as it reloads.";
                case EnemyType.TaserGuard: return "Charges, then fires a white bolt that stuns. Strafe or parry it.";
                case EnemyType.Brute: case EnemyType.Thug: case EnemyType.Gravedigger: return "A slow, heavy rusher. Back off and keep firing.";
                case EnemyType.Spinner: return "Sprays spirals in bursts. Wait out each burst.";
                case EnemyType.Lunger: case EnemyType.Shiv: return "Winds up, then leaps in a line. Sidestep it.";
                case EnemyType.ScytheWraith: return "Marks a long line, then reaps along it. Leave the line sideways.";
                case EnemyType.Buzzsaw: return "A spinning saw that hurts to touch. Keep your distance.";
                case EnemyType.SpikeBlock: return "Lunges along a lane after a wind-up. Dodge, then hit it.";
                case EnemyType.Charger: return "Winds up, dashes, then bursts in a ring. Leave the lane.";
                case EnemyType.Splitter: return "Fires a pair of shots and breaks into Shards when it dies.";
                case EnemyType.Mortar: return "Lobs bombs at where you stand. Move off the ring.";
                case EnemyType.Shielder: return "Five-shot fan behind a shield. Go round it: it turns slowly.";
                case EnemyType.Medic: return "Heals the enemies around it. Kill it first.";
                case EnemyType.Pauser: return "Its shots hang in the air, then rush you. Keep moving.";
                case EnemyType.FuseRunner: return "Runs in and blows up after a short fuse. Shoot it or back off.";
                case EnemyType.Phantom: case EnemyType.Shade: return "Blinks near you and fires a ring. Watch the ring on the floor.";
                case EnemyType.Hookman: return "Hooks you and yanks you in. Stay out of its harpoon line.";
                case EnemyType.Sapper: return "Lays tripwire pods. Shoot a pod before it arms.";
                case EnemyType.BeamSentry: return "Plants, then sweeps a beam. Get out of its line.";
                case EnemyType.Leader: return "Aims two shots where you will be. Strafe, don't run straight.";
                case EnemyType.Hexer: return "Curving shots from both sides. Move into the gap.";
                case EnemyType.Stitcher: return "Fires orbs joined by a thread. Don't cross the thread.";
                case EnemyType.Scorcher: return "Leaves burning embers on the floor. Don't stand in them.";
                case EnemyType.Juggernaut: return "A ring of big, slow shots. Step through the gaps.";
                case EnemyType.Swordsman: return "Raises its sword, then cuts the cone on the floor. Step out of it.";
                case EnemyType.ShieldSwordsman: return "Its shield stops shots from the front. Shoot it after it cuts.";
                case EnemyType.Reaver: return "Winds up a cone swipe, then is open. Hit it after.";
                case EnemyType.Nest: return "Spawns Shards every few seconds. Kill it to stop them.";
                case EnemyType.Warder: return "Holds shield bubbles over other enemies. Kill it first.";
            }
            return "";
        }
    }
}
