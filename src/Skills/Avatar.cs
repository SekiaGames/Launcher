using Godot;

namespace Launcher;

/// <summary>
/// 天神下凡 AHav。对标客户端 Src/Skills/Avatar.cs + AvatarBuff：
/// 自身 Duration 秒内 +DataA 护甲、+DataB 生命、+DataC 攻击，并魔法免疫。
/// 物编（1 级）：DataA=5 DataB=500 DataC=20 Dur=60 Cool=180 Cost=150。
/// </summary>
public sealed class Avatar : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;

    public float ArmorBonus => Data('A');
    public float HitPointBonus => Data('B');
    public float DamageBonus => Data('C');

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (Owner == null) return false;

        var buff = new AvatarBuff
        {
            Owner = Owner,
            Source = this,
            Name = Name,
            Remaining = Duration > 0f ? Duration : 60f,
            ArmorBonus = ArmorBonus,
            HitPointBonus = HitPointBonus,
            DamageBonus = DamageBonus,
        };
        Owner.Buffs.Add(buff);
        buff.OnApply(world);
        return true;
    }
}
