using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 研究完成就把自己换成升级后的单位（Sbsk 猎头者→狂暴战士、Srtt 蒸汽机车→升级机车）。
/// 对标客户端把这两个码做成挂在单位身上的技能：研究一完成，场上和之后造出来的同类单位都换型。
/// </summary>
public sealed class UnitTypeUpgrade : Ability
{
    private static readonly Dictionary<string, string> ResearchByAbility = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Sbsk"] = "Robk",
        ["Srtt"] = "Rhrt",
    };

    public override bool IsPassive => true;

    public string UpgradedTypeId => Type?.SummonAt(Level) ?? "";
    public string ResearchId => ResearchByAbility.TryGetValue(Id, out string id) ? id : "";

    private float _timer = 1f;

    public override void OnWorldTick(World world, float delta)
    {
        if (world == null || Owner == null || !Owner.Alive || Owner.Type == null) return;
        if (ResearchId.Length == 0) return;
        if (string.Equals(Owner.Type.Id, UpgradedTypeId, StringComparison.OrdinalIgnoreCase)) return;

        _timer -= delta;
        if (_timer > 0f) return;
        _timer = 1f;

        if (world.ResearchLevel(Owner.Player, ResearchId) <= 0) return;

        UnitType upgraded = UnitFactory.Get(UpgradedTypeId);
        if (upgraded == null) return;
        world.ReplaceType(Owner, upgraded);
    }
}
