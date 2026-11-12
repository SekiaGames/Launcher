using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

/// <summary>
/// 地精修补匠。对标客户端 RuntimeRoboGoblinAbility / RuntimeEngineeringUpgradeAbility /
/// RuntimeDemolishAbility（L3_Simulation/Simulation/Configuration），数值全部读物编：
/// ANrg 机器人地精（双向形态 + 力量/护甲）、ANeg 工程升级（移速/攻击 + 技能等级替换）、
/// ANde 粉碎（对建筑倍率）、ANsy 口袋工厂（召工厂并定时出兵）。
/// </summary>
public sealed class RuntimeRoboGoblinAbility : Ability
{
    public override AbilityTargetMode TargetMode => AbilityTargetMode.None;
    public override AbilityCastPriority AiCastPriority => AbilityCastPriority.Support;

    /// <summary>DataA 是人类形态、UnitID 是机器人形态（ANrg 系列四码各自覆盖）。</summary>
    public string HumanFormId => (Type?.DataTextAt(Level, 'A') ?? "").Trim();
    public string RobotFormId => Type?.SummonAt(Level) ?? "Nrob";

    public int StrengthBonus => Mathf.RoundToInt(Data('E'));
    public float ArmorBonus => Data('F');

    private int _appliedStrength;
    private float _appliedArmor;

    public bool InRobotForm => Owner?.Type != null
        && string.Equals(Owner.Type.Id, RobotFormId, StringComparison.OrdinalIgnoreCase);

    public override bool OnCast(World world, Unit target, Vector3 point)
    {
        if (world == null || Owner?.Type == null || !Owner.Alive) return false;

        bool robotNow = InRobotForm;
        UnitType form = UnitFactory.Get(robotNow ? HumanFormId : RobotFormId);
        if (form == null) return false;
        if (!world.BeginMorph(Owner, form, 0f, disable: false)) return false;

        ApplyFormBonus(robotNow ? 0 : StrengthBonus, robotNow ? 0f : ArmorBonus);
        SyncDemolish(!robotNow);
        return true;
    }

    /// <summary>粉碎是机器人形态的固有技能（英雄变形不会自动换技能栏，这里手动授/收）。</summary>
    private void SyncDemolish(bool robot)
    {
        if (Owner == null) return;
        if (robot)
        {
            foreach (string id in DemolishIds)
            {
                if (Owner.FindAbility(id) == null) continue;
                return;
            }
            Owner.GiveAbility("ANde", 1);
            return;
        }
        foreach (string id in DemolishIds) Owner.RemoveAbility(id);
    }

    private static readonly string[] DemolishIds = { "ANde", "ANd1", "ANd2", "ANd3" };

    /// <summary>机器人形态的力量按「+1 力量 = +25 生命」直接落地（变形期间不做属性重算）。</summary>
    private void ApplyFormBonus(int strength, float armor)
    {
        if (Owner == null) return;

        Owner.BonusStrength += strength - _appliedStrength;
        Owner.Strength += strength - _appliedStrength;
        Owner.MaxHp += (strength - _appliedStrength) * 25f;
        Owner.Hp = Mathf.Clamp(Owner.Hp + (strength - _appliedStrength) * 25f, 1f, Owner.MaxHp);
        Owner.BonusArmor += armor - _appliedArmor;
        Owner.View?.SetHp(Owner.Hp / Mathf.Max(1f, Owner.MaxHp), Owner.Alive);

        _appliedStrength = strength;
        _appliedArmor = armor;
    }

    public override bool TryAutoCast(World world) => false;

    public override void OnOwnerDeath(World world) => ApplyFormBonus(0, 0f);
}
