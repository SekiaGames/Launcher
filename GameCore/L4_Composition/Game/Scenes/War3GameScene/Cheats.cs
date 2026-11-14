using Godot;
using System;
using System.Collections.Generic;

using OpenWC3.Game.UI;
namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    public void ScriptedOrderTest()
    {
        var movers = new List<Unit>();
        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.Player != ViewPlayer || unit.IsBuilding || unit.IsWorker) continue;
            movers.Add(unit);
            if (movers.Count >= 3) break;
        }
        if (movers.Count == 0)
        {
            foreach (Unit unit in _allUnits)
            {
                if (!unit.Alive || unit.Player != ViewPlayer || unit.IsBuilding) continue;
                movers.Add(unit);
                if (movers.Count >= 3) break;
            }
        }
        if (movers.Count == 0)
        {
            GD.Print("[ORDER] 演示：没有可用单位");
            return;
        }

        ClearSelection();
        foreach (Unit unit in movers)
        {
            _selected.Add(unit);
            unit.View?.SetSelected(true);
        }
        RefreshSelection();
        UpdateCommandCard();

        Vector3 center = _camera.Target;
        Vector3 first = new(center.X + 420f, 0f, center.Z);
        Vector3 second = new(center.X - 420f, 0f, center.Z + 320f);
        first.Y = MapWorldBuilder.HeightAt(_map.Terrain, first.X, first.Z);
        second.Y = MapWorldBuilder.HeightAt(_map.Terrain, second.X, second.Z);

        IssueMoveOrders(first, false);
        IssueMoveOrders(second, true);

        GD.Print($"[ORDER] 演示：{movers.Count} 个单位先到 A({first.X:0},{first.Z:0})"
            + $" 再到 B({second.X:0},{second.Z:0})，队列 {movers[0].PendingOrders.Count} 条");
        _hud?.SetHint("订单演示：先走 A，到达后自动拐去排队的 B；按 S 可随时停止");
    }

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
        RefreshSelection();
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
        RefreshSelection();
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
        RefreshSelection();
        UpdateCommandCard();
        PlaySelectionVoice(_selected.Count > 0 ? _selected[^1] : null, true);
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
        RefreshSelection();
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
        RefreshSelection();
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
        RefreshSelection();

        string buildingId = null;
        foreach (string candidate in worker.Type.Builds)
        {
            UnitType type = UnitFactory.Get(candidate);
            if (type == null) continue;
            if (candidate == hall.Type.Id) continue;
            if (type.GoldCost > 0 || type.LumberCost > 0) { buildingId = candidate; break; }
        }
        if (buildingId == null)
        {
            GD.Print("[TEST] 农民没有可建造项目");
            return;
        }

        UnitType buildType = UnitFactory.Get(buildingId);
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
            RefreshSelection();
            UpdateCommandCard();
            if (site.Type.Trains.Length > 0) StartTraining(site, site.Type.Trains[0]);
        };
    }

    public void RunAttackProbe()
    {
        Unit hall = FindHall(ViewPlayer);
        Vector3 origin = hall != null ? hall.Position : Vector3.Zero;

        Unit attacker = Spawn("hfoo", ViewPlayer, origin.X + 500f, origin.Z, 0f, 0f, 1f);
        Unit victim = Spawn("hfoo", 1, origin.X + 600f, origin.Z, 0f, 0f, 1f);
        if (attacker == null || victim == null)
        {
            GD.Print($"[ATK] 生成失败 attacker={attacker != null} victim={victim != null}");
            return;
        }

        victim.MaxHp = 100000f;
        victim.Hp = 100000f;
        attacker.BonusAttackSpeedPct = 0.5f;

        _world?.Units.Add(attacker);
        _world?.Units.Add(victim);
        attacker.Target = victim;

        GD.Print($"[ATK] 摆好 world=({origin.X + 500f:0},{origin.Z:0}) "
            + $"focus=({-8192f - origin.X - 500f:0},{-8192f + origin.Z:0})");
    }

    public void RunTurretProbe()
    {
        Unit hall = FindHall(ViewPlayer);
        float x = hall != null ? hall.Position.X + 420f : 0f;
        float z = hall != null ? hall.Position.Z : 0f;

        Unit tower = Spawn("hgtw", ViewPlayer, x, z, 0f, 0f, 1f);
        Unit enemy = Spawn("hfoo", 1, x, z - 520f, 0f, 0f, 1f);
        if (tower != null) _world?.Units.Add(tower);
        if (enemy != null) _world?.Units.Add(enemy);
        if (tower != null && enemy != null)
        {
            tower.Target = enemy;
            GD.Print($"[TURRET] 塔={tower.TypeId} 目标={enemy.TypeId} @({x:0},{z:0})");
        }
        else
        {
            GD.Print($"[TURRET] 生成失败 tower={tower != null} enemy={enemy != null}");
        }
    }

    public void RunFlyProbe()
    {
        Unit hall = FindHall(ViewPlayer);
        Vector3 origin = hall != null ? hall.Position : Vector3.Zero;

        Spawn("hfoo", ViewPlayer, origin.X + 260f, origin.Z, 0f, 0f, 1f);
        Spawn("hgry", ViewPlayer, origin.X + 360f, origin.Z, 0f, 0f, 1f);
        Spawn("hgyr", ViewPlayer, origin.X + 460f, origin.Z, 0f, 0f, 1f);

        GD.Print($"[FLY] 步兵/狮鹫/飞行机器 @({origin.X:0},{origin.Z:0})");
    }

    public void RunAppearanceProbe()
    {
        Unit hall = FindHall(ViewPlayer);
        Vector3 origin = hall != null ? hall.Position : Vector3.Zero;

        Unit ethereal = Spawn("ugho", ViewPlayer, origin.X + 300f, origin.Z - 200f, 0f, 0f, 1f);
        ethereal?.State.Add(StateMod.Ethereal);

        Unit illusion = Spawn("ugho", ViewPlayer, origin.X + 300f, origin.Z, 0f, 0f, 1f);
        if (illusion != null) illusion.IsIllusion = true;

        Unit invisible = Spawn("ugho", ViewPlayer, origin.X + 300f, origin.Z + 200f, 0f, 0f, 1f);
        invisible?.State.Add(StateMod.Invisible);

        GD.Print($"[APPEAR] 虚无={ethereal?.TypeId} 幻象={illusion?.TypeId} 隐身={invisible?.TypeId} @({origin.X:0},{origin.Z:0})");
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
    public void RunCardProbe()
    {
        Unit hall = FindHall(ViewPlayer);
        Vector3 origin = hall != null ? hall.Position : Vector3.Zero;
        Unit hero = Spawn("Hpal", ViewPlayer, origin.X + 240f, origin.Z + 240f, 0f, 0f, 1f);
        if (hero == null)
        {
            GD.Print("[CARD] 英雄模型缺失");
            return;
        }

        _world.Units.Add(hero);
        hero.Level = 6;
        hero.SkillPoints = 3;
        _world.RefreshHeroAttributes(hero);
        hero.Hp = hero.MaxHp;
        hero.Mana = hero.MaxMana;
        _world.LearnAbility(hero, "AHhb");
        _world.LearnAbility(hero, "AHds");

        var spawnedItems = new List<Item>();
        foreach (string itemId in new[] { "ratc", "rat9", "phea", "pman", "pomn", "ankh" })
        {
            if (!ItemCatalog.Exists(itemId)) continue;
            var spot = new Vector3(hero.Position.X + 40f, hero.Position.Y, hero.Position.Z + 40f);
            Item spawned = SpawnItem(itemId, spot);
            if (spawned != null) spawnedItems.Add(spawned);
        }
        foreach (Item spawned in spawnedItems) _world.IssueOrder(hero, UnitOrder.Pickup(spawned), true);
        for (int tick = 0; tick <= spawnedItems.Count; tick++) _world.Advance();

        _selected.Clear();
        _selected.Add(hero);
        hero.View?.SetSelected(true);
        RefreshSelection();
        UpdateCommandCard();

        int carried = 0;
        foreach (Item item in hero.Inventory) if (item != null) carried++;
        foreach (Item detailItem in hero.Inventory)
        {
            if (detailItem?.Type == null) continue;
            string detail = MeleeHudController.ItemDetailText(detailItem.Type).Replace("\n", " / ");
            GD.Print($"[ITEM] {detailItem.Type.Id} 详情：{detail}");
        }
        _hud?.ShowItemDetailForTest(0);
        GD.Print($"[CARD] 命令卡 {DescribeCommandCard(hero)}");
        GD.Print($"[CARD] 物品 {carried}/{Unit.InventorySize} 格，命令卡格位 0..4 是移动/停止/据守/攻击/巡逻");

        ProbeShopEntry(hero);
        ProbeUiEntry(hero);
        ProbeHeroGuardEntry(hero);
        ProbeCommandCardPages(hero);
    }

    /// <summary>
    /// 命令卡四个功能格的探针：学习页（Researchbuttonpos + 第 12 格返回）、建造页、
    /// 放置模式取消格、混选聚合。打印的是真正落到 12 格上的结果。
    /// </summary>
    private void ProbeCommandCardPages(Unit hero)
    {
        if (hero == null) return;

        CancelPlacement();
        _selection?.SelectClicked(hero, false);
        GD.Print($"[CARD] 英雄技能卡 {_hud?.DescribeCommandSlots()}");

        _newHud?.OpenLearnMenu(hero);
        UpdateCommandCard();
        GD.Print($"[CARD] 学习页 {_hud?.DescribeCommandSlots()}");
        _hud?.ClickCommandSlot(2);
        GD.Print($"[CARD] 点学习页第 3 格后 技能点={hero.SkillPoints}"
            + $" 学习页={_newHud?.LearnMenuOpen} {_hud?.DescribeCommandSlots()}");
        _newHud?.OpenLearnMenu(hero);
        UpdateCommandCard();
        _newHud?.BackMenu();
        UpdateCommandCard();
        GD.Print($"[CARD] 返回后 {_hud?.DescribeCommandSlots()}");

        Unit worker = Spawn("hpea", ViewPlayer, hero.Position.X - 140f, hero.Position.Z + 140f, 0f, 0f, 1f);
        if (worker != null)
        {
            _world.Units.Add(worker);
            _selection?.SelectClicked(worker, false);
            GD.Print($"[CARD] 工人卡 {_hud?.DescribeCommandSlots()}");

            _newHud?.OpenBuildMenu(worker);
            UpdateCommandCard();
            GD.Print($"[CARD] 建造页 {_hud?.DescribeCommandSlots()}");
            _hud?.ClickCommandSlot(0);
            GD.Print($"[CARD] 点建造页第 1 格后 建造页={_newHud?.BuildMenuOpen}"
                + $" 放置={_placingId ?? "无"} {_hud?.DescribeCommandSlots()}");
            CancelPlacement();
            UpdateCommandCard();
        }

        Unit mate = Spawn("hfoo", ViewPlayer, hero.Position.X + 80f, hero.Position.Z - 80f, 0f, 0f, 1f);
        if (mate == null) return;
        _world.Units.Add(mate);
        _selected.Add(hero);
        _selected.Add(mate);
        _selection?.SelectFromGroup(hero);
        GD.Print($"[CARD] 混选(英雄+步兵) 成员={_selected.Count} 主位={PrimarySelection?.Type?.Id}"
            + $" 条目={BuildCommandEntries().Count} {_hud?.DescribeCommandSlots()}");

        _selection?.SelectClicked(hero, false);
        ProbeCommandHotkeys(hero);
        ProbeSpellBookPage(hero);
    }

    private void ProbeCommandHotkeys(Unit hero)
    {
        PlayerSlot slot = Slot(ViewPlayer);
        if (slot != null)
        {
            slot.Gold = 9999;
            slot.Lumber = 9999;
        }
        foreach (string id in new[] { "hbla", "hlum", "hkee", "hcas", "hfrm", "hfrm", "hfrm", "hfrm" })
        {
            Unit support = Spawn(id, ViewPlayer, hero.Position.X, hero.Position.Z + 260f, 0f, 0f, 1f);
            if (support != null) _world.Units.Add(support);
        }

        _selection?.SelectClicked(hero, false);
        UpdateCommandCard();
        LogCommandHotkey("英雄", 'T');
        LogCommandHotkey("英雄", 'D');
        LogCommandHotkey("英雄", 'V');

        Unit barracks = Spawn("hbar", ViewPlayer, hero.Position.X + 320f, hero.Position.Z, 0f, 0f, 1f);
        if (barracks != null)
        {
            _world.Units.Add(barracks);
            _selection?.SelectClicked(barracks, false);
            UpdateCommandCard();
            GD.Print($"[CARD] 兵营卡 {_hud?.DescribeCommandSlots()}");
            LogCommandHotkey("兵营", 'F');
            LogCommandHotkey("兵营", 'R');
            LogCommandHotkey("兵营", 'K');
            LogCommandHotkey("兵营", 'S');
        }

        Unit smith = Spawn("hbla", ViewPlayer, hero.Position.X + 480f, hero.Position.Z, 0f, 0f, 1f);
        if (smith != null)
        {
            _world.Units.Add(smith);
            _selection?.SelectClicked(smith, false);
            UpdateCommandCard();
            GD.Print($"[CARD] 铁匠铺卡 {_hud?.DescribeCommandSlots()}");
            LogCommandHotkey("铁匠铺", 'S');
            LogCommandHotkey("铁匠铺", 'G');
            LogCommandHotkey("铁匠铺", 'P');
            LogCommandHotkey("铁匠铺", 'A');
        }
    }

    private void LogCommandHotkey(string label, char hotkey)
    {
        CommandEntry entry = _hud?.CommandAtHotkey(hotkey);
        GD.Print($"[CARD] {label}热键 {hotkey} → {entry?.Id ?? "无"}（{entry?.Name ?? "-"}）");
    }

    /// <summary>法术书页：拾起 sbok → 点物品 → 子技能按序号进 0..10、第 12 格返回 → 返回关页。</summary>
    private void ProbeSpellBookPage(Unit hero)
    {
        if (hero == null || !ItemCatalog.Exists("sbok")) return;

        for (int slot = Unit.InventorySize - 1; slot >= 0; slot--)
        {
            if (hero.Inventory[slot] == null) continue;
            _world.DropItemFromSlot(hero, slot);
            break;
        }

        var spot = new Vector3(hero.Position.X + 32f, hero.Position.Y, hero.Position.Z + 32f);
        Item bookItem = SpawnItem("sbok", spot);
        if (bookItem == null)
        {
            GD.Print("[CARD] 法术书物品模型缺失，跳过");
            return;
        }
        _world.IssuePickupItem(hero, bookItem);
        _world.Advance();

        int bookSlot = -1;
        for (int slot = 0; slot < Unit.InventorySize; slot++)
            if (ReferenceEquals(hero.Inventory[slot], bookItem)) bookSlot = slot;
        if (bookSlot < 0)
        {
            GD.Print("[CARD] 法术书没有进背包，跳过");
            return;
        }

        ActivateItem(hero, bookItem, bookSlot);
        int children = 0;
        foreach (Ability ability in hero.Abilities)
            if (ability is RuntimeSpellBookAbility book) children = book.Children.Count;
        GD.Print($"[CARD] 法术书 背包格={bookSlot} 子技能={children} 页面={_newHud?.SpellBookOpen}"
            + $" {_hud?.DescribeCommandSlots()}");

        float manaBefore = hero.Mana;
        _hud?.ClickCommandSlot(2);
        GD.Print($"[CARD] 法术书点咆哮 法力 {manaBefore:0}->{hero.Mana:0} 页面={_newHud?.SpellBookOpen}");

        _hud?.ClickCommandSlot(11);
        GD.Print($"[CARD] 法术书返回后 页面={_newHud?.SpellBookOpen} {_hud?.DescribeCommandSlots()}");

        ActivateItem(hero, bookItem, bookSlot);
        _world.DropItemFromSlot(hero, bookSlot);
        UpdateCommandCard();
        int books = 0;
        foreach (Ability ability in hero.Abilities)
            if (ability is RuntimeSpellBookAbility) books++;
        GD.Print($"[CARD] 法术书离手后 页面={_newHud?.SpellBookOpen} 书={books}"
            + $" {_hud?.DescribeCommandSlots()}");
    }

    private void ProbeHeroGuardEntry(Unit hero)
    {
        Unit hall = FindHall(ViewPlayer);
        PlayerSlot slot = Slot(ViewPlayer);
        if (hero == null || hall == null || slot == null) return;

        Vector3 home = hero.Position;
        hero.Position = hall.Position + new Vector3(2200f, 0f, 0f);
        hero.Hp = hero.MaxHp * 0.3f;
        hero.Target = null;

        Unit foe = Spawn("hfoo", 1, hero.Position.X + 260f, hero.Position.Z, 0f, 0f, 1f);
        if (foe != null)
        {
            _world.Units.Add(foe);
            foe.Target = hero;
        }

        Unit escort = Spawn("hfoo", ViewPlayer, hero.Position.X + 180f, hero.Position.Z + 180f, 0f, 0f, 1f);
        if (escort != null) _world.Units.Add(escort);

        TickAiHeroGuard(slot, hall);
        GD.Print($"[UI] 英雄护卫 残血撤退={hero.RetreatOrdered} 护送至={escort?.HeroEscort == true}"
            + $" 奉命跟随={escort?.MoveTarget.HasValue == true} 朝基地={hero.MoveTarget.HasValue}");

        hero.Hp = hero.MaxHp;
        hero.Position = home;
        TickAiHeroGuard(slot, hall);
        GD.Print($"[UI] 英雄回血后 撤退={hero.RetreatOrdered} 护送至={escort?.HeroEscort == true}");

        if (foe != null) _world.KillUnit(foe, null);
    }

    private void ProbeUiEntry(Unit hero)
    {
        BeginPlacement("hhou");
        UnitType house = UnitFactory.Get("hhou");
        bool openValid = false;
        bool blockedValid = true;
        bool foundOpen = false;
        Vector3 openPoint = default;
        Unit hallUnit = FindHall(ViewPlayer);
        if (house != null && hallUnit != null)
        {
            float footprint = Mathf.Max(32f, house.FootprintRadius);
            for (float radius = 400f; radius <= 1600f && !foundOpen; radius += 200f)
            {
                for (int step = 0; step < 16; step++)
                {
                    float angle = Mathf.Tau * step / 16f;
                    var candidate = new Vector3(
                        hallUnit.Position.X + Mathf.Cos(angle) * radius,
                        0f,
                        hallUnit.Position.Z + Mathf.Sin(angle) * radius);
                    candidate.Y = MapWorldBuilder.HeightAt(_map.Terrain, candidate.X, candidate.Z);
                    if (_pathGrid != null && !_pathGrid.IsAreaFree(candidate, footprint)) continue;
                    bool nearBuilding = false;
                    foreach (Unit other in _allUnits)
                    {
                        if (!other.Alive || !other.IsBuilding) continue;
                        if (new Vector2(other.Position.X - candidate.X, other.Position.Z - candidate.Z).Length()
                            < (footprint + other.Type.FootprintRadius) * 0.92f)
                        {
                            nearBuilding = true;
                            break;
                        }
                    }
                    if (nearBuilding) continue;
                    openPoint = candidate;
                    foundOpen = true;
                    break;
                }
            }

            if (foundOpen)
            {
                RebuildFootprint(openPoint, house);
                openValid = PlacementPreviewValid;
            }
            RebuildFootprint(hallUnit.Position, house);
            blockedValid = PlacementPreviewValid;
        }
        GD.Print($"[UI] 建造预览 活跃={PlacementPreviewActive} 找到空地={foundOpen} 空地合法={openValid} 建筑上合法={blockedValid}");
        CancelPlacement();
        GD.Print($"[UI] 取消后 活跃={PlacementPreviewActive}");

        _selected.Clear();
        _selected.Add(hero);
        RefreshSelection();
        _worldInteraction.MinimapOrderAt(hero.Position + new Vector3(200f, 0f, 0f), false);
        GD.Print($"[UI] 小地图右键下令 MoveTarget={hero.MoveTarget.HasValue}");
        PingMinimap(new System.Numerics.Vector2(-hero.Position.X, hero.Position.Z));
        GD.Print($"[UI] 小地图信号 active={_minimapPings.Count > 0}");

        Unit fast = Spawn("hkni", ViewPlayer, hero.Position.X + 300f, hero.Position.Z + 300f, 0f, 0f, 1f);
        Unit slow = Spawn("hfoo", ViewPlayer, hero.Position.X + 360f, hero.Position.Z + 360f, 0f, 0f, 1f);
        if (fast != null && slow != null)
        {
            _world.Units.Add(fast);
            _world.Units.Add(slow);
            _selected.Clear();
            _selected.Add(fast);
            _selected.Add(slow);
            RefreshSelection();
            _selected.Clear();
            _selected.Add(hero);
            RefreshSelection();
            UpdateUnitSummary();
            _hud?.SetQueueSlots(new List<QueueEntry>
            {
                new() { Name = "训练 步兵", Total = 20f, Remaining = 10f },
            });
            _hud?.SetBuffs(new List<BuffEntry>
            {
                new() { Name = "测试增益", Remaining = 5f },
            });
            GD.Print($"[UI] 面板 {_hud?.DescribeExtras()}");

            _selected.Clear();
            _selected.Add(fast);
            _selected.Add(slow);
            RefreshSelection();
            ProbeCreepSleepEntry();
            float fastBase = World.SpeedOf(fast);
            IssueMoveOrders(hero.Position + new Vector3(800f, 0f, 800f), false);
            GD.Print($"[UI] 群组限速 骑兵 {fastBase:0}->{World.SpeedOf(fast):0} 步兵 {World.SpeedOf(slow):0} cap={fast.GroupSpeedCap:0}");
            GD.Print($"[UI] 群组路径 航点 {fast.Path?.Count ?? 0}/{slow.Path?.Count ?? 0} 组 {fast.GroupMoveId}/{slow.GroupMoveId}");
            for (int tick = 0; tick < 40; tick++) _world.Advance();
            GD.Print($"[UI] 群组路径 40tick 索引 {fast.PathIndex}/{slow.PathIndex} 偏移 {Mathf.Abs(fast.PathIndex - slow.PathIndex)}");

            _hud?.SetCommands(new List<CommandEntry>
            {
                new() { Name = "测试冷却", CooldownRemaining = 3f, CooldownTotal = 5f },
            });
            _hud?.RefreshCooldownNow();
            GD.Print($"[UI] 冷却 {_hud?.DescribeExtras()}");
            UpdateCommandCard();

            Unit hireShop = null;
            foreach (Unit unit in _world.Units)
            {
                if (unit != null && unit.Alive && unit.Type != null && unit.Type.SellsUnits) { hireShop = unit; break; }
            }
            PlayerSlot ownSlot = Slot(ViewPlayer);
            if (hireShop != null && ownSlot != null)
            {
                ownSlot.Gold = 2000;
                ownSlot.Lumber = 2000;
                Unit mercCamp = Spawn("nmer", MeleeRules.PlayerNeutralPassive,
                    hireShop.Position.X + 400f, hireShop.Position.Z, 0f, 0f, 1f);
                if (mercCamp != null)
                {
                    _world.Units.Add(mercCamp);
                    foreach (string soldId in mercCamp.Type.SellUnits) mercCamp.UnitStock[soldId] = 2;
                }
                Unit extraFarm = Spawn("hhou", ViewPlayer, hero.Position.X + 700f, hero.Position.Z, 0f, 0f, 1f);
                if (extraFarm != null) _world.Units.Add(extraFarm);
                fast.Position = hireShop.Position + new Vector3(1500f, 0f, 0f);
                fast.HireShop = null;
                fast.MoveTarget = null;
                TickAiHiring(ownSlot);
                GD.Print($"[UI] 买兵派工 shop={hireShop.Type.Id} 派={fast.HireShop?.Type?.Id}");
            }
        }
    }

    private void ProbeCreepSleepEntry()
    {
        int sleepers = 0;
        Unit sample = null;
        foreach (Unit unit in _world.Units)
        {
            if (unit == null || !unit.Alive || !unit.IsCreepSleeper) continue;
            if (!unit.State.Has(StateMod.Sleep)) continue;
            sleepers++;
            sample ??= unit;
        }
        bool woke = false;
        if (sample != null)
        {
            _world.IsNight = true;
            _world.Advance();
            woke = !sample.State.Has(StateMod.Sleep);
            _world.IsNight = false;
        }
        GD.Print($"[UI] 野怪睡眠 {sleepers} 只，入夜唤醒={woke}");
    }

    private void ProbeShopEntry(Unit hero)
    {
        Unit shop = Spawn("hvlt", ViewPlayer, hero.Position.X + 160f, hero.Position.Z + 160f, 0f, 0f, 1f);
        if (shop == null)
        {
            GD.Print("[SHOPCARD] 人类商店模型缺失");
            return;
        }
        _world.Units.Add(shop);
        GD.Print($"[SHOPCARD] 人类藏宝室 商品 {shop.Type.SellItems.Length} IsShop={shop.Type.IsShop}");

        _selected.Clear();
        _selected.Add(shop);
        RefreshSelection();
        UpdateCommandCard();
        GD.Print($"[SHOPCARD] 自有商店卡：{_hud?.DescribeVisibleCommands()}");

        string first = shop.Type.SellItems.Length > 0 ? shop.Type.SellItems[0] : null;
        if (first != null)
        {
            _world.DropItemFromSlot(hero, 0);
            int beforeFull = hero.ItemCount;
            bool direct = BuyItemFromShop(shop, ViewPlayer, first);
            int groundAfterDirect = _world.GroundItems.Count;
            bool dropWhenFull = false;
            for (int guard = 0; guard < Unit.InventorySize && hero.HasFreeSlot; guard++)
            {
                _world.DropItemFromSlot(hero, hero.FreeSlot);
            }
            dropWhenFull = BuyItemFromShop(shop, ViewPlayer, first) && _world.GroundItems.Count > groundAfterDirect;
            GD.Print($"[SHOPCARD] 买 {first}: 进包={direct}（{beforeFull}→{hero.ItemCount}，地面 {groundAfterDirect}）"
                + $" 满包落地={dropWhenFull}（地面 {_world.GroundItems.Count}）");
        }

        Unit tavern = Spawn("ntav", MeleeRules.PlayerNeutralPassive,
            hero.Position.X - 160f, hero.Position.Z - 160f, 0f, 0f, 1f);
        if (tavern == null)
        {
            GD.Print("[SHOPCARD] 酒馆模型缺失");
            return;
        }
        _world.Units.Add(tavern);
        GD.Print($"[SHOPCARD] 中立酒馆 IsNeutralShop={IsNeutralShop(tavern)}");

        _selected.Clear();
        _selected.Add(tavern);
        RefreshSelection();
        UpdateCommandCard();
        GD.Print($"[SHOPCARD] 中立商店卡：{_hud?.DescribeVisibleCommands()}");
    }

    /// <summary>
    /// --hire-probe：中立建筑买兵。在自家基地旁摆一座酒馆与一个买家，
    /// 打印卖兵清单与命令卡，等过 stockStart 后买一个中立英雄，再摆雇佣兵营地买一个巨魔。
    /// </summary>
    /// <summary>
    /// --bolt-probe：引擎闪电束。摆一个先知放闪电链、一个恶魔猎手放法力燃烧，
    /// 打印生成了几条光束，方便配合 --shot 截图看宽度/贴图/抖动的实际观感。
    /// </summary>
    public void RunBoltProbe()
    {
        Unit hall = FindHall(ViewPlayer);
        if (hall == null)
        {
            GD.Print("[BOLT] 找不到主基地，跳过");
            return;
        }

        Vector3 origin = hall.Position;
        Unit seer = Spawn("Ofar", ViewPlayer, origin.X + 260f, origin.Z + 120f, 0f, 0f, 1f);
        if (seer == null)
        {
            GD.Print("[BOLT] 先知模型缺失");
            return;
        }
        _world.Units.Add(seer);
        seer.Level = 6;
        seer.SkillPoints = 3;
        _world.RefreshHeroAttributes(seer);
        seer.Hp = seer.MaxHp;
        seer.Mana = seer.MaxMana;
        _world.LearnAbility(seer, "AOcl");

        var victims = new List<Unit>();
        for (int index = 0; index < 3; index++)
        {
            Unit creep = Spawn("nftr", 12, origin.X + 420f + index * 130f, origin.Z + 60f, 0f, 0f, 1f);
            if (creep == null) continue;
            _world.Units.Add(creep);
            victims.Add(creep);
        }
        if (victims.Count == 0)
        {
            GD.Print("[BOLT] 没有可打的目标");
            return;
        }

        Ability chain = seer.FindAbility("AOcl");
        bool ok = _world.TryCast(seer, chain, victims[0], victims[0].Position);
        GD.Print($"[BOLT] 闪电链 {ok}，落点 {(chain is ChainLightning lightning ? lightning.LastChain.Count : 0)} 个，光束 {_lightningBeams.Count} 条");

        Unit demon = Spawn("Edem", ViewPlayer, origin.X + 120f, origin.Z + 120f, 0f, 0f, 1f);
        if (demon == null) return;
        _world.Units.Add(demon);
        demon.Level = 4;
        demon.SkillPoints = 2;
        _world.RefreshHeroAttributes(demon);
        demon.Hp = demon.MaxHp;
        demon.Mana = demon.MaxMana;
        _world.LearnAbility(demon, "AEmb");
        Unit burnTarget = Spawn("nftr", 12, demon.Position.X + 150f, demon.Position.Z, 0f, 0f, 1f);
        if (burnTarget == null)
        {
            GD.Print("[BOLT] 法力燃烧目标缺失");
            return;
        }
        _world.Units.Add(burnTarget);
        Ability burn = demon.FindAbility("AEmb");
        bool burned = _world.TryCast(demon, burn, burnTarget, burnTarget.Position);
        GD.Print($"[BOLT] 法力燃烧 {burned}，光束 {_lightningBeams.Count} 条");

        Unit primary = seer;
        Ability chainAbility = chain;
        Unit burnCaster = demon;
        Ability burnAbility = burn;
        Unit chainFirst = victims[0];
        Unit burnVictim = burnTarget;
        foreach (Unit dummy in victims) { dummy.MaxHp = 6000f; dummy.Hp = 6000f; }
        burnTarget.MaxHp = 6000f;
        burnTarget.Hp = 6000f;

        int remaining = 10;
        void Repeat()
        {
            if (remaining-- <= 0) return;
            if (primary == null || !primary.Alive) return;
            primary.Mana = primary.MaxMana;
            burnCaster.Mana = burnCaster.MaxMana;
            chainAbility.CooldownRemaining = 0f;
            burnAbility.CooldownRemaining = 0f;
            if (chainFirst != null && chainFirst.Alive) _world.TryCast(primary, chainAbility, chainFirst, chainFirst.Position);
            if (burnVictim != null && burnVictim.Alive) _world.TryCast(burnCaster, burnAbility, burnVictim, burnVictim.Position);
            SceneTreeTimer next = GetTree().CreateTimer(0.45);
            next.Timeout += Repeat;
        }
        SceneTreeTimer start = GetTree().CreateTimer(2.2);
        start.Timeout += Repeat;
    }

    /// <summary>--rally-probe：给己方前几座建筑设集结点，用来看旗帜模型与队伍色。</summary>
    /// <summary>
    /// --subgroup-probe：验证 Tab 与点头像只换主位、成员一个不丢，且面板顺序与 Tab 顺序同源。
    /// </summary>
    public void RunSubgroupProbe()
    {
        Unit hall = FindHall(ViewPlayer);
        if (hall == null)
        {
            GD.Print("[SUB] 找不到主基地，跳过");
            return;
        }

        ClearSelection();
        var spawned = new List<Unit>();
        for (int index = 0; index < 2; index++)
        {
            Unit rifleman = Spawn("hrif", ViewPlayer, hall.Position.X + 220f + index * 70f, hall.Position.Z + 300f, 0f, 0f, 1f);
            if (rifleman != null) { _world.Units.Add(rifleman); spawned.Add(rifleman); }
            Unit footman = Spawn("hfoo", ViewPlayer, hall.Position.X + 220f + index * 70f, hall.Position.Z + 220f, 0f, 0f, 1f);
            if (footman != null) { _world.Units.Add(footman); spawned.Add(footman); }
        }
        if (spawned.Count == 0)
        {
            GD.Print("[SUB] 模型缺失，跳过");
            return;
        }

        // 第一段：真框选。用屏幕坐标拖一个框，走的是玩家同一条 PointerDown/PointerUp 路径。
        BoxSelectSpawned(spawned);
        GD.Print($"[SUB] 真框选后 {DescribeSelection()}");

        // 第二段：直接喂控制器，成员定死是这 4 个，用来核对排序与主位轮换。
        _selection.SelectRect(spawned, false);
        GD.Print($"[SUB] 框选后 {DescribeSelection()}");

        CycleSubgroup();
        GD.Print($"[SUB] Tab 一次 {DescribeSelection()}");
        CycleSubgroup();
        GD.Print($"[SUB] Tab 两次 {DescribeSelection()}");

        // 点头像 = 只换主位（客户端 SelectFromGroup），成员不变。
        OnSelectionGroupClicked(spawned[0]);
        GD.Print($"[SUB] 点第一个兵的头像 {DescribeSelection()}");
        OnSelectionGroupClicked(spawned[spawned.Count - 1]);
        GD.Print($"[SUB] 点最后一个兵的头像 {DescribeSelection()}");
        OnSelectionGroupClicked(PrimarySelection);
        GD.Print($"[SUB] 再点已聚焦的头像 {DescribeSelection()}");

        // 收尾回到多选状态：出图时能直接看到排序后的头像格。
        _selection.SelectRect(spawned, false);
    }

    private void BoxSelectSpawned(List<Unit> spawned)
    {
        Camera3D camera = _camera?.Camera;
        if (camera == null) return;
        var lo = new Vector2(float.MaxValue, float.MaxValue);
        var hi = new Vector2(float.MinValue, float.MinValue);
        foreach (Unit unit in spawned)
        {
            Vector3 point = unit.Position + new Vector3(0f, 30f, 0f);
            if (camera.IsPositionBehind(point)) return;
            Vector2 screen = camera.UnprojectPosition(point);
            lo = new Vector2(Mathf.Min(lo.X, screen.X), Mathf.Min(lo.Y, screen.Y));
            hi = new Vector2(Mathf.Max(hi.X, screen.X), Mathf.Max(hi.Y, screen.Y));
        }
        var from = new Vector2(lo.X - 24f, lo.Y - 24f);
        var to = new Vector2(hi.X + 24f, hi.Y + 24f);
        GD.Print($"[SUB] 框选屏幕 ({from.X:0},{from.Y:0})-({to.X:0},{to.Y:0})");
        _worldInteraction.PointerDown(from, MouseButton.Left);
        _worldInteraction.PointerMove(to);
        _worldInteraction.PointerUp(to, MouseButton.Left);
    }

    /// <summary>--order-probe：逐条核对右键语境指令的分支，以及"干不了这批活的单位继续走"的补全。</summary>
    public async void RunOrderProbe()
    {
        Unit hall = FindHall(ViewPlayer);
        if (hall == null)
        {
            GD.Print("[ORD] 找不到主基地，跳过");
            return;
        }

        Unit mine = null;
        float mineDistance = float.MaxValue;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Gold <= 0) continue;
            float distance = unit.DistanceTo(hall);
            if (distance >= mineDistance) continue;
            mineDistance = distance;
            mine = unit;
        }

        TreeState tree = null;
        float treeDistance = float.MaxValue;
        foreach (TreeState candidate in _trees)
        {
            if (candidate == null || !candidate.Alive) continue;
            float distance = new Vector2(candidate.Position.X - hall.Position.X,
                candidate.Position.Z - hall.Position.Z).Length();
            if (distance >= treeDistance) continue;
            treeDistance = distance;
            tree = candidate;
        }

        var spawned = new List<Unit>();
        string[] ids = { "hpea", "hpea", "hfoo", "hfoo" };
        for (int index = 0; index < ids.Length; index++)
        {
            float angle = index * 0.9f;
            Unit unit = Spawn(ids[index], ViewPlayer,
                hall.Position.X + Mathf.Cos(angle) * 320f,
                hall.Position.Z + Mathf.Sin(angle) * 320f, 0f, 0f, 1f);
            if (unit == null) continue;
            _world.Units.Add(unit);
            spawned.Add(unit);
        }
        if (spawned.Count < 4)
        {
            GD.Print($"[ORD] 只造出 {spawned.Count} 个单位，跳过");
            return;
        }

        Unit workerA = spawned[0];
        Unit workerB = spawned[1];
        Unit footA = spawned[2];
        Unit footB = spawned[3];

        // 1. 修理：农民接活，步兵继续走向目标（原来全体发呆）。
        hall.Hp = hall.MaxHp * 0.5f;
        _selection.SelectRect(spawned, false);
        Vector2 hallScreen = ScreenOf(hall.Position + new Vector3(0f, 60f, 0f));
        _worldInteraction.OrderAt(hallScreen, false);
        GD.Print($"[ORD] 修理 农民={Label(workerA.RepairTarget)}/{Label(workerB.RepairTarget)} "
            + $"步兵移动={(footA.MoveTarget.HasValue ? "有" : "无")}/{(footB.MoveTarget.HasValue ? "有" : "无")}");

        // 2. 交资源：携带中的农民点主城，送到指定建筑。
        workerA.Carry = 10;
        workerA.GoldMine = null;
        workerA.Tree = null;
        _selection.SelectRect(new[] { workerA }, false);
        _worldInteraction.OrderAt(hallScreen, false);
        GD.Print($"[ORD] 交资源 ReturnHall={Label(workerA.ReturnHall)} 携带={workerA.Carry}");

        // 3. 采金：挑屏幕上看得见的那座矿。
        mine = null;
        mineDistance = float.MaxValue;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Gold <= 0) continue;
            if (!TryScreenOf(unit.Position + new Vector3(0f, 60f, 0f), out _)) continue;
            float distance = unit.DistanceTo(hall);
            if (distance >= mineDistance) continue;
            mineDistance = distance;
            mine = unit;
        }
        if (mine != null)
        {
            Vector2 mineScreen = ScreenOf(mine.Position + new Vector3(0f, 60f, 0f));
            Unit pickedMine = _picking.PickUnit(mineScreen, unit => unit.Alive);
            GD.Print($"[ORD] 采金拾取={Label(pickedMine)} 阵营 {pickedMine?.Player} "
                + $"点内={mineScreen.DistanceTo(_camera.Camera.UnprojectPosition(mine.Position + new Vector3(0f, 60f, 0f))):0}px");
            _selection.SelectRect(new[] { workerA }, false);
            _worldInteraction.OrderAt(mineScreen, false);
            GD.Print($"[ORD] 采金 GoldMine={Label(workerA.GoldMine)}（目标 {mine.Type.Name} 储量 {mine.Gold}）");
        }
        else GD.Print("[ORD] 采金 屏幕上没有金矿，跳过");

        // 4. 伐木：挑屏幕上看得见的树。
        tree = null;
        treeDistance = float.MaxValue;
        foreach (TreeState candidate in _trees)
        {
            if (candidate == null || !candidate.Alive) continue;
            if (!TryScreenOf(candidate.Position + new Vector3(0f, 90f, 0f), out _)) continue;
            float distance = new Vector2(candidate.Position.X - hall.Position.X,
                candidate.Position.Z - hall.Position.Z).Length();
            if (distance >= treeDistance) continue;
            treeDistance = distance;
            tree = candidate;
        }
        if (tree != null)
        {
            Vector2 treeScreen = ScreenOf(tree.Position + new Vector3(0f, 90f, 0f));
            TreeState pickedTree = _picking.PickTree(treeScreen);
            GD.Print($"[ORD] 伐木拾取={(pickedTree != null ? "有" : "无")} 树高屏幕 {treeScreen}");
            _selection.SelectRect(new[] { workerA }, false);
            _worldInteraction.OrderAt(treeScreen, false);
            GD.Print($"[ORD] 伐木 Tree={(workerA.Tree != null ? "已指派" : "无")} 树 HP {tree.Hp:0}");
        }
        else GD.Print("[ORD] 伐木 屏幕上没有树，跳过");

        // 5. 混选集结点：主城吃集结点，步兵继续走向那一点。
        hall.RallyPoint = null;
        footA.MoveTarget = null;
        if (TryEmptyGroundScreen(hall.Position, 320f, out Vector2 groundScreen))
        {
            _selection.SelectRect(new[] { hall, footA }, false);
            _worldInteraction.OrderAt(groundScreen, false);
            GD.Print($"[ORD] 集结点={(hall.RallyPoint.HasValue || hall.RallyTarget != null || hall.RallyTree != null ? "已设" : "未设")} "
                + $"步兵移动={(footA.MoveTarget.HasValue ? "有" : "无")} 屏幕({groundScreen.X:0},{groundScreen.Y:0})");
        }
        else GD.Print("[ORD] 集结点 屏幕内找不到空地，跳过");

        // 6. 攻击：敌人是第 2 号玩家，能打的去打，农民照样走向目标。
        Unit enemy = null;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player == ViewPlayer) continue;
            if (!AreAllied(unit.Player, ViewPlayer) && TryScreenOf(unit.Position, out _))
            {
                enemy = unit;
                break;
            }
        }
        if (enemy == null)
        {
            enemy = Spawn("hfoo", 2, footA.Position.X + 200f, footA.Position.Z + 120f, 0f, 0f, 1f);
            if (enemy != null)
            {
                _world.Units.Add(enemy);
                _allUnits.Add(enemy);
            }
        }
        if (enemy != null && TryScreenOf(enemy.Position + new Vector3(0f, 60f, 0f), out Vector2 enemyScreen))
        {
            footA.Target = null;
            workerA.MoveTarget = null;
            _selection.SelectRect(new[] { footA }, false);
            _worldInteraction.OrderAt(enemyScreen, false);
            GD.Print($"[ORD] 攻击 步兵目标={Label(footA.Target)} 目标={enemy.Type.Name} "
                + $"（修理那条已证过补全：农民接活、步兵继续走）");
        }
        else GD.Print("[ORD] 攻击 屏幕上没有敌人，跳过");

        // 7. 跟随友军。
        footB.FollowTarget = null;
        _selection.SelectRect(new[] { footB }, false);
        _worldInteraction.OrderAt(ScreenOf(workerB.Position + new Vector3(0f, 60f, 0f)), false);
        GD.Print($"[ORD] 跟随 步兵跟随={Label(footB.FollowTarget)}（目标农民）");

        // 8. 建造：首座立即，第二座按住 Shift 进工人队列并保持放置模式。
        _selection.SelectRect(new[] { workerB }, false);
        if (TryPlacementScreen("hhou", null, out Vector2 firstScreen))
        {
            BeginPlacement("hhou");
            TryPlace(firstScreen, false);
            Unit firstSite = workerB.BuildSite;
            GD.Print($"[ORD] 首座 农场={(firstSite != null ? "已开工" : "失败")} 放置模式={_placingId ?? "无"}");
            if (firstSite != null && TryPlacementScreen("hhou", firstSite.Position, out Vector2 secondScreen))
            {
                BeginPlacement("hhou");
                TryPlace(secondScreen, true);
                GD.Print($"[ORD] Shift 连造 队列={workerB.BuildQueue.Count} 放置模式={_placingId ?? "无"} "
                    + $"队列目标={(workerB.BuildQueue.Count > 0 ? "农场" : "无")}");
            }
            CancelPlacement();
        }
        else GD.Print("[ORD] 建造 屏幕内找不到合法落点，跳过");

        // 9. 继续建造：另一批农民右键在建工地，接的是"继续建造"而不是走过去站着。
        if (workerB.BuildSite != null
            && TryScreenOf(workerB.BuildSite.Position + new Vector3(0f, 40f, 0f), out Vector2 siteScreen))
        {
            workerA.Tree = null;
            workerA.GoldMine = null;
            _selection.SelectRect(new[] { workerA }, false);
            _worldInteraction.OrderAt(siteScreen, false);
            GD.Print($"[ORD] 继续建造 接单={(ReferenceEquals(workerA.BuildSite, workerB.BuildSite) ? "同一工地" : "无")}");
        }

        // 10. 交资源要真的到账：携带 10 金右键主城，跑一趟之后国库进账、手上清空。
        workerA.Tree = null;
        workerA.GoldMine = null;
        workerA.Carry = 10;
        int goldBefore = Slot(ViewPlayer).Gold;
        _selection.SelectRect(new[] { workerA }, false);
        _worldInteraction.OrderAt(hallScreen, false);
        await ToSignal(GetTree().CreateTimer(6.0f), "timeout");
        GD.Print($"[ORD] 交资源到账 携带={workerA.Carry} 国库+{Slot(ViewPlayer).Gold - goldBefore}");
    }

    private Vector2 ScreenOf(Vector3 point)
    {
        Camera3D camera = _camera.Camera;
        if (camera.IsPositionBehind(point)) return Vector2.Zero;
        return camera.UnprojectPosition(point);
    }

    private bool TryScreenOf(Vector3 point, out Vector2 screen)
    {
        screen = ScreenOf(point);
        if (screen == Vector2.Zero) return false;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        return screen.X >= 0f && screen.Y >= 0f && screen.X <= size.X && screen.Y <= size.Y;
    }

    private bool TryEmptyGroundScreen(Vector3 origin, float clearance, out Vector2 screen)
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        for (float y = 0.58f; y <= 0.9f; y += 0.05f)
        {
            for (float x = 0.18f; x <= 0.82f; x += 0.05f)
            {
                var candidate = new Vector2(size.X * x, size.Y * y);
                if (!TryGroundPoint(candidate, out Vector3 point)) continue;
                bool clear = true;
                foreach (Unit unit in _allUnits)
                {
                    if (unit == null || !unit.Alive) continue;
                    if (new Vector2(unit.Position.X - point.X,
                            unit.Position.Z - point.Z).Length() < clearance + unit.Radius)
                    {
                        clear = false;
                        break;
                    }
                }
                if (!clear) continue;
                screen = candidate;
                return true;
            }
        }
        screen = Vector2.Zero;
        return false;
    }

    private bool TryPlacementScreen(string buildingId, Vector3? awayFrom, out Vector2 screen)
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        UnitType type = UnitFactory.Get(buildingId);
        if (type == null)
        {
            screen = Vector2.Zero;
            return false;
        }
        float footprint = Mathf.Max(32f, type.FootprintRadius);
        for (float y = 0.62f; y <= 0.88f; y += 0.04f)
        {
            for (float x = 0.2f; x <= 0.8f; x += 0.04f)
            {
                var candidate = new Vector2(size.X * x, size.Y * y);
                if (!TryGroundPoint(candidate, out Vector3 point)) continue;
                var snapped = new Vector3(Mathf.Round(point.X / 32f) * 32f, point.Y,
                    Mathf.Round(point.Z / 32f) * 32f);
                snapped.Y = MapWorldBuilder.HeightAt(_map.Terrain, snapped.X, snapped.Z);
                if (_pathGrid != null && !_pathGrid.IsAreaFree(snapped, footprint)) continue;

                bool blocked = false;
                foreach (Unit other in _allUnits)
                {
                    if (other == null || !other.Alive || !other.IsBuilding) continue;
                    float distance = new Vector2(other.Position.X - snapped.X,
                        other.Position.Z - snapped.Z).Length();
                    if (distance >= (footprint + other.Type.FootprintRadius) * 0.92f) continue;
                    blocked = true;
                    break;
                }
                if (blocked) continue;
                if (awayFrom.HasValue && new Vector2(awayFrom.Value.X - snapped.X,
                        awayFrom.Value.Z - snapped.Z).Length() < footprint * 2.4f) continue;
                screen = candidate;
                return true;
            }
        }
        screen = Vector2.Zero;
        return false;
    }

    /// <summary>--item-probe：几何拾取、地面物品详情、物品栏拖拽（换格/转交/卖出/丢地）。</summary>
    public async void RunItemProbe()
    {
        Unit hall = FindHall(ViewPlayer);
        if (hall == null)
        {
            GD.Print("[ITEM] 找不到主基地，跳过");
            return;
        }

        Unit hero = Spawn("Hpal", ViewPlayer, hall.Position.X + 300f, hall.Position.Z + 180f, 0f, 0f, 1f);
        Unit escort = Spawn("Hpal", ViewPlayer, hall.Position.X + 380f, hall.Position.Z + 180f, 0f, 0f, 1f);
        if (hero == null)
        {
            GD.Print("[ITEM] 英雄模型缺失，跳过");
            return;
        }
        _world.Units.Add(hero);
        _allUnits.Add(hero);
        if (escort != null)
        {
            _world.Units.Add(escort);
            _allUnits.Add(escort);
        }

        // 1. 几何拾取：模型顶端与身体各点一下，再看身体旁边 140px 的空地。
        float height = hero.View != null ? hero.View.Height : 100f;
        Vector2 head = ScreenOf(hero.Position + new Vector3(0f, height * 0.95f, 0f));
        Vector2 body = ScreenOf(hero.Position + new Vector3(0f, height * 0.45f, 0f));
        Vector2 beside = body + new Vector2(140f, 0f);
        GD.Print($"[ITEM] 拾取 头顶={Label(_picking.PickUnit(head, unit => unit.Alive))} "
            + $"身体={Label(_picking.PickUnit(body, unit => unit.Alive))} "
            + $"旁边={Label(_picking.PickUnit(beside, unit => unit.Alive))} 模型高 {height:0}");

        var pickWatch = System.Diagnostics.Stopwatch.StartNew();
        for (int repeat = 0; repeat < 200; repeat++) _picking.PickUnit(body, unit => unit.Alive);
        pickWatch.Stop();
        GD.Print($"[ITEM] 拾取 200 次 {pickWatch.Elapsed.TotalMilliseconds:0}ms"
            + $"（单次 {pickWatch.Elapsed.TotalMilliseconds / 200:0.00}ms）");

        // 2. 地面物品详情：换成农民再点物品，只开详情不去捡。
        var groundSpot = new Vector3(hero.Position.X - 180f, 0f, hero.Position.Z + 220f);
        groundSpot.Y = MapWorldBuilder.HeightAt(_map.Terrain, groundSpot.X, groundSpot.Z);
        Item groundItem = SpawnItem("phea", groundSpot);
        if (groundItem != null)
        {
            Unit peasant = Spawn("hpea", ViewPlayer, hero.Position.X + 160f,
                hero.Position.Z - 120f, 0f, 0f, 1f);
            if (peasant != null)
            {
                _world.Units.Add(peasant);
                _allUnits.Add(peasant);
            }
            Vector2 itemScreen = ScreenOf(groundItem.Position + new Vector3(0f, 16f, 0f));
            _selection.Clear();
            if (peasant != null) _selection.SelectRect(new[] { peasant }, false);
            _worldInteraction.PointerDown(itemScreen, MouseButton.Left);
            _worldInteraction.PointerUp(itemScreen, MouseButton.Left);
            GD.Print($"[ITEM] 地面物品详情 命中={_picking.PickGroundItem(itemScreen, null, out _)} "
                + $"单位拾取={Label(_picking.PickUnit(itemScreen, unit => unit.Alive))} "
                + $"选中={(_selectedGroundItem != null ? _selectedGroundItem.Type.Name : "无")}");
        }

        // 3. 拖拽：换格 → 转交给友方英雄 → 丢到地面。
        Item potion = new() { Type = ItemCatalog.Get("phea"), Position = hero.Position };
        Item ring = new() { Type = ItemCatalog.Get("ratc"), Position = hero.Position };
        Item claw = new() { Type = ItemCatalog.Get("ankh"), Position = hero.Position };
        if (potion.Type != null && hero.AddItem(potion)) _world.ApplyItemBonus(hero, potion);
        if (ring.Type != null && hero.AddItem(ring)) _world.ApplyItemBonus(hero, ring);
        if (claw.Type != null && hero.AddItem(claw)) _world.ApplyItemBonus(hero, claw);

        _selection.SelectRect(new[] { hero }, false);
        GD.Print($"[ITEM] 背包 0={SlotName(hero, 0)} 1={SlotName(hero, 1)} 2={SlotName(hero, 2)}");

        HandleInventoryDragReleased(0, 3, System.Numerics.Vector2.Zero);
        GD.Print($"[ITEM] 换格后 0={SlotName(hero, 0)} 3={SlotName(hero, 3)}");

        if (escort != null)
        {
            Vector2 escortScreen = ScreenOf(escort.Position + new Vector3(0f, 50f, 0f));
            HandleInventoryDragReleased(3, -1, UiOf(escortScreen));
            GD.Print($"[ITEM] 转交后 自己的 3={SlotName(hero, 3)} 对方的 0={SlotName(escort, 0)}");
        }

        if (TryEmptyGroundScreen(hall.Position, 260f, out Vector2 emptyScreen))
        {
            int before = _world.GroundItems.Count;
            HandleInventoryDragReleased(1, -1, UiOf(emptyScreen));
            GD.Print($"[ITEM] 丢地后 背包 1={SlotName(hero, 1)} 地面 {before}→{_world.GroundItems.Count}");
        }

        // 4. 卖出：把镜头挪到最近的商店，再把物品拖到店上。
        Unit shop = null;
        float shopDistance = float.MaxValue;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || !unit.IsShop) continue;
            float distance = unit.DistanceTo(hall);
            if (distance >= shopDistance) continue;
            shopDistance = distance;
            shop = unit;
        }
        if (shop != null)
        {
            _camera.Focus(shop.Position);
            await ToSignal(GetTree().CreateTimer(0.8f), "timeout");
            if (TryScreenOf(shop.Position + new Vector3(0f, 40f, 0f), out Vector2 shopScreen))
            {
                int goldBefore = Slot(ViewPlayer).Gold;
                HandleInventoryDragReleased(2, -1, UiOf(shopScreen));
                GD.Print($"[ITEM] 卖出 {shop.Type.Name} 后 背包 2={SlotName(hero, 2)} "
                    + $"金币+{Slot(ViewPlayer).Gold - goldBefore}");
            }
            else GD.Print($"[ITEM] 商店 {shop.Type.Name} 移到镜头里也没投影出来");
        }
        else GD.Print("[ITEM] 地图上没有商店，卖出这条跳过");

        // 收尾：把一件地面物品选中，出图时能看到右下角的详情面板。
        foreach (Item item in _world.GroundItems)
        {
            if (item == null || !item.OnGround) continue;
            _selection.Clear();
            SelectGroundItem(item);
            GD.Print($"[ITEM] 收尾展示详情 {item.Type.Name} 说明={War3Text.Describe(item.Type.Ubertip)}");
            break;
        }
    }

    private static string SlotName(Unit unit, int slot)
        => unit?.Inventory != null && slot >= 0 && slot < unit.Inventory.Length
            ? unit.Inventory[slot]?.Type?.Name ?? "空"
            : "越界";

    private System.Numerics.Vector2 UiOf(Vector2 screen)
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        return new System.Numerics.Vector2(screen.X * GameHudScreen.DesignWidth / Mathf.Max(1f, size.X),
            screen.Y * GameHudScreen.DesignHeight / Mathf.Max(1f, size.Y));
    }

    /// <summary>--float-probe：飘字走屏幕空间文字、样式表逐项对齐、镜头远近字号不变。</summary>
    public async void RunFloatProbe()
    {
        Unit hall = FindHall(ViewPlayer);
        if (hall == null)
        {
            GD.Print("[FLT] 找不到主基地，跳过");
            return;
        }

        War3GameplayConfig config = War3GameplayConfig.Default;
        GD.Print($"[FLT] 表 CriticalStrike 色={Describe(config.CriticalStrikeText)} 寿命={config.CriticalStrikeText.Lifetime:0.##} 淡出={config.CriticalStrikeText.FadeStart:0.##}");
        GD.Print($"[FLT] 表 OrdinaryDamage 色={Describe(config.OrdinaryDamageText)} 寿命={config.OrdinaryDamageText.Lifetime:0.##} 高度={config.OrdinaryDamageText.Height:0.###}");
        GD.Print($"[FLT] 表 Miss 色={Describe(config.MissText)} 寿命={config.MissText.Lifetime:0.##}");
        GD.Print($"[FLT] 表 LumberBounty 透明度={config.LumberBountyText.Alpha:0.##} 高度={config.LumberBountyText.Height:0.###}");

        Unit hero = Spawn("Hpal", ViewPlayer, hall.Position.X + 300f, hall.Position.Z + 200f, 0f, 0f, 1f);
        if (hero == null)
        {
            GD.Print("[FLT] 英雄模型缺失，跳过");
            return;
        }
        _world.Units.Add(hero);
        _allUnits.Add(hero);
        _selection.SelectRect(new[] { hero }, false);
        _camera.Focus(hero.Position);
        await ToSignal(GetTree().CreateTimer(0.6f), "timeout");

        // 1. 九种反馈逐条错开 0.22 秒，出图时能在同一列里分清爽。
        FloatingText.ShowCriticalStrike(hero, 123f);
        await ToSignal(GetTree().CreateTimer(0.22f), "timeout");
        FloatingText.ShowOrdinaryDamage(hero, 45f);
        await ToSignal(GetTree().CreateTimer(0.22f), "timeout");
        FloatingText.ShowMiss(hero);
        await ToSignal(GetTree().CreateTimer(0.22f), "timeout");
        FloatingText.ShowResource(hero, 25, lumber: false);
        await ToSignal(GetTree().CreateTimer(0.22f), "timeout");
        FloatingText.ShowResource(hero, 30, lumber: true);
        await ToSignal(GetTree().CreateTimer(0.22f), "timeout");
        FloatingText.ShowBounty(hero, 60);
        await ToSignal(GetTree().CreateTimer(0.22f), "timeout");
        FloatingText.ShowBounty(hero, 40, lumber: true);
        await ToSignal(GetTree().CreateTimer(0.22f), "timeout");
        FloatingText.ShowManaBurn(hero, 40f);
        await ToSignal(GetTree().CreateTimer(0.22f), "timeout");
        FloatingText.ShowShadowStrike(hero, 75f, initial: true);
        await ToSignal(GetTree().CreateTimer(0.22f), "timeout");
        FloatingText.ShowShadowStrike(hero, 10f, initial: false);
        await ToSignal(GetTree().CreateTimer(0.3f), "timeout");

        GD.Print($"[FLT] 出字 {_world.Floats.Count} 条");
        foreach (FloatingText text in _world.Floats)
        {
            GD.Print($"[FLT]   \"{text.Text}\" 色={Describe(text.Color)} 寿命={text.Life:0.##} 透明={text.Alpha:0.##} 高度={text.WorldHeight:0}");
        }

        // 2. 镜头拉远的字号不变：飘字字号只由帧缓冲高度决定，投影位置才跟镜头走。
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float scaleY = viewport.Y / GameHudScreen.DesignHeight;
        float near = TextTagFrame.FontLineHeight(config.CriticalStrikeText.Height, (int)viewport.Y, scaleY);
        UpdateWorldText();
        float nearY = FirstTagScreenY();
        SetCameraDistanceScale(2.4f);
        await ToSignal(GetTree().CreateTimer(1.2f), "timeout");
        float far = TextTagFrame.FontLineHeight(config.CriticalStrikeText.Height, (int)viewport.Y, scaleY);
        UpdateWorldText();
        float farY = FirstTagScreenY();
        GD.Print($"[FLT] 字号 近景={near:0.##} 远景={far:0.##}；投影高度 近景={nearY:0} 远景={farY:0}（{viewport.X:0}x{viewport.Y:0}）");
        SetCameraDistanceScale(1f);

        // 3. 普通伤害数字受「伤害数字显示」门禁：关着不进视图，开着才进来。
        _world.Floats.Clear();
        FloatingText.ShowOrdinaryDamage(hero, 33f);
        ShowDamageNumbers = false;
        UpdateWorldText();
        int hidden = CountTagViews("33");
        ShowDamageNumbers = true;
        UpdateWorldText();
        int shown = CountTagViews("33");
        GD.Print($"[FLT] 普通伤害数字 关={hidden} 开={shown}");

        // 4. 出图前再补几条，确认飘字层一直在刷。
        for (int round = 0; round < 4; round++)
        {
            FloatingText.ShowCriticalStrike(hero, 88f + round);
            await ToSignal(GetTree().CreateTimer(0.35f), "timeout");
            FloatingText.ShowOrdinaryDamage(hero, 20f + round);
            await ToSignal(GetTree().CreateTimer(0.35f), "timeout");
        }
    }

    private float FirstTagScreenY() => _textTagViews.Count > 0 ? _textTagViews[0].ScreenY : -1f;

    private int CountTagViews(string text)
    {
        int count = 0;
        foreach (TextTagFrame.TagView view in _textTagViews)
        {
            if (view.Text == text) count++;
        }
        return count;
    }

    private static string Describe(War3FloatingTextStyle style)
        => $"{style.Color.R * 255f:0},{style.Color.G * 255f:0},{style.Color.B * 255f:0}/{style.Alpha:0.##}";

    private static string Describe(Color color)
        => $"{color.R * 255f:0},{color.G * 255f:0},{color.B * 255f:0}";

    private static string Label(Unit unit) => unit == null ? "无" : unit.Type?.Name ?? "?";

    private string DescribeSelection()
    {
        var order = new List<string>();
        foreach (Unit unit in _selected) order.Add(unit?.Type?.Id ?? "?");
        return $"成员 {string.Join(",", order)} 共{_selected.Count} 个，主位 {PrimarySelection?.Type?.Id ?? "无"}";
    }

    public void RunRallyProbe()
    {
        int count = 0;
        foreach (Unit unit in _allUnits)
        {
            if (unit == null || !unit.Alive || unit.Player != ViewPlayer || !unit.IsBuilding) continue;
            if (unit.RallyPoint != null) continue;

            Vector3 point = unit.Position + new Vector3(240f, 0f, 240f);
            point.Y = MapWorldBuilder.HeightAt(_map.Terrain, point.X, point.Z);
            _world.SetRally(unit, point);
            GD.Print($"[RALLY] {unit.Type?.Name}({unit.Type?.Id}) 集结点 {point}");

            UnitType infantry = UnitFactory.Get("hfoo");
            if (infantry != null)
            {
                UnitType probe = infantry;
                Vector3 spot = ResolveTrainedSpot(unit, probe, out float yaw);
                float radius = Mathf.Max(16f, probe.CollisionRadius);
                bool free = _pathGrid == null || _pathGrid.IsAreaFree(spot, radius);
                float distance = new Vector2(spot.X - unit.Position.X, spot.Z - unit.Position.Z).Length();
                GD.Print($"[RALLY] 出兵落点 ({spot.X:0},{spot.Z:0}) 朝向 {yaw:0.00} 可走={free} 距建筑 {distance:0}（建筑半径 {unit.Radius:0}）");
            }
            if (++count >= 4) break;
        }
        GD.Print($"[RALLY] 共设 {count} 个集结点");
        ProbeWaterPath();
    }

    private void ProbeWaterPath()
    {
        if (_pathGrid == null || _map?.Terrain == null)
        {
            GD.Print("[WATERPATH] 无寻路网格，跳过");
            return;
        }

        var terrain = _map.Terrain;
        Vector3 start = default;
        Vector3 end = default;
        bool found = false;
        for (int cy = 4; cy < terrain.Height - 4 && !found; cy++)
        {
            for (int cx = 4; cx < terrain.Width - 4 && !found; cx++)
            {
                if (!terrain.Corners[cy * terrain.Width + cx].HasWater) continue;
                var point = new Vector3(-cx * 128f, 0f, cy * 128f);
                if (!OpenWater(point)) continue;

                for (int dx = 4; dx < 12; dx++)
                {
                    if (cx + dx >= terrain.Width - 4) break;
                    var other = new Vector3(-(cx + dx) * 128f, 0f, cy * 128f);
                    if (!OpenWater(other)) continue;
                    start = point;
                    end = other;
                    found = true;
                    break;
                }
            }
        }

        if (!found)
        {
            GD.Print("[WATERPATH] 地图里找不到连续水面，跳过");
            return;
        }

        bool groundBlocks = _pathGrid.IsBlockedWorld(start.X, start.Z, PathLayer.Ground);
        bool airFree = !_pathGrid.IsBlockedWorld(start.X, start.Z, PathLayer.Air);
        float waterY = MapWorldBuilder.WaterHeightAt(terrain, start.X, start.Z);
        var waterPath = _pathGrid.FindPath(start, end, 24f, PathLayer.Water);
        var airPath = _pathGrid.FindPath(start, end, 24f, PathLayer.Air);

        GD.Print($"[WATERPATH] 水面 ({start.X:0},{start.Z:0})→({end.X:0},{end.Z:0}) 地面阻挡={groundBlocks} 空中可走={airFree}"
            + $" 水面高度={waterY:0.0} 船航点={waterPath?.Count ?? -1} 空中航点={airPath?.Count ?? -1}");

        Unit boat = Spawn("nbot", ViewPlayer, start.X, start.Z, 0f, 0f, 1f);
        if (boat != null)
        {
            GD.Print($"[WATERPATH] 船生成 Y={boat.Position.Y:0.0} 水面={waterY:0.0} 分层={boat.Type.MoveType}");
        }
    }

    private bool OpenWater(Vector3 point)
    {
        if (_pathGrid == null) return false;
        if (!_pathGrid.IsAreaFree(point, 24f, PathLayer.Water)) return false;
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                float x = point.X + dx * 32f;
                float z = point.Z + dz * 32f;
                if (_pathGrid.IsBlockedWorld(x, z, PathLayer.Air)) return false;
            }
        }
        return true;
    }

    public void RunHireProbe()
    {
        Unit hall = FindHall(ViewPlayer);
        if (hall == null)
        {
            GD.Print("[HIRE] 找不到主基地，跳过");
            return;
        }

        PlayerSlot slot = Slot(ViewPlayer);
        slot.Gold = 3000;
        slot.Lumber = 800;
        RefreshHud();

        if (!HasBuilding(slot, "halt"))
        {
            Unit altar = Spawn("halt", ViewPlayer, hall.Position.X + 240f, hall.Position.Z - 200f, 0f, 0f, 1f);
            if (altar != null) _world.Units.Add(altar);
        }

        Unit tavern = Spawn("ntav", ViewPlayer, hall.Position.X + 520f, hall.Position.Z + 200f, 0f, 0f, 1f);
        if (tavern == null)
        {
            GD.Print("[HIRE] 酒馆模型缺失");
            return;
        }
        _world.Units.Add(tavern);

        Unit buyer = Spawn("hfoo", ViewPlayer, tavern.Position.X + 96f, tavern.Position.Z, 0f, 0f, 1f);
        if (buyer != null) _world.Units.Add(buyer);

        GD.Print($"[HIRE] 酒馆({tavern.Type.Name}) 卖 {tavern.Type.SellUnits.Length} 种：{DescribeSellUnits(tavern)}");
        GD.Print($"[HIRE] 购买半径 {ShopPurchaseRange(tavern):0}，初始库存 {DescribeUnitStock(tavern)}");

        var commands = new List<CommandEntry>();
        AddSellUnitCommands(commands, slot, tavern);
        var parts = new List<string>();
        foreach (CommandEntry entry in commands) parts.Add($"{entry.Name}／{entry.Cost}／{(entry.Enabled ? "可买" : "禁用")}");
        GD.Print($"[HIRE] 命令卡 {commands.Count} 格：{string.Join(" | ", parts)}");

        bool early = BuyUnitFromShop(tavern, ViewPlayer, "Nalc");
        GD.Print($"[HIRE] 未到 stockStart 直接买 Nalc：{(early ? "成功（不对）" : "失败（正确）")}");

        AdvanceShopSeconds(150f);
        GD.Print($"[HIRE] 过 150s 库存 {DescribeUnitStock(tavern)}");
        int goldBefore = slot.Gold;
        bool bought = BuyUnitFromShop(tavern, ViewPlayer, "Nalc");
        GD.Print($"[HIRE] 买 Nalc：{(bought ? "成功" : "失败")}  金币 {goldBefore}→{slot.Gold}"
            + $"  英雄 {HeroCount(ViewPlayer)}  剩余货 {tavern.UnitStockOf("Nalc")}");

        bool duplicate = BuyUnitFromShop(tavern, ViewPlayer, "Nalc");
        GD.Print($"[HIRE] 再买同名英雄：{(duplicate ? "成功（不对）" : "失败（正确，同名上限 1）")}");

        Unit camp = Spawn("nmer", ViewPlayer, hall.Position.X - 520f, hall.Position.Z + 200f, 0f, 0f, 1f);
        if (camp == null)
        {
            GD.Print("[HIRE] 雇佣兵营地模型缺失");
            return;
        }
        _world.Units.Add(camp);
        Unit campBuyer = Spawn("hfoo", ViewPlayer, camp.Position.X + 96f, camp.Position.Z, 0f, 0f, 1f);
        if (campBuyer != null) _world.Units.Add(campBuyer);

        GD.Print($"[HIRE] 雇佣兵营地({camp.Type.Name}) 卖 {camp.Type.SellUnits.Length} 种：{DescribeSellUnits(camp)}");

        // 佣兵占人口，先把供给补上（不然一定是「人口不足」而不是链路问题）
        for (int index = 0; index < 2; index++)
        {
            Unit farm = Spawn("hhou", ViewPlayer,
                hall.Position.X - 360f + index * 200f, hall.Position.Z - 260f, 0f, 0f, 1f);
            if (farm != null) _world.Units.Add(farm);
        }
        GD.Print($"[HIRE] 补两座农场后人口 {FoodUsed(ViewPlayer)}/{FoodCap(ViewPlayer)}");

        AdvanceShopSeconds(240f);
        GD.Print($"[HIRE] 过 240s 库存 {DescribeUnitStock(camp)}");
        bool merc = BuyUnitFromShop(camp, ViewPlayer, "nftb");
        GD.Print($"[HIRE] 买森林巨魔：{(merc ? "成功" : "失败")}  金币 {slot.Gold}  剩余货 {camp.UnitStockOf("nftb")}");

        UpdateCommandCard();
    }

    private string DescribeSellUnits(Unit shop)
    {
        var parts = new List<string>();
        foreach (string id in shop.Type.SellUnits)
        {
            UnitType type = UnitFactory.Get(id);
            if (type == null)
            {
                parts.Add($"{id}(缺数据)");
                continue;
            }
            parts.Add($"{type.Name}({id}) {type.GoldCost}金/{type.LumberCost}木/{type.FoodUsed}食");
        }
        return string.Join("、", parts);
    }

    private string DescribeUnitStock(Unit shop)
    {
        var parts = new List<string>();
        foreach (string id in shop.Type.SellUnits)
        {
            UnitType type = UnitFactory.Get(id);
            if (type == null) continue;
            parts.Add($"{id} {shop.UnitStockOf(id)}/{shop.UnitStockMaxOf(id)}");
        }
        return string.Join("、", parts);
    }

    private void AdvanceShopSeconds(float seconds)
    {
        int steps = Mathf.CeilToInt(seconds / World.FrameSeconds);
        for (int step = 0; step < steps; step++) _world.TickShops();
    }

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

        DamageData lethal = World.MakeAbilityDamage(null, hero, 100000f, AttackType.Spells, DamageType.Universal);
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
        var spawnedItems = new List<Item>();
        foreach (string itemId in new[] { "ckng", "phea" })
        {
            ItemType itemType = ItemCatalog.Get(itemId);
            if (itemType == null) continue;
            var spot = new Vector3(hero.Position.X + 40f, hero.Position.Y, hero.Position.Z + 40f);
            Item spawned = SpawnItem(itemId, spot);
            if (spawned != null) spawnedItems.Add(spawned);
        }
        foreach (Item spawned in spawnedItems) _world.IssueOrder(hero, UnitOrder.Pickup(spawned), true);
        for (int tick = 0; tick <= spawnedItems.Count; tick++) _world.Advance();

        _selected.Clear();
        _selected.Add(hero);
        hero.View?.SetSelected(true);
        RefreshSelection();
        UpdateCommandCard();
        GD.Print($"[SKILL] 命令卡：{DescribeCommandCard(hero)}");

        GD.Print("[SKILL] 应答节流：连点 5 次同一英雄，期望只响 1 次（语音时长+250ms）");
        for (int i = 0; i < 5; i++) _ackSounds?.PlaySelection(hero, true);
        GD.Print($"[SKILL] 应答计数 {_ackSounds?.SelectionCount}（满 3 次才转 Pissed）");
        PlaySelectionVoice(creep, false);
        GD.Print($"[SKILL] 选中敌方 {creep.Type.Name}（不可控）→ 期望 InterfaceClick");

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
                    + $" 波数{(blizzard as RuntimeBlizzardAbility)?.Waves} 半径{(blizzard as RuntimeBlizzardAbility)?.Radius:0}"
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
        RefreshSelection();

        Vector2 screen = _camera.Camera.UnprojectPosition(destination + new Vector3(0f, 40f, 0f));
        _worldInteraction.OrderAt(screen, false);

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
        RefreshSelection();
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
            if (ItemUseRules.Usable(picked.Type) && ItemUseRules.TargetModeFor(picked.Type) == ItemTargetMode.None)
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
    /// 凤凰整条链路：召唤 hphx → 生命归零被 Aphx 接管变成 hpxe 蛋 → 模型换成 RuntimePhoenixEggBuff → 10 秒后孵回。
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

    public int RunEventSoundProbe()
    {
        int failures = 0;
        void Verify(string label, bool ok)
        {
            if (!ok) failures++;
            GD.Print($"[EVENT-SOUND] {(ok ? "通过" : "不符")} {label}");
        }

        UnitType heroType = UnitFactory.Get("Hpal");
        if (heroType == null || _world == null)
        {
            GD.Print("[EVENT-SOUND] 缺少英雄类型或世界未就绪");
            return 1;
        }

        var ownHero = new Unit { Type = heroType, Player = ViewPlayer, IsHero = true };
        Verify("自己的英雄阵亡播 HeroDiesSound",
            HeroPresentation.DeathSound(ownHero, ViewPlayer, AreAllied) == "HeroDiesSound");

        ownHero.IsIllusion = true;
        Verify("幻象阵亡不出声",
            HeroPresentation.DeathSound(ownHero, ViewPlayer, AreAllied) == null);

        var silentHero = new Unit
        {
            Type = new UnitType { Id = "probe", HideHeroDeathMsg = true },
            Player = ViewPlayer,
            IsHero = true,
        };
        Verify("hideHeroDeathMsg 不出声",
            HeroPresentation.DeathSound(silentHero, ViewPlayer, AreAllied) == null);

        int other = -1;
        foreach (int id in _active)
        {
            if (id == ViewPlayer) continue;
            other = id;
            break;
        }
        if (other < 0)
        {
            GD.Print("[EVENT-SOUND] 只有一家参战，跳过同盟与攻击事件");
            GD.Print(failures == 0 ? "[EVENT-SOUND] 全部通过" : $"[EVENT-SOUND] {failures} 项不符");
            return failures == 0 ? 0 : 1;
        }

        var otherHero = new Unit { Type = heroType, Player = other, IsHero = true };
        Verify("敌方英雄阵亡不出声",
            HeroPresentation.DeathSound(otherHero, ViewPlayer, AreAllied) == null);

        _allyMasks.TryGetValue(ViewPlayer, out int savedView);
        _allyMasks.TryGetValue(other, out int savedOther);
        _allyMasks[ViewPlayer] = savedView | (1 << other);
        _allyMasks[other] = savedOther | (1 << ViewPlayer);
        Verify("盟友英雄阵亡播 AllyHeroDiesSound",
            HeroPresentation.DeathSound(otherHero, ViewPlayer, AreAllied) == "AllyHeroDiesSound");
        _allyMasks[ViewPlayer] = savedView;
        _allyMasks[other] = savedOther;

        Vector3 spot = FogProbeSpot();
        Unit attacker = ProbeUnit("hfoo", other, spot);
        Unit building = ProbeUnit("hhou", ViewPlayer, spot + new Vector3(128f, 0f, 0f));
        Unit soldier = ProbeUnit("hfoo", ViewPlayer, spot + new Vector3(128f, 0f, 128f));
        if (attacker == null || building == null || soldier == null)
        {
            GD.Print("[EVENT-SOUND] 缺单位类型，无法验证被攻击事件");
            return 1;
        }
        _world.Units.Add(attacker);
        _world.Units.Add(building);
        _world.Units.Add(soldier);

        int events = 0;
        Unit lastVictim = null;
        void Handler(Unit source, Unit victim) { events++; lastVictim = victim; }
        _world.UnitAttacked += Handler;

        _world.BeginAttack(attacker, building);
        Verify("起手时派发 UnitAttacked", events == 1 && ReferenceEquals(lastVictim, building));
        Verify("建筑被攻击选 TownAttackSound", AttackSoundOf(building) == "TownAttackSound");

        attacker.EndWindup();
        attacker.AttackCooldown = 0f;
        attacker.Backswing = 0f;
        events = 0;
        lastVictim = null;
        _world.BeginAttack(attacker, soldier);
        Verify("单位被攻击选 UnderAttackSound",
            events == 1 && ReferenceEquals(lastVictim, soldier)
            && AttackSoundOf(soldier) == "UnderAttackSound");

        _world.UnitAttacked -= Handler;

        GD.Print(failures == 0 ? "[EVENT-SOUND] 全部通过" : $"[EVENT-SOUND] {failures} 项不符");
        return failures == 0 ? 0 : 1;
    }

    private static Unit ProbeUnit(string id, int player, Vector3 position)
    {
        UnitType type = UnitFactory.Get(id);
        if (type == null) return null;
        return new Unit
        {
            Type = type,
            Player = player,
            Position = position,
            Hp = type.Hp,
            MaxHp = type.Hp,
            IsBuilding = type.IsBuilding,
        };
    }
}
