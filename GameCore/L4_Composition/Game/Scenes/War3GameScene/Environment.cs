using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    /// <summary>
    /// MeleeInitVictoryDefeat 的最小版本：没有建筑即判负，只剩一家存活即结束。
    /// 完整客户端还有「建造/死亡/离开/同盟变化」触发器与 2 秒延迟检查，这里先对齐判定口径。
    /// </summary>
    private SceneLighting _sceneLighting;
    private War3FogConfig _fogConfig = War3FogConfig.None;
    private float _sceneLightingHour = -1f;
    private float _terrainLightTimer = 0.1f;

    private void TickTerrainLights(float delta)
    {
        if (_terrainBuilder?.TerrainMaterial == null) return;
        _terrainLightTimer += delta;
        if (_terrainLightTimer < 0.1f) return;
        _terrainLightTimer = 0f;

        var lights = new DynamicLightCollector();
        Camera3D camera = _camera?.Camera;
        foreach (Node3D node in _terrainBuilder.DoodadLightNodes)
        {
            if (lights.IsFull) break;
            if (!GodotObject.IsInstanceValid(node) || !node.IsVisibleInTree()) continue;
            if (camera != null && !camera.IsPositionInFrustum(node.GlobalPosition)) continue;
            lights.Add(DynamicLight.FromNode(node));
        }
        foreach (Unit unit in _allUnits)
        {
            if (lights.IsFull) break;
            UnitSceneComponent view = unit?.View;
            if (view == null || view.FogFrozen || !view.Root.Visible) continue;
            foreach (Node3D node in view.LightNodes)
            {
                if (lights.IsFull) break;
                if (camera != null && !camera.IsPositionInFrustum(node.GlobalPosition)) continue;
                lights.Add(DynamicLight.FromNode(node));
            }
        }
        _terrainBuilder.ApplyDynamicLights(lights);
    }

    private readonly ScoutingMemory<UnitSceneComponent> _buildingScouting = new();
    private readonly Dictionary<int, UnitSceneComponent> _retiredBuildingViews = new();

    private void TickClock()
    {
        float scale = 1f / Mathf.Max(0.02f, TimeScaleHint);
        _todHours += World.FrameSeconds * scale * 24f / Mathf.Max(60f, DaySeconds);
        if (_todHours >= 24f) _todHours -= 24f;
        if (_world != null) _world.IsNight = IsNight;
        if (_sceneLighting != null && Mathf.Abs(_todHours - _sceneLightingHour) >= 0.05f)
        {
            _sceneLightingHour = _todHours;
            _sceneLighting.Update(_todHours);
            SceneLightRig.Apply(this, _sceneLighting.Terrain, _sceneLighting.Units);
        }
    }

    private bool IsNight => _todHours < 6f || _todHours >= 18f;

    /// <summary>白天用 sight、夜里用 nsight（UnitBalance 的两列）；夜视与物品视野在这里叠加。</summary>
    private float SightOf(Unit unit)
    {
        if (unit?.Type == null) return 800f;

        float bonus = unit.BonusSight;
        bool nightSight = false;
        foreach (Ability ability in unit.Abilities)
        {
            if (!ability.Learned) continue;
            bonus += ability.PassiveSightBonus;
            nightSight |= ability.NightSight;
        }

        float sight = IsNight && !nightSight ? unit.Type.SightNight : unit.Type.SightDay;
        return sight + bonus;
    }

    private string ClockText()
    {
        int hours = Mathf.FloorToInt(_todHours);
        int minutes = Mathf.FloorToInt((_todHours - hours) * 60f);
        return $"{hours:00}:{minutes:00}" + (IsNight ? " 夜" : "");
    }

    /// <summary>迷雾外看不见的敌人也不出声（脚步/建造循环音同口径）。</summary>
    private bool UnitAudibleForAudio(Unit unit)
    {
        if (unit == null) return false;
        bool own = unit.Player == ViewPlayer || AreAllied(unit.Player, ViewPlayer);
        return own || _fog == null || !_fog.Enabled || _fog.IsVisible(unit.Position);
    }

    private void TickFog(float delta)
    {
        if (_fog == null) return;

        _fogTimer += delta;
        if (_fogTimer < 0.2f) return;
        _fogTimer = 0f;

        _fog.Refresh(_allUnits, ViewPlayer, AreAllied, SightOf, _world?.Reveals);
        RefreshBuildingScouting();
        if (!_fog.Enabled) return;

        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.View == null) continue;
            // 进矿的侍僧、装进货舱的尸体与驻军里的苦工都不显示模型
            if (unit.InMine || unit.CorpseCarrier != null || unit.IsCargoed)
            {
                unit.View.Root.Visible = false;
                continue;
            }
            bool own = unit.Player == ViewPlayer || AreAllied(unit.Player, ViewPlayer);
            bool visible = own || _fog.IsVisible(unit.Position);
            // 隐身（疾风步）：敌方看不见（有反隐单位/岗哨在附近时照常显示）；自己/盟友能看见，但只能看到一个淡影
            if (unit.State.Invisible) visible = own || (_fog.IsVisible(unit.Position) && _world != null && _world.IsDetectedBy(ViewPlayer, unit));

            if (visible)
            {
                unit.View.FogSeen = true;
                unit.View.SetFogFrozen(false);
                unit.View.Root.Visible = true;
                continue;
            }

            if (unit.IsBuilding && unit.View.FogSeen && _fog.IsExplored(unit.Position))
            {
                unit.View.SetFogFrozen(true);
                continue;
            }

            unit.View.Root.Visible = false;
        }
    }

    private void RefreshBuildingScouting()
    {
        _buildingScouting.Begin(_fog, _retiredBuildingViews);
        foreach (KeyValuePair<int, UnitSceneComponent> pair in _retiredBuildingViews)
        {
            UnitSceneComponent view = pair.Value;
            if (view?.Root == null || !GodotObject.IsInstanceValid(view.Root)) continue;
            view.FogMemorial = false;
            view.Root.Visible = false;
        }
        _retiredBuildingViews.Clear();

        foreach (KeyValuePair<int, ScoutingMemory<UnitSceneComponent>.Entry> pair
            in _buildingScouting.Entries)
        {
            ScoutingMemory<UnitSceneComponent>.Entry entry = pair.Value;
            var position = new Vector3(entry.X, 0f, entry.Y);
            if (_fog.IsVisible(position) || !_fog.IsExplored(position)) continue;
            entry.State.Root.Visible = true;
        }
    }

    public int RunPauseProbe()
    {
        int failures = 0;
        void Verify(string label, bool ok)
        {
            if (!ok) failures++;
            GD.Print($"[PAUSE] {(ok ? "通过" : "不符")} {label}");
        }

        if (_newHud == null)
        {
            GD.Print("[PAUSE] HUD 未就绪");
            return 1;
        }

        bool manual = Paused;
        Verify("初始未因对话框暂停", !MenuPauseRequested);
        _newHud.ShowAboutDialog();
        Verify("设置页打开时暂停模拟", SimulationBlocked && MenuPauseRequested);
        Verify("不改写手动暂停状态", Paused == manual);
        _newHud.HideAboutDialog();
        Verify("关闭设置页恢复模拟", !SimulationBlocked || manual);
        Paused = true;
        _newHud.ShowAboutDialog();
        _newHud.HideAboutDialog();
        Verify("关闭设置页保留手动暂停", SimulationBlocked);
        Paused = manual;
        Verify("手动暂停状态已还原", Paused == manual);
        GD.Print(failures == 0 ? "[PAUSE] 全部通过" : $"[PAUSE] {failures} 项不符");
        return failures;
    }

    public int RunFogMemoryProbe()
    {
        int failures = 0;
        void Verify(string label, bool ok)
        {
            if (!ok) failures++;
            GD.Print($"[FOG-MEM] {(ok ? "通过" : "不符")} {label}");
        }

        int enemy = -1;
        foreach (int id in _active)
        {
            if (id == ViewPlayer || AreAllied(id, ViewPlayer)) continue;
            enemy = id;
            break;
        }
        if (enemy < 0 || _fog == null || _world == null)
        {
            GD.Print("[FOG-MEM] 缺少敌方玩家或迷雾/世界未就绪");
            return 1;
        }

        Vector3 spot = FogProbeSpot();
        Unit seenBuilding = Spawn("hhou", enemy, spot.X, spot.Z, 0f, 0f, 1f);
        if (seenBuilding == null)
        {
            GD.Print("[FOG-MEM] 缺建筑模型 hhou");
            return 1;
        }

        _world.AddFogReveal(ViewPlayer, spot, FogOfWar.FogCellSize * 2f, 60f);
        TickFog(0.25f);
        Verify("首次看见后记为已看到", seenBuilding.View.FogSeen);
        Verify("首次看见时不冻结", !seenBuilding.View.FogFrozen && seenBuilding.View.Root.Visible);

        _world.Reveals.Clear();
        TickFog(0.25f);
        Verify("离开视野后定格", seenBuilding.View.FogFrozen && seenBuilding.View.Root.Visible);

        _world.KillUnit(seenBuilding);
        Verify("雾中阵亡保留定格模型",
            seenBuilding.View.FogMemorial && seenBuilding.View.Root.Visible
            && seenBuilding.DeathPresentationGone);
        seenBuilding.TickCorpse(seenBuilding.DecayLife + 1f);
        Verify("尸体计时不隐藏定格模型", seenBuilding.View.Root.Visible);

        TickFog(0.25f);
        Verify("雾里持续保留定格模型",
            seenBuilding.View.FogMemorial && seenBuilding.View.Root.Visible
            && _buildingScouting.Entries.Count() == 1);

        _world.AddFogReveal(ViewPlayer, spot, FogOfWar.FogCellSize * 2f, 60f);
        TickFog(0.25f);
        Verify("重新看见后清掉定格模型",
            !seenBuilding.View.FogMemorial && !seenBuilding.View.Root.Visible);
        Verify("记忆表清空", _buildingScouting.Entries.Count() == 0);

        Unit visibleBuilding = Spawn("hhou", enemy, spot.X, spot.Z, 0f, 0f, 1f);
        if (visibleBuilding == null)
        {
            GD.Print("[FOG-MEM] 缺建筑模型 hhou");
            return 1;
        }
        TickFog(0.25f);
        _world.KillUnit(visibleBuilding);
        Verify("看得见时阵亡不进记忆",
            !visibleBuilding.View.FogMemorial && _buildingScouting.Entries.Count() == 0);
        visibleBuilding.TickCorpse(visibleBuilding.DecayLife + 1f);
        Verify("看得见时阵亡照常隐藏", !visibleBuilding.View.Root.Visible);

        _world.Reveals.Clear();
        TickFog(0.25f);
        Unit unseenBuilding = Spawn("hhou", enemy, spot.X, spot.Z, 0f, 0f, 1f);
        if (unseenBuilding == null)
        {
            GD.Print("[FOG-MEM] 缺建筑模型 hhou");
            return 1;
        }
        _world.KillUnit(unseenBuilding);
        Verify("没看过的建筑不留记忆",
            !unseenBuilding.View.FogMemorial && _buildingScouting.Entries.Count() == 0);

        GD.Print(failures == 0 ? "[FOG-MEM] 全部通过" : $"[FOG-MEM] {failures} 项不符");
        return failures == 0 ? 0 : 1;
    }

    private Vector3 FogProbeSpot()
    {
        Vector3 best = Vector3.Zero;
        float bestDistance = -1f;
        for (int gy = 1; gy < _map.Terrain.Height; gy += 8)
        {
            for (int gx = 1; gx < _map.Terrain.Width; gx += 8)
            {
                Vector3 world = ToWorld(_map.Terrain.OffsetX + gx * FogOfWar.FogCellSize,
                    _map.Terrain.OffsetY + gy * FogOfWar.FogCellSize);
                float nearest = float.MaxValue;
                foreach (Unit unit in _allUnits)
                {
                    if (!unit.Alive) continue;
                    float distance = new Vector2(unit.Position.X - world.X,
                        unit.Position.Z - world.Z).Length();
                    if (distance < nearest) nearest = distance;
                }
                if (nearest <= bestDistance) continue;
                bestDistance = nearest;
                best = world;
            }
        }
        return best;
    }
}
