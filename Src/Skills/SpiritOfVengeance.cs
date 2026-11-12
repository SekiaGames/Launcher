using Godot;

namespace Launcher;

/// <summary>
/// 复仇之魂 AEsv（守望者终极）。阵亡时召出 UnitID（espv）代替自己继续战斗 Dur（180s），
/// 本人照常死亡；技能自身进 Cool（180s）冷却。物编 Targ=自身、无施法流程。
/// </summary>
public sealed class SpiritOfVengeance : Ability
{
    public override bool IsPassive => true;

    public string SpiritUnitId => Type?.SummonAt(Level) ?? "espv";
    public float Lifetime => Duration > 0f ? Duration : 180f;

    /// <summary>死亡触发：在世界里就地召出复仇之魂（不走施法，不扣法力）。</summary>
    public bool SummonSpirit(World world, Unit hero)
    {
        if (world == null || hero == null) return false;
        return world.Summon(hero, SpiritUnitId, hero.Position, Lifetime) != null;
    }
}