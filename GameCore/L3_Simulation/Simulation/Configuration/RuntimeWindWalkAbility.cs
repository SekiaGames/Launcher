using Godot;

namespace OpenWC3.Game;

/// <summary>
/// 疾风步 AOwk（剑圣）。对标 Wiki RuntimeWindWalkAbility：
/// DataA(0.6s) 淡入隐身 → Dur(20/35/50) 秒隐形并 +DataB(10%/40%/70%) 移速；
/// 期间第一次出手破隐，这一击额外打 DataC(40/70/100)。
/// </summary>
public sealed class RuntimeWindWalkAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public float FadeSeconds => Data('A');
    public float MoveSpeedFactor => Data('B');
    public float BackstabDamage => Data('C');

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        for (int index = Owner.Buffs.Count - 1; index >= 0; index--)
        {
            if (Owner.Buffs[index] is not WindWalkBuff) continue;
            Owner.Buffs[index].OnRemove(world);
            Owner.Buffs.RemoveAt(index);
        }

        var buff = new WindWalkBuff
        {
            Owner = Owner,
            SourceAbility = this,
            Name = Name,
            Remaining = EffectiveDuration > 0f ? EffectiveDuration : 20f,
            FadeSeconds = FadeSeconds,
            MoveSpeedFactor = MoveSpeedFactor,
            BackstabDamage = BackstabDamage,
        };
        buff.OnApply(world);
        Owner.Buffs.Add(buff);
        return true;
    }

    /// <summary>AI：身边有人、又还没隐身时用（用来贴脸，不是拿来逃命）。</summary>
    public override bool TryAutoCast(World world)
    {
        if (Owner == null || Owner.State.Invisible) return false;
        if (!HasEnemyNearby(world, 1200f)) return false;
        return world.TryCast(Owner, this, null, Owner.Position);
    }
}
