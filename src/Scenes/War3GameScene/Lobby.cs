using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class War3GameScene
{
    private sealed class PlayerSlot
    {
        public int Id;
        public int Gold;
        public int Lumber;
        public bool Ai = true;
        public bool Done;
        public bool Announced;
        public float Timer;
        public float StatusTimer;
        /// <summary>AI 集火/买装备的节流计时。</summary>
        public float FocusTimer;
        public float ShopTimer;
        /// <summary>AI 守家阵型重排的节流计时。</summary>
        public float FormationTimer;
        /// <summary>最近一次统计的对手阵容（每 2 秒刷新），出兵与波次编成按它打分。</summary>
        public EnemyComposition Enemy = new();
        public float EnemyTimer;
        public readonly HashSet<string> KnownBuildings = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> Research = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> BuiltCounts = new(StringComparer.OrdinalIgnoreCase);
        public readonly List<InhouseTask> Tasks = new();

        public int ResearchLevel(string id) => Research.TryGetValue(id, out int level) ? level : 0;
        public int BuiltCount(string id) => BuiltCounts.TryGetValue(id, out int count) ? count : 0;
    }

    private sealed class InhouseTask
    {
        public string Kind;
        public string Id;
        public int Total = 1;
        public string Label = "";
    }

    private readonly PlayerSlot[] _players = new PlayerSlot[24];

    private PlayerSlot Slot(int id)
    {
        if (id < 0 || id >= _players.Length) return null;
        return _players[id] ??= new PlayerSlot { Id = id };
    }

    private IEnumerable<PlayerSlot> Slots()
    {
        foreach (int id in _active)
        {
            PlayerSlot slot = Slot(id);
            if (slot != null) yield return slot;
        }
    }

    private void ResolveActivePlayers()
    {
        _active.Clear();
        _playerNames.Clear();

        var seats = new List<W3iPlayer>();
        foreach (W3iPlayer player in _map.Players)
        {
            if (!MeleeRules.IsPlayingController(player.Controller)) continue;
            if (player.StartX == 0f && player.StartY == 0f) continue;
            seats.Add(player);
            _playerNames[player.Id] = War3Strings.Resolve(player.Name);
        }

        if (seats.Count == 0)
        {
            GD.Print("[INIT] w3i 没有可玩座位，退回 P0");
            _active.Add(0);
            ViewPlayer = 0;
            return;
        }

        if (ActiveSlots.Count > 0)
        {
            foreach (W3iPlayer player in seats)
            {
                if (ActiveSlots.Contains(player.Id)) _active.Add(player.Id);
            }
        }
        else if (ActivePlayers > 0)
        {
            for (int i = 0; i < seats.Count && i < ActivePlayers; i++) _active.Add(seats[i].Id);
        }
        else
        {
            foreach (W3iPlayer player in seats) _active.Add(player.Id);
        }

        if (_active.Count == 0) _active.Add(seats[0].Id);
        if (!_active.Contains(ViewPlayer)) ViewPlayer = seats[0].Id;

        BuildAlliances();

        var active = new List<int>(_active);
        active.Sort();
        GD.Print($"[INIT] 地图可玩座位 {seats.Count} 个（{string.Join(" ", seats.ConvertAll(p => $"P{p.Id}/{RaceName(p.Race)}"))}），"
            + $"本局参战 {active.Count} 个：{string.Join(" ", active.ConvertAll(id => $"P{id}"))}");
    }

    /// <summary>
    /// 同盟来自 w3i 的 forces（同一 force 且带 AlliedFlag），没有 forces 时退回玩家的 ally 位掩码。
    /// 客户端 GetPlayerAlliance 读的就是这份初始状态。
    /// </summary>
    private void BuildAlliances()
    {
        _allyMasks.Clear();

        if (Teams.Count > 0)
        {
            foreach (List<int> team in Teams)
            {
                int mask = 0;
                foreach (int id in team) mask |= 1 << id;
                foreach (int id in team) _allyMasks[id] = mask;
            }
            var lobby = new List<string>();
            foreach (List<int> team in Teams)
            {
                if (team.Count > 1) lobby.Add(string.Join("+", team.ConvertAll(id => $"P{id}")));
            }
            GD.Print(lobby.Count > 0 ? $"[INIT] 大厅队伍：{string.Join(" | ", lobby)}" : "[INIT] 大厅队伍：无（各自为战）");
            return;
        }

        if (_map.Info != null && _map.Info.Forces.Count > 0)
        {
            foreach (W3iForce force in _map.Info.Forces)
            {
                if ((force.Flags & W3iForce.AlliedFlag) == 0) continue;
                foreach (W3iPlayer player in _map.Players)
                {
                    if (!force.ContainsPlayer(player.Id)) continue;
                    _allyMasks.TryGetValue(player.Id, out int mask);
                    _allyMasks[player.Id] = mask | (int)force.PlayerMask;
                }
            }
        }

        // 注意：w3i 的 allyLow/allyHigh 是 AI 的盟友优先级位，不是同盟关系。
        // 实测海龟岛 FFA 每人是「自己那一位」（P0=8/P1=4/P2=2/P3=1，位序还是反的），
        // 拿来判定同盟会把 FFA 误判成盟友，所以只用 forces 与大厅 --teams。

        var groups = new List<string>();
        foreach (W3iForce force in _map.Info?.Forces ?? new List<W3iForce>())
        {
            if ((force.Flags & W3iForce.AlliedFlag) == 0) continue;
            var members = new List<string>();
            foreach (W3iPlayer player in _map.Players)
            {
                if (force.ContainsPlayer(player.Id) && IsActivePlayer(player.Id)) members.Add($"P{player.Id}");
            }
            if (members.Count > 1) groups.Add(string.Join("+", members));
        }
        GD.Print(groups.Count > 0
            ? $"[INIT] 同盟队伍：{string.Join(" | ", groups)}"
            : "[INIT] 同盟队伍：无（各自为战）");
    }

    /// <summary>对局层同盟判定：同队(true)、野怪互不攻击、中立被动不参战。</summary>
    public bool AreAllied(int a, int b)
    {
        if (a == b) return true;
        if (a == MeleeRules.PlayerNeutralHostile && b == MeleeRules.PlayerNeutralHostile) return true;
        if (a is MeleeRules.PlayerNeutralHostile or MeleeRules.PlayerNeutralPassive) return false;
        if (b is MeleeRules.PlayerNeutralHostile or MeleeRules.PlayerNeutralPassive) return false;
        if (_allyMasks.TryGetValue(a, out int mask) && (mask & (1 << b)) != 0) return true;
        return false;
    }

    private static string RaceName(int race)
    {
        return race switch
        {
            1 => "人",
            2 => "兽",
            3 => "亡",
            4 => "暗夜",
            _ => $"种族{race}",
        };
    }

    private string PlayerName(int player)
    {
        if (_playerNames.TryGetValue(player, out string name) && !string.IsNullOrWhiteSpace(name)) return name;
        return $"P{player}";
    }
}
