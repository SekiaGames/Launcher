using Godot;

namespace Launcher;

/// <summary>战斗平台（Abtl）：把地洞标成「驻军才开火」。原版是研究/开关，这里是常驻被动。</summary>
public sealed class RuntimeBattleStationsAbility : Ability
{
    public override bool IsPassive => true;

    public override void OnLearned()
    {
        if (Owner == null) return;
        Owner.GarrisonOnly = true;
    }
}
