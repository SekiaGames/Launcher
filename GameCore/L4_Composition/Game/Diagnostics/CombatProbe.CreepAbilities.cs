using Godot;
using System.Collections.Generic;

namespace OpenWC3.Game;

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
        CheckGrabTreeAbility();
        CheckRootAbility();
    }

    /// <summary>扎根/拔根（Aro1）：拔根后不再是建筑、护甲类型换 DataD，再按一次扎根回来。</summary>
    private static void CheckRootAbility()
    {
        AbilityType type = AbilityCatalog.Get("Aro1");
        if (type == null) return;

        var world = new World(null, null);
        Unit ancient = ProbeUnit(world, "etoa", 0, Vector3.Zero, "Aro1");
        if (ancient == null) return;

        Ability root = ancient.FindAbility("Aro1");
        bool uprooted = world.TryCast(ancient, root, null, ancient.Position);
        Verify("拔根可用", 1f, uprooted ? 1f : 0f, 0f);
        Verify("拔根后不再是建筑", 0f, ancient.IsBuilding ? 1f : 0f, 0f);
        Verify("拔根后护甲类型 =DataD", type.DataAt(1, 'D'), (float)ancient.DefenseTypeOrDefault, 0f);
        Verify("拔根过渡期不能生产", 1f, ancient.ProductionBlocked ? 1f : 0f, 0f);

        root.CooldownRemaining = 0f;
        bool blocked = !world.TryCast(ancient, root, null, ancient.Position);
        Verify("过渡期不能再按", 1f, blocked ? 1f : 0f, 0f);

        for (int index = 0; index < 60; index++) world.TickAbilities();
        Verify("过渡结束解生产锁", 0f, ancient.ProductionBlocked ? 1f : 0f, 0f);

        bool planted = world.TryCast(ancient, root, null, ancient.Position);
        Verify("再按一次扎根", 1f, planted && ancient.IsBuilding ? 1f : 0f, 0f);
        Verify("扎根后护甲类型还原", 0f, ancient.DefenseTypeOverride.HasValue ? 1f : 0f, 0f);
    }

    /// <summary>战棍（Agra）：拔树后攻击变攻城、射程 +DataE，挥满 DataE 次恢复。</summary>
    private static void CheckGrabTreeAbility()
    {
        AbilityType type = AbilityCatalog.Get("Agra");
        if (type == null) return;

        var trees = new List<TreeState> { new() { Position = new Vector3(40f, 0f, 0f), Hp = 50f } };
        var world = new World(null, null, trees);
        Unit giant = ProbeUnit(world, "emtg", 0, Vector3.Zero, "Agra");
        if (giant == null) return;

        float baseReach = giant.AttackReach;
        bool cast = world.TryCast(giant, giant.FindAbility("Agra"), null, giant.Position);

        Verify("战棍可用", 1f, cast ? 1f : 0f, 0f);
        Verify("拔树后树消失", 0f, trees[0].Alive ? 1f : 0f, 0f);
        Verify("拔树后是攻城攻击", (float)AttackType.Siege, (float)(giant.AttackTypeOverride ?? AttackType.Normal), 0f);
        Verify("拔树后射程 +DataE", baseReach + type.DataAt(1, 'E'), giant.AttackReach, 0.01f);

        int charges = Mathf.RoundToInt(type.DataAt(1, 'E'));
        Unit victim = ProbeUnit(world, "hfoo", 1, new Vector3(200f, 0f, 0f));
        for (int index = 0; index < charges; index++) world.ApplyLaunchModifiers(giant, victim, 10f);
        world.TickAbilities();

        Verify("挥满次数恢复普通攻击", (float)AttackType.Normal, (float)(giant.AttackTypeOverride ?? AttackType.Normal), 0f);
        Verify("战棍射程加成收回", 0f, giant.AttackRangeBonus, 0.01f);
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

        float baseSpeed = World.SpeedOf(victim);
        for (int index = 0; index < 12; index++) world.TickAbilities();
        Verify("缓慢光环把敌人减速", 1f, World.SpeedOf(victim) < baseSpeed ? 1f : 0f, 0f);
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