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
        CheckMeatCargo();
        CheckBurrowGarrison();
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

    /// <summary>绞肉车：装尸体暂停衰减、卸货倒回车周围、掘尸定时补货。</summary>
    private static void CheckMeatCargo()
    {
        World world = MakeSummonWorld();
        Unit cart = ProbeUnit(world, "umtw", 0, Vector3.Zero, "Amel", "Amed", "Aexh");
        Unit victim = ProbeUnit(world, "hfoo", 1, new Vector3(80f, 0f, 0f));
        if (cart == null || victim == null) return;

        world.KillUnit(victim, cart);
        Verify("步兵已阵亡留尸", 1f, victim.IsCorpse ? 1f : 0f, 0f);

        MeatCargo cargo = cart.FindPassive<MeatCargo>() as MeatCargo;
        bool loaded = world.TryCast(cart, cart.FindAbility("Amel"), null, cart.Position);
        Verify("装尸体施放成功", 1f, loaded ? 1f : 0f, 0f);
        Verify("货舱里有一具尸体", 1f, cargo != null ? cargo.CorpseCount : 0f, 0f);
        Verify("尸体归到车名下", 1f, victim.CorpseCarrier == cart ? 1f : 0f, 0f);

        victim.TickCorpse(120f);
        Verify("货舱内尸体不衰减", 1f, victim.IsCorpse ? 1f : 0f, 0f);

        bool dropped = world.TryCast(cart, cart.FindAbility("Amed"), null, cart.Position);
        Verify("卸尸体施放成功", 1f, dropped ? 1f : 0f, 0f);
        Verify("卸货后尸体仍在场", 1f, victim.IsCorpse && victim.CorpseCarrier == null ? 1f : 0f, 0f);

        for (int index = 0; index < 400; index++) world.TickAbilities();
        Verify("掘尸定时补货", 1f, cargo != null ? cargo.CorpseCount : 0f, 0f);
    }

    /// <summary>兽族地洞：装苦工藏人、驻军才开火、放人出来。</summary>
    private static void CheckBurrowGarrison()
    {
        var world = new World(null, null);
        Unit burrow = ProbeUnit(world, "otrb", 0, Vector3.Zero, "Abun", "Abtl", "Astd");
        Unit peonA = ProbeUnit(world, "opeo", 0, new Vector3(60f, 0f, 0f));
        Unit peonB = ProbeUnit(world, "opeo", 0, new Vector3(90f, 0f, 0f));
        Unit enemy = ProbeUnit(world, "hfoo", 1, new Vector3(300f, 0f, 0f));
        AbilityType holdType = AbilityCatalog.Get("Abun");
        if (burrow == null || peonA == null || peonB == null || enemy == null || holdType == null) return;

        Verify("地洞容量 =DataA", Mathf.Max(1, (int)holdType.DataAt(1, 'A')), burrow.CargoCapacity, 0f);
        Verify("空地名洞不开火", 0f, World.CanStrike(burrow, enemy) ? 1f : 0f, 0f);

        bool loaded = world.TryCast(burrow, burrow.FindAbility("Abun"), null, burrow.Position);
        Verify("装驻军施放成功", 1f, loaded ? 1f : 0f, 0f);
        Verify("两个苦工都进去了", 2f, burrow.Cargo.Count, 0f);
        Verify("苦工不再参与世界模拟", 2f, world.Units.Count, 0f);
        Verify("驻军后开火", 1f, World.CanStrike(burrow, enemy) ? 1f : 0f, 0f);

        Ability toggle = burrow.FindAbility("Abun");
        if (toggle != null) toggle.CooldownRemaining = 0f;
        bool unloaded = world.TryCast(burrow, toggle, null, burrow.Position);
        Verify("放人出来施放成功", 1f, unloaded ? 1f : 0f, 0f);
        Verify("两个苦工回到场上", 4f, world.Units.Count, 0f);
        Verify("苦工脱离驻军状态", 0f, peonA.IsCargoed || peonB.IsCargoed ? 1f : 0f, 0f);
        Verify("人走空后停火", 0f, World.CanStrike(burrow, enemy) ? 1f : 0f, 0f);
    }
}