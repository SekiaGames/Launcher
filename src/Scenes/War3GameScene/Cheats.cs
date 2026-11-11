using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class War3GameScene
{
    public void ScriptedMoveTest()
    {
        int moved = 0;
        var target = new Vector3();
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != ViewPlayer || unit.IsBuilding || !unit.Type.CanAttack) continue;
            if (moved == 0)
            {
                target = unit.Position + new Vector3(500f, 0f, 500f);
                target.Y = MapWorldBuilder.HeightAt(_map.Terrain, target.X, target.Z);
            }
            _selected.Add(unit);
            unit.View?.SetSelected(true);
            unit.MoveTarget = target + new Vector3(60f * moved, 0f, 0f);
            moved++;
            if (moved >= 4) break;
        }
        _hud?.SetSelection(_selected);
        _hud?.SetHint($"脚本测试：{moved} 个单位向 {target.X:0},{target.Z:0} 移动");
    }

    public void RunClickTest()
    {
        SceneTreeTimer selectTimer = GetTree().CreateTimer(2.0);
        selectTimer.Timeout += () =>
        {
            Unit target = null;
            foreach (Unit unit in _allUnits)
            {
                if (unit.Alive && unit.Player == ViewPlayer && !unit.IsBuilding) { target = unit; break; }
            }
            if (target?.View == null) return;

            Vector3 point = target.Position + new Vector3(0f, target.View.Height * 0.5f, 0f);
            Vector2 screen = _camera.Camera.UnprojectPosition(point);
            PushClick(screen, MouseButton.Left);
        };

        SceneTreeTimer orderTimer = GetTree().CreateTimer(3.5);
        orderTimer.Timeout += () =>
        {
            Vector3 point = _camera.Target + new Vector3(-500f, 0f, 400f);
            point.Y = MapWorldBuilder.HeightAt(_map.Terrain, point.X, point.Z);
            Vector2 screen = _camera.Camera.UnprojectPosition(point);
            PushClick(screen, MouseButton.Right);
        };
    }

    public void ScriptedGatherTest()
    {
        Unit worker = FindWorker(ViewPlayer);
        if (worker == null)
        {
            GD.Print("[TEST] 没有农民");
            return;
        }

        Unit mine = null;
        float best = float.MaxValue;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Gold <= 0) continue;
            float distance = worker.DistanceTo(unit);
            if (distance < best)
            {
                best = distance;
                mine = unit;
            }
        }
        if (mine == null)
        {
            GD.Print("[TEST] 没有金矿");
            return;
        }

        ClearSelection();
        _selected.Add(worker);
        worker.View?.SetSelected(true);
        _world.SetGather(worker, mine);
        _hud?.SetSelection(_selected);
        _hud?.SetHint($"采集测试：{worker.Type.Name} → 金矿（{mine.Gold} 金，距离 {best:0}）");
        GD.Print($"[TEST] 采集 {worker.Type.Name} → 金矿 距离 {best:0} 储量 {mine.Gold}");
    }

    public void ScriptedTrainTest()
    {
        Unit hall = FindHall(ViewPlayer);
        if (hall == null || hall.Type.Trains.Length == 0)
        {
            GD.Print("[TEST] 没有可训练的主基地");
            return;
        }

        ClearSelection();
        _selected.Add(hall);
        hall.View?.SetSelected(true);
        foreach (Unit unit in _allUnits)
        {
            if (_selected.Count >= 8) break;
            if (!unit.Alive || unit.Player != ViewPlayer || unit.IsBuilding) continue;
            _selected.Add(unit);
            unit.View?.SetSelected(true);
        }
        _hud?.SetSelection(_selected);
        UpdateCommandCard();
        PlaySelectionVoice();
        StartTraining(hall, hall.Type.Trains[0]);
        StartTraining(hall, hall.Type.Trains[0]);
    }

    public void ScriptedMoveTo(float mapX, float mapY)
    {
        float worldX = -(mapX - _map.Terrain.OffsetX);
        float worldZ = mapY - _map.Terrain.OffsetY;
        var goal = new Vector3(worldX, MapWorldBuilder.HeightAt(_map.Terrain, worldX, worldZ), worldZ);

        int moved = 0;
        ClearSelection();
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != ViewPlayer || unit.IsBuilding) continue;
            unit.GoldMine = null;
            unit.Target = null;
            unit.Path = null;
            unit.MoveTarget = goal;
            _selected.Add(unit);
            unit.View?.SetSelected(true);
            moved++;
            if (moved >= 6) break;
        }
        _hud?.SetSelection(_selected);
        GD.Print($"[TEST] 移动测试单位 {moved} 个");
    }

    public void PathProbe()
    {
        if (_pathGrid == null || !_pathGrid.Ready)
        {
            GD.Print("[PROBE] 寻路网格为空");
            return;
        }

        Unit worker = FindWorker(ViewPlayer);
        if (worker == null) return;

        for (int i = 0; i < 8; i++)
        {
            float angle = Mathf.Tau * i / 8f;
            Vector3 goal = worker.Position + new Vector3(Mathf.Cos(angle) * 600f, 0f, Mathf.Sin(angle) * 600f);
            List<Vector3> path = _pathGrid.FindPath(worker.Position, goal, 28f);
            GD.Print($"[PROBE] 方向{i} 终点{goal.X:0},{goal.Z:0} 阻挡={_pathGrid.IsBlockedWorld(goal.X, goal.Z)} 航点={path?.Count ?? -1}");
        }
    }

    public void ScriptedLumberTest()
    {
        Unit worker = FindWorker(ViewPlayer);
        if (worker == null || _trees.Count == 0)
        {
            GD.Print("[TEST] 没有农民或树");
            return;
        }

        TreeState tree = null;
        float best = float.MaxValue;
        foreach (TreeState candidate in _trees)
        {
            if (!candidate.Alive) continue;
            float distance = new Vector2(worker.Position.X - candidate.Position.X, worker.Position.Z - candidate.Position.Z).Length();
            if (distance < best) { best = distance; tree = candidate; }
        }
        if (tree == null) return;

        ClearSelection();
        _selected.Add(worker);
        worker.View?.SetSelected(true);
        _world.SetChop(worker, tree);
        _hud?.SetSelection(_selected);
        _hud?.SetHint($"伐木测试：{worker.Type.Name} → 树 HP {tree.Hp:0} 距离 {best:0}");
    }

    public void ScriptedBuildTest()
    {
        Unit worker = FindWorker(ViewPlayer);
        Unit hall = FindHall(ViewPlayer);
        if (worker == null || hall == null)
        {
            GD.Print("[TEST] 缺少农民或基地");
            return;
        }

        ClearSelection();
        _selected.Add(worker);
        worker.View?.SetSelected(true);
        _hud?.SetSelection(_selected);

        string buildingId = null;
        foreach (string candidate in worker.Type.Builds)
        {
            UnitType type = UnitCatalog.Get(candidate);
            if (type == null) continue;
            if (candidate == hall.Type.Id) continue;
            if (type.GoldCost > 0 || type.LumberCost > 0) { buildingId = candidate; break; }
        }
        if (buildingId == null)
        {
            GD.Print("[TEST] 农民没有可建造项目");
            return;
        }

        UnitType buildType = UnitCatalog.Get(buildingId);
        if (!TryFindBuildSpot(hall.Position, Mathf.Max(32f, buildType?.FootprintRadius ?? 96f), out Vector3 ground))
        {
            GD.Print("[TEST] 找不到可建造位置");
            return;
        }

        Unit site = PlaceBuildingAt(Slot(ViewPlayer), buildingId, ground, true);
        if (site == null) return;

        GD.Print($"[TEST] {site.Type.Name} 开工 位置 {ground} 工期 {site.BuildTotal:0}s");
        SceneTreeTimer timer = GetTree().CreateTimer(site.BuildTotal + 0.5f);
        timer.Timeout += () =>
        {
            if (!site.Alive || site.IsUnderConstruction) return;
            ClearSelection();
            _selected.Add(site);
            site.View?.SetSelected(true);
            _hud?.SetSelection(_selected);
            UpdateCommandCard();
            if (site.Type.Trains.Length > 0) StartTraining(site, site.Type.Trains[0]);
        };
    }

    /// <summary>--hero-probe：在最近野怪营地放一个圣骑士，验证经验/升级链路。</summary>
    public void RunHeroProbe()
    {
        Unit creep = null;
        float bestDistance = float.MaxValue;
        Unit hall = FindHall(ViewPlayer);
        Vector3 origin = hall != null ? hall.Position : Vector3.Zero;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != 12) continue;
            float distance = new Vector2(unit.Position.X - origin.X, unit.Position.Z - origin.Z).Length();
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            creep = unit;
        }
        if (creep == null)
        {
            GD.Print("[HERO] 没有野怪可打");
            return;
        }

        Unit hero = Spawn("Hpal", ViewPlayer, creep.Position.X + 220f, creep.Position.Z + 220f, 0f, 0f, 1f);
        if (hero == null)
        {
            GD.Print("[HERO] 英雄模型缺失");
            return;
        }

        _world.Units.Add(hero);
        GD.Print($"[HERO] 圣骑士 生命{hero.MaxHp:0} 法力{hero.MaxMana:0} 力量{hero.Strength} 敏捷{hero.Agility} 智力{hero.Intelligence}"
            + $" 攻击{hero.Type.MinDamage + _world.DamageBonus(hero):0}-{hero.Type.MaxDamage + _world.DamageBonus(hero):0}");

        float elapsed = 0f;
        void TickHero()
        {
            elapsed += 1f;
            GD.Print($"[HERO] t={elapsed:0}s 等级={hero.Level} 经验={hero.Xp:0}/{HeroRules.XpToNext(hero.Level)}"
                + $" 生命={hero.Hp:0}/{hero.MaxHp:0} 力量={hero.Strength} 技能点={hero.SkillPoints}");
            if (elapsed >= 30f) return;
            if (elapsed % 5f < 0.5f) _world.AddExperience(hero, 200);   // 探针：模拟击杀经验，验证升级与属性重算
            SceneTreeTimer timer = GetTree().CreateTimer(1f);
            timer.Timeout += TickHero;
        }

        SceneTreeTimer first = GetTree().CreateTimer(1f);
        first.Timeout += TickHero;
    }

    public void RunCreepProbe()
    {
        Unit creep = null;
        float bestDistance = float.MaxValue;
        Unit hall = FindHall(ViewPlayer);
        Vector3 origin = hall != null ? hall.Position : Vector3.Zero;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != 12) continue;
            float distance = new Vector2(unit.Position.X - origin.X, unit.Position.Z - origin.Z).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                creep = unit;
            }
        }
        if (creep == null)
        {
            GD.Print("[PROBE] 没有野怪");
            return;
        }

        var baitPosition = new Vector3(creep.Position.X + 350f, creep.Position.Y, creep.Position.Z);
        Unit bait = Spawn("hrif", ViewPlayer, baitPosition.X, baitPosition.Z, 0f, 0f, 1f);
        if (bait == null) bait = Spawn("hfoo", ViewPlayer, baitPosition.X, baitPosition.Z, 0f, 0f, 1f);
        if (bait == null)
        {
            GD.Print("[PROBE] 假人模型缺失");
            return;
        }
        _world.Units.Add(bait);

        Unit melee = Spawn("hfoo", ViewPlayer, baitPosition.X + 40f, baitPosition.Z - 60f, 0f, 0f, 1f);
        if (melee != null) _world.Units.Add(melee);

        GD.Print($"[PROBE] 目标野怪 {creep.Type.Id} 距基地 {bestDistance:0}，假人放在 350 外");
        float elapsed = 0f;
        const float step = 1f;
        const float pullAt = 6f;

        void TickProbe()
        {
            elapsed += step;
            float toGuard = new Vector2(creep.Position.X - creep.Home.X, creep.Position.Z - creep.Home.Z).Length();
            string state = creep.Returning ? "回营" : creep.ReturnDelay > 0f ? $"等待{creep.ReturnDelay:0.0}s" : creep.Target != null ? "追击" : "待机";
            GD.Print($"[PROBE] t={elapsed:0}s 状态={state} 距营地={toGuard:0} 距假人={creep.DistanceTo(bait):0} HP={creep.Hp:0}"
                + $" 假人 目标={bait.Target?.Type.Id ?? "无"} 目标HP={bait.Target?.Hp:0} 射程={bait.Type.AttackRange:0} 弹道={bait.Type.MissileScene ?? "无"}");

            if (Mathf.Abs(elapsed - pullAt) < 0.01f)
            {
                bait.Position = new Vector3(creep.Home.X + 1400f, bait.Position.Y, creep.Home.Z);
                bait.View.Root.Position = bait.Position;
                bait.Path = null;
                GD.Print("[PROBE] 把假人拉出 1400，观察放弃追击与回营");
            }

            if (elapsed >= 24f)
            {
                foreach (Unit extra in new[] { bait, melee })
                {
                    if (extra == null) continue;
                    extra.Alive = false;
                    extra.View?.Root.QueueFree();
                    _allUnits.Remove(extra);
                    _world.Units.Remove(extra);
                }
                GD.Print("[PROBE] 结束");
                return;
            }
            SceneTreeTimer timer = GetTree().CreateTimer(step);
            timer.Timeout += TickProbe;
        }

        SceneTreeTimer first = GetTree().CreateTimer(step);
        first.Timeout += TickProbe;
    }

    /// <summary>--spawn-item=xxx,yyy：在开局焦点附近摆几件物品，方便手测拾取与物品栏。</summary>
    public void SpawnDebugItems(IReadOnlyList<string> ids)
    {
        Unit hall = FindHall(ViewPlayer);
        Vector3 origin = hall != null ? hall.Position : Vector3.Zero;

        int index = 0;
        foreach (string id in ids)
        {
            if (!ItemCatalog.Exists(id))
            {
                GD.Print($"[ITEM] 未知物品 {id}");
                continue;
            }
            var position = new Vector3(origin.X + 60f + index * 70f, 0f, origin.Z + 140f);
            position.Y = MapWorldBuilder.HeightAt(_map.Terrain, position.X, position.Z);
            Item item = SpawnItem(id, position);
            if (item == null) continue;
            GD.Print($"[ITEM] 摆放 {item.Type.Name}({id}) Lv{item.Type.Level} 于 ({position.X:0},{position.Z:0})");
            index++;
        }
    }

    /// <summary>
    /// --revive-probe：在自家基地摆一个祭坛与一个圣骑士，当场击杀，
    /// 验证「英雄阵亡 → 排队扣金 → 倒计时 → 祭坛旁满血复活」整条链路。
    /// </summary>
    public void RunReviveProbe()
    {
        Unit hall = FindHall(ViewPlayer);
        if (hall == null)
        {
            GD.Print("[REVIVE] 找不到主基地，无法摆放祭坛");
            return;
        }

        Unit altar = Spawn("halt", ViewPlayer, hall.Position.X + 420f, hall.Position.Z + 120f, 0f, 0f, 1f);
        if (altar == null)
        {
            GD.Print("[REVIVE] 祭坛模型缺失");
            return;
        }
        _world.Units.Add(altar);

        Unit hero = Spawn("Hpal", ViewPlayer, hall.Position.X + 120f, hall.Position.Z + 120f, 0f, 0f, 1f);
        if (hero == null)
        {
            GD.Print("[REVIVE] 英雄模型缺失");
            return;
        }
        _world.Units.Add(hero);
        hero.Level = 3;
        _world.GrantKillExperience(null, hero);

        GD.Print($"[REVIVE] 准备击杀 P{ViewPlayer} {hero.Type.Name} Lv{hero.Level}"
            + $" 造价{hero.Type.GoldCost} 训练时长{hero.Type.TrainSeconds:0}s"
            + $" 预计花费{_world.HeroReviveCostOf(hero)} 预计时间{_world.HeroReviveSecondsOf(hero):0}s");

        DamageInfo lethal = World.MakeAbilityDamage(null, hero, 100000f, AttackType.Spells, DamageType.Universal);
        _world.DealDamage(lethal);
        GD.Print($"[REVIVE] 击杀完成 存活={hero.Alive} 留尸={hero.IsCorpse} 待复活={hero.AwaitingRevive}");

        SceneTreeTimer watch = GetTree().CreateTimer(6.0);
        watch.Timeout += () =>
        {
            GD.Print($"[REVIVE] 6s 后 存活={hero.Alive} 倒计时={hero.ReviveRemaining:0.0}/{hero.ReviveTotal:0.0}"
                + $" 祭坛={(hero.ReviveAltar != null ? hero.ReviveAltar.Type.Name : "无")}");
        };
    }

    /// <summary>
    /// --skill-probe：在最近野怪旁放一个圣骑士，学 3 个技能并当场施放，
    /// 验证 技能点→学习→命令卡→施法 整条链路。
    /// </summary>
    public void RunSkillProbe()
    {
        Unit creep = null;
        float bestDistance = float.MaxValue;
        Unit hall = FindHall(ViewPlayer);
        Vector3 origin = hall != null ? hall.Position : Vector3.Zero;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != 12) continue;
            float distance = new Vector2(unit.Position.X - origin.X, unit.Position.Z - origin.Z).Length();
            if (distance < bestDistance)
            {
                bestDistance = distance;
                creep = unit;
            }
        }

        if (creep == null)
        {
            GD.Print("[SKILL] 没有野怪可打");
            return;
        }

        Unit hero = Spawn("Hpal", ViewPlayer, creep.Position.X + 260f, creep.Position.Z + 260f, 0f, 0f, 1f);
        if (hero == null)
        {
            GD.Print("[SKILL] 英雄模型缺失");
            return;
        }

        _world.Units.Add(hero);
        hero.Level = 6;
        hero.SkillPoints = 3;   // 学 2 个技能后还剩 1 点 → 命令卡上会出现「+」升级标记
        _world.RefreshHeroAttributes(hero);
        hero.Hp = hero.MaxHp;
        hero.Mana = hero.MaxMana;

        GD.Print($"[SKILL] 圣骑士 Lv{hero.Level} 法力{hero.Mana:0}/{hero.MaxMana:0} 技能点{hero.SkillPoints}"
            + $" 可学 [{string.Join(",", hero.Type.HeroAbilList)}]");

        // 只学前两个，留 1 点给命令卡的「+」升级标记
        int learnt = 0;
        foreach (string id in hero.Type.HeroAbilList)
        {
            if (learnt++ >= 2) break;
            AbilityType type = AbilityCatalog.Get(id);
            if (type == null) continue;
            bool ok = _world.LearnAbility(hero, id);
            Ability ability = hero.FindAbility(id);
            GD.Print($"[SKILL] 学习 {id} {type.Name} → {(ok ? $"Lv{ability.Level}" : "失败")}"
                + $"（{type.MaxLevel} 级，法力 {type.CostAt(1):0} 冷却 {type.CooldownAt(1):0}s）");
        }

        // 摆两件物品让英雄自己捡（顺便验装备加成），再演示 使用/丢弃
        foreach (string itemId in new[] { "ckng", "phea" })
        {
            ItemType itemType = ItemCatalog.Get(itemId);
            if (itemType == null) continue;
            var spot = new Vector3(hero.Position.X + 40f, hero.Position.Y, hero.Position.Z + 40f);
            SpawnItem(itemId, spot);
        }
        _world.TickItemPickup();

        _selected.Clear();
        _selected.Add(hero);
        hero.View?.SetSelected(true);
        _hud?.SetSelection(_selected);
        UpdateCommandCard();
        GD.Print($"[SKILL] 命令卡：{DescribeCommandCard(hero)}");

        // 留 3 秒让截图能看到「左技能 / 右物品」的原版布局，再演示使用与丢弃
        SceneTreeTimer itemTimer = GetTree().CreateTimer(3.0);
        itemTimer.Timeout += () => DemoItemUse(hero);

        // 山丘之王演示 单体伤害+眩晕 / 自身 AoE 减速
        Unit mountain = Spawn("Hmkg", ViewPlayer, creep.Position.X + 120f, creep.Position.Z + 120f, 0f, 0f, 1f);
        if (mountain != null)
        {
            _world.Units.Add(mountain);
            mountain.Level = 6;
            mountain.SkillPoints = 4;
            _world.RefreshHeroAttributes(mountain);
            mountain.Hp = mountain.MaxHp;
            mountain.Mana = mountain.MaxMana;
            foreach (string id in mountain.Type.HeroAbilList) _world.LearnAbility(mountain, id);

            Ability bolt = mountain.FindAbility("AHtb");
            if (bolt != null)
            {
                float before = creep.Hp;
                bool cast = _world.TryCast(mountain, bolt, creep, creep.Position);
                GD.Print($"[SKILL] 风暴之锤 {(cast ? "弹道出手" : "失败")} 出手时血量 {before:0}"
                    + $"（飞行中不结算） 法力{mountain.Mana:0}");
                SceneTreeTimer hitTimer = GetTree().CreateTimer(1.2);
                hitTimer.Timeout += () =>
                {
                    GD.Print($"[SKILL] 风暴之锤命中 {creep.Type.Name} {before:0}→{creep.Hp:0}"
                        + $" 眩晕{creep.State.Remaining(StateMod.Stun):0.0}s");
                };
            }

            Ability clap = mountain.FindAbility("AHtc");
            if (clap != null)
            {
                bool cast = _world.TryCast(mountain, clap, null, mountain.Position);
                GD.Print($"[SKILL] 雷霆一击 {(cast ? "施放" : "失败")} 半径{clap.Area:0}"
                    + $" 命中野怪 {creep.Hp:0}/{creep.MaxHp:0} 减速{creep.SlowMovePct * 100f:0}%");
            }

            GD.Print($"[SKILL] 山丘技能栏 " + string.Join("、",
                mountain.Abilities.ConvertAll(a => $"{a.Name}{(a.Learned ? " Lv" + a.Level : " 未学")}")));
        }

        Ability holy = hero.FindAbility("AHhb");
        if (holy != null)
        {
            holy.CooldownRemaining = 0f;
            hero.Hp = Mathf.Max(1f, hero.Hp - 300f);
            float before = hero.Hp;
            bool cast = _world.TryCast(hero, holy, hero, hero.Position);
            GD.Print($"[SKILL] 圣光术自疗 {(cast ? "成功" : "失败（自身不是合法目标）")} {before:0}→{hero.Hp:0}");
        }

        // 商店：地精商店（ngme）摆在英雄旁边，买一瓶药水验证 扣金 → 落地 → 自动拾取
        Unit shop = Spawn("ngme", ViewPlayer, hero.Position.X + 160f, hero.Position.Z + 160f, 0f, 0f, 1f);
        if (shop != null)
        {
            _world.Units.Add(shop);
            PlayerSlot shopSlot = Slot(ViewPlayer);
            int goldBefore = shopSlot?.Gold ?? 0;
            string goods = shop.Type.SellItems.Length > 0 ? string.Join(",", shop.Type.SellItems) : "（无）";
            GD.Print($"[SHOP] {shop.Type.Name} 商品 {goods}，金币 {goldBefore}");

            if (shop.Type.SellItems.Length > 0)
            {
                string first = shop.Type.SellItems[0];
                int bought = 0;
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    if (BuyItemFromShop(shop, ViewPlayer, first)) bought++;
                }
                GD.Print($"[SHOP] 连买 {first} {bought} 次（库存 {shop.StockOf(first)}/{shop.StockMaxOf(first)}）"
                    + $" 金币 {goldBefore}→{shopSlot?.Gold} 背包 {hero.ItemCount} 件 地面 {_world.GroundItems.Count} 件");

                for (int index = 0; index < Unit.InventorySize; index++)
                {
                    Item held = hero.Inventory[index];
                    if (held?.Type == null || held.Type.GoldCost <= 0) continue;
                    int goldBeforeSell = shopSlot?.Gold ?? 0;
                    int price = _world.SellItem(shop, held);
                    if (price <= 0) continue;
                    if (shopSlot != null) shopSlot.Gold += price;
                    GD.Print($"[SHOP] 卖回 {held.Type.Name} +{price} 金 金币 {goldBeforeSell}→{shopSlot?.Gold}"
                        + $" 库存 {shop.StockOf(held.Type.Id)}/{shop.StockMaxOf(held.Type.Id)}");
                    break;
                }
            }
        }

        // 大法师演示召唤通道：水元素由场景层建出来，交给 World 管
        Unit mage = Spawn("Hamg", ViewPlayer, creep.Position.X - 200f, creep.Position.Z + 260f, 0f, 0f, 1f);
        if (mage != null)
        {
            _world.Units.Add(mage);
            mage.Level = 6;
            mage.SkillPoints = 3;
            _world.RefreshHeroAttributes(mage);
            mage.Hp = mage.MaxHp;
            mage.Mana = mage.MaxMana;
            _world.LearnAbility(mage, "AHwe");
            _world.LearnAbility(mage, "AHbz");

            Ability summon = mage.FindAbility("AHwe");
            if (summon != null)
            {
                int before = _world.Units.Count;
                bool cast = _world.TryCast(mage, summon, null, mage.Position + new Vector3(90f, 0f, 0f));
                Unit elemental = _world.Units.Count > before ? _world.Units[^1] : null;
                GD.Print($"[SKILL] 水元素召唤 {(cast ? "成功" : "失败")} 单位数 {before}→{_world.Units.Count}"
                    + (elemental != null ? $" 存在 {elemental.SummonRemaining:0}s" : ""));
            }
            // 暴风雪：点目标 + 引导，落点就放野怪脚下
            Ability blizzard = mage.FindAbility("AHbz");
            if (blizzard != null)
            {
                mage.Mana = mage.MaxMana;
                Vector3 spot = new(creep.Position.X, creep.Position.Y, creep.Position.Z);
                float hpBefore = creep.Hp;
                bool started = _world.TryCast(mage, blizzard, null, spot);
                GD.Print($"[SKILL] 暴风雪 {(started ? "开始引导" : "施放失败")} 落点({spot.X:0},{spot.Z:0})"
                    + $" 波数{(blizzard as Blizzard)?.Waves} 半径{(blizzard as Blizzard)?.Radius:0}"
                    + $" 引导{blizzard.Duration:0}s 野怪血量 {hpBefore:0}");

                SceneTreeTimer waveTimer = GetTree().CreateTimer(3.4);
                waveTimer.Timeout += () =>
                {
                    GD.Print($"[SKILL] 暴风雪结束 野怪 {hpBefore:0}→{creep.Hp:0}"
                        + $" 引导中={mage.IsChanneling} 剩余引导任务={_world.ChannelCount}");
                };
            }

        }

        // 守望者：点地面闪烁（点目标 + 瞬移 + 0.8×上限钳制）
        Unit warden = Spawn("Ewar", ViewPlayer, hero.Position.X - 260f, hero.Position.Z - 120f, 0f, 0f, 1f);
        if (warden != null)
        {
            _world.Units.Add(warden);
            warden.Level = 6;
            warden.SkillPoints = 2;
            _world.RefreshHeroAttributes(warden);
            warden.Hp = warden.MaxHp;
            warden.Mana = warden.MaxMana;
            _world.LearnAbility(warden, "AEbl");

            Ability blink = warden.FindAbility("AEbl");
            var blinkAbility = blink as Blink;
            if (blinkAbility != null)
            {
                // 落点必须能站人，绕一圈找一个合法方向再闪（不能站就原地不动）
                Vector3 from = warden.Position;
                Vector3 spot = from;
                bool found = false;
                for (int step = 0; step < 8 && !found; step++)
                {
                    float angle = Mathf.Tau * step / 8f;
                    var candidate = new Vector3(
                        from.X + Mathf.Cos(angle) * 420f,
                        from.Y,
                        from.Z + Mathf.Sin(angle) * 420f);
                    if (!blinkAbility.CheckTarget(_world, null, candidate)) continue;
                    spot = candidate;
                    found = true;
                }

                bool cast = found && _world.TryCast(warden, blink, null, spot);
                GD.Print($"[SKILL] 闪烁 {(cast ? "成功" : "无合法落点")}"
                    + $" 实移 ({warden.Position.X - from.X:0},{warden.Position.Z - from.Z:0})"
                    + $"（上限 {blinkAbility.MaximumRange:0} 最近 {blinkAbility.MinimumRange:0}，绕 8 个方向找落点）");
            }
        }

        // 尸体目标：腐尸甲虫这类取材技能必须能通过命令卡点到尸体
        DemoCorpseTarget(creep);

        // 远视：把一块原本看不见的区域刷成可见（World.Reveals → FogOfWar.Refresh）
        DemoFarSight(creep);

        // 阵型：选中近战+远程一起右键地面，验证近战站前排、远程站后排
        DemoGroupMove(creep);

        // 拉扯：远程被贴脸后退到射程边缘
        DemoKite(creep);

        GD.Print($"[SKILL] 技能栏 {hero.Abilities.Count} 个："
            + string.Join("、", hero.Abilities.ConvertAll(a => $"{a.Name}{(a.Learned ? " Lv" + a.Level : " 未学")}")));
    }

    /// <summary>
    /// 拉扯：步枪被近战贴到 80，直接跑一遍 AI 的 TickAiKite，看它是否下令后撤、
    /// 1.8 秒后实际距离是不是拉回到了射程附近。
    /// </summary>
    private void DemoKite(Unit creep)
    {
        Unit rifle = Spawn("hrif", ViewPlayer, creep.Position.X + 320f, creep.Position.Z + 320f, 0f, 0f, 1f);
        Unit threat = Spawn("hfoo", 1, creep.Position.X + 400f, creep.Position.Z + 320f, Mathf.Pi, 0f, 1f);
        if (rifle == null || threat == null)
        {
            GD.Print("[KITE] 缺火枪/步兵模型，跳过拉扯验证");
            return;
        }

        _world.Units.Add(rifle);
        _world.Units.Add(threat);
        rifle.Target = threat;
        rifle.KiteCooldown = 0f;

        PlayerSlot slot = Slot(ViewPlayer);
        if (slot == null)
        {
            GD.Print("[KITE] 当前玩家不在参战列表，跳过拉扯验证");
            return;
        }

        float before = rifle.DistanceTo(threat);
        TickAiKite(slot);
        GD.Print($"[KITE] 被贴脸 {(rifle.Kiting ? "已下令后撤" : "未触发")} 距离 {before:0}"
            + $"（射程 {rifle.Type.AttackRange:0}，目标点 {(rifle.MoveTarget.HasValue ? $"({rifle.MoveTarget.Value.X:0},{rifle.MoveTarget.Value.Z:0})" : "无")}）");

        SceneTreeTimer timer = GetTree().CreateTimer(1.8);
        timer.Timeout += () =>
        {
            GD.Print($"[KITE] 1.8 秒后距离 {rifle.DistanceTo(threat):0}（射程 {rifle.Type.AttackRange:0}，还在后撤={rifle.Kiting}）");
        };
    }

    /// <summary>
    /// 编队阵型：真正走玩家右键的 OrderAt 路径（屏幕坐标 → 地面点 → GroupFormation），
    /// 2 秒后看四单位的相对深度，近战必须在远程前面。
    /// </summary>
    private void DemoGroupMove(Unit creep)
    {
        var group = new List<Unit>();
        for (int index = 0; index < 2; index++)
        {
            Unit melee = Spawn("hfoo", ViewPlayer, creep.Position.X - 240f + index * 40f, creep.Position.Z - 120f, 0f, 0f, 1f);
            Unit ranged = Spawn("hrif", ViewPlayer, creep.Position.X - 240f + index * 40f, creep.Position.Z - 220f, 0f, 0f, 1f);
            if (melee != null) { _world.Units.Add(melee); group.Add(melee); }
            if (ranged != null) { _world.Units.Add(ranged); group.Add(ranged); }
        }
        if (group.Count < 2)
        {
            GD.Print("[FORM] 缺步兵/火枪模型，跳过阵型验证");
            return;
        }

        var destination = new Vector3(creep.Position.X, creep.Position.Y, creep.Position.Z + 320f);
        _camera.Focus(destination);

        ClearSelection();
        foreach (Unit unit in group)
        {
            _selected.Add(unit);
            unit.View?.SetSelected(true);
        }
        _hud?.SetSelection(_selected);

        Vector2 screen = _camera.Camera.UnprojectPosition(destination + new Vector3(0f, 40f, 0f));
        OrderAt(screen);

        SceneTreeTimer timer = GetTree().CreateTimer(2.5);
        timer.Timeout += () =>
        {
            float meleeFront = float.MaxValue;
            float rangedFront = float.MaxValue;
            foreach (Unit unit in group)
            {
                if (!unit.Alive) continue;
                float depth = destination.Z - unit.Position.Z;
                if (unit.FormationRank == 0) meleeFront = Mathf.Min(meleeFront, depth);
                else rangedFront = Mathf.Min(rangedFront, depth);
            }

            GD.Print($"[FORM] 阵型落位 近战最近 {meleeFront:0} 远程最近 {rangedFront:0}"
                + $"（间距 成员 {GroupFormation.MemberGap:0} 行 {GroupFormation.RowGap:0} 层 {GroupFormation.RankGap:0}）");
        };
    }

    /// <summary>
    /// 命令卡 → 拾取 全链路验尸体目标：地穴领主学腐尸甲虫，把镜头对准尸体后
    /// 用屏幕坐标走 TryCastAt，与玩家真实点击走同一条代码路径（PickUnit 的 includeCorpses）。
    /// </summary>
    private void DemoCorpseTarget(Unit creep)
    {
        Unit cryptLord = Spawn("Ucrl", ViewPlayer, creep.Position.X + 320f, creep.Position.Z - 260f, 0f, 0f, 1f);
        Unit corpse = Spawn("hfoo", ViewPlayer, creep.Position.X + 260f, creep.Position.Z - 200f, 0f, 0f, 1f);
        if (cryptLord == null || corpse == null)
        {
            GD.Print("[CORPSE] 缺模型（Ucrl/hfoo），跳过尸体目标验证");
            return;
        }

        _world.Units.Add(cryptLord);
        _world.Units.Add(corpse);
        cryptLord.Level = 6;
        cryptLord.SkillPoints = 2;
        _world.RefreshHeroAttributes(cryptLord);
        cryptLord.Hp = cryptLord.MaxHp;
        cryptLord.Mana = cryptLord.MaxMana;
        _world.LearnAbility(cryptLord, "AUcb");

        Ability scarabs = cryptLord.FindAbility("AUcb");
        if (scarabs == null)
        {
            GD.Print("[CORPSE] 腐尸甲虫技能没学会");
            return;
        }

        corpse.EnterDeath();
        _camera.Focus(corpse.Position);

        ClearSelection();
        _selected.Add(cryptLord);
        cryptLord.View?.SetSelected(true);
        _hud?.SetSelection(_selected);
        UpdateCommandCard();

        BeginCast(cryptLord, scarabs);
        float lift = corpse.View != null ? corpse.View.Height * 0.5f : 60f;
        Vector2 screen = _camera.Camera.UnprojectPosition(corpse.Position + new Vector3(0f, lift, 0f));
        bool clicked = TryCastAt(screen);

        int beetles = 0;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Type == null) continue;
            if (!unit.Type.Id.StartsWith("ucs", StringComparison.OrdinalIgnoreCase)) continue;
            beetles++;
        }

        GD.Print($"[CORPSE] 命令卡点尸体 点击命中={clicked} 尸体已消耗={!corpse.IsCorpse}"
            + $" 召出甲虫={beetles} 屏幕点({screen.X:0},{screen.Y:0}) 技能={scarabs.Name}（尸体目标={scarabs.TargetsCorpse}）");
    }

    /// <summary>
    /// 远视整条链路：施法往 World.Reveals 里记一块区域 → FogOfWar.Refresh 接受揭示区域 → IsVisible 变 true。
    /// 不靠截图，直接查雾格。
    /// </summary>
    private void DemoFarSight(Unit anchor)
    {
        Unit seer = Spawn("Ofar", ViewPlayer, anchor.Position.X + 120f, anchor.Position.Z + 200f, 0f, 0f, 1f);
        if (seer == null)
        {
            GD.Print("[SIGHT] 缺先知模型，跳过远视验证");
            return;
        }

        _world.Units.Add(seer);
        seer.Level = 6;
        seer.SkillPoints = 2;
        _world.RefreshHeroAttributes(seer);
        seer.Hp = seer.MaxHp;
        seer.Mana = seer.MaxMana;
        _world.LearnAbility(seer, "AOfs");

        Ability sight = seer.FindAbility("AOfs");
        if (sight == null)
        {
            GD.Print("[SIGHT] 远视没学会");
            return;
        }

        float maxX = -(_map.Terrain.Width - 1) * MapWorldBuilder.CellSize + 320f;
        float maxZ = (_map.Terrain.Height - 1) * MapWorldBuilder.CellSize - 320f;
        Vector3 spot = default;
        bool found = false;
        for (int step = 0; step < 16 && !found; step++)
        {
            float angle = Mathf.Tau * step / 16f;
            var candidate = new Vector3(
                seer.Position.X + Mathf.Cos(angle) * 1500f,
                seer.Position.Y,
                seer.Position.Z + Mathf.Sin(angle) * 1500f);
            if (candidate.X > -320f || candidate.X < maxX) continue;
            if (candidate.Z < 320f || candidate.Z > maxZ) continue;
            if (_fog.IsVisible(candidate)) continue;
            spot = candidate;
            found = true;
        }

        if (!found)
        {
            GD.Print("[SIGHT] 附近找不到看不见的落点，跳过");
            return;
        }

        bool before = _fog.IsVisible(spot);
        bool cast = _world.TryCast(seer, sight, null, spot);
        _fog.Refresh(_allUnits, ViewPlayer, AreAllied, SightOf, _world.Reveals);
        bool after = _fog.IsVisible(spot);
        GD.Print($"[SIGHT] 远视 {(cast ? "成功" : "失败")} 落点({spot.X:0},{spot.Z:0}) 半径{sight.Area:0}"
            + $" 雾前={before} 雾后={after} 揭示区域={_world.Reveals.Count}");
    }

    private void DemoItemUse(Unit hero)
    {
        if (hero == null || _world == null) return;

        foreach (Item picked in hero.Inventory)
        {
            if (picked?.Type == null) continue;
            if (!picked.Type.Consumable.IsEmpty)
            {
                hero.Hp = Mathf.Max(1f, hero.Hp - 300f);
                float hpBefore = hero.Hp;
                bool used = _world.UseItem(hero, picked);
                GD.Print($"[SKILL] 使用 {picked.Type.Name} {(used ? "成功" : "失败")} {hpBefore:0}→{hero.Hp:0}"
                    + $" 剩余 {hero.ItemCount} 件");
            }
        }

        for (int slot = 0; slot < Unit.InventorySize; slot++)
        {
            Item held = hero.Inventory[slot];
            if (held?.Type == null) continue;
            int strengthBefore = hero.Strength;
            bool dropped = _world.DropItemFromSlot(hero, slot);
            GD.Print($"[SKILL] 丢弃 {held.Type.Name} {(dropped ? "成功" : "失败")}"
                + $" 力量 {strengthBefore}→{hero.Strength} 地面 {_world.GroundItems.Count} 件");
            break;
        }
    }

    /// <summary>
    /// --morph-probe：妖术变羊 + 恶魔变身。验证「换类型 → 重建模型节点 → 到期还原」整条链路，
    /// 每次变形都会打印新的模型节点名（morph_xxx），说明场景层确实重建过。
    /// </summary>
    public void RunMorphProbe()
    {
        Unit creep = null;
        float bestDistance = float.MaxValue;
        Unit hall = FindHall(ViewPlayer);
        Vector3 origin = hall != null ? hall.Position : Vector3.Zero;
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != 12) continue;
            float distance = new Vector2(unit.Position.X - origin.X, unit.Position.Z - origin.Z).Length();
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            creep = unit;
        }
        if (creep == null)
        {
            GD.Print("[MORPH] 没有野怪可打");
            return;
        }

        Unit hexer = Spawn("Oshd", ViewPlayer, creep.Position.X + 240f, creep.Position.Z + 240f, 0f, 0f, 1f);
        if (hexer == null)
        {
            GD.Print("[MORPH] 暗影猎手模型缺失");
            return;
        }

        _world.Units.Add(hexer);
        hexer.Level = 6;
        hexer.SkillPoints = 3;
        _world.RefreshHeroAttributes(hexer);
        hexer.Hp = hexer.MaxHp;
        hexer.Mana = hexer.MaxMana;
        _world.LearnAbility(hexer, "AOhx");
        _world.LearnAbility(hexer, "AOhx");

        Ability hex = hexer.FindAbility("AOhx");
        string originId = creep.Type.Id;
        string originModel = creep.View?.Root?.Name ?? "无";
        float originRatio = creep.Hp / Mathf.Max(1f, creep.MaxHp);
        bool cast = hex != null && _world.TryCast(hexer, hex, creep, creep.Position);
        GD.Print($"[MORPH] 妖术 {(cast ? "成功" : "失败")} {originId} → {creep.Type.Id}（{creep.Type.Name}）"
            + $" 模型 {originModel} → {creep.View?.Root?.Name}"
            + $" 生命比例 {originRatio:0.00}→{creep.Hp / Mathf.Max(1f, creep.MaxHp):0.00}"
            + $" 可攻击={creep.State.CanAttack} 可施法={creep.State.CanCast}");

        SceneTreeTimer restoreTimer = GetTree().CreateTimer(4.0);
        restoreTimer.Timeout += () =>
        {
            _world.EndMorph(creep);
            GD.Print($"[MORPH] 妖术手动还原 → {creep.Type.Id}（{creep.Type.Name}）模型 {creep.View?.Root?.Name}"
                + $" 生命 {creep.Hp:0}/{creep.MaxHp:0} 可攻击={creep.State.CanAttack}");
        };

        Unit demon = Spawn("Edem", ViewPlayer, creep.Position.X - 240f, creep.Position.Z + 240f, 0f, 0f, 1f);
        if (demon == null)
        {
            GD.Print("[MORPH] 恶魔猎手模型缺失");
            return;
        }

        _world.Units.Add(demon);
        demon.Level = 6;
        demon.SkillPoints = 3;
        _world.RefreshHeroAttributes(demon);
        demon.Hp = demon.MaxHp;
        demon.Mana = demon.MaxMana;
        _world.LearnAbility(demon, "AEme");

        Ability transform = demon.FindAbility("AEme");
        float demonHp = demon.MaxHp;
        bool morphed = transform != null && _world.TryCast(demon, transform, null, demon.Position);
        GD.Print($"[MORPH] 变身 {(morphed ? "成功" : "失败")} Edem → {demon.Type.Id}（{demon.Type.Name}）"
            + $" 生命上限 {demonHp:0}→{demon.MaxHp:0} 模型 {demon.View?.Root?.Name}"
            + $" 可攻击={demon.State.CanAttack} 剩余 {demon.MorphRemaining:0}s");

        SceneTreeTimer demonTimer = GetTree().CreateTimer(6.0);
        demonTimer.Timeout += () =>
        {
            GD.Print($"[MORPH] 变身到期前 类型={demon.Type.Id} 生命 {demon.Hp:0}/{demon.MaxHp:0}"
                + $" 剩余 {demon.MorphRemaining:0.0}s");
            _world.EndMorph(demon);
            GD.Print($"[MORPH] 变身还原 → {demon.Type.Id} 生命上限 {demon.MaxHp:0}"
                + $" 英雄={demon.IsHero} 模型 {demon.View?.Root?.Name}");
        };

        DemoDruidForm(creep);
        DemoPhoenix(creep);
        DemoIllusion(creep);
    }

    /// <summary>
    /// 镜像与疾风步：场景层看幻象真的建出了模型节点、隐身真的挂在状态上且自己眼里只留淡影。
    /// </summary>
    private void DemoIllusion(Unit creep)
    {
        Unit blade = Spawn("Obla", ViewPlayer, creep.Position.X + 620f, creep.Position.Z - 620f, 0f, 0f, 1f);
        if (blade == null)
        {
            GD.Print("[ILLUSION] 缺剑圣模型，跳过镜像验证");
            return;
        }

        _world.Units.Add(blade);
        blade.Level = 6;
        blade.SkillPoints = 3;
        _world.RefreshHeroAttributes(blade);
        blade.Hp = blade.MaxHp;
        blade.Mana = blade.MaxMana;
        _world.LearnAbility(blade, "AOmi");
        _world.LearnAbility(blade, "AOwk");

        Ability mirror = blade.FindAbility("AOmi");
        bool cast = mirror != null && _world.TryCast(blade, mirror, null, blade.Position);

        int images = 0;
        string names = "";
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || !unit.IsIllusion) continue;
            images++;
            names += (unit.View?.Root?.Name ?? "?") + " ";
        }
        GD.Print($"[ILLUSION] 镜像 {(cast ? "成功" : "失败")} 幻象 {images} 个：{names}本体模型 {blade.View?.Root?.Name}");

        Ability wind = blade.FindAbility("AOwk");
        float baseSpeed = World.SpeedOf(blade);
        bool windCast = wind != null && _world.TryCast(blade, wind, null, blade.Position);

        SceneTreeTimer timer = GetTree().CreateTimer(1.2);
        timer.Timeout += () =>
        {
            GD.Print($"[ILLUSION] 疾风步 {(windCast ? "成功" : "失败")} 隐身={blade.State.Invisible}"
                + $" 移速 {baseSpeed:0}→{World.SpeedOf(blade):0} 自己看得到={blade.View?.Root?.Visible}");
        };
    }

    /// <summary>
    /// 凤凰整条链路：召唤 hphx → 生命归零被 Aphx 接管变成 hpxe 蛋 → 模型换成 PhoenixEgg → 10 秒后孵回。
    /// 只看类型/生命/模型节点，不截图。
    /// </summary>
    private void DemoPhoenix(Unit creep)
    {
        Unit mage = Spawn("Hblm", ViewPlayer, creep.Position.X + 520f, creep.Position.Z - 520f, 0f, 0f, 1f);
        if (mage == null)
        {
            GD.Print("[PHOENIX] 缺血法师模型，跳过凤凰验证");
            return;
        }

        _world.Units.Add(mage);
        mage.Level = 6;
        mage.SkillPoints = 2;
        _world.RefreshHeroAttributes(mage);
        mage.Hp = mage.MaxHp;
        mage.Mana = mage.MaxMana;
        _world.LearnAbility(mage, "AHpx");

        Ability phoenix = mage.FindAbility("AHpx");
        if (phoenix == null)
        {
            GD.Print("[PHOENIX] 凤凰大招没学会");
            return;
        }

        bool cast = _world.TryCast(mage, phoenix, null, mage.Position);
        Unit bird = null;
        foreach (Unit unit in _allUnits)
        {
            if (unit != mage && unit.Type != null && unit.Type.Id == "hphx") bird = unit;
        }
        if (bird == null)
        {
            GD.Print($"[PHOENIX] 召唤失败 {(cast ? "" : "（施法返回 false）")}");
            return;
        }

        GD.Print($"[PHOENIX] 召唤成功 {bird.Type.Id} 生命 {bird.Hp:0}/{bird.MaxHp:0}"
            + $" 模型 {bird.View?.Root?.Name} 自带回血 {bird.Type.RegenHp:0}/s");

        _world.DealDamage(World.MakeAbilityDamage(mage, bird, 9999f, AttackType.Spells, DamageType.Magic));
        GD.Print($"[PHOENIX] 生命归零 → 类型 {bird.Type.Id} 生命 {bird.Hp:0}/{bird.MaxHp:0}"
            + $" 存活 {bird.Alive} 模型 {bird.View?.Root?.Name} 待孵化 {_world.PendingPhoenixEggCount}");

        SceneTreeTimer hatch = GetTree().CreateTimer(11.0);
        hatch.Timeout += () =>
        {
            GD.Print($"[PHOENIX] 孵化后 类型 {bird.Type.Id} 生命 {bird.Hp:0}/{bird.MaxHp:0}"
                + $" 模型 {bird.View?.Root?.Name}");
        };
    }

    /// <summary>
    /// 德鲁伊形态（永久）：换类型 → 模型重建 → 技能栏换成新形态 → 再放一次变回。
    /// 全程看模型节点名与技能列表，不截图。
    /// </summary>
    private void DemoDruidForm(Unit creep)
    {
        Unit druid = Spawn("edoc", ViewPlayer, creep.Position.X + 420f, creep.Position.Z - 420f, 0f, 0f, 1f);
        if (druid == null)
        {
            GD.Print("[MORPH] 缺德鲁伊模型，跳过形态验证");
            return;
        }

        _world.Units.Add(druid);
        druid.MaxMana = 200f;
        druid.Mana = 200f;
        Ability form = druid.GiveAbility("Abrf", 1);
        if (form == null)
        {
            GD.Print("[MORPH] 熊形态技能没挂上");
            return;
        }

        string druidModel = druid.View?.Root?.Name ?? "无";
        bool toBear = _world.TryCast(druid, form, null, druid.Position);
        string bearModel = druid.View?.Root?.Name ?? "无";
        string bearSkills = DescribeUnitSkillIds(druid);

        Ability back = druid.FindAbility("Abrf");
        bool restored = back != null && _world.TryCast(druid, back, null, druid.Position);
        GD.Print($"[MORPH] 德鲁伊 {(toBear ? "变熊" : "失败")} {druidModel} → {bearModel}"
            + $" 技能[{bearSkills}]；{(restored ? "变回" : "回退失败")} → {druid.Type.Id} 模型 {druid.View?.Root?.Name}"
            + $" 技能[{DescribeUnitSkillIds(druid)}] 法力 {druid.Mana:0}/200");
    }

    private static string DescribeUnitSkillIds(Unit unit)
    {
        var ids = new List<string>();
        foreach (Ability ability in unit.Abilities) ids.Add(ability.Id);
        return ids.Count == 0 ? "空" : string.Join(",", ids);
    }
}
