using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed class UnitSceneComponent
{
    private static readonly Color[] PlayerColors =
    {
        Color.FromHtml("c8322b"), Color.FromHtml("0042ff"), Color.FromHtml("1ce6b9"),
        Color.FromHtml("540081"), Color.FromHtml("fffc01"), Color.FromHtml("fe8a0e"),
        Color.FromHtml("20c000"), Color.FromHtml("e55bb0"), Color.FromHtml("959697"),
        Color.FromHtml("7ebff1"), Color.FromHtml("106246"), Color.FromHtml("4e2a04"),
    };

    public Node3D Root { get; private set; }
    public Unit Owner;
    public bool FogSeen;
    public bool FogFrozen { get; private set; }

    public AnimationPlayer Player { get; private set; }
    public float Height { get; private set; } = 100f;
    public bool Selected { get; private set; }
    public string CurrentAnimation { get; private set; }

    public ModelEventEmitter ModelEvents { get; private set; }
    private bool _modelEventsResolved;

    public ModelEventEmitter ResolveModelEvents()
    {
        if (!_modelEventsResolved)
        {
            _modelEventsResolved = true;
            ModelEvents = ModelEventEmitter.Create(Root);
        }
        return ModelEvents;
    }

    public MeshInstance3D ProgressNode { get; private set; }
    private ImmediateMesh _progressMesh;
    private float _progressRadius;
    private float _progressDrawn = -1f;

    public static UnitSceneComponent Create(PackedScene scene, Color teamColor, float scale, bool isBuilding, float footprintRadius, string scenePath, SceneLight light)
    {
        var view = new UnitSceneComponent();
        var root = scene.Instantiate<Node3D>();
        root.Scale = Vector3.One * scale;
        view.Root = root;
        ModelLibrary.ApplyMissingMaterials(root, scenePath);
        EnsureFallbackMaterials(root);

        var tree = root.FindChild("AnimationTree", true, false) as AnimationTree;
        if (tree != null) tree.Active = false;
        view.Player = root.FindChild("AnimationPlayer", true, false) as AnimationPlayer;

        view.ApplyTeamColor(teamColor);
        MapWorldBuilder.ApplyUnitTextureFilter(root);
        ModelLightRig.Apply(root, light);

        view.Height = MeasureHeight(root, isBuilding);
        view._progressRadius = Mathf.Max(40f, footprintRadius);
        if (isBuilding) view.BuildProgressRing();
        return view;
    }

    private static void EnsureFallbackMaterials(Node3D root)
    {
        foreach (Node node in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh == null || mesh.MaterialOverride != null) continue;
            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.GetSurfaceOverrideMaterial(surface) != null) continue;
                if (mesh.Mesh.SurfaceGetMaterial(surface) != null) continue;
                mesh.SetSurfaceOverrideMaterial(surface, new StandardMaterial3D { AlbedoColor = Colors.White });
            }
        }
    }

    public static Color ColorOf(int player)
    {
        return PlayerColors[Mathf.Abs(player) % PlayerColors.Length];
    }

    public void ApplyShadow(string artName, float width, float height, float offsetX, float offsetY)
    {
        if (Root == null || string.IsNullOrWhiteSpace(artName)) return;
        string name = artName.Trim();
        if (name is "_" or "-" or "") return;
        if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) name = name[..^4];

        Texture2D texture = Assets.LoadTexture(Assets.Res("ReplaceableTextures/Shadows/" + name + ".png"));
        if (texture == null) return;

        Vector2 textureSize = texture.GetSize();
        float w = width > 0f ? width : Mathf.Max(64f, textureSize.X * 32f);
        float h = height > 0f ? height : Mathf.Max(64f, textureSize.Y * 32f);

        var material = new StandardMaterial3D
        {
            AlbedoTexture = texture,
            AlbedoColor = new Color(0f, 0f, 0f, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
        };

        var node = new MeshInstance3D
        {
            Name = "Shadow",
            Mesh = new QuadMesh { Size = new Vector2(w, h) },
            MaterialOverride = material,
            Position = new Vector3(offsetX, 1.5f, offsetY),
            Rotation = new Vector3(-Mathf.Pi / 2f, 0f, 0f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        Root.AddChild(node);
    }

    /// <summary>换阵营时重刷模型上的队伍色。</summary>
    public void SetTeamColor(Color color) => ApplyTeamColor(color);

    private bool _ghost;

    /// <summary>隐身时只留一个淡影（自己的单位能看见自己在哪里）。</summary>
    public void SetFogFrozen(bool frozen)
    {
        FogFrozen = frozen;
        if (Player != null)
        {
            if (frozen) Player.Pause();
            else if (!string.IsNullOrEmpty(CurrentAnimation))
                Player.Play(CurrentAnimation);
        }
        Root.Visible = true;
    }

    public void SetGhost(bool ghost)
    {
        if (_ghost == ghost) return;
        _ghost = ghost;
        float transparency = ghost ? 1f - 127f / 255f : 0f;

        foreach (Node node in Root.FindChildren("*", "GeometryInstance3D", true, false))
        {
            if (node is GeometryInstance3D geometry) geometry.Transparency = transparency;
        }
    }

    private void ApplyTeamColor(Color color) => ApplyTeamColorTo(Root, color);

    /// <summary>把模型里 use_team_color 的材质换成带队伍色的副本（集结点旗帜这类非单位模型也用它）。</summary>
    public static void ApplyTeamColorTo(Node3D root, Color color)
    {
        if (root == null) return;

        foreach (Node node in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh == null) continue;

            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                Material material = mesh.MaterialOverride
                    ?? mesh.GetSurfaceOverrideMaterial(surface)
                    ?? mesh.Mesh.SurfaceGetMaterial(surface);
                if (material is not ShaderMaterial shader) continue;
                if (!shader.GetShaderParameter("use_team_color").AsBool()) continue;

                ShaderMaterial copy = (ShaderMaterial)shader.Duplicate();
                copy.SetShaderParameter("team_color", color);

                if (mesh.MaterialOverride != null && mesh.MaterialOverride == material)
                {
                    mesh.MaterialOverride = copy;
                }
                else
                {
                    mesh.SetSurfaceOverrideMaterial(surface, copy);
                }
            }
        }
    }

    private static float MeasureHeight(Node3D root, bool isBuilding)
    {
        float top = 0f;
        foreach (Node node in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh == null) continue;
            if (!VisibleInTree(mesh, root)) continue;

            Aabb box = mesh.Mesh.GetAabb();
            Transform3D local = RelativeTransform(root, mesh);
            for (int corner = 0; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? box.Position.X : box.End.X,
                    (corner & 2) == 0 ? box.Position.Y : box.End.Y,
                    (corner & 4) == 0 ? box.Position.Z : box.End.Z);
                float candidate = (local * point).Y;
                if (candidate > top) top = candidate;
            }
        }

        if (top <= 1f) top = isBuilding ? 300f : 120f;
        return top * root.Scale.Y;
    }

    private static Transform3D RelativeTransform(Node3D root, Node3D child)
    {
        Transform3D transform = child.Transform;
        Node parent = child.GetParent();
        while (parent != null && !ReferenceEquals(parent, root))
        {
            if (parent is Node3D parent3D) transform = parent3D.Transform * transform;
            parent = parent.GetParent();
        }
        return transform;
    }

    private static bool VisibleInTree(Node3D node, Node3D root)
    {
        Node current = node;
        while (current != null)
        {
            if (current is Node3D node3D && !node3D.Visible) return false;
            if (ReferenceEquals(current, root)) return true;
            current = current.GetParent();
        }
        return true;
    }

    private readonly AnimationTagCache _animationTags = new();
    private readonly Dictionary<string, (HashSet<string> Primary, HashSet<string> Secondary,
        HashSet<string> Form)> _sequenceTags = new(StringComparer.Ordinal);

    public void Play(string kind, float rate = 1f)
    {
        if (Player == null) return;

        string primary = kind;
        string[] requested = Array.Empty<string>();
        int space = kind.IndexOf(' ');
        if (space > 0)
        {
            primary = kind[..space];
            requested = kind[(space + 1)..].Split(' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        string harvest = Owner == null ? null
            : Owner.Carry > 0 ? "Gold"
            : Owner.CarryLumber > 0 ? "Lumber" : null;
        bool cutting = Owner != null && (Owner.BuildingNow
            || (Owner.Tree != null && Owner.CarryLumber <= 0 && Owner.Carry <= 0
                && !Owner.MoveTarget.HasValue));
        (IReadOnlyCollection<string> tags, _) = _animationTags.Get(Owner, requested, harvest, cutting);
        string found = SelectSequence(primary, tags) ?? PickAnimation(kind);
        if (found == null) return;
        Player.SpeedScale = kind == "Walk" ? Mathf.Max(0.05f, rate) : 1f;
        if (found == CurrentAnimation) return;

        CurrentAnimation = found;
        Player.Play(found);
        if (primary is "Stand" or "Walk")
        {
            Player.GetAnimation(found).LoopMode = Animation.LoopModeEnum.Linear;
        }
    }

    private string SelectSequence(string primary, IReadOnlyCollection<string> requestedTags)
    {
        if (string.IsNullOrEmpty(primary)) return null;
        var requestedForm = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var requestedNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string tag in requestedTags ?? Array.Empty<string>())
        {
            bool numeric = tag.Length > 0 && tag.All(char.IsDigit);
            (numeric ? requestedNumbers : requestedForm).Add(tag);
        }

        string[] names = Player.GetAnimationList();
        var matches = new List<(string Name, HashSet<string> Secondary, HashSet<string> Form)>();
        foreach (string name in names)
        {
            (HashSet<string> primaryTags, HashSet<string> secondary, HashSet<string> form) = TagsOf(name);
            if (primaryTags.Contains(primary)) matches.Add((name, secondary, form));
        }
        if (matches.Count == 0) return null;

        string best = null;
        int bestMatches = -1;
        int smallestFormCount = int.MaxValue;
        foreach ((string name, HashSet<string> secondary, HashSet<string> form) in matches)
        {
            if (!form.SetEquals(requestedForm)) continue;
            bool numbersOk = true;
            foreach (string number in requestedNumbers)
                if (!secondary.Contains(number)) { numbersOk = false; break; }
            if (!numbersOk) continue;
            if (best == null) best = name;
        }
        if (best != null) return best;

        bool wantSwim = requestedForm.Contains("SWIM");
        foreach ((string name, HashSet<string> secondary, HashSet<string> form) in matches)
        {
            if (!wantSwim && form.Contains("SWIM")) continue;
            int matching = 0;
            foreach (string tag in form)
                if (requestedForm.Contains(tag)) matching++;
            if (matching > bestMatches
                || (matching == bestMatches && form.Count < smallestFormCount))
            {
                bestMatches = matching;
                smallestFormCount = form.Count;
                best = name;
            }
        }
        return best;
    }

    private (HashSet<string> Primary, HashSet<string> Secondary, HashSet<string> Form) TagsOf(string name)
    {
        if (_sequenceTags.TryGetValue(name, out var cached)) return cached;
        var primary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var secondary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string token in name.Split(new[] { ' ', '\t', '\r', '\n', ',' },
            StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.All(character => character == '-')) continue;
            if (PrimaryTags.Contains(token)) primary.Add(token);
            else if (SecondaryTags.Contains(token)) secondary.Add(token);
            else if (primary.Count > 0 && token.All(char.IsDigit)) secondary.Add(token);
            else break;
        }
        var form = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string tag in secondary)
            if (!tag.All(char.IsDigit)) form.Add(tag);
        var result = (primary, secondary, form);
        _sequenceTags[name] = result;
        return result;
    }

    private static readonly HashSet<string> PrimaryTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "ATTACK", "BIRTH", "DEATH", "DECAY", "DISSIPATE", "MORPH", "PORTRAIT",
        "SLEEP", "SPELL", "STAND", "WALK",
    };

    private static readonly HashSet<string> SecondaryTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALTERNATE", "ALTERNATEEX", "BONE", "CHAIN", "CHANNEL", "COMPLETE", "CRITICAL",
        "DEFEND", "DRAIN", "EATTREE", "FAST", "FILL", "FLAIL", "FLESH", "FIFTH", "FIRE",
        "FIRST", "FIVE", "FOUR", "FOURTH", "GOLD", "HIT", "LARGE", "LEFT", "LIGHT",
        "LOOPING", "LUMBER", "MEDIUM", "MODERATE", "OFF", "ONE", "PUKE", "READY",
        "RIGHT", "SECOND", "SEVERE", "SLAM", "SMALL", "SPIKED", "SPIN", "SPELL",
        "CINEMATIC", "SWIM", "TALK", "THIRD", "THREE", "THROW", "TWO", "TURN",
        "VICTORY", "WORK", "WOUNDED", "UPGRADE",
    };

    private string PickAnimation(string kind)
    {
        string[] names = Player.GetAnimationList();
        foreach (string name in names)
        {
            if (string.Equals(name, kind, StringComparison.OrdinalIgnoreCase)) return name;
        }
        foreach (string name in names)
        {
            if (name.StartsWith(kind, StringComparison.OrdinalIgnoreCase)) return name;
        }
        return null;
    }

    private void BuildProgressRing()
    {
        _progressMesh = new ImmediateMesh();
        ProgressNode = new MeshInstance3D
        {
            Mesh = _progressMesh,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                AlbedoColor = new Color(0.35f, 1f, 0.45f, 0.55f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = false,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            },
            Position = new Vector3(0f, 12f, 0f),
            Visible = false,
            Name = "BuildProgress",
        };
        Root.AddChild(ProgressNode);
    }

    private void UpdateProgressRing(float progress)
    {
        if (_progressMesh == null) return;

        if (progress >= 1f)
        {
            ProgressNode.Visible = false;
            _progressDrawn = -1f;
            return;
        }

        ProgressNode.Visible = true;
        if (Mathf.Abs(progress - _progressDrawn) < 0.02f) return;
        _progressDrawn = progress;

        // 客户端是贴着建筑底边的一圈细环，不是实心圆盘
        const int segments = 48;
        int filled = Mathf.Max(1, Mathf.CeilToInt(segments * Mathf.Clamp(progress, 0f, 1f)));
        float outer = _progressRadius * 1.02f;
        float inner = outer * 0.90f;

        _progressMesh.ClearSurfaces();
        _progressMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        _progressMesh.SurfaceSetColor(new Color(0.35f, 1f, 0.45f, 0.55f));
        for (int i = 0; i < filled; i++)
        {
            float a0 = Mathf.Tau * i / segments - Mathf.Pi * 0.5f;
            float a1 = Mathf.Tau * (i + 1) / segments - Mathf.Pi * 0.5f;

            var i0 = new Vector3(Mathf.Cos(a0) * inner, 0f, Mathf.Sin(a0) * inner);
            var o0 = new Vector3(Mathf.Cos(a0) * outer, 0f, Mathf.Sin(a0) * outer);
            var i1 = new Vector3(Mathf.Cos(a1) * inner, 0f, Mathf.Sin(a1) * inner);
            var o1 = new Vector3(Mathf.Cos(a1) * outer, 0f, Mathf.Sin(a1) * outer);

            _progressMesh.SurfaceAddVertex(i0);
            _progressMesh.SurfaceAddVertex(o0);
            _progressMesh.SurfaceAddVertex(o1);

            _progressMesh.SurfaceAddVertex(i0);
            _progressMesh.SurfaceAddVertex(o1);
            _progressMesh.SurfaceAddVertex(i1);
        }
        _progressMesh.SurfaceEnd();
    }

    public void SetBuildProgress(float progress)
    {
        UpdateProgressRing(progress);
        if (Player == null) return;

        string found = PickAnimation("Birth");
        if (found == null) return;

        if (CurrentAnimation != found)
        {
            CurrentAnimation = found;
            Player.Play(found);
        }

        Animation animation = Player.GetAnimation(found);
        if (animation == null) return;

        Player.Seek((double)(Mathf.Clamp(progress, 0f, 1f) * (float)animation.Length), true);
        Player.Pause();
    }

    public void SetSelected(bool selected) => Selected = selected;
}