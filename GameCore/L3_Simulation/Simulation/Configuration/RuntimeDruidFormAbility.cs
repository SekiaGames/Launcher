using Godot;

namespace Launcher;

/// <summary>
/// 德鲁伊形态 Abrf / Arav（利爪德鲁伊 / 猛禽德鲁伊）。对标客户端 Units/Unit.TypeChange.cs：
/// 换类型时保留单位实例、生命比例与队伍，技能栏换成目标形态的 abilList；再点一次变回来。
/// 物编 DataA 是「人形单位 id」、UnitID 是「变形后单位 id」，两份数据互为对方的形态，所以不写死。
/// </summary>
public class RuntimeDruidFormAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public string NormalFormId => (Type?.DataTextAt(Level, 'A') ?? "").Trim();
    public string MorphFormId => Type?.SummonAt(Level) ?? "";

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        if (Owner.Morphed)
        {
            world.EndMorph(Owner);
            return true;
        }

        bool normal = string.Equals(Owner.Type.Id, NormalFormId, System.StringComparison.OrdinalIgnoreCase);
        UnitType form = UnitFactory.Get(normal ? MorphFormId : NormalFormId);
        return form != null && world.BeginMorph(Owner, form, 0f, disable: false);
    }

    /// <summary>AI：利爪德鲁伊见到敌人就变熊，脱离交战后再变回人形；猛禽德鲁伊保持人形放法术。</summary>
    public override bool TryAutoCast(World world)
    {
        if (Owner == null) return false;
        if (!string.Equals(Id, "Abrf", System.StringComparison.OrdinalIgnoreCase)) return false;

        if (Owner.Morphed)
        {
            if (HasEnemyNearby(world, 900f)) return false;
            return world.TryCast(Owner, this, null, Owner.Position);
        }

        if (!HasEnemyNearby(world, 700f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
