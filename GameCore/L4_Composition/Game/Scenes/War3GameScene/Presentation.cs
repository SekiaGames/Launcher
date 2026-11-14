using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

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
        public readonly War3MissileAnimation Animation = new();
        public AnimationPlayer Player;
        public float Age;
        public bool Impacted;
        public float ImpactAge;
        public Vector3 ImpactPosition;
        public float ImpactYaw;
    }

    private readonly List<Projectile> _projectiles = new();

    private void OnMissileFired(Unit attacker, Unit target)
    {
        if (attacker?.View == null || target == null) return;

        Node3D node = null;
        string missileScene = attacker.NextMissileScene ?? attacker.Type.MissileScene;
        attacker.NextMissileScene = null;
        PackedScene scene = Assets.LoadScene(missileScene);
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

        Vector3 from = LaunchPoint(attacker);
        float impactHeight = ImpactHeight(target);
        Vector3 to = target.Position + new Vector3(0f, impactHeight, 0f);
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
            TargetHeight = impactHeight,
            Player = node.FindChild("AnimationPlayer", true, false) as AnimationPlayer,
        };
        projectile.Animation.Start(projectile.Player);
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
        if (scene == null && EffectArtTable.TryResolveMissile(ability.Id, out string fallbackMissile))
        {
            scene = Assets.LoadScene(fallbackMissile);
        }
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

        Vector3 from = LaunchPoint(caster);
        float impactHeight = ImpactHeight(target);
        Vector3 to = target.Position + new Vector3(0f, impactHeight, 0f);
        node.Position = from;
        node.Name = $"AbilityMissile_{ability.Id}";
        AddChild(node);

        var projectile = new Projectile
        {
            Node = node,
            From = from,
            To = to,
            Arc = 0f,
            Speed = Mathf.Max(200f, ability.MissileSpeed),
            Length = Mathf.Max(1f, from.DistanceTo(to)),
            Target = target,
            TargetHeight = impactHeight,
            Player = node.FindChild("AnimationPlayer", true, false) as AnimationPlayer,
        };
        projectile.Animation.Start(projectile.Player);
        _projectiles.Add(projectile);

        if (_missileLogBudget > 0)
        {
            _missileLogBudget--;
            GD.Print($"[MISSILE] 技能 {ability.Name} → {target.Type?.Id} 距离 {from.DistanceTo(to):0}"
                + $" 速度 {ability.MissileSpeed:0} 模型 {(scene != null ? ability.MissileArt : "占位球")}");
        }
    }

    /// <summary>出手点：武器挂点优先，其次是右手，最后按身高估。</summary>
    private static Vector3 LaunchPoint(Unit shooter)
    {
        if (shooter == null) return Vector3.Zero;
        Node3D root = shooter.View?.Root;
        Node3D attach = AttachmentPoints.Find(root, "weapon")
            ?? AttachmentPoints.Find(root, "hand right")
            ?? AttachmentPoints.Find(root, "origin");
        if (attach != null) return attach.GlobalPosition;
        return shooter.Position + new Vector3(0f, (shooter.View?.Height ?? 100f) * 0.6f, 0f);
    }

    /// <summary>命中高度：胸口挂点优先，缺失时按身高一半。</summary>
    private static float ImpactHeight(Unit target)
    {
        if (target == null) return 0f;
        Node3D attach = AttachmentPoints.Find(target.View?.Root, "chest");
        if (attach != null) return attach.GlobalPosition.Y - target.Position.Y;
        return (target.View?.Height ?? 100f) * 0.5f;
    }

    /// <summary>引导中的范围盘：一个半透明圆盘放在落点上，半径取技能 Area。</summary>
    private sealed class ChannelEffect
    {
        public MeshInstance3D Node;
        public Ability Ability;
        public Vector3 Point;
    }

    private readonly Dictionary<Unit, ChannelEffect> _channelEffects = new();
    private static int _channelLogBudget = 12;

    private void OnChannelStarted(Unit caster, Ability ability, Vector3 point)
    {
        if (caster == null || ability == null) return;

        float radius = Mathf.Max(40f, ability.Area);
        float groundY = MapWorldBuilder.HeightAt(_map.Terrain, point.X, point.Z) + 4f;
        var node = new MeshInstance3D
        {
            Name = $"Channel_{ability.Id}",
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 1.5f, RadialSegments = 40 },
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = new Color(0.55f, 0.75f, 1f, 0.28f),
            },
            Position = new Vector3(point.X, groundY, point.Z),
        };
        AddChild(node);
        _channelEffects[caster] = new ChannelEffect { Node = node, Ability = ability, Point = point };

        if (_channelLogBudget <= 0) return;
        _channelLogBudget--;
        GD.Print($"[CHANNEL] {caster.Type?.Name} 开始引导 {ability.Name} Lv{ability.Level}"
            + $" 落点({point.X:0},{point.Z:0}) 半径{radius:0} 时长{ability.ChannelTotal:0}s"
            + $" 每{ability.ChannelWaveInterval:0.0}s一波");
    }

    private void OnChannelWave(Unit caster, Ability ability)
    {
        if (caster == null || ability == null) return;
        if (!_channelEffects.TryGetValue(caster, out ChannelEffect effect)) return;

        // 波次表现：圆盘闪一下，并在范围内随机落一颗技能特效（原版每波下落一套冰锥/腐化地面）
        if (effect.Node?.MaterialOverride is StandardMaterial3D material)
        {
            material.AlbedoColor = new Color(0.85f, 0.95f, 1f, 0.45f);
            SceneTreeTimer flash = GetTree().CreateTimer(0.15);
            flash.Timeout += () =>
            {
                if (IsInstanceValid(material)) material.AlbedoColor = new Color(0.55f, 0.75f, 1f, 0.28f);
            };
        }

        if (!EffectArtTable.TryResolve(ability.Id, out string resPath, out _, out _)) return;

        float radius = Mathf.Max(60f, ability.Area);
        float angle = (float)GD.RandRange(0d, Math.PI * 2d);
        float distance = (float)GD.RandRange(0d, radius);
        Vector3 point = effect.Point + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        point.Y = MapWorldBuilder.HeightAt(_map.Terrain, point.X, point.Z) + 2f;
        PlayGroundEffect(point, resPath, 1.1f);
    }

    private void OnChannelEnded(Unit caster, Ability ability, bool interrupted)
    {
        if (caster == null || !_channelEffects.TryGetValue(caster, out ChannelEffect effect)) return;

        if (effect.Node != null && IsInstanceValid(effect.Node)) effect.Node.QueueFree();
        _channelEffects.Remove(caster);

        if (interrupted && _channelLogBudget >= 0)
        {
            GD.Print($"[CHANNEL] {caster.Type?.Name} 的 {ability?.Name} 被打断");
        }
    }

    private void TickProjectiles(float delta)
    {        for (int index = _projectiles.Count - 1; index >= 0; index--)
        {
            Projectile projectile = _projectiles[index];
            if (projectile.Node == null || !IsInstanceValid(projectile.Node))
            {
                _projectiles.RemoveAt(index);
                continue;
            }

            projectile.Age += delta;
            if (!projectile.Impacted)
            {
                if (projectile.Target != null && projectile.Target.Alive)
                {
                    projectile.To = projectile.Target.Position
                        + new Vector3(0f, projectile.TargetHeight, 0f);
                }

                projectile.Travel += projectile.Speed * delta / projectile.Length;
                if (projectile.Travel >= 1f)
                {
                    projectile.Impacted = true;
                    projectile.ImpactAge = 0f;
                    projectile.ImpactPosition = projectile.To;
                    Vector3 impactDirection = projectile.To - projectile.From;
                    projectile.ImpactYaw = impactDirection.LengthSquared() > 1f
                        ? Mathf.Atan2(impactDirection.X, impactDirection.Z)
                        : projectile.Node.Rotation.Y;
                    projectile.Node.Position = projectile.ImpactPosition;
                    projectile.Node.Rotation = new Vector3(0f, projectile.ImpactYaw, 0f);
                    if (projectile.Animation.Advance(projectile.Player, projectile.Age, 0f))
                    {
                        projectile.Node.QueueFree();
                        _projectiles.RemoveAt(index);
                    }
                    continue;
                }

                Vector3 position = projectile.From.Lerp(projectile.To, projectile.Travel);
                position.Y += projectile.Arc * projectile.Length * 4f
                    * projectile.Travel * (1f - projectile.Travel);
                Vector3 direction = projectile.To - projectile.From;
                if (direction.LengthSquared() > 1f)
                {
                    projectile.Node.Rotation = new Vector3(0f,
                        Mathf.Atan2(direction.X, direction.Z), 0f);
                }
                projectile.Node.Position = position;
                projectile.Animation.Advance(projectile.Player, projectile.Age, null);
                continue;
            }

            projectile.ImpactAge += delta;
            projectile.Node.Position = projectile.ImpactPosition;
            projectile.Node.Rotation = new Vector3(0f, projectile.ImpactYaw, 0f);
            if (projectile.Animation.Advance(projectile.Player, projectile.Age, projectile.ImpactAge))
            {
                projectile.Node.QueueFree();
                _projectiles.RemoveAt(index);
            }
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

    /// <summary>只看得见的地方才听得到（迷雾外的战斗音不播）。</summary>
    private bool IsAudible(Vector3 position)
    {
        return _fog == null || !_fog.Enabled || _fog.IsVisible(position);
    }

    private void OnTreeChopped(Unit worker)
    {
        if (worker == null || !IsAudible(worker.Position)) return;
        if (!War3SoundTables.Has("MetalLightChopWood")) return;
        _audio?.Play("MetalLightChopWood", worker.Position + new Vector3(0f, 40f, 0f), 0.8f);
    }

    private void PlayPlacementSound(Vector3 ground)
    {
        if (!War3SoundTables.Has("PlaceBuildingDefault")) return;
        _audio?.Play("PlaceBuildingDefault", ground + new Vector3(0f, 40f, 0f));
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
        if (site.Player == ViewPlayer) _log?.Add($"{site.Type.Name} 建造完成");
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


    private sealed class FoundationRecord
    {
        public Unit Owner;
        public string Texture;
        public int BlendMode;
        public float Birth;
        public float Pause;
        public float Decay;
        public Color Start;
        public Color Middle;
        public Color End;
        public float Size;
        public float X;
        public float Y;
        public bool Positioned;
        public float Age;
        public float ReleaseAge = -1f;
        public bool Seen;
        public bool Drawable;
    }

    private readonly Dictionary<Unit, FoundationRecord> _buildingFoundations = new();
    private readonly List<Unit> _foundationExpired = new();
    private readonly Dictionary<string, List<ShadowRenderer.Quad>> _foundationStatic = new();
    private readonly Dictionary<string, List<ShadowRenderer.ColoredQuad>> _foundationFading = new();
    private readonly Dictionary<string, int> _foundationBlend = new();
    private ShadowRenderer _groundDecals;

    private void TickBuildingFoundations(float delta)
    {
        if (_worldRoot == null || _map?.Terrain == null) return;
        _groundDecals ??= new ShadowRenderer(_worldRoot, _map.Terrain);
        AdvanceFoundations(delta);
        foreach (Unit unit in _allUnits) ObserveFoundation(unit);
        DrawBuildingFoundations();
        _groundDecals.Flush();
        int drawn = _groundDecals.DrawnQuadCount;
        if (drawn != _foundationLogCount)
        {
            _foundationLogCount = drawn;
            GD.Print($"[DECAL] 建筑底座 {drawn} 块（记录 {_buildingFoundations.Count} 条）");
        }
    }

    private int _foundationLogCount = -1;

    private void AdvanceFoundations(float delta)
    {
        float step = Mathf.Max(0f, delta);
        _foundationExpired.Clear();
        foreach (FoundationRecord record in _buildingFoundations.Values)
        {
            if (record.Seen)
            {
                record.Age += step;
                record.Seen = false;
                continue;
            }
            if (record.ReleaseAge < 0f) record.ReleaseAge = 0f;
            record.ReleaseAge += step;
            if (record.Decay <= 0f || record.ReleaseAge >= record.Decay) _foundationExpired.Add(record.Owner);
        }
        for (int i = 0; i < _foundationExpired.Count; i++) _buildingFoundations.Remove(_foundationExpired[i]);
    }

    private void ObserveFoundation(Unit unit)
    {
        if (unit == null || !unit.IsBuilding || !unit.Alive || unit.View == null) return;
        UnitType type = unit.Type;
        if (type == null || !type.HasGroundSplat) return;
        if (!_buildingFoundations.TryGetValue(unit, out FoundationRecord record))
        {
            record = new FoundationRecord { Owner = unit, X = unit.Position.X, Y = unit.Position.Z };
            _buildingFoundations[unit] = record;
        }
        record.Texture = type.GroundSplatTexture;
        record.BlendMode = type.GroundSplatBlendMode;
        record.Birth = Mathf.Max(0f, type.GroundSplatBirth);
        record.Pause = Mathf.Max(0f, type.GroundSplatPause);
        record.Decay = Mathf.Max(0f, type.GroundSplatDecay);
        record.Start = type.GroundSplatStartColor;
        record.Middle = type.GroundSplatMiddleColor;
        record.End = type.GroundSplatEndColor;
        record.Size = type.GroundSplatScale * 2f;
        record.Seen = true;
        record.ReleaseAge = -1f;
        record.Drawable = IsFoundationVisible(unit);
    }

    private bool IsFoundationVisible(Unit unit)
    {
        if (_fog == null || !_fog.Enabled) return true;
        if (unit.Player == ViewPlayer || AreAllied(unit.Player, ViewPlayer)) return true;
        return _fog.IsVisible(unit.Position) || _fog.IsExplored(unit.Position);
    }

    private static Color FoundationMiddle(FoundationRecord record) =>
        record.Middle == new Color(0f, 0f, 0f, 0f) ? new Color(1f, 1f, 1f, 1f) : record.Middle;

    private static Color FoundationColor(FoundationRecord record)
    {
        Color middle = FoundationMiddle(record);
        if (record.Birth <= 0f && record.Decay <= 0f) return middle;
        Color start = record.Start == new Color(0f, 0f, 0f, 0f)
            ? new Color(middle.R, middle.G, middle.B, 0f) : record.Start;
        Color end = record.End == new Color(0f, 0f, 0f, 0f)
            ? new Color(middle.R, middle.G, middle.B, 0f) : record.End;
        if (record.Seen)
        {
            if (record.Birth <= 0f || record.Age >= record.Birth) return middle;
            return start.Lerp(middle, Mathf.Clamp(record.Age / record.Birth, 0f, 1f));
        }
        float releaseAge = Mathf.Max(0f, record.ReleaseAge);
        if (releaseAge <= 0f) return middle;
        if (record.Decay <= 0f || releaseAge >= record.Decay) return end;
        return middle.Lerp(end, Mathf.Clamp(releaseAge / record.Decay, 0f, 1f));
    }

    private void DrawBuildingFoundations()
    {
        _groundDecals.Begin();
        foreach (List<ShadowRenderer.Quad> list in _foundationStatic.Values) list.Clear();
        foreach (List<ShadowRenderer.ColoredQuad> list in _foundationFading.Values) list.Clear();
        _foundationBlend.Clear();

        foreach (FoundationRecord record in _buildingFoundations.Values)
        {
            Color color = FoundationColor(record);
            if (color.A <= 0.004f) continue;
            if (record.Seen)
            {
                Node3D root = record.Owner?.View?.Root;
                if (root == null) continue;
                record.X = root.Position.X;
                record.Y = root.Position.Z;
                record.Positioned = true;
                if (!record.Drawable) continue;
            }
            else if (!record.Positioned || !record.Drawable)
            {
                continue;
            }

            float half = record.Size * 0.5f;
            float mapX = 0f - record.X, mapY = record.Y;
            var quad = new ShadowRenderer.Quad(mapX - half, mapY - half, mapX + half, mapY + half);
            _foundationBlend[record.Texture] = record.BlendMode;
            if (color == FoundationMiddle(record))
            {
                if (!_foundationStatic.TryGetValue(record.Texture, out List<ShadowRenderer.Quad> list))
                {
                    list = new List<ShadowRenderer.Quad>();
                    _foundationStatic[record.Texture] = list;
                }
                list.Add(quad);
            }
            else
            {
                if (!_foundationFading.TryGetValue(record.Texture, out List<ShadowRenderer.ColoredQuad> list))
                {
                    list = new List<ShadowRenderer.ColoredQuad>();
                    _foundationFading[record.Texture] = list;
                }
                list.Add(new ShadowRenderer.ColoredQuad(quad, color));
            }
        }

        foreach (KeyValuePair<string, List<ShadowRenderer.Quad>> pair in _foundationStatic)
        {
            if (pair.Value.Count == 0) continue;
            int blend = _foundationBlend.GetValueOrDefault(pair.Key);
            _groundDecals.Draw(pair.Value, pair.Key, 1f, null, ShadowRenderer.GroundSplatOffset, blend);
        }
        foreach (KeyValuePair<string, List<ShadowRenderer.ColoredQuad>> pair in _foundationFading)
        {
            if (pair.Value.Count == 0) continue;
            int blend = _foundationBlend.GetValueOrDefault(pair.Key);
            _groundDecals.DrawColored(pair.Value, pair.Key, ShadowRenderer.GroundSplatOffset, blend);
        }
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

    private void PlayErrorSound()
    {
        PlayUiSound("InterfaceError", _camera.Target, ViewPlayer);
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
