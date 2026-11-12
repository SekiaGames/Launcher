using Godot;

namespace OpenWC3.Game;

/// <summary>
/// 分离（Adec）。对标客户端 RuntimeDecoupleAbility：DataA / DataB 是要拆出来的两个单位，
/// 角鹰兽骑士按当前血量比例拆回弓箭手与角鹰兽。
/// </summary>
public sealed class RuntimeDecoupleAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Utility;

    public string FirstType => (Type?.DataTextAt(Level, 'A') ?? "").Trim();
    public string SecondType => (Type?.DataTextAt(Level, 'B') ?? "").Trim();

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;
        if (UnitFactory.Get(FirstType) == null || UnitFactory.Get(SecondType) == null) return false;

        float ratio = Mathf.Clamp(Owner.Hp / Mathf.Max(1f, Owner.MaxHp), 0.05f, 1f);
        Vector3 spot = Owner.Position;
        float facing = Owner.Facing;

        Unit first = Split(world, Owner, FirstType, spot, facing, ratio);
        Unit second = Split(world, Owner, SecondType, spot, facing, ratio);
        if (first == null && second == null) return false;

        world.RemoveUnit(Owner);
        return true;
    }

    public override bool TryAutoCast(World world) => false;

    private static Unit Split(World world, Unit owner, string typeId, Vector3 spot, float facing, float ratio)
    {
        Unit unit = world.CreateFor(owner, typeId, spot);
        if (unit == null) return null;

        unit.Facing = facing;
        unit.Hp = Mathf.Clamp(unit.MaxHp * ratio, 1f, unit.MaxHp);
        unit.View?.Root.Position = new Vector3(spot.X, unit.Position.Y, spot.Z);

        Ability couple = unit.FindAbility("Aco2") ?? unit.FindAbility("Aco3");
        if (couple != null) couple.CooldownRemaining = couple.Cooldown;
        return unit;
    }
}
