using Godot;

namespace Launcher;

/// <summary>
/// 烈焰风暴 AHfs（血法师）。对标 Wiki RuntimeFlameStrikeAbility：
/// 点地面 → 前 FullDamageDuration（HeroDur 2.67s）每 DataB(0.33s) 结算一次 DataA 伤害，
/// 之后每 DataD(1s) 结算一次 DataC(4) 的余烬伤害，总共 Dur(9s)，建筑乘 DataE(0.75)。
/// 不引导，施法者可以立刻走开（地面效果挂在 <see cref="IGroundSpell"/> 上推进）。
/// </summary>
public sealed class FlameStrike : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.Point;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Burst;

    public float FullDamage => Data('A');
    public float FullInterval => Data('B') > 0f ? Data('B') : 0.33f;
    public float HalfDamage => Data('C');
    public float HalfInterval => Data('D') > 0f ? Data('D') : 1f;
    public float BuildingFactor => Data('E') > 0f ? Data('E') : 0.75f;
    public float TotalDuration => Duration > 0f ? Duration : 9f;
    public float FullDuration => HeroDuration > 0f ? HeroDuration : TotalDuration;
    public float Radius => Area > 0f ? Area : 200f;

    public override bool CheckTarget(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;
        float range = CastRange > 0f ? CastRange : 800f;
        return FlatDistance(Owner.Position, point) <= range;
    }

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null) return false;

        world.AddGroundSpell(new FlameStrikeBurn
        {
            Source = Owner,
            Point = Flatten(point),
            Radius = Radius,
            TotalDuration = TotalDuration,
            FullDuration = Mathf.Min(FullDuration, TotalDuration),
            FullDamage = FullDamage,
            FullInterval = FullInterval,
            HalfDamage = HalfDamage,
            HalfInterval = HalfInterval,
            BuildingFactor = BuildingFactor,
            Timer = FullInterval,
        });
        return true;
    }

    /// <summary>AI：朝射程内最近的敌人脚下砸；半径里的敌人越多越值。</summary>
    public override bool TryAutoCast(World world)
    {
        Unit target = FindEnemyInRange(world, preferHero: true, accept: other => !other.State.MagicImmune);
        if (target == null) return false;
        return world.TryCast(Owner, this, null, AiTactics.LeadPoint(target));
    }
}
