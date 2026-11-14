using Godot;
using System;
using OpenWC3.UI;

namespace OpenWC3.Game;

internal sealed class TimeOfDayViewport
{
    const float ModelToDesign = 1280f;
    const float DesignHeight = 768f;
    const float ResolutionScale = 4f;

    const string AdditiveShaderCode = """
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled, blend_add;

uniform sampler2D base_texture : source_color, filter_nearest, repeat_disable;
uniform vec4 tint : source_color = vec4(1.0);
uniform float glow_gain = 0.7;

void fragment() {
	vec4 sampled = texture(base_texture, UV) * tint;
	if (sampled.a < 0.004) {
		discard;
	}
	ALBEDO = sampled.rgb * sampled.a * glow_gain;
	ALPHA = clamp(sampled.a * max(max(sampled.r, sampled.g), sampled.b) * glow_gain, 0.0, 1.0);
}
""";

    static Shader _additiveShader;

    readonly SubViewport _viewport;
    readonly Node3D _stage;
    readonly Camera3D _camera;
    AnimationPlayer _player;
    Animation _sequence;
    double _hours = double.NaN;

    public ViewportTexture Texture { get; }
    public UiRect ModelArea { get; private set; }
    public bool Loaded => _sequence != null;

    public TimeOfDayViewport(Node parent, string modelPath)
    {
        _viewport = new SubViewport
        {
            Name = "TimeOfDayViewport",
            TransparentBg = true,
            OwnWorld3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Msaa3D = Viewport.Msaa.Msaa4X,
        };
        parent.AddChild(_viewport);
        _stage = new Node3D { Name = "TimeOfDayStage" };
        _viewport.AddChild(_stage);
        _camera = new Camera3D
        {
            Name = "TimeOfDayCamera",
            Projection = Camera3D.ProjectionType.Orthogonal,
            Near = 0.001f,
            Far = 10f,
        };
        _viewport.AddChild(_camera);
        _camera.Current = true;
        Texture = _viewport.GetTexture();
        Load(modelPath);
    }

    void Load(string modelPath)
    {
        string path = PathResolve.ModelScene(modelPath);
        PackedScene scene = path == null ? null : Assets.LoadScene(path);
        if (scene == null)
        {
            GD.Print($"[TOD] 缺时间指示器模型 {modelPath}");
            return;
        }

        Node3D root = scene.Instantiate<Node3D>();
        if (root == null) return;
        BuildUiMaterials(root);
        if (root.FindChild("AnimationTree", true, false) is AnimationTree tree) tree.Active = false;
        root.Rotation = new Vector3(Mathf.Pi * .5f, 0f, 0f);
        _stage.AddChild(root);

        Aabb bounds = Bounds(root);
        if (bounds.Size == Vector3.Zero)
        {
            GD.Print($"[TOD] 时间指示器没有几何体 {modelPath}");
            return;
        }

        Vector3 center = bounds.Position + bounds.Size * .5f;
        _camera.Size = Mathf.Max(0.0001f, bounds.Size.Y);
        _camera.Position = new Vector3(center.X, center.Y, bounds.End.Z + 0.5f);
        _camera.LookAt(new Vector3(center.X, center.Y, center.Z), Vector3.Up);

        float width = Mathf.Max(1f, bounds.Size.X * ModelToDesign);
        float height = Mathf.Max(1f, bounds.Size.Y * ModelToDesign);
        ModelArea = new UiRect(bounds.Position.X * ModelToDesign,
            DesignHeight - bounds.End.Y * ModelToDesign, width, height);
        _viewport.Size = new Vector2I(Mathf.RoundToInt(width * ResolutionScale),
            Mathf.RoundToInt(height * ResolutionScale));

        _player = root.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
        if (_player == null) return;
        _player.Autoplay = "";
        string sequence = _player.HasAnimation("Stand") ? "Stand" : null;
        if (sequence == null)
        {
            string[] names = _player.GetAnimationList();
            sequence = names.Length > 0 ? names[0] : null;
        }
        if (sequence == null) return;
        _sequence = _player.GetAnimation(sequence);
        _player.Play(sequence);
        _player.Pause();
        GD.Print($"[TOD] 时间指示器 {modelPath} 序列 {sequence} 长 {_sequence.Length:0.##} 秒 区域 {ModelArea}");
    }

    public void SetClock(double timeOfDay)
    {
        if (_player == null || _sequence == null) return;
        double hours = ((timeOfDay % 24d) + 24d) % 24d;
        if (Math.Abs(hours - _hours) < 0.0005d) return;
        _hours = hours;
        _player.Seek(hours / 24d * _sequence.Length, true);
    }

    static void BuildUiMaterials(Node3D root)
    {
        _additiveShader ??= new Shader { Code = AdditiveShaderCode };
        foreach (Node node in root.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (node is not MeshInstance3D mesh || mesh.Mesh == null) continue;
            for (int surface = 0; surface < mesh.Mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.GetSurfaceOverrideMaterial(surface) is not ShaderMaterial source) continue;
                Texture2D texture = source.GetShaderParameter("base_texture").As<Texture2D>();
                Color tint = new(1f, 1f, 1f, 1f);
                Variant tintValue = source.GetShaderParameter("geoset_tint");
                if (tintValue.VariantType == Variant.Type.Color) tint = tintValue.AsColor();
                bool additive = source.Shader != null
                    && source.Shader.Code.Contains("blend_add", StringComparison.Ordinal);
                mesh.SetSurfaceOverrideMaterial(surface,
                    additive ? AdditiveMaterial(texture, tint) : OpaqueMaterial(texture, tint));
            }
        }
    }

    static ShaderMaterial AdditiveMaterial(Texture2D texture, Color tint)
    {
        var material = new ShaderMaterial { Shader = _additiveShader };
        material.SetShaderParameter("base_texture", texture);
        material.SetShaderParameter("tint", new Color(tint.R, tint.G, tint.B, 1f));
        return material;
    }

    static StandardMaterial3D OpaqueMaterial(Texture2D texture, Color tint)
    {
        return new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            AlbedoTexture = texture,
            AlbedoColor = new Color(tint.R, tint.G, tint.B, 1f),
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold = 0.5f,
        };
    }

    static Aabb Bounds(Node root)
    {
        bool has = false;
        Aabb result = default;
        void Walk(Node node)
        {
            if (node is MeshInstance3D mesh && mesh.Mesh != null)
            {
                Aabb world = mesh.GlobalTransform * mesh.Mesh.GetAabb();
                if (!has) { result = world; has = true; }
                else result = result.Merge(world);
            }
            foreach (Node child in node.GetChildren()) Walk(child);
        }
        Walk(root);
        return has ? result : default;
    }
}
