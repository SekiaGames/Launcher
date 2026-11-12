using Godot;
using System.Collections.Generic;

namespace Launcher;

/// <summary>
/// 野怪、古树与召唤物自带的技能（客户端 L3_Simulation/Configuration/Runtime*Ability 的同款实现）：
/// 吃树回血、狂暴、缓慢光环、永久献祭、龙卷风伤害。都不需要玩家下指令，全部走被动 tick。
/// </summary>
public sealed class EatTree : Ability
{
    /// <summary>物编 Rng 只是「够得着」，实际按这个接近半径找树。</summary>
    private const float ReachPadding = 160f;

    public override bool IsPassive => true;

    private float _scanTimer;
    private float _restoreTimer;

    public override void OnWorldTick(World world, float delta)
    {
        if (Owner == null || !Owner.Alive || world == null) return;

        if (_restoreTimer > 0f) _restoreTimer -= delta;
        _scanTimer -= delta;
        if (_scanTimer > 0f) return;
        _scanTimer = 2f;
        if (_restoreTimer > 0f) return;

        // 原版是玩家主动点树；这里退化成「血少了就吃最近的树」，免得古树站着不动永远不吃
        if (Owner.Hp > Owner.MaxHp * 0.7f) return;

        float total = Data('C');
        float seconds = Duration > 0f ? Duration : 30f;
        if (total <= 0f) return;

        TreeState tree = world.TakeNearestTree(Owner.Position, CastRange + ReachPadding);
        if (tree == null) return;

        world.ConsumeTree(tree);
        world.ApplyBuff(Owner, new HealOverTimeBuff { AmountPerTick = total / seconds, Interval = 1f }, seconds);
        _restoreTimer = seconds + 10f;
    }
}
