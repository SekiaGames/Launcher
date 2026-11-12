using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public static partial class CombatProbe
{
    /// <summary>
    /// 幻象与隐身：镜像分身、疾风步、黑蚀箭、生命汲取。数值一律读物编。
    /// </summary>
    private static void CheckIllusionSkills()
    {
        GD.Print("==== 幻象与隐身自检 ====");

        CheckMirrorImage();
        CheckWindWalk();
        CheckBlackArrow();
        CheckLifeDrain();
    }

    private static void CheckMirrorImage()
    {
        UnitType bladeType = UnitFactory.Get("Obla");
        AbilityType mirrorType = AbilityCatalog.Get("AOmi");
        if (bladeType == null || mirrorType == null) return;

        var world = new World(null, null);
        world.SummonHandler = (caster, id, position) =>
        {
            Unit created = MakeUnit(UnitFactory.Get(id), caster.Player);
            created.Position = position;
            return created;
        };

        Unit blade = MakeHero(bladeType, 0, 3, world);
        blade.Mana = 400f;
        blade.MaxMana = 400f;
        world.Units.Add(blade);

        Ability mirror = blade.GiveAbility("AOmi", 1);
        bool cast = world.TryCast(blade, mirror, null, blade.Position);
        Verify("镜像分身施放成功", 1f, cast ? 1f : 0f, 0f);

        int images = CountIllusions(world);
        Verify("镜像数量=DataA", mirrorType.DataAt(1, 'A'), images, 0f);

        Unit image = FirstIllusion(world);
        if (image == null) return;

        Verify("幻象造成伤害倍率=DataB", mirrorType.DataAt(1, 'B'), image.IllusionDamageDealt, 0.001f);
        Verify("幻象受到伤害倍率=DataC", mirrorType.DataAt(1, 'C'), image.IllusionDamageReceived, 0.001f);
        Verify("幻象生命上限抄本体", blade.MaxHp, image.MaxHp, 0.5f);

        // 幻象死亡不给经验：旁边放一个敌方英雄盯经验
        UnitType enemyType = UnitFactory.Get("Nbrn");
        Unit watcher = null;
        if (enemyType != null)
        {
            watcher = MakeHero(enemyType, 1, 3, world);
            watcher.Position = image.Position;
            world.Units.Add(watcher);
        }
        float xpBefore = watcher?.Xp ?? 0f;
        world.DealDamage(World.MakeAbilityDamage(blade, image, 99999f, AttackType.Spells, DamageType.Universal));
        Verify("幻象被打死", 0f, image.Alive ? 1f : 0f, 0f);
        if (watcher != null) Verify("幻象死亡不给经验", xpBefore, watcher.Xp, 0.01f);

        mirror.CooldownRemaining = 0f;
        world.TryCast(blade, mirror, null, blade.Position);
        Verify("再放一次先收掉旧幻象", mirrorType.DataAt(1, 'A'), CountIllusions(world), 0f);
        GD.Print($"[自检] 镜像分身 {images} 个幻象，受伤×{mirrorType.DataAt(1, 'C'):0}，"
            + $"站位半径 {mirrorType.RangeAt(1):0}，持续 {mirrorType.DurationAt(1):0}s");
    }

    private static int CountIllusions(World world)
    {
        int count = 0;
        foreach (Unit unit in world.Units)
        {
            if (unit.Alive && unit.IsIllusion) count++;
        }
        return count;
    }

    private static Unit FirstIllusion(World world)
    {
        foreach (Unit unit in world.Units)
        {
            if (unit.Alive && unit.IsIllusion) return unit;
        }
        return null;
    }

    private static void CheckWindWalk()
    {
        UnitType bladeType = UnitFactory.Get("Obla");
        UnitType footmanType = UnitFactory.Get("hfoo");
        AbilityType windType = AbilityCatalog.Get("AOwk");
        if (bladeType == null || footmanType == null || windType == null) return;

        var world = new World(null, null);
        Unit blade = MakeHero(bladeType, 0, 3, world);
        blade.Mana = 200f;
        blade.MaxMana = 200f;
        world.Units.Add(blade);
        Unit footman = MakeUnit(footmanType, 1);
        footman.Position = new Vector3(300f, 0f, 0f);
        world.Units.Add(footman);

        Ability wind = blade.GiveAbility("AOwk", 1);
        bool cast = world.TryCast(blade, wind, null, blade.Position);
        Verify("疾风步施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("淡入前不隐身", 0f, blade.State.Invisible ? 1f : 0f, 0f);

        float baseSpeed = blade.Type.Speed;
        for (int tick = 0; tick < 20; tick++) world.Advance();
        Verify("淡入后隐身", 1f, blade.State.Invisible ? 1f : 0f, 0f);
        Verify("疾风步移速加成=DataB", 1f + windType.DataAt(1, 'B'), World.SpeedOf(blade) / baseSpeed, 0.01f);

        float backstab = world.ApplyLaunchModifiers(blade, footman, 50f);
        Verify("破隐一击附加伤害=DataC", 50f + windType.DataAt(1, 'C'), backstab, 0.01f);
        Verify("出手后破隐", 0f, blade.State.Invisible ? 1f : 0f, 0f);

        for (int tick = 0; tick < 400; tick++) world.Advance();
        Verify("到期后移速还原", baseSpeed, World.SpeedOf(blade), 0.5f);
        GD.Print($"[自检] 疾风步 淡入 {windType.DataAt(1, 'A'):0.0}s 隐身 {windType.DurationAt(1):0}s"
            + $" 移速 +{windType.DataAt(1, 'B'):P0} 破隐一击 +{windType.DataAt(1, 'C'):0}");
    }

    private static void CheckBlackArrow()
    {
        UnitType rangerType = UnitFactory.Get("Nbrn");
        UnitType footmanType = UnitFactory.Get("hfoo");
        AbilityType arrowType = AbilityCatalog.Get("ANba");
        if (rangerType == null || footmanType == null || arrowType == null) return;
        if (UnitFactory.Get(arrowType.SummonAt(1)) == null) return;

        var world = new World(null, null);
        world.SummonHandler = (caster, id, position) =>
        {
            Unit created = MakeUnit(UnitFactory.Get(id), caster.Player);
            created.Position = position;
            return created;
        };

        Unit ranger = MakeHero(rangerType, 0, 3, world);
        ranger.Mana = 100f;
        ranger.MaxMana = 100f;
        world.Units.Add(ranger);

        Unit footman = MakeUnit(footmanType, 1);
        footman.MaxHp = 800f;
        footman.Hp = 800f;
        footman.Position = new Vector3(200f, 0f, 0f);
        world.Units.Add(footman);

        Ability arrow = ranger.GiveAbility("ANba", 1);
        float modified = world.ApplyAttackModifiers(ranger, footman, 50f);
        Verify("黑蚀箭附加伤害=DataA", 50f + arrowType.DataAt(1, 'A'), modified, 0.01f);
        Verify("黑蚀箭扣蓝=Cost", 100f - arrowType.CostAt(1), ranger.Mana, 0.01f);

        bool marked = false;
        foreach (Buff buff in footman.Buffs)
        {
            if (buff is BlackArrowMarkBuff) marked = true;
        }
        Verify("黑蚀箭挂上标记", 1f, marked ? 1f : 0f, 0f);

        world.DealDamage(World.MakeAbilityDamage(ranger, footman, 99999f, AttackType.Spells, DamageType.Universal));

        int skeletons = 0;
        string skeletonId = arrowType.SummonAt(1);
        foreach (Unit unit in world.Units)
        {
            if (!unit.Alive || unit.Type == null) continue;
            if (string.Equals(unit.Type.Id, skeletonId, StringComparison.OrdinalIgnoreCase)) skeletons++;
        }
        Verify("标记击杀召骷髅=DataB", arrowType.DataAt(1, 'B'), skeletons, 0f);
        GD.Print($"[自检] 黑蚀箭 +{arrowType.DataAt(1, 'A'):0} 伤害 / {arrowType.CostAt(1):0} 蓝，"
            + $"标记 {arrowType.DurationAt(1):0}s，召 {skeletons} 只 {skeletonId}（{arrowType.DataAt(1, 'C'):0}s）");
    }

    private static void CheckLifeDrain()
    {
        UnitType rangerType = UnitFactory.Get("Nbrn");
        UnitType footmanType = UnitFactory.Get("hfoo");
        AbilityType drainType = AbilityCatalog.Get("ANdr");
        if (rangerType == null || footmanType == null || drainType == null) return;

        var world = new World(null, null);
        Unit ranger = MakeUnit(rangerType, 0);
        ranger.Mana = 200f;
        ranger.MaxMana = 200f;
        ranger.MaxHp = 600f;
        ranger.Hp = 100f;
        world.Units.Add(ranger);

        // 先空转 2 秒量自然回量，再把生命压回去，免得把回血算进汲取
        for (int tick = 0; tick < 40; tick++) world.Advance();
        float regen = ranger.Hp - 100f;
        ranger.Hp = 100f;

        Unit footman = MakeUnit(footmanType, 1);
        footman.MaxHp = 900f;
        footman.Hp = 900f;
        footman.Position = new Vector3(220f, 0f, 0f);
        world.Units.Add(footman);

        Ability drain = ranger.GiveAbility("ANdr", 1);
        bool cast = world.TryCast(ranger, drain, footman, footman.Position);
        Verify("生命汲取施放成功", 1f, cast ? 1f : 0f, 0f);
        Verify("生命汲取进入引导", 1f, world.ChannelCount, 0f);

        for (int tick = 0; tick < 40; tick++) world.Advance();
        float drained = 900f - footman.Hp;
        float healed = ranger.Hp - 100f;
        Verify("生命汲取 2 秒抽≈DataA×2", drainType.DataAt(1, 'A') * 2f, drained, 8f);
        Verify("抽到多少回多少", drained + regen, healed, 1f);
        GD.Print($"[自检] 生命汲取 2 秒抽 {drained:0} 回 {healed:0}（每跳 {drainType.DataAt(1, 'A'):0}，"
            + $"间隔 {drainType.DataAt(1, 'C'):0}s，共 {drainType.DurationAt(1):0}s）");
    }
}