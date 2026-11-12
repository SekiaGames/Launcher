using Godot;

namespace Launcher;

/// <summary>
/// 凤凰蛋 Aphx。对标 Wiki RuntimePhoenixEggBuff：凤凰生命耗尽时不是死亡，
/// 而是换成 UnitID(hpxe) 蛋形态，HeroDur(10) 秒后孵化回 hphx 并满血。
/// 蛋被打死就真的死了。技能不出现在命令卡上（原版 NativeCommandProviderEmpty）。
/// </summary>
public sealed class RuntimePhoenixEggBuff : Ability
{
    public override bool IsPassive => true;

    public string EggUnitId => Type?.SummonAt(Level) ?? "hpxe";
    public float HatchSeconds => HeroDuration > 0f ? HeroDuration : 10f;
}
