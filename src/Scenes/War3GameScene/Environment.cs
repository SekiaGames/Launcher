using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class War3GameScene
{
    /// <summary>
    /// MeleeInitVictoryDefeat 的最小版本：没有建筑即判负，只剩一家存活即结束。
    /// 完整客户端还有「建造/死亡/离开/同盟变化」触发器与 2 秒延迟检查，这里先对齐判定口径。
    /// </summary>
    private void TickClock()
    {
        float scale = 1f / Mathf.Max(0.02f, TimeScaleHint);
        _todHours += World.Tick * scale * 24f / Mathf.Max(60f, DaySeconds);
        if (_todHours >= 24f) _todHours -= 24f;
    }

    private bool IsNight => _todHours < 6f || _todHours >= 18f;

    /// <summary>白天用 sight、夜里用 nsight（UnitBalance 的两列）。</summary>
    private float SightOf(UnitType type) => type == null ? 800f : (IsNight ? type.SightNight : type.SightDay);

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

        _fog.Refresh(_allUnits, ViewPlayer, AreAllied, SightOf);
        if (!_fog.Enabled) return;

        foreach (Unit unit in _allUnits)
        {
            if (!unit.Alive || unit.View == null) continue;
            bool visible = !unit.InMine
                && (unit.Player == ViewPlayer || AreAllied(unit.Player, ViewPlayer) || _fog.IsVisible(unit.Position));
            unit.View.Root.Visible = visible;
        }
    }
}
