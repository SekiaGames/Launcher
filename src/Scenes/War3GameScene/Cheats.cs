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
}
