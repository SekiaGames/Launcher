using Godot;
using System.Collections.Generic;

namespace Launcher;

public static partial class CombatProbe
{
    /// <summary>野怪 / 古树 / 召唤物自带技能：吃树、狂暴、缓慢光环、永久献祭、龙卷风伤害。</summary>
    private static void CheckCreepAbilities()
    {
        CheckEatTreeAbility();
        CheckFrenzyAbility();
        CheckSlowAuraAbility();
        CheckPermanentImmolationAbility();
        CheckTornadoDamageAbility();
    }

    private static void CheckEatTreeAbility()
    {
        var trees = new List<TreeState> { new() { Position = new Vector3(40f, 0f, 0f), Hp = 50f } };
        var world = new World(null, null, trees);
        Unit giant = ProbeUnit(world, "ncap", 12, Vector3.Zero, "Aeat");
        if (giant == null) return;

        giant.Hp = giant.MaxHp * 0.5f;
        float before = giant.Hp;
        for (int index = 0; index < 40; index++) world.TickAbilities();

        Verify("吃树把树吃掉", 0f, trees[0].Alive ? 1f : 0f, 0f);
        Verify("吃树在回血", 1f, giant.Hp > before ? 1f : 0f, 0f);
    }

    private static void CheckFrenzyAbility()
    {
        AbilityType type = AbilityCatalog.Get("Afzy");
        if (type == null) return;

        var world = new World(null, null);
        Unit creep = ProbeUnit(world, "nqb2", 12, Vector3.Zero, "Afzy");
        if (creep == null) return;
        ProbeUnit(world, "hfoo", 0, new Vector3(300f, 0f, 0f));

        Ability frenzy = creep.FindAbility("Afzy");
        bool cast = world.TryCast(creep, frenzy, null, creep.Position);
        Verify("狂暴可用", 1f, cast ? 1f : 0f, 0f);
        Verify("狂暴攻速 =DataA", type.DataAt(1, 'A'), creep.BonusAttackSpeedPct, 0.001f);
    }

    private static void CheckSlowAuraAbility()
    {
        var world = new World(null, null);
        if (ProbeUnit(world, "ntor", 12, Vector3.Zero, "Aasl") == null) return;
        Unit victim = ProbeUnit(world, "hfoo", 0, new Vector3(200f, 0f, 0f));

        for (int index = 0; index < 12; index++) world.TickAbilities();
        Verify("缓慢光环把敌人减速", 1f, victim.Slowed ? 1f : 0f, 0f);
    }

    private static void CheckPermanentImmolationAbility()
    {
        var world = new World(null, null);
        if (ProbeUnit(world, "ninf", 12, Vector3.Zero, "ANpi") == null) return;
        Unit victim = ProbeUnit(world, "hfoo", 0, new Vector3(120f, 0f, 0f));

        float before = victim.Hp;
        for (int index = 0; index < 25; index++) world.TickAbilities();
        Verify("永久献祭烧到附近敌人", 1f, victim.Hp < before ? 1f : 0f, 0f);
    }

    private static void CheckTornadoDamageAbility()
    {
        var world = new World(null, null);
        if (ProbeUnit(world, "ntor", 12, Vector3.Zero, "Atdg") == null) return;
        Unit hall = ProbeUnit(world, "htow", 0, new Vector3(300f, 0f, 0f));

        float before = hall.Hp;
        for (int index = 0; index < 25; index++) world.TickAbilities();
        Verify("龙卷风每秒磨建筑", 1f, hall.Hp < before ? 1f : 0f, 0f);
    }
}