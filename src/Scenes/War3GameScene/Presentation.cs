using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

public sealed partial class War3GameScene
{
    private sealed class Projectile
    {
        public Node3D Node;
        public Vector3 From;
        public Vector3 To;
        public float Arc;
        public float Speed;
        public float Length = 1f;
        public float Travel;
        public Unit Target;
        public float TargetHeight = 60f;
    }

    private readonly List<Projectile> _projectiles = new();

    private void OnMissileFired(Unit attacker, Unit target)
    {
        if (attacker?.View == null || target == null) return;

        Node3D node = null;
        PackedScene scene = Assets.LoadScene(attacker.Type.MissileScene);
        if (scene != null)
        {
            node = scene.Instantiate<Node3D>();
            MapWorldBuilder.ApplyUnitTextureFilter(node);
        }
        else
        {
            node = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 12f, Height = 24f },
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    AlbedoColor = new Color(1f, 0.85f, 0.4f),
                },
            };
        }

        Vector3 from = attacker.Position + new Vector3(0f, attacker.View.Height * 0.6f, 0f);
        Vector3 to = target.Position + new Vector3(0f, (target.View?.Height ?? 100f) * 0.5f, 0f);
        node.Position = from;
        node.Name = $"Missile_{attacker.Type.Id}";
        AddChild(node);

        var projectile = new Projectile
        {
            Node = node,
            From = from,
            To = to,
            Arc = Mathf.Max(0f, attacker.Type.MissileArc),
            Speed = Mathf.Max(200f, attacker.Type.MissileSpeed),
            Length = Mathf.Max(1f, from.DistanceTo(to)),
            Target = target,
            TargetHeight = (target.View?.Height ?? 100f) * 0.5f,
        };
        _projectiles.Add(projectile);
        if (_missileLogBudget > 0)
        {
            _missileLogBudget--;
            GD.Print($"[MISSILE] {attacker.Type.Id} → {target.Type.Id} 距离 {projectile.Length:0} 速度 {projectile.Speed:0} 弧 {projectile.Arc:0.00}");
        }
    }

    private static int _missileLogBudget = 6;

    /// <summary>技能投射物：与武器共用 Projectile 飞行列表，只是模型与速度来自技能。</summary>
    private void OnAbilityMissileFired(Unit caster, Ability ability, Unit target)
    {
        if (caster?.View == null || target == null || ability == null) return;

        Node3D node = null;
        PackedScene scene = Assets.LoadScene(PathResolve.ModelScene(ability.MissileArt));
        if (scene != null)
        {
            node = scene.Instantiate<Node3D>();
            MapWorldBuilder.ApplyUnitTextureFilter(node);
        }
        else
        {
            node = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 14f, Height = 28f },
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    AlbedoColor = new Color(1f, 0.95f, 0.55f),
                },
            };
        }

        Vector3 from = caster.Position + new Vector3(0f, caster.View.Height * 0.6f, 0f);
        Vector3 to = target.Position + new Vector3(0f, (target.View?.Height ?? 100f) * 0.5f, 0f);
        node.Position = from;
        node.Name = $"AbilityMissile_{ability.Id}";
        AddChild(node);

        _projectiles.Add(new Projectile
        {
            Node = node,
            From = from,
            To = to,
            Arc = 0f,
            Speed = Mathf.Max(200f, ability.MissileSpeed),
            Length = Mathf.Max(1f, from.DistanceTo(to)),
            Target = target,
            TargetHeight = (target.View?.Height ?? 100f) * 0.5f,
        });

        if (_missileLogBudget > 0)
        {
            _missileLogBudget--;
            GD.Print($"[MISSILE] 技能 {ability.Name} → {target.Type?.Id} 距离 {from.DistanceTo(to):0}"
                + $" 速度 {ability.MissileSpeed:0} 模型 {(scene != null ? ability.MissileArt : "占位球")}");
        }
    }

    private void TickProjectiles(float delta)
    {
        for (int index = _projectiles.Count - 1; index >= 0; index--)
        {
            Projectile projectile = _projectiles[index];
            if (projectile.Node == null || !IsInstanceValid(projectile.Node))
            {
                _projectiles.RemoveAt(index);
                continue;
            }

            if (projectile.Target != null && projectile.Target.Alive)
            {
                projectile.To = projectile.Target.Position + new Vector3(0f, projectile.TargetHeight, 0f);
            }

            projectile.Travel += projectile.Speed * delta / projectile.Length;
            if (projectile.Travel >= 1f)
            {
                projectile.Node.QueueFree();
                _projectiles.RemoveAt(index);
                continue;
            }

            Vector3 position = projectile.From.Lerp(projectile.To, projectile.Travel);
            position.Y += projectile.Arc * projectile.Length * 4f * projectile.Travel * (1f - projectile.Travel);
            Vector3 direction = projectile.To - projectile.From;
            if (direction.LengthSquared() > 1f)
            {
                projectile.Node.Rotation = new Vector3(0f, Mathf.Atan2(direction.X, direction.Z), 0f);
            }
            projectile.Node.Position = position;
        }
    }

    /// <summary>客户端语音：选中 What、下令 Yes、死亡 Death（音效集取自 UnitUI.unitSound）。</summary>
    private void PlayVoice(Unit unit, string kind, string fallback = "Yes")
    {
        if (unit?.Type == null || _audio == null) return;
        if (string.IsNullOrEmpty(unit.Type.SoundSet)) return;

        string name = unit.Type.SoundSet + kind;
        if (!War3SoundTables.Has(name) && !string.IsNullOrEmpty(fallback)) name = unit.Type.SoundSet + fallback;
        if (!War3SoundTables.Has(name)) return;

        _audio.Play(name, unit.Position + new Vector3(0f, 80f, 0f));
    }

    /// <summary>只看得到的地方才听得到（迷雾外的战斗音不播）。</summary>
    private bool IsAudible(Vector3 position)
    {
        return _fog == null || !_fog.Enabled || _fog.IsVisible(position);
    }

    /// <summary>命中音：UnitWeapons.weapType1 + 目标材质（单位 Flesh / 建筑 Wood）。</summary>
    private void OnAttackLanded(Unit attacker, Unit target)
    {
        if (attacker?.Type == null || target == null) return;
        string weapon = attacker.Type.WeaponSound;
        if (string.IsNullOrEmpty(weapon)) return;
        if (!IsAudible(target.Position)) return;

        string name = weapon + (target.IsBuilding ? "Wood" : "Flesh");
        if (!War3SoundTables.Has(name)) return;
        _audio?.Play(name, target.Position + new Vector3(0f, 40f, 0f));
    }

    /// <summary>
    /// 建筑完工：客户端没有「每栋建筑一个完成音」的条目（Ready 是单位语音），
    /// 所以只有该音效集确实带 Ready 时才播，否则安静。
    /// </summary>
    private void OnConstructionFinished(Unit site)
    {
        if (site?.Type == null) return;
        PlayVoice(site, "Ready", null);
    }

    /// <summary>英雄复活：回到场上时播 Ready 语音（客户端同口径）。</summary>
    private void OnHeroRevived(Unit hero)
    {
        if (hero?.Type == null) return;
        PlayVoice(hero, "Ready", null);
        RefreshHud();
    }

    /// <summary>war3skins.txt 里的段名 / UI 音后缀：Human / Orc / Undead / NightElf。</summary>
    private string RaceMusicName(int player)
    {
        foreach (W3iPlayer entry in _map.Players)
        {
            if (entry.Id != player) continue;
            return entry.Race switch
            {
                2 => "Orc",
                3 => "Undead",
                4 => "NightElf",
                _ => "Human",
            };
        }
        return "Human";
    }

    /// <summary>UI 音里的种族后缀：Human / Orc / Undead / NightElf。</summary>
    private string RaceSoundSuffix(int player)
    {
        foreach (W3iPlayer entry in _map.Players)
        {
            if (entry.Id != player) continue;
            return entry.Race switch
            {
                2 => "Orc",
                3 => "Undead",
                4 => "NightElf",
                _ => "Human",
            };
        }
        return "Human";
    }

    private static int _uiSoundLogBudget = 6;

    private void PlayUiSound(string soundName, Vector3 position, int player)
    {
        if (player != ViewPlayer) return;
        if (!War3SoundTables.Has(soundName)) return;
        _audio?.Play(soundName, position);
        if (_uiSoundLogBudget > 0)
        {
            _uiSoundLogBudget--;
            GD.Print($"[AUDIO] UI {soundName}");
        }
    }

    private readonly Dictionary<Unit, (int Count, float Last)> _clickMarks = new();

    /// <summary>客户端口径：同一个单位在短时间内被点 4 次会切到 Pissed 语音。</summary>
    private void PlaySelectionVoice()
    {
        Unit unit = null;
        foreach (Unit candidate in _selected)
        {
            if (!candidate.Alive) continue;
            unit = candidate;
            break;
        }
        if (unit == null) return;

        float now = Time.GetTicksMsec() / 1000f;
        if (!_clickMarks.TryGetValue(unit, out (int Count, float Last) mark) || now - mark.Last > 12f)
        {
            mark = (0, now);
        }
        mark.Count++;
        mark.Last = now;
        _clickMarks[unit] = mark;

        if (mark.Count >= 4)
        {
            _clickMarks[unit] = (0, now);
            PlayVoice(unit, "Pissed", "What");
            return;
        }

        PlayVoice(unit, "What");
    }
}
