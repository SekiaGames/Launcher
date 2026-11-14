using Godot;
using System;
using System.Collections.Generic;
using NVector4 = System.Numerics.Vector4;

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

        IReadOnlyList<string> arts = ChannelWaveArts(ability);
        if (arts.Count == 0) return;
        if (_world == null) return;

        float radius = Mathf.Max(60f, ability.Area);
        for (int index = 0; index < arts.Count; index++)
        {
            float angle = (float)GD.RandRange(0d, Math.PI * 2d);
            float distance = (float)GD.RandRange(0d, radius);
            Vector3 point = effect.Point + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            _world.AddSpecialEffect(arts[index], new Vec2(point.X, point.Z), lifeSpan: 1.1f);
        }
    }

    private static IReadOnlyList<string> ChannelWaveArts(Ability ability)
    {
        if (ability.AreaEffectArts.Count > 0) return ability.AreaEffectArts;
        if (ability.SpecialArts.Count > 0) return ability.SpecialArts;
        return Array.Empty<string>();
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
    private readonly War3SplatSystem _groundSplats = new();

    private void TickGroundDecals(float delta)
    {
        if (_worldRoot == null || _map?.Terrain == null) return;
        _groundDecals ??= new ShadowRenderer(_worldRoot, _map.Terrain);
        _groundDecals.Begin();
        AdvanceFoundations(delta);
        foreach (Unit unit in _allUnits) ObserveFoundation(unit);
        DrawBuildingFoundations();
        TickSplatEvents(delta);
        _groundSplats.Draw(_groundDecals);
        _groundDecals.Flush();
        int drawn = _groundDecals.DrawnQuadCount;
        if (drawn != _foundationLogCount)
        {
            _foundationLogCount = drawn;
            GD.Print($"[DECAL] 地面贴花 {drawn} 块（底座 {_buildingFoundations.Count} 条，事件 {_groundSplats.Count} 个）");
        }
    }

    private void TickSplatEvents(float delta)
    {
        _groundSplats.Update(delta);
        foreach (Unit unit in _allUnits)
        {
            UnitSceneComponent view = unit?.View;
            if (view == null || view.Root == null || !IsInstanceValid(view.Root)) continue;
            view.ResolveModelEvents()?.Tick(_groundSplats.Add, view.Root.Visible);
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

    private readonly struct VisualEffectKey : IEquatable<VisualEffectKey>
    {
        public readonly int Kind;
        public readonly object Owner;
        public readonly int Art;
        public readonly int Attachment;

        public VisualEffectKey(int kind, object owner, int art, int attachment)
        {
            Kind = kind;
            Owner = owner;
            Art = art;
            Attachment = attachment;
        }

        public bool Equals(VisualEffectKey other) => Kind == other.Kind
            && ReferenceEquals(Owner, other.Owner) && Art == other.Art && Attachment == other.Attachment;

        public override bool Equals(object other) => other is VisualEffectKey key && Equals(key);

        public override int GetHashCode() => HashCode.Combine(Kind, Owner == null ? 0
            : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Owner), Art, Attachment);
    }

    private readonly struct VisualEffectRequest
    {
        public readonly VisualEffectKey Key;
        public readonly string ModelPath;
        public readonly Unit Target;
        public readonly string Attachment;
        public readonly Vector3 Position;
        public readonly bool WorldPosition;
        public readonly float Scale;
        public readonly float Age;
        public readonly bool Death;
        public readonly NVector4 Color;
        public readonly int TeamColor;
        public readonly bool StandAfterBirth;
        public readonly string PrimaryAnimation;
        public readonly string AnimationProperties;
        public readonly SpecialEffect Special;

        public VisualEffectRequest(VisualEffectKey key, string modelPath, Unit target, string attachment,
            Vector3 position, bool worldPosition, float scale, float age, bool death, NVector4 color,
            int teamColor, bool standAfterBirth, string primaryAnimation, string animationProperties,
            SpecialEffect special)
        {
            Key = key;
            ModelPath = modelPath;
            Target = target;
            Attachment = attachment;
            Position = position;
            WorldPosition = worldPosition;
            Scale = scale;
            Age = age;
            Death = death;
            Color = color;
            TeamColor = teamColor;
            StandAfterBirth = standAfterBirth;
            PrimaryAnimation = primaryAnimation;
            AnimationProperties = animationProperties;
            Special = special;
        }
    }

    private sealed class VisualEffectNode
    {
        public Node3D Root;
        public AnimationPlayer Player;
        public Unit Target;
        public string Attachment;
        public string SequenceKey;
        public string Sequence;
        public int LastSeenFrame;
        public int TeamColor = int.MinValue;
        public bool Failed;
        public ModelEventEmitter Events;
        public bool EventsResolved;
    }

    private readonly Dictionary<VisualEffectKey, VisualEffectNode> _visualEffects = new();
    private readonly List<VisualEffectKey> _staleVisualEffects = new();
    private readonly HashSet<string> _missingEffectModels = new(StringComparer.OrdinalIgnoreCase);
    private int _visualEffectFrame;

    public int ActiveEffectCount => _visualEffects.Count;

    private void RenderVisualEffects(float delta)
    {
        if (_worldRoot == null) return;
        _visualEffectFrame++;
        CollectWorldEffects();
        CollectUnitEffectVisuals();
        RemoveUnseenVisualEffects();
        ReportVisualEffects(delta);
    }

    private float _visualEffectReportTimer;
    private int _visualEffectReported = -1;
    private int _visualEffectLoadLogBudget = 20;

    private void ReportVisualEffects(float delta)
    {
        _visualEffectReportTimer += delta;
        if (_visualEffectReportTimer < 2f) return;
        _visualEffectReportTimer = 0f;
        if (_visualEffects.Count == _visualEffectReported) return;
        _visualEffectReported = _visualEffects.Count;
        GD.Print($"[FX] 在场特效 {_visualEffects.Count}");
    }

    private void CollectWorldEffects()
    {
        if (_world == null) return;
        IReadOnlyList<Effect> effects = _world.Effects;
        for (int index = 0; index < effects.Count; index++)
        {
            Effect effect = effects[index];
            if (effect is MissileEffect) continue;
            string modelPath = effect.VisualModelPath;
            if (string.IsNullOrWhiteSpace(modelPath)) continue;
            Unit target = effect.VisualTarget;
            if (target != null && (target.View?.Root == null || !target.View.Root.Visible)) continue;
            SpecialEffect special = effect as SpecialEffect;
            int teamColor = special != null && special.TeamColorIndex >= 0
                ? special.TeamColorIndex
                : target?.TeamColorIndex ?? 0;
            float ground = _map?.Terrain != null
                ? MapWorldBuilder.HeightAt(_map.Terrain, effect.Pos.X, effect.Pos.Y) : 0f;
            DrawVisualEffect(new VisualEffectRequest(
                new VisualEffectKey(0, effect, 0, 0), modelPath, target, effect.VisualAttachmentPoint,
                new Vector3(effect.Pos.X, ground + effect.VisualHeight, effect.Pos.Y),
                target == null, effect.VisualScale, effect.VisualAgeSeconds,
                special != null && special.Destroyed, special?.Color ?? NVector4.One, teamColor,
                special != null && special.PlayStandAfterBirth, special?.AnimationName,
                special == null ? null : string.Join(' ', special.AnimationSubProperties), special));
        }
    }

    private void CollectUnitEffectVisuals()
    {
        for (int index = 0; index < _allUnits.Count; index++)
        {
            Unit unit = _allUnits[index];
            if (unit == null || !unit.Alive || unit.View?.Root == null) continue;
            if (!unit.View.Root.Visible) continue;
            CollectBuffVisuals(unit);
            CollectPersistentArts(unit);
        }
    }

    private readonly List<(string Model, string Attachment)> _unitBuffVisuals = new();

    private void CollectBuffVisuals(Unit unit)
    {
        _unitBuffVisuals.Clear();
        List<Buff> buffs = unit.Buffs;
        for (int index = 0; index < buffs.Count; index++) DrawBuffVisual(unit, buffs[index]);
        IReadOnlyList<Ability> abilities = unit.Abilities.Items;
        for (int index = 0; index < abilities.Count; index++)
        {
            if (abilities[index] is Buff buff) DrawBuffVisual(unit, buff);
        }
        _unitBuffVisuals.Clear();
    }

    private void DrawBuffVisual(Unit unit, Buff buff)
    {
        if (buff == null) return;
        string modelPath = buff.VisualModelPath;
        if (string.IsNullOrWhiteSpace(modelPath)) return;
        string attachment = buff.VisualAttachmentPoint;
        if (AlreadyDrawnForUnit(modelPath, attachment)) return;
        _unitBuffVisuals.Add((modelPath, attachment));
        DrawVisualEffect(new VisualEffectRequest(
            new VisualEffectKey(1, buff, 0, 0), modelPath, unit, attachment,
            Vector3.Zero, false, 1f, buff.VisualAgeSeconds, false, NVector4.One,
            unit.TeamColorIndex, false, null, null, null));
    }

    private bool AlreadyDrawnForUnit(string modelPath, string attachment)
    {
        foreach ((string model, string point) in _unitBuffVisuals)
        {
            if (!string.Equals(model, modelPath, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(point, attachment, StringComparison.OrdinalIgnoreCase)) continue;
            return true;
        }
        return false;
    }

    private void CollectPersistentArts(Unit unit)
    {
        IReadOnlyList<Ability> abilities = unit.Abilities.Items;
        float time = Time.GetTicksMsec() / 1000f;
        for (int index = 0; index < abilities.Count; index++)
        {
            Ability ability = abilities[index];
            if (ability is Buff || ability.Level <= 0 || ability.IsDisabled) continue;
            for (int kind = 0; kind < 3; kind++)
            {
                bool persistent = kind switch
                {
                    0 => ability.HasPersistentTargetArts,
                    1 => ability.HasPersistentCasterArts,
                    _ => ability.HasPersistentSpecialArts,
                };
                if (!persistent) continue;
                IReadOnlyList<string> arts = kind switch
                {
                    0 => ability.TargetArts,
                    1 => ability.CasterArts,
                    _ => ability.SpecialArts,
                };
                IReadOnlyList<string> attachments = kind switch
                {
                    0 => ability.TargetAttachments,
                    1 => ability.CasterAttachments,
                    _ => ability.SpecialAttachments,
                };
                for (int artIndex = 0; artIndex < arts.Count; artIndex++)
                {
                    for (int attachmentIndex = 0; attachmentIndex < attachments.Count; attachmentIndex++)
                    {
                        if (kind == 0
                            && !ability.IsPersistentTargetArtVisible(artIndex, attachmentIndex)) continue;
                        string art = arts[artIndex];
                        if (string.IsNullOrWhiteSpace(art)) continue;
                        DrawVisualEffect(new VisualEffectRequest(
                            new VisualEffectKey(2 + kind, ability, artIndex, attachmentIndex),
                            art, unit, attachments[attachmentIndex], Vector3.Zero, false, 1f,
                            time, false, NVector4.One, unit.TeamColorIndex, false, null, null, null));
                    }
                }
            }
        }
    }

    private void DrawVisualEffect(in VisualEffectRequest request)
    {
        if (!_visualEffects.TryGetValue(request.Key, out VisualEffectNode node)
            || !IsReusable(node, request))
        {
            FreeVisualEffectNode(node);
            node = CreateVisualEffectNode(request);
            _visualEffects[request.Key] = node;
        }
        node.LastSeenFrame = _visualEffectFrame;
        if (node.Player != null) ApplyVisualEffectPose(node, request);
        TickVisualEffectEvents(node);
    }

    private void TickVisualEffectEvents(VisualEffectNode node)
    {
        if (node?.Root == null || !IsInstanceValid(node.Root)) return;
        if (!node.EventsResolved)
        {
            node.EventsResolved = true;
            node.Events = ModelEventEmitter.Create(node.Root);
        }
        node.Events?.Tick(_groundSplats.Add, node.Root.IsVisibleInTree());
    }

    private static bool IsReusable(VisualEffectNode node, in VisualEffectRequest request)
        => node != null && (node.Failed
            || (IsInstanceValid(node.Root)
                && ReferenceEquals(node.Target, request.Target)
                && string.Equals(node.Attachment, request.Attachment, StringComparison.OrdinalIgnoreCase)));

    private void FreeVisualEffectNode(VisualEffectNode node)
    {
        if (node?.Root == null || !IsInstanceValid(node.Root)) return;
        node.Root.QueueFree();
    }

    private VisualEffectNode CreateVisualEffectNode(in VisualEffectRequest request)
    {
        string path = PathResolve.ModelScene(request.ModelPath);
        PackedScene scene = Assets.LoadScene(path);
        if (scene == null)
        {
            if (_missingEffectModels.Add(request.ModelPath))
                GD.Print($"[FX] 缺模型 {request.ModelPath}");
            return new VisualEffectNode { Failed = true };
        }

        Node3D root = scene.Instantiate<Node3D>();
        if (root == null) return new VisualEffectNode { Failed = true };
        ModelLibrary.ApplyMissingMaterials(root, path);
        UnitSceneComponent.EnsureFallbackMaterials(root);
        MapWorldBuilder.ApplyUnitTextureFilter(root);
        var tree = root.FindChild("AnimationTree", true, false) as AnimationTree;
        if (tree != null) tree.Active = false;

        var node = new VisualEffectNode
        {
            Root = root,
            Player = root.FindChild("AnimationPlayer", true, false) as AnimationPlayer,
            Target = request.Target,
            Attachment = request.Attachment,
        };
        root.Name = "fx_" + System.IO.Path.GetFileNameWithoutExtension(request.ModelPath);
        if (node.Player != null) node.Player.Autoplay = "";

        if (request.Target != null)
        {
            Node3D host = AttachmentPoints.Find(request.Target.View.Root, request.Attachment)
                ?? request.Target.View.Root;
            host.AddChild(root);
        }
        else
        {
            root.Position = request.Position;
            _worldRoot.AddChild(root);
        }

        ApplyVisualEffectTeamColor(node, request.TeamColor);
        ApplyVisualEffectColor(root, request.Color);
        if (_visualEffectLoadLogBudget > 0)
        {
            _visualEffectLoadLogBudget--;
            GD.Print($"[FX] 载入 {path.Replace("res://extract/", "")}"
                + $"{(request.Target == null ? "" : $" → {request.Target.Type?.Name} 的 {request.Attachment}")}");
        }
        return node;
    }

    private static void ApplyVisualEffectTeamColor(VisualEffectNode node, int teamColor)
    {
        if (node.TeamColor == teamColor) return;
        node.TeamColor = teamColor;
        UnitSceneComponent.ApplyTeamColorTo(node.Root, UnitSceneComponent.ColorOf(teamColor));
    }

    private static void ApplyVisualEffectColor(Node3D root, NVector4 color)
    {
        if (color == NVector4.One) return;
        foreach (Node child in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (child is not MeshInstance3D mesh || mesh.Mesh == null) continue;
            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                Material material = mesh.MaterialOverride
                    ?? mesh.GetSurfaceOverrideMaterial(surface)
                    ?? mesh.Mesh.SurfaceGetMaterial(surface);
                if (material is not ShaderMaterial shader) continue;
                var copy = (ShaderMaterial)shader.Duplicate();
                copy.SetShaderParameter("geoset_tint", new Color(color.X, color.Y, color.Z, 1f));
                copy.SetShaderParameter("layer_alpha", color.W);
                if (ReferenceEquals(mesh.MaterialOverride, material)) mesh.MaterialOverride = copy;
                else mesh.SetSurfaceOverrideMaterial(surface, copy);
            }
        }
    }

    private static void ApplyVisualEffectPose(VisualEffectNode node, in VisualEffectRequest request)
    {
        node.Root.Scale = Vector3.One * Mathf.Max(0.001f, request.Scale);
        if (request.WorldPosition) node.Root.Position = request.Position;

        string sequence = SelectEffectSequence(node, request);
        if (sequence == null) return;
        Animation animation = node.Player.GetAnimation(sequence);
        float age = Mathf.Max(0f, request.Age);
        float length = animation == null ? 0f : (float)animation.Length;

        if (request.StandAfterBirth && !request.Death)
        {
            string birth = FindEffectSequence(node.Player, "Birth", request.AnimationProperties);
            if (birth != null && string.Equals(sequence, birth, StringComparison.Ordinal)
                && length > 0f && age >= length)
            {
                string stand = FindEffectSequence(node.Player, "Stand", request.AnimationProperties);
                if (stand != null)
                {
                    sequence = stand;
                    animation = node.Player.GetAnimation(sequence);
                    length = animation == null ? 0f : (float)animation.Length;
                    age -= Mathf.Max(1f, length);
                }
            }
        }

        if (node.Sequence != sequence || node.Player.CurrentAnimation != sequence)
        {
            node.Sequence = sequence;
            node.Player.Play(sequence);
            node.Player.Pause();
        }

        bool loop = animation != null && animation.LoopMode != Animation.LoopModeEnum.None;
        float time = length <= 0f ? 0f : loop ? age % length : Mathf.Min(length, age);
        node.Player.Seek(time, true);

        if (request.Special == null || request.Special.Destroyed) return;
        if (request.Special.LifeSpan > 0f || loop || length <= 0f) return;
        if (age >= length) request.Special.Expire();
    }

    private static string SelectEffectSequence(VisualEffectNode node, in VisualEffectRequest request)
    {
        string key = (request.Death ? "death|" : "live|")
            + (request.PrimaryAnimation ?? "") + "|" + (request.AnimationProperties ?? "");
        if (node.SequenceKey == key && node.Sequence != null
            && node.Player.HasAnimation(node.Sequence)) return node.Sequence;
        node.SequenceKey = key;

        string found = request.Death
            ? FindEffectSequence(node.Player, "Death", request.AnimationProperties) : null;
        if (found == null && !string.IsNullOrEmpty(request.PrimaryAnimation))
            found = FindEffectSequence(node.Player, request.PrimaryAnimation, request.AnimationProperties);
        found ??= FindEffectSequence(node.Player, "Birth", request.AnimationProperties);
        found ??= FindEffectSequence(node.Player, "Stand", request.AnimationProperties);
        if (found == null)
        {
            string[] names = node.Player.GetAnimationList();
            found = names.Length > 0 ? names[0] : null;
        }
        node.Sequence = found;
        return found;
    }

    private static string FindEffectSequence(AnimationPlayer player, string primary, string properties)
    {
        if (player == null || string.IsNullOrEmpty(primary)) return null;
        string[] requested = SplitSequenceTokens(primary + " " + properties);
        if (requested.Length == 0) return null;

        string best = null;
        int bestMatches = -1;
        int bestExtra = int.MaxValue;
        foreach (string name in player.GetAnimationList())
        {
            string[] tokens = SplitSequenceTokens(name);
            if (tokens.Length == 0) continue;
            if (!string.Equals(tokens[0], requested[0], StringComparison.OrdinalIgnoreCase)) continue;
            int matches = 0;
            for (int index = 1; index < requested.Length; index++)
            {
                for (int candidate = 1; candidate < tokens.Length; candidate++)
                {
                    if (!string.Equals(tokens[candidate], requested[index],
                        StringComparison.OrdinalIgnoreCase)) continue;
                    matches++;
                    break;
                }
            }
            int extra = tokens.Length - matches;
            if (matches < bestMatches || (matches == bestMatches && extra >= bestExtra)) continue;
            best = name;
            bestMatches = matches;
            bestExtra = extra;
        }
        return best;
    }

    private static string[] SplitSequenceTokens(string value)
        => string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(new[] { ' ', '\t', ',', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private readonly HashSet<string> _preloadedScenes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _preloadedUnitTypes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _preloadedAbilities = new(StringComparer.Ordinal);

    private void PreloadPresentationResources()
    {
        int unitsBefore = _preloadedUnitTypes.Count;
        int abilitiesBefore = _preloadedAbilities.Count;
        int scenesBefore = _preloadedScenes.Count;

        foreach (Unit unit in _allUnits) PreloadUnitType(unit.Type);
        foreach (Unit unit in _allUnits) PreloadAbilityList(unit.Type);

        var pending = new Queue<UnitType>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (Unit unit in _allUnits)
        {
            if (unit?.Type == null || unit.Player < 0 || unit.Player >= 12) continue;
            pending.Enqueue(unit.Type);
        }
        while (pending.Count > 0)
        {
            UnitType type = pending.Dequeue();
            if (type?.Id == null || !visited.Add(type.Id)) continue;
            foreach (string child in ProductionChildren(type))
            {
                UnitType next = UnitFactory.Get(child);
                if (next == null) continue;
                PreloadUnitType(next);
                PreloadAbilityList(next);
                pending.Enqueue(next);
            }
        }

        GD.Print($"[PRELOAD] 单位类型 {_preloadedUnitTypes.Count - unitsBefore}，"
            + $"技能 {_preloadedAbilities.Count - abilitiesBefore}，"
            + $"模型 {_preloadedScenes.Count - scenesBefore}");
    }

    private static IEnumerable<string> ProductionChildren(UnitType type)
    {
        foreach (string child in type.Builds) yield return child;
        foreach (string child in type.Trains) yield return child;
        foreach (string child in type.Researches) yield return child;
        foreach (string child in type.UpgradeTo) yield return child;
    }

    private void PreloadUnitType(UnitType type)
    {
        if (type == null || string.IsNullOrWhiteSpace(type.Id)) return;
        if (!_preloadedUnitTypes.Add(type.Id)) return;
        PreloadScene(type.ModelPath);
        PreloadScene(type.Weapon?.MissileScene);
        PreloadScene(type.Weapon2?.MissileScene);
    }

    private void PreloadAbilityList(UnitType type)
    {
        foreach (string id in type.Abilities) PreloadAbility(id);
        foreach (string id in type.HeroAbilList) PreloadAbility(id);
        foreach (string id in type.SellItems) PreloadAbility(id);
        foreach (string id in type.SellUnits) PreloadAbility(id);
    }

    private void PreloadAbility(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId) || !_preloadedAbilities.Add(abilityId)) return;
        AbilityType type = AbilityCatalog.Get(abilityId);
        if (type == null) return;
        Ability ability = RuntimeAbilityFactory.Create(type);
        if (ability == null) return;
        ability.BindObjectData(type.Id, new AbilityTypeObjectData(type), 1);

        PreloadScene(PathResolve.ModelScene(ability.MissileArt));
        PreloadArts(ability.CasterArts);
        PreloadArts(ability.TargetArts);
        PreloadArts(ability.SpecialArts);
        PreloadArts(ability.EffectArts);
        PreloadArts(ability.AreaEffectArts);
        PreloadArts(ability.RuntimePreloadArts);
    }

    private void PreloadArts(IReadOnlyList<string> arts)
    {
        if (arts == null) return;
        for (int i = 0; i < arts.Count; i++) PreloadScene(PathResolve.ModelScene(arts[i]));
    }

    private void PreloadScene(string scene)
    {
        if (string.IsNullOrWhiteSpace(scene)) return;
        if (!_preloadedScenes.Add(scene)) return;
        Assets.LoadScene(scene);
    }

    private void RemoveUnseenVisualEffects()
    {
        _staleVisualEffects.Clear();
        foreach (KeyValuePair<VisualEffectKey, VisualEffectNode> pair in _visualEffects)
        {
            if (pair.Value != null && pair.Value.LastSeenFrame == _visualEffectFrame) continue;
            _staleVisualEffects.Add(pair.Key);
        }
        foreach (VisualEffectKey key in _staleVisualEffects)
        {
            FreeVisualEffectNode(_visualEffects[key]);
            _visualEffects.Remove(key);
        }
        _staleVisualEffects.Clear();
    }
}
