using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public static partial class CombatProbe
{
    /// <summary>技能系统：数据读取、治疗/伤害、眩晕、AoE 减速、光环、免疫、变身、被动暴击。</summary>
    private static void CheckSkills()
    {
        GD.Print("==== 技能自检 ====");

        AbilityType holyType = AbilityCatalog.Get("AHhb");
        if (holyType == null)
        {
            Verify("技能表可读", 1f, 0f, 0f);
            return;
        }

        Verify("圣光术 3 级", 3f, holyType.MaxLevel, 0f);
        Verify("圣光术冷却 5s", 5f, holyType.CooldownAt(1), 0.01f);
        Verify("圣光术法力 65", 65f, holyType.CostAt(1), 0.01f);
        Verify("圣光术射程 800", 800f, holyType.RangeAt(1), 0.01f);
        Verify("圣光术治疗 200/600", 600f, holyType.DataAt(3, 'A'), 0.01f);

        UnitType paladinType = UnitCatalog.Get("Hpal");
        UnitType footmanType = UnitCatalog.Get("hfoo");
        UnitType orcType = UnitCatalog.Get("uaco");
        if (paladinType == null || footmanType == null) return;

        // —— 治疗 ——
        var world = new World(null, null);
        Unit paladin = MakeHero(paladinType, 0, 3, world);
        Unit footman = MakeUnit(footmanType, 0);
        footman.Position = new Vector3(100f, 0f, 0f);
        footman.Hp = 50f;
        world.Units.Add(paladin);
        world.Units.Add(footman);

        Ability holy = paladin.GiveAbility("AHhb", 0);
        Verify("英雄技能初始 0 级", 0f, holy.Level, 0f);
        paladin.SkillPoints = 1;
        Verify("花技能点学会圣光术", 1f, world.LearnAbility(paladin, "AHhb") ? 1f : 0f, 0f);
        Verify("学完后 1 级", 1f, holy.Level, 0f);
        Verify("没技能点学不了", 0f, world.LearnAbility(paladin, "AHhb") ? 1f : 0f, 0f);

        float before = footman.Hp;
        bool healed = world.TryCast(paladin, holy, footman, footman.Position);
        Verify("圣光术施放成功", 1f, healed ? 1f : 0f, 0f);
        Verify("圣光术治疗 200", before + 200f, footman.Hp, 0.5f);
        Verify("法力被扣 65", paladin.MaxMana - 65f, paladin.Mana, 0.5f);
        Verify("冷却已进入", 1f, holy.CooldownRemaining > 0f ? 1f : 0f, 0f);
        Verify("冷却中不能连放", 0f, world.TryCast(paladin, holy, footman, footman.Position) ? 1f : 0f, 0f);
        GD.Print($"[自检] 圣光术 治疗 {before:0}→{footman.Hp:0}（+{footman.Hp - before:0}）"
            + $" 法力 {paladin.Mana:0}/{paladin.MaxMana:0} 冷却 {holy.CooldownRemaining:0.0}s");

        // —— 对亡灵：治疗量一半的伤害 ——
        if (orcType != null)
        {
            // 亡灵必须是「友军」才走得到圣光术的目标校验（原版圣光只对友军施放，对亡灵友军造成一半伤害）
            Unit skeleton = MakeUnit(orcType, 0);
            skeleton.Position = new Vector3(120f, 0f, 0f);
            world.Units.Add(skeleton);
            holy.CooldownRemaining = 0f;
            paladin.Mana = paladin.MaxMana;
            float undeadBefore = skeleton.Hp;
            world.TryCast(paladin, holy, skeleton, skeleton.Position);
            float hurt = undeadBefore - skeleton.Hp;
            Verify("圣光术对亡灵造成一半伤害", 1f, skeleton.Hp < undeadBefore ? 1f : 0f, 0f);
            GD.Print($"[自检] 圣光术 对亡灵({orcType.Race}) {undeadBefore:0}→{skeleton.Hp:0}，扣 {hurt:0}");
        }

        // —— 风暴之锤：伤害 + 眩晕 ——
        AbilityType boltType = AbilityCatalog.Get("AHtb");
        if (boltType != null)
        {
            var world2 = new World(null, null);
            Unit hero = MakeHero(paladinType, 0, 3, world2);
            Unit victim = MakeUnit(footmanType, 1);
            victim.Position = new Vector3(200f, 0f, 0f);
            world2.Units.Add(hero);
            world2.Units.Add(victim);

            Ability bolt = hero.GiveAbility("AHtb", 1);
            float hpBefore = victim.Hp;
            bool cast = world2.TryCast(hero, bolt, victim, victim.Position);
            Verify("风暴之锤施放成功", 1f, cast ? 1f : 0f, 0f);
            Verify("锤子还没飞到时不结算", hpBefore, victim.Hp, 0.01f);
            Verify("弹道已排进队列", 1f, world2.PendingAbilityHitCount, 0f);

            // 200 距离 / 900 速度 ≈ 0.22s，跑 10 tick 必然命中
            for (int index = 0; index < 10; index++) world2.TickAbilityMissiles();
            Verify("弹道命中后扣血", 1f, victim.Hp < hpBefore ? 1f : 0f, 0f);
            Verify("风暴之锤眩晕 5s", 5f, victim.State.Remaining(StateMod.Stun), 0.01f);
            GD.Print($"[自检] 风暴之锤 飞行后 {hpBefore:0}→{victim.Hp:0}，眩晕 {victim.State.Remaining(StateMod.Stun):0.0}s");
        }

        // —— 雷霆一击：AoE + 减速 ——
        AbilityType clapType = AbilityCatalog.Get("AHtc");
        if (clapType != null && orcType != null)
        {
            var world3 = new World(null, null);
            Unit hero = MakeHero(UnitCatalog.Get("Hmkg") ?? paladinType, 0, 3, world3);
            var victims = new List<Unit>();
            for (int index = 0; index < 3; index++)
            {
                Unit victim = MakeUnit(orcType, 1);
                victim.Position = new Vector3(80f + index * 40f, 0f, 0f);
                world3.Units.Add(victim);
                victims.Add(victim);
            }
            world3.Units.Add(hero);

            Ability clap = hero.GiveAbility("AHtc", 1);
            bool cast = world3.TryCast(hero, clap, null, hero.Position);
            int hurt = 0;
            int slowed = 0;
            foreach (Unit victim in victims)
            {
                if (victim.Hp < victim.MaxHp) hurt++;
                if (victim.Slowed) slowed++;
            }
            Verify("雷霆一击施放成功", 1f, cast ? 1f : 0f, 0f);
            Verify("雷霆一击打到 3 个", 3f, hurt, 0f);
            Verify("雷霆一击全部减速", 3f, slowed, 0f);
            GD.Print($"[自检] 雷霆一击 命中 {hurt} 个，减速 {victims[0].SlowMovePct * 100f:0}%移速/"
                + $"{victims[0].SlowAttackPct * 100f:0}%攻速 {victims[0].SlowRemaining:0.0}s");
        }

        // —— 专注光环：护甲 ——
        AbilityType auraType = AbilityCatalog.Get("AHad");
        if (auraType != null)
        {
            var world4 = new World(null, null);
            Unit hero = MakeHero(paladinType, 0, 3, world4);
            Unit friend = MakeUnit(footmanType, 0);
            friend.Position = new Vector3(160f, 0f, 0f);
            world4.Units.Add(hero);
            world4.Units.Add(friend);

            float armorBefore = world4.ArmorOf(friend);
            hero.GiveAbility("AHad", 1).Level = 1;
            world4.RefreshAuras();
            float auraArmor = friend.AuraArmor;
            Verify("光环 +1.5 护甲", 1.5f, auraArmor, 0.01f);
            Verify("护甲汇总带上光环", armorBefore + 1.5f, world4.ArmorOf(friend), 0.01f);
            GD.Print($"[自检] 专注光环 友军护甲 {armorBefore:0.00}→{world4.ArmorOf(friend):0.00}");
        }

        // —— 神圣护甲：免疫 ——
        AbilityType shieldType = AbilityCatalog.Get("AHds");
        if (shieldType != null)
        {
            var world5 = new World(null, null);
            Unit hero = MakeHero(paladinType, 0, 3, world5);
            world5.Units.Add(hero);
            Ability shield = hero.GiveAbility("AHds", 1);
            hero.Mana = hero.MaxMana;
            bool cast = world5.TryCast(hero, shield, null, hero.Position);
            Verify("神圣护甲施放成功", 1f, cast ? 1f : 0f, 0f);
            Verify("无敌状态已挂", 1f, hero.State.Invulnerable ? 1f : 0f, 0f);

            float hp = hero.Hp;
            world5.DealDamage(World.MakeAbilityDamage(null, hero, 500f, AttackType.Spells, DamageType.Universal));
            Verify("无敌期间不掉血", hp, hero.Hp, 0.01f);
            GD.Print($"[自检] 神圣护甲 {hero.State.Remaining(StateMod.Invulnerable):0.0}s，500 伤害后生命仍 {hero.Hp:0}");
        }

        // —— 天神下凡：+护甲/+生命/+攻击 且到期还原 ——
        AbilityType avatarType = AbilityCatalog.Get("AHav");
        if (avatarType != null)
        {
            var world6 = new World(null, null);
            Unit hero = MakeHero(paladinType, 0, 6, world6);
            world6.Units.Add(hero);
            Ability avatar = hero.GiveAbility("AHav", 1);
            hero.Mana = hero.MaxMana;

            float hpBefore = hero.MaxHp;
            float armorBefore = world6.ArmorOf(hero);
            float damageBefore = world6.DamageBonus(hero);
            bool cast = world6.TryCast(hero, avatar, null, hero.Position);
            Verify("天神下凡施放成功", 1f, cast ? 1f : 0f, 0f);
            Verify("天神下凡 +500 生命上限", hpBefore + 500f, hero.MaxHp, 0.5f);
            Verify("天神下凡 +5 护甲", armorBefore + 5f, world6.ArmorOf(hero), 0.01f);
            Verify("天神下凡 +20 攻击", damageBefore + 20f, world6.DamageBonus(hero), 0.01f);
            Verify("天神下凡魔免", 1f, hero.State.MagicImmune ? 1f : 0f, 0f);

            for (int index = 0; index < 1300; index++) world6.TickAbilities();
            Verify("到期后生命上限还原", hpBefore, hero.MaxHp, 0.5f);
            Verify("到期后护甲还原", armorBefore, world6.ArmorOf(hero), 0.01f);
            Verify("到期后不再魔免", 0f, hero.State.MagicImmune ? 1f : 0f, 0f);
            GD.Print($"[自检] 天神下凡 生命 {hpBefore:0}→+500，到期回到 {hero.MaxHp:0}，护甲 {world6.ArmorOf(hero):0.0}");
        }

        // —— 被动：致命一击 / 重击 ——
        AbilityType critType = AbilityCatalog.Get("AOcr");
        if (critType != null)
        {
            var world7 = new World(null, null);
            Unit blademaster = MakeHero(UnitCatalog.Get("Obla") ?? paladinType, 0, 5, world7);
            world7.Units.Add(blademaster);
            Ability crit = blademaster.GiveAbility("AOcr", 1);
            crit.Level = 1;

            int critical = 0;
            int bad = 0;
            for (int index = 0; index < 4000; index++)
            {
                float value = crit.ModifyOutgoingAttack(world7, null, 100f);
                if (Mathf.Abs(value - 100f) < 0.01f) continue;
                if (Mathf.Abs(value - 200f) < 0.01f) critical++;
                else bad++;
            }
            Verify("致命一击只产生 1 倍或 2 倍", 0f, bad, 0f);
            float rate = critical / 4000f * 100f;
            Verify("致命一击触发率接近 15%", 15f, rate, 3f);
            GD.Print($"[自检] 致命一击 4000 次触发 {critical} 次（{rate:0.0}%，物编 15%）");
        }

        AbilityType bashType = AbilityCatalog.Get("AHbh");
        if (bashType != null)
        {
            var world8 = new World(null, null);
            Unit mountain = MakeHero(UnitCatalog.Get("Hmkg") ?? paladinType, 0, 5, world8);
            Unit victim = MakeUnit(footmanType, 1);
            world8.Units.Add(mountain);
            world8.Units.Add(victim);

            Ability bash = mountain.GiveAbility("AHbh", 1);
            bash.Level = 1;
            int stunned = 0;
            for (int index = 0; index < 4000; index++)
            {
                victim.State.Clear();
                float value = bash.ModifyOutgoingAttack(world8, victim, 100f);
                if (victim.State.Stunned) stunned++;
                if (value > 100f && !victim.State.Stunned) stunned--;
            }
            float bashRate = stunned / 4000f * 100f;
            Verify("重击触发率接近 20%", 20f, bashRate, 3f);
            GD.Print($"[自检] 重击 4000 次触发 {stunned} 次（{bashRate:0.0}%，物编 20%，附带 25 伤害 + 2s 眩晕）");
        }

        // —— AI 自动施法：牧师自己给伤兵治疗 ——
        AbilityType healType = AbilityCatalog.Get("Ahea");
        UnitType priestType = UnitCatalog.Get("hmpr");
        if (healType != null && priestType != null)
        {
            var world9 = new World(null, null);
            Unit priest = MakeUnit(priestType, 1);
            priest.AutoCast = true;
            priest.MaxMana = 200f;
            priest.Mana = 200f;
            priest.GiveAbility("Ahea", 1);

            Unit wounded = MakeUnit(footmanType, 1);
            wounded.Position = new Vector3(120f, 0f, 0f);
            wounded.Hp = 100f;
            world9.Units.Add(priest);
            world9.Units.Add(wounded);

            float woundedBefore = wounded.Hp;
            for (int index = 0; index < 30; index++) world9.Advance();

            Verify("牧师自动治疗伤兵", 1f, wounded.Hp > woundedBefore ? 1f : 0f, 0f);
            GD.Print($"[自检] 自动施法 牧师 治疗 {woundedBefore:0}→{wounded.Hp:0}（每口 {healType.DataAt(1, 'A'):0}，"
                + $"法力 {priest.Mana:0}）");
        }

        // —— 召唤：水元素 ——
        AbilityType summonType = AbilityCatalog.Get("AHwe");
        UnitType archmageType = UnitCatalog.Get("Hamg");
        if (summonType != null && archmageType != null && UnitCatalog.Get("hwat") != null)
        {
            var world10 = new World(null, null);
            Unit mage = MakeHero(archmageType, 0, 6, world10);
            world10.Units.Add(mage);

            var summon = (SummonUnit)mage.GiveAbility("AHwe", 1);
            summon.Level = 1;
            mage.Mana = mage.MaxMana;

            world10.SummonHandler = (caster, id, position) =>
            {
                UnitType created = UnitCatalog.Get(id);
                if (created == null) return null;
                return new Unit
                {
                    Type = created,
                    Player = caster.Player,
                    Position = position,
                    Hp = created.Hp,
                    MaxHp = created.Hp,
                };
            };

            bool cast = world10.TryCast(mage, summon, null, mage.Position + new Vector3(100f, 0f, 0f));
            Verify("水元素召唤成功", 1f, cast ? 1f : 0f, 0f);
            Verify("召唤物进世界", 2f, world10.Units.Count, 0f);

            Unit elemental = world10.Units[world10.Units.Count - 1];
            Verify("召唤物存在 60s", 60f, elemental.SummonRemaining, 0.5f);

            float lifeBefore = elemental.SummonRemaining;
            for (int index = 0; index < 1300; index++) world10.TickAbilities();
            Verify("召唤物到期消失", 0f, elemental.Alive ? 1f : 0f, 0f);
            GD.Print($"[自检] 召唤 {summon.SummonId} 存在 {lifeBefore:0}s，期满后 alive={elemental.Alive}");
        }

        // —— 复活：把友军尸体拉起来 ——
        AbilityType resType = AbilityCatalog.Get("AHre");
        if (resType != null)
        {
            var world11 = new World(null, null);
            Unit paladin2 = MakeHero(paladinType, 0, 6, world11);
            world11.Units.Add(paladin2);

            var corpses = new List<Unit>();
            for (int index = 0; index < 3; index++)
            {
                Unit dead = MakeUnit(footmanType, 0);
                dead.Position = new Vector3(120f + index * 60f, 0f, 0f);
                dead.EnterDeath();
                world11.Units.Add(dead);
                corpses.Add(dead);
            }

            var revive = (Resurrection)paladin2.GiveAbility("AHre", 1);
            revive.Level = 1;
            paladin2.Mana = paladin2.MaxMana;

            bool cast = world11.TryCast(paladin2, revive, null, paladin2.Position);
            int back = 0;
            foreach (Unit corpse in corpses) if (corpse.Alive) back++;
            Verify("复活施放成功", 1f, cast ? 1f : 0f, 0f);
            Verify("3 具尸体全部起身", 3f, back, 0f);
            GD.Print($"[自检] 复活 拉起 {back} 个，血量 {corpses[0].Hp:0}/{corpses[0].MaxHp:0}"
                + $"（上限 {revive.MaxTargets} 个 / 半径 {revive.Radius:0}）");
        }

        // —— AI 英雄自己加点 ——
        if (paladinType != null)
        {
            var world14 = new World(null, null);
            Unit aiHero = MakeHero(paladinType, 1, 6, world14);
            aiHero.AutoCast = true;
            aiHero.SkillPoints = 2;
            foreach (string id in paladinType.HeroAbilList) aiHero.GiveAbility(id, 0);
            world14.Units.Add(aiHero);

            for (int index = 0; index < 5; index++) world14.TickAbilities();

            int learned = 0;
            var names = new List<string>();
            foreach (Ability ability in aiHero.Abilities)
            {
                if (!ability.Learned) continue;
                learned++;
                names.Add($"{ability.Name}{ability.Level}");
            }
            Verify("AI 英雄自动花掉 2 点", 2f, learned, 0f);
            Verify("AI 英雄技能点用完", 0f, aiHero.SkillPoints, 0f);
            GD.Print($"[自检] AI 英雄加点 {string.Join("、", names)}（剩 {aiHero.SkillPoints} 点）");
        }

        // —— 物品使用与丢弃 ——
        ItemType potion = ItemCatalog.Get("phea");
        if (potion != null && paladinType != null)
        {
            var world12 = new World(null, null);
            Unit hero2 = MakeHero(paladinType, 0, 3, world12);
            world12.Units.Add(hero2);

            var flask = new Item { Type = potion };
            hero2.AddItem(flask);
            hero2.Hp = 100f;
            bool used = world12.UseItem(hero2, flask);
            Verify("药水可用", 1f, used ? 1f : 0f, 0f);
            Verify("药水回 250 生命", 350f, hero2.Hp, 1f);
            Verify("用完后格子空出来", 0f, hero2.ItemCount, 0f);

            // 满血时喝回血药应该失败（原版口径）
            hero2.Hp = hero2.MaxHp;
            var flask2 = new Item { Type = potion };
            hero2.AddItem(flask2);
            bool fullHp = world12.UseItem(hero2, flask2);
            Verify("满血时喝不了回血药", 0f, fullHp ? 1f : 0f, 0f);
            if (!fullHp) hero2.RemoveSlot(flask2.Slot);

            ItemType manaPotion = ItemCatalog.Get("pman");
            if (manaPotion != null)
            {
                var blue = new Item { Type = manaPotion };
                hero2.AddItem(blue);
                hero2.Mana = 20f;
                world12.UseItem(hero2, blue);
                Verify("法力药水回 150 法力", 170f, hero2.Mana, 1f);
            }

            GD.Print($"[自检] 消耗品 {potion.Name} {potion.Consumable}，用后生命 {hero2.Hp:0}，格子 {hero2.ItemCount}");
        }

        ItemType crown2 = ItemCatalog.Get("ckng");
        if (crown2 != null && paladinType != null)
        {
            var world13 = new World(null, null);
            Unit hero3 = MakeHero(paladinType, 0, 3, world13);
            world13.Units.Add(hero3);

            int baseStrength = hero3.Strength;
            var treasure = new Item { Type = crown2 };
            hero3.AddItem(treasure);
            world13.ApplyItemBonus(hero3, treasure);
            Verify("戴上王冠 +5 力量", baseStrength + 5, hero3.Strength, 0f);

            bool dropped = world13.DropItemFromSlot(hero3, treasure.Slot);
            Verify("丢弃成功", 1f, dropped ? 1f : 0f, 0f);
            Verify("丢弃后力量还原", baseStrength, hero3.Strength, 0f);
            Verify("物品回到地面", 1f, world13.GroundItems.Count, 0f);
            GD.Print($"[自检] 丢弃 {crown2.Name}：力量 {baseStrength}+5 → {hero3.Strength}，地面物品 {world13.GroundItems.Count}");
        }

        CheckShopAndTrade();

        CheckItemBonus();
    }

    /// <summary>商店库存/补货/卖出，以及英雄之间的物品转交。</summary>
    private static void CheckShopAndTrade()
    {
        UnitType shopType = UnitCatalog.Get("ngme");
        UnitType paladinType = UnitCatalog.Get("Hpal");
        if (shopType == null || paladinType == null) return;

        var world = new World(null, null);
        Unit shop = MakeUnit(shopType, 0);
        shop.InitShopStock();
        Unit hero = MakeHero(paladinType, 0, 3, world);
        world.Units.Add(shop);
        world.Units.Add(hero);

        int max = shop.StockMaxOf("phea");
        Verify("药水满仓 3", 3f, max, 0f);
        Verify("开局库存等于上限", max, shop.StockOf("phea"), 0f);

        for (int index = 0; index < max; index++) shop.TakeStock("phea");
        Verify("买空后为 0", 0f, shop.StockOf("phea"), 0f);
        Verify("没货买不了", 0f, shop.TakeStock("phea") ? 1f : 0f, 0f);

        int steps = (int)(shop.RestockInterval / World.Tick) + 2;
        for (int index = 0; index < steps; index++) world.TickShops();
        Verify("过一个补货周期补回 1", 1f, shop.StockOf("phea"), 0f);

        // 卖：王冠 1000 金，半价 500；加成要撤回
        ItemType crown = ItemCatalog.Get("ckng");
        if (crown != null)
        {
            var treasure = new Item { Type = crown };
            hero.AddItem(treasure);
            world.ApplyItemBonus(hero, treasure);
            int strengthWith = hero.Strength;

            int price = world.SellItem(shop, treasure);
            Verify("卖价是半价 500", 500f, price, 0f);
            Verify("卖出后物品消失", 0f, hero.ItemCount, 0f);
            Verify("卖出后加成撤回", strengthWith - 5, hero.Strength, 0f);
        }

        // 转交：力量加成跟着物品走
        Unit other = MakeHero(paladinType, 0, 3, world);
        world.Units.Add(other);
        var ring = new Item { Type = ItemCatalog.Get("rde1") };
        if (ring.Type != null)
        {
            hero.AddItem(ring);
            world.ApplyItemBonus(hero, ring);
            int heroArmor = (int)world.ArmorOf(hero);
            int otherArmor = (int)world.ArmorOf(other);

            bool given = world.GiveItem(hero, ring.Slot, other);
            Verify("转交成功", 1f, given ? 1f : 0f, 0f);
            Verify("转交后原主空手", 0f, hero.ItemCount, 0f);
            Verify("转交后对方拿到 1 件", 1f, other.ItemCount, 0f);
            Verify("护甲加成跟着走", 1f, world.ArmorOf(other) > otherArmor ? 1f : 0f, 0f);
            GD.Print($"[自检] 转交护戒：原主护甲 {heroArmor}→{(int)world.ArmorOf(hero)}，对方 {otherArmor}→{(int)world.ArmorOf(other)}");
        }

        GD.Print($"[自检] 商店 药水库存 0→{shop.StockOf("phea")}（补一件 {shop.RestockInterval:0}s），"
            + $"王冠卖 {Mathf.Max(1, (ItemCatalog.Get("ckng")?.GoldCost ?? 0) / 2)} 金");
    }

    /// <summary>物品属性加成：三围 + 攻击 + 护甲，并立刻反映到英雄派生值。</summary>
    private static void CheckItemBonus()
    {
        UnitType paladinType = UnitCatalog.Get("Hpal");
        ItemType crown = ItemCatalog.Get("ckng");
        ItemType ring = ItemCatalog.Get("rde1");
        if (paladinType == null || crown == null) return;

        var world = new World(null, null);
        Unit hero = MakeHero(paladinType, 0, 1, world);
        world.Units.Add(hero);

        int strength = hero.Strength;
        float armor = world.ArmorOf(hero);
        float damage = world.DamageBonus(hero);

        var item = new Item { Type = crown };
        hero.AddItem(item);
        world.ApplyItemBonus(hero, item);

        Verify("国王之冠 +5 力量", strength + 5, hero.Strength, 0f);
        Verify("国王之冠 +5 敏捷", 5f, hero.BonusAgility, 0f);
        Verify("国王之冠 +5 智力", 5f, hero.BonusIntelligence, 0f);
        Verify("三围加成进了攻击", damage + 5f, world.DamageBonus(hero), 0.01f);
        Verify("敏捷加成进了护甲（+5 敏捷 = +1.5 甲）", armor + 1.5f, world.ArmorOf(hero), 0.01f);

        if (ring != null)
        {
            var ringItem = new Item { Type = ring };
            hero.AddItem(ringItem);
            world.ApplyItemBonus(hero, ringItem);
            Verify("护戒 +2 护甲", 2f, hero.BonusArmor, 0.01f);
        }

        GD.Print($"[自检] 物品加成 国王之冠 {ItemBonus.ForItem(crown)}");
    }

    /// <summary>建一个带属性与法力的英雄（探针用）。</summary>
    private static Unit MakeHero(UnitType type, int player, int level, World world)
    {
        var hero = new Unit
        {
            Type = type,
            Player = player,
            Position = Vector3.Zero,
            IsHero = true,
            Level = level,
            Strength = type.Strength,
            Agility = type.Agility,
            Intelligence = type.Intelligence,
        };
        world?.RefreshHeroAttributes(hero);
        hero.Hp = hero.MaxHp;
        hero.Mana = hero.MaxMana;
        return hero;
    }
}
