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
    private float _sceneLightingHour = -1f;

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

    private void TickFog(float delta)
    {
        if (_fog == null) return;

        _fogTimer += delta;
        if (_fogTimer < 0.2f) return;
        _fogTimer = 0f;

        _fog.Refresh(_allUnits, ViewPlayer, AreAllied, SightOf, _world?.Reveals);
        _terrainBuilder?.BindWaterFogOfWar(_fog.VisibilityTexture, _fog.SampleUv, _fog.Enabled);
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
}
