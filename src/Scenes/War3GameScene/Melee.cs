using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class War3GameScene
{
    private void OnUnitDied(Unit unit)
    {
        if (unit != null) PlayVoice(unit, "Death");
        if (unit != null && unit.IsHero)
        {
            _log?.Add($"{PlayerName(unit.Player)} 的 {unit.Type?.Name} 阵亡");
        }

        if (_selected.Remove(unit))
        {
            _hud?.SetSelection(_selected);
            UpdateCommandCard();
        }
        RefreshHud();
    }

    private void TickVictoryCheck(float delta)
    {
        if (_gameOver) return;
        _victoryTimer += delta;
        if (_victoryTimer < 2f) return;
        _victoryTimer = 0f;

        var alive = new List<int>();
        foreach (int id in _active)
        {
            bool hasBuilding = false;
            foreach (Unit unit in _allUnits)
            {
                // 用类型级 IsBuilding：古树拔根后运行时不再是建筑，但仍然算「还有建筑」
                if (!unit.Alive || unit.Player != id || unit.Type == null || !unit.Type.IsBuilding) continue;
                hasBuilding = true;
                break;
            }

            if (!hasBuilding)
            {
                if (_defeated.Add(id))
                {
                    GD.Print($"[MELEE] P{id} {PlayerName(id)} 没有任何建筑，判负");
                    _hud?.SetHint($"{PlayerName(id)} 已被击败");
                }
                continue;
            }

            _defeated.Remove(id);
            alive.Add(id);
        }

        if (alive.Count == 0 || _active.Count < 2) return;
        bool oneTeamLeft = true;
        for (int i = 1; i < alive.Count; i++)
        {
            if (!AreAllied(alive[0], alive[i])) { oneTeamLeft = false; break; }
        }
        if (!oneTeamLeft) return;

        _gameOver = true;
        var winners = new List<string>();
        foreach (int id in alive) winners.Add(PlayerName(id));
        GD.Print($"[MELEE] 对局结束，胜者：{string.Join("、", winners)}");
        int version = IsReignOfChaos ? 0 : 1;
        _music?.PlayEnding(RaceMusicName(ViewPlayer), version, AreAllied(ViewPlayer, alive[0]));
        _hud?.SetHint($"对局结束，胜者：{string.Join("、", winners)}");
    }
}
