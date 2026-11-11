using Godot;

namespace Launcher;

/// <summary>
/// 变身 AEme（恶魔猎手终极）。变成 UnitID(Edmm) 恶魔形态，HeroDur(60) 秒，
/// 期间额外 +DataE(500) 生命上限；照常战斗（不禁攻击、不禁施法）。
/// 物编：Cool=180 Cost=150。
/// </summary>
public sealed class Metamorphosis : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        UnitType form = UnitCatalog.Get(Type.SummonAt(Level));
        if (form == null) return false;

        return world.BeginMorph(Owner, form, EffectiveDuration, disable: false, bonusHp: Data('E'));
    }

    /// <summary>AI：身边有敌人就变身。</summary>
    public override bool TryAutoCast(World world)
    {
        if (!HasEnemyNearby(world, 800f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
