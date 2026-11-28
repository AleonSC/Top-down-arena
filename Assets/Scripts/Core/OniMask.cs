using System;

namespace Arena
{
    /// Oni Mask: the DPS Ninja's fourth ultimate (key 3 of the lobby's Ultimate row; Death Lotus stays the default), from its
    /// mockup (claude.ai artifact "Oni Mask"). A red demon mask drops onto your face (OmCastT s, can't be hit) and for OmTime s:
    ///   - you move OmMove times as fast (OmSpeed, in Player.Speed),
    ///   - every hit you land is OmDmg times as hard (OniDmgMult, in HurtEnemy),
    ///   - your max health is OmHp times as high (OmHpMult, in Recalc), the gain given as health on the cast.
    /// Each enemy you kill adds OmPerKill s (OmPerKillMastered mastered), a heavy, elite or boss OmPerHeavy s, until the mask has
    /// lasted OmMax s in all (OmMaxMastered mastered). It ends on time, on going down, at the wave's end or on a slot change;
    /// then your max health drops back and your health is kept up to it (never below 1).
    /// Replicated: Player.OmT (time left; 0 = off) and Player.OmLen (how long it will have lasted, kills included); the time
    /// since the cast is OmLen - OmT. Moments are FxKind.OniMask (Radius = OmFx*). Drawn by Game/OniMaskLook.cs.
    public partial class Game
    {
        public const float OmTime = 10f, OmCastT = 0.4f, OmPerKill = 0.5f, OmPerKillMastered = 0.75f, OmPerHeavy = 2f;
        public const float OmMax = 25f, OmMaxMastered = 30f;
        public const float OmMove = 1.3f, OmDmg = 1.4f, OmHp = 1.5f;
        // FxKind.OniMask's Radius: which moment
        public const int OmFxCast = 0, OmFxKill = 1, OmFxEnd = 2;
        public const float OmFxCastT = 0.7f, OmFxKillT = 0.6f, OmFxEndT = 0.7f;

        public static readonly Rgba OmRed = new Rgba(0.88f, 0.19f, 0.23f), OmDeep = new Rgba(0.43f, 0.05f, 0.08f), OmGold = new Rgba(1f, 0.8f, 0.35f);

        /// Oni Mask's entry: key 3 of the DPS Ninja's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] OniUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.OniMask, Kit = Kit.Ninja, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Oni Mask", Cooldown = 75f,
                Desc = "Put on a demon mask for 10 s: 30% faster, 40% more damage and 50% more max health. Each kill adds 0.5 s (a heavy 2 s), up to 25 s.",
                Capstone = "0.75 s a kill, up to 30 s.",
                Bot = b => b.NearEnemies >= 3,
            },
        };

        /// The mask is on (from the cast, its drop included).
        public static bool OmOn(Player p) => p.OmT > 0f && !p.Downed;

        /// Seconds since the cast.
        public static float OmAge(Player p) => p.OmLen - p.OmT;

        public static float OmSpeed(Player p) => OmOn(p) ? OmMove : 1f;
        public static float OmHpMult(Player p) => OmOn(p) ? OmHp : 1f;

        /// How much harder a hit from `attacker` lands (HurtEnemy).
        float OniDmgMult(int attacker)
        {
            Player p;
            return attacker > 0 && Players.TryGetValue(attacker, out p) && OmOn(p) ? OmDmg : 1f;
        }

        /// Max health from the mask's state, on every game: a gain comes as health, a loss keeps health up to the new max.
        void OmRefreshHp(Player p)
        {
            float max = (p.BaseHp + p.Stats.BonusHp) * OmHpMult(p);
            if (max > p.MaxHp && !p.Downed)
                p.Hp += max - p.MaxHp;
            p.MaxHp = max;
            if (p.Hp > max)
                p.Hp = Math.Max(1f, max);
        }

        void CastOniMask(Player p, bool mastered)
        {
            if (p.OmT > 0f)
                return;
            p.OmT = p.OmLen = OmTime;
            p.OmCap = mastered;
            p.OmKills = 0;
            p.InvulnT = Math.Max(p.InvulnT, OmCastT);
            p.UltT = p.OmT;
            OmRefreshHp(p);
            BroadcastFx(FxKind.OniMask, p.Pos, p.Pos, OmRed, OmFxCast, OmFxCastT, Sfx.DogmaRoar);
        }

        void EndOniMask(Player p)
        {
            if (p.OmT <= 0f)
                return;
            p.OmT = p.OmLen = 0f;
            OmRefreshHp(p);
            if (p.Slots[Upgrades.UltSlot].Id == AbilityId.OniMask)
                p.UltT = 0f;
            BroadcastFx(FxKind.OniMask, p.Pos, p.Pos, OmRed, OmFxEnd, OmFxEndT, Sfx.NinjaVanish);
        }

        /// Host, every tick: the mask's time running out.
        void TickOniMask(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.OmT <= 0f)
                    continue;
                if (p.Downed || !Battle || p.Slots[Upgrades.UltSlot].Id != AbilityId.OniMask)
                {
                    EndOniMask(p);
                    continue;
                }
                p.OmT -= dt;
                p.UltT = Math.Max(0f, p.OmT);
                if (p.OmT <= 0f)
                {
                    p.OmT = 0.0001f;  // (so EndOniMask runs its end)
                    EndOniMask(p);
                }
            }
        }

        /// Host, from EnemyDied: a kill of yours while masked adds time, up to the cap.
        void OniKill(Player killer, Enemy e)
        {
            if (killer == null || !OmOn(killer) || e.Def.Xp <= 0)
                return;
            float add = e.Def.IsBoss || e.Def.Heavy || e.Elite ? OmPerHeavy : killer.OmCap ? OmPerKillMastered : OmPerKill;
            float room = (killer.OmCap ? OmMaxMastered : OmMax) - killer.OmLen;
            add = Math.Max(0f, Math.Min(add, room));
            killer.OmKills++;
            killer.OmAdded += add;
            killer.OmT += add;
            killer.OmLen += add;
            killer.UltT = killer.OmT;
            BroadcastFx(FxKind.OniMask, killer.Pos, e.Pos, add > 0f ? OmGold : OmRed, OmFxKill, OmFxKillT, Sfx.None);
        }

        // --- Network: the player's Oni Mask fields (SyncPlayer) ---

        void SyncOniMask(NetSync s, Player p)
        {
            float was = p.OmT;
            s.Float(ref p.OmT);
            if (p.OmT > 0f)
                s.Float(ref p.OmLen);
            else
                p.OmLen = 0f;
            if (s.Reading && (was > 0f) != (p.OmT > 0f))
                p.MaxHp = (p.BaseHp + p.Stats.BonusHp) * OmHpMult(p);  // (the client's max health follows the mask; health comes in the snapshot)
        }
    }
}
