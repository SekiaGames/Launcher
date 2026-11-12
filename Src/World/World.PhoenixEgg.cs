using Godot;
using System;
using System.Collections.Generic;

using OpenWC3.Game;
namespace OpenWC3;

/// <summary>
/// 凤凰蛋计时。对标 Wiki RuntimePhoenixEggBuff：凤凰生命耗尽时换成蛋形态并接管生命值，
/// HeroDur 秒后孵化回凤凰。蛋期间单位不死亡，所以不给对面经验、不掉落。
/// </summary>
public sealed partial class World
{
    private sealed class PendingPhoenixEgg
    {
        public Unit Unit;
        public UnitType Phoenix;
        public float Remaining;
    }

    private readonly List<PendingPhoenixEgg> _pendingPhoenixEggs = new();

    public int PendingPhoenixEggCount => _pendingPhoenixEggs.Count;

    /// <summary>生命耗尽时调用：有 Aphx 且当前不是蛋就换成蛋形态，返回 true 表示这次不死。</summary>
    public bool TryPhoenixEgg(Unit unit)
    {
        if (unit == null || !unit.Alive || unit.Type == null) return false;

        RuntimePhoenixEggBuff egg = null;
        foreach (Ability ability in unit.Abilities)
        {
            if (ability is RuntimePhoenixEggBuff found && found.Learned)
            {
                egg = found;
                break;
            }
        }
        if (egg == null) return false;

        UnitType eggType = UnitFactory.Get(egg.EggUnitId);
        if (eggType == null) return false;
        if (string.Equals(unit.Type.Id, eggType.Id, StringComparison.OrdinalIgnoreCase)) return false;

        UnitType phoenixType = unit.Type;
        unit.Type = eggType;
        unit.MaxHp = Mathf.Max(1f, eggType.Hp);
        unit.Hp = unit.MaxHp;
        unit.CancelAttack();
        unit.Target = null;
        unit.MoveTarget = null;
        unit.Path = null;

        _pendingPhoenixEggs.Add(new PendingPhoenixEgg
        {
            Unit = unit,
            Phoenix = phoenixType,
            Remaining = Mathf.Max(World.FrameSeconds, egg.HatchSeconds),
        });

        UnitMorphed?.Invoke(unit);
        GD.Print($"[PHOENIX] P{unit.Player} 凤凰变蛋 {unit.Hp:0}/{unit.MaxHp:0}，{egg.HatchSeconds:0}s 后孵化");
        return true;
    }

    public void TickPhoenixEgg()
    {
        for (int index = _pendingPhoenixEggs.Count - 1; index >= 0; index--)
        {
            PendingPhoenixEgg pending = _pendingPhoenixEggs[index];
            pending.Remaining -= FrameSeconds;
            if (pending.Remaining > 0f) continue;

            _pendingPhoenixEggs.RemoveAt(index);
            Unit unit = pending.Unit;
            if (unit == null || !unit.Alive || pending.Phoenix == null) continue;

            unit.Type = pending.Phoenix;
            unit.MaxHp = Mathf.Max(1f, pending.Phoenix.Hp);
            unit.Hp = unit.MaxHp;
            UnitMorphed?.Invoke(unit);
            GD.Print($"[PHOENIX] P{unit.Player} 凤凰孵化 {unit.Hp:0}/{unit.MaxHp:0}");
        }
    }
}