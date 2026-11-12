using Godot;

namespace Launcher;

/// <summary>
/// 操纵死尸 AUan（死亡骑士终极）。把 Area(900) 内最多 DataA(6) 具尸体拉起来，Dur(40) 秒无敌。
/// 口径：尸体连人带尸体一起归施法者（原版就是这样抢尸体），到期消失、不留尸、不给经验。
/// 英雄尸体不拉（原版只拉普通单位）。
/// </summary>
public sealed class AnimateDead : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public int MaxCorpses => Mathf.RoundToInt(Data('A'));

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        int limit = Mathf.Max(1, MaxCorpses);
        int raised = 0;

        foreach (Unit corpse in world.Units)
        {
            if (raised >= limit) break;
            if (corpse == null || corpse.Alive || !corpse.IsCorpse) continue;
            if (corpse.IsHero || corpse.IsBuilding) continue;
            if (Owner.DistanceTo(corpse) > Area) continue;

            corpse.Player = Owner.Player;
            corpse.ReviveFromCorpse(corpse.Position);
            corpse.State.Add(StateMod.Invulnerable, Duration);
            corpse.Summoner = Owner;
            corpse.SummonRemaining = Duration;
            raised++;
        }

        return raised > 0;
    }

    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, Area)) return false;

        int corpses = 0;
        foreach (Unit other in world.Units)
        {
            if (other == null || other.Alive || !other.IsCorpse || other.IsHero || other.IsBuilding) continue;
            if (Owner.DistanceTo(other) > Area) continue;
            corpses++;
        }

        int required = Mathf.Min(3, Mathf.Max(1, MaxCorpses));
        return corpses >= required && world.TryCast(Owner, this, null, Owner.Position);
    }
}
