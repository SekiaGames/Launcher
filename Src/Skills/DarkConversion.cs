using Godot;

namespace OpenWC3;

/// <summary>
/// 黑暗转换 ANdc（玛尔加尼斯）。对标客户端 Src/Skills/CampaignAndAuraSkills.cs 的 DarkConversion：
/// 只对 DataA 指定种族（commoner）的非英雄活物用；目标沉睡 Dur（6s）后被换成 UnitID（nzom）并消失。
/// 物编 Cast=5 是施法前摇，这里按客户端注释口径「施法即开始转换」，不再额外等 5 秒。
/// </summary>
public sealed class DarkConversion : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Unit;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public string ResultUnitId => Type?.SummonAt(Level) ?? "nzom";
    public string RequiredRace => (Type?.DataTextAt(Level, 'A') ?? "").Trim();

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null || target == null || !target.Alive) return false;
        if (target.IsHero || target.IsBuilding) return false;
        if (world.AreAllied(Owner.Player, target.Player)) return false;
        if (target.Type != null && !RaceMatches(target.Type.Race)) return false;
        return InRange(target);
    }

    private bool RaceMatches(string race)
    {
        if (string.IsNullOrEmpty(RequiredRace) || RequiredRace == "-") return true;
        return string.Equals(race, RequiredRace, System.StringComparison.OrdinalIgnoreCase);
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || target == null || !target.Alive) return false;

        for (int index = target.Buffs.Count - 1; index >= 0; index--)
        {
            if (target.Buffs[index] is DarkConversionBuff) target.Buffs.RemoveAt(index);
        }

        var buff = new DarkConversionBuff
        {
            Owner = target,
            Source = this,
            Name = Name,
            Remaining = Duration > 0f ? Duration : 6f,
            ResultUnitId = ResultUnitId,
            Caster = Owner,
        };
        buff.OnApply(world);
        target.Buffs.Add(buff);
        return true;
    }

    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, accept: other =>
            !other.IsHero && !other.IsBuilding && other.Type != null && RaceMatches(other.Type.Race));
        return target != null && world.TryCast(Owner, this, target, target.Position);
    }
}
