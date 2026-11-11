using Godot;

namespace Launcher;

/// <summary>
/// 专注光环 AHad（被动光环）。对标客户端 Src/Skills/DevotionAura{,Buff}.cs：
/// 给半径 Area 内友军 +DataA 护甲。光环值取「同源取最大」，每 tick 由 World.RefreshAuras 重算。
/// 物编：Area=900 DataA=1.5/3/4.5。
/// </summary>
public sealed class DevotionAura : Ability
{
    public override bool IsPassive => true;
    public override bool IsAura => true;

    public float ArmorBonus => Data('A');
    public float AuraArea => Area > 0f ? Area : 900f;
}
