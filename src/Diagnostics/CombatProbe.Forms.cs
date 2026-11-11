using Godot;

namespace Launcher;

public static partial class CombatProbe
{
    /// <summary>形态类机制：骑乘合成、分离、研究换型、潜水、硬化皮肤、祖灵复活。</summary>
    private static void CheckFormMechanics()
    {
        CheckCouple();
        CheckDecouple();
        CheckUnitTypeUpgrade();
        CheckSubmerge();
        CheckHardenedSkinChance();
        CheckAncestralSpirit();
    }

    private static World MakeSummonWorld()
    {
        var world = new World(null, null);
        world.SummonHandler = (caster, id, position) =>
        {
            UnitType type = UnitCatalog.Get(id);
            if (type == null) return null;
            Unit created = MakeUnit(type, caster.Player);
            created.Position = position;
            foreach (string abilityId in type.Abilities) created.GiveAbility(abilityId, 1);
            return created;
        };
        return world;
    }

    private static void CheckCouple()
    {
        World world = MakeSummonWorld();
        Unit archer = ProbeUnit(world, "earc", 0, Vector3.Zero, "Aco2");
        Unit hippogryph = ProbeUnit(world, "ehip", 0, new Vector3(50f, 0f, 0f), "Aco3");
        if (archer == null || hippogryph == null) return;

        bool cast = world.TryCast(archer, archer.FindAbility("Aco2"), null, archer.Position);
        Verify("骑乘合成施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("合成后场上只剩一个单位", 1f, world.Units.Count, 0f);

        Unit rider = world.Units.Count > 0 ? world.Units[0] : null;
        Verify("合成出角鹰兽骑士", 1f,
            string.Equals(rider?.Type?.Id, "ehpr", System.StringComparison.OrdinalIgnoreCase) ? 1f : 0f, 0f);
        Verify("骑士带分离技能", 1f, rider != null && rider.FindAbility("Adec") != null ? 1f : 0f, 0f);
        Verify("骑士继承满血比例", 1f, rider != null ? rider.Hp / rider.MaxHp : 0f, 0.01f);
    }

    private static void CheckDecouple()
    {
        World world = MakeSummonWorld();
        Unit rider = ProbeUnit(world, "ehpr", 0, Vector3.Zero, "Adec");
        if (rider == null) return;

        rider.Hp = rider.MaxHp * 0.5f;
        bool cast = world.TryCast(rider, rider.FindAbility("Adec"), null, rider.Position);

        Verify("分离施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("分离出两个单位", 2f, world.Units.Count, 0f);

        float lowest = 1f;
        foreach (Unit split in world.Units) lowest = Mathf.Min(lowest, split.Hp / Mathf.Max(1f, split.MaxHp));
        Verify("两个单位都继承 50% 血量", 0.5f, lowest, 0.02f);
    }

    private static void CheckUnitTypeUpgrade()
    {
        var world = new World(null, null);
        Unit headhunter = ProbeUnit(world, "ohun", 0, Vector3.Zero, "Sbsk");
        if (headhunter == null) return;

        for (int index = 0; index < 40; index++) world.TickAbilities();
        Verify("未研究时不换型", 1f,
            string.Equals(headhunter.Type.Id, "ohun", System.StringComparison.OrdinalIgnoreCase) ? 1f : 0f, 0f);

        world.SetResearchLevel(0, "Robk", 1);
        for (int index = 0; index < 40; index++) world.TickAbilities();

        Verify("研究完成后换狂暴战士", 1f,
            string.Equals(headhunter.Type.Id, "otbk", System.StringComparison.OrdinalIgnoreCase) ? 1f : 0f, 0f);
        Verify("换型后生命上限按新类型", UnitCatalog.Get("otbk").Hp, headhunter.MaxHp, 0.5f);
    }

    private static void CheckSubmerge()
    {
        var world = new World(null, null);
        Unit myrmidon = ProbeUnit(world, "nmyr", 0, Vector3.Zero, "Asb1");
        if (myrmidon == null) return;

        Ability submerge = myrmidon.FindAbility("Asb1");
        bool dive = world.TryCast(myrmidon, submerge, null, myrmidon.Position);
        Verify("潜水施放成功", 1f, dive ? 1f : 0f, 0f);
        Verify("潜水后换成水下形态", 1f,
            string.Equals(myrmidon.Type.Id, "nmys", System.StringComparison.OrdinalIgnoreCase) ? 1f : 0f, 0f);
        Verify("潜水期间隐形", 1f, myrmidon.State.Invisible ? 1f : 0f, 0f);
        Verify("潜水期间不能攻击", 0f, myrmidon.State.CanAttack ? 1f : 0f, 0f);

        if (submerge != null) submerge.CooldownRemaining = 0f;
        Ability again = myrmidon.FindAbility("Asb1");
        bool surface = again != null && world.TryCast(myrmidon, again, null, myrmidon.Position);
        Verify("浮出水面施放成功", 1f, surface ? 1f : 0f, 0f);
        Verify("浮出后恢复水面形态", 1f,
            string.Equals(myrmidon.Type.Id, "nmyr", System.StringComparison.OrdinalIgnoreCase) ? 1f : 0f, 0f);
        Verify("浮出后解除隐形", 0f, myrmidon.State.Invisible ? 1f : 0f, 0f);
    }

    private static void CheckHardenedSkinChance()
    {
        AbilityType type = AbilityCatalog.Get("Ansk");
        if (type == null) return;

        var world = new World(null, null);
        Unit turtle = ProbeUnit(world, "nhyc", 0, Vector3.Zero, "Ansk");
        Unit attacker = ProbeUnit(world, "hfoo", 1, new Vector3(80f, 0f, 0f));
        if (turtle == null || attacker == null) return;

        Ability skin = turtle.FindAbility("Ansk");
        var info = new DamageInfo { Source = attacker, Target = turtle, Raw = 20f, Amount = 20f };
        skin.ModifyIncomingDamage(world, info);
        Verify("龙龟硬化皮肤减伤 DataC", 20f - type.DataAt(1, 'C'), info.Amount, 0.01f);

        var big = new DamageInfo { Source = attacker, Target = turtle, Raw = 5f, Amount = 5f };
        skin.ModifyIncomingDamage(world, big);
        Verify("伤害低于减免时保底 DataB", type.DataAt(1, 'B'), big.Amount, 0.01f);
    }

    private static void CheckAncestralSpirit()
    {
        var world = new World(null, null);
        Unit walker = ProbeUnit(world, "ospm", 0, Vector3.Zero, "Aast");
        Unit tauren = ProbeUnit(world, "otau", 0, new Vector3(120f, 0f, 0f));
        if (walker == null || tauren == null) return;

        walker.Mana = 500f;
        tauren.Alive = false;
        tauren.Hp = 0f;
        tauren.IsCorpse = true;
        tauren.CorpseRemaining = 60f;

        bool cast = world.TryCast(walker, walker.FindAbility("Aast"), null, walker.Position);
        Verify("祖灵施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("牛头人已复活", 1f, tauren.Alive ? 1f : 0f, 0f);
        Verify("复活血量为 DataA 比例", tauren.MaxHp, tauren.Hp, 1f);
    }
}