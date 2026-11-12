using Godot;
using System.Collections.Generic;

namespace Launcher;

public sealed class RuntimeSpiritLinkBuff : Buff
{
    public SpiritLinkGroup Group;

    public override bool IsHarmful => false;

    public override void ModifyIncomingDamage(World world, DamageData info)
    {
        if (world == null || info == null || info.Amount <= 0f || Group?.Members == null) return;

        var receivers = new List<Unit>();
        foreach (Unit member in Group.Members)
        {
            if (member == null || member == Owner || !member.Alive) continue;
            receivers.Add(member);
        }
        if (receivers.Count == 0) return;

        float share = info.Amount * Group.Ratio / receivers.Count;
        info.Amount -= share * receivers.Count;

        foreach (Unit receiver in receivers)
        {
            receiver.Hp -= share;
            receiver.View?.SetHp(receiver.Hp / Mathf.Max(1f, receiver.MaxHp), receiver.Alive);
            if (receiver.Hp <= 0f) world.KillUnit(receiver, info.Source);
        }
    }
}
