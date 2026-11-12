using Godot;
using System;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 黑暗之门（ANdp）：在 Area 内随机位置召出 DataB(3) 只 DataA 列表里的单位（nbal / nfel）。
/// </summary>
public sealed class RuntimeDarkPortalAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    public int Count => Mathf.Max(1, (int)Data('B'));
    public float Radius => Area > 0f ? Area : 800f;
    public string[] PortalUnits => (Type?.DataTextAt(Level, 'A') ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner == null || !Owner.Alive) return false;

        string[] units = PortalUnits;
        if (units.Length == 0) return false;

        int summoned = 0;
        for (int index = 0; index < Count; index++)
        {
            string unitId = units[world.RandomIndex(units.Length)].Trim();
            if (unitId.Length < 4) continue;
            float angle = (world.RandomIndex(16) / 16f) * Mathf.Tau;
            Vector3 spot = Owner.Position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (Radius * 0.3f);
            if (world.Summon(Owner, unitId[..4], spot, 0f) != null) summoned++;
        }
        return summoned > 0;
    }

    public override bool TryAutoCast(World world) => false;
}
