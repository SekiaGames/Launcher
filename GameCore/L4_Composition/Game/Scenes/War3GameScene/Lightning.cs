using Godot;
using System.Collections.Generic;

namespace Launcher;

public sealed partial class War3GameScene
{
    /// <summary>
    /// 引擎闪电束：两点之间一条带噪点的带状网格，每 50ms 重抖一次，到期淡出。
    /// 宽度/分段/颜色/贴图/噪点全部来自 `Units\LightningData.slk`。
    /// </summary>
    private sealed class LightningBeam
    {
        public MeshInstance3D Node;
        public StandardMaterial3D Material;
        public Unit From;
        public Unit To;
        public Vector3 FromPoint;
        public Vector3 ToPoint;
        public LightningInfo Info;
        public float Remaining;
        public float Rebuild;
    }

    private readonly List<LightningBeam> _lightningBeams = new();
    private const int MaxLightningBeams = 24;

    /// <summary>技能放完后的闪电表现：闪电链逐跳连，法力燃烧/医疗波这类单体一道。</summary>
    private void PlayLightningEffects(Unit caster, Ability ability, Unit target)
    {
        string bolt = LightningTable.BoltFor(ability);
        if (bolt == null || caster == null) return;

        List<Unit> chain = ability switch
        {
            ChainLightning chainLightning => chainLightning.LastChain,
            RuntimeHealingWaveAbility healingWave => healingWave.LastChain,
            _ => null,
        };
        if (chain != null && chain.Count > 0)
        {
            Unit previous = caster;
            foreach (Unit hit in chain)
            {
                if (hit == null) continue;
                SpawnLightningBeam(bolt, previous, hit, 0.3f);
                previous = hit;
            }
            return;
        }

        if (target == null || !target.Alive) return;
        SpawnLightningBeam(bolt, caster, target, 0.3f);
    }

    private LightningBeam SpawnLightningBeam(string boltId, Unit from, Unit to, float seconds)
    {
        LightningInfo info = LightningTable.Get(boltId);
        if (info == null || from == null || to == null) return null;
        if ((from.Position - to.Position).LengthSquared() < 1f) return null;
        return CreateLightningBeam(info, from, to, from.Position, to.Position, seconds);
    }

    private LightningBeam CreateLightningBeam(LightningInfo info, Unit from, Unit to,
        Vector3 fromPoint, Vector3 toPoint, float seconds)
    {
        if (_lightningBeams.Count >= MaxLightningBeams) return null;

        var material = new StandardMaterial3D
        {
            AlbedoTexture = Assets.LoadTexture(info.TexturePath),
            AlbedoColor = info.Tint,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
            DisableReceiveShadows = true,
        };
        var node = new MeshInstance3D
        {
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            TopLevel = true,
        };
        (_worldRoot ?? this).AddChild(node);

        var beam = new LightningBeam
        {
            Node = node,
            Material = material,
            From = from,
            To = to,
            FromPoint = fromPoint,
            ToPoint = toPoint,
            Info = info,
            Remaining = Mathf.Max(0.08f, seconds),
        };
        _lightningBeams.Add(beam);
        BuildLightningMesh(beam);
        if (_lightningLogBudget > 0)
        {
            _lightningLogBudget--;
            GD.Print($"[BOLT] {info.Id} {from?.Type?.Name ?? "地面"}→{to?.Type?.Name ?? "地面"}"
                + $" 宽 {info.Width:0} 长 {(toPoint - fromPoint).Length():0} 贴图 {info.TexturePath}");
        }
        return beam;
    }

    private int _lightningLogBudget = 6;

    private void TickLightningBeams(float delta)
    {
        if (_lightningBeams.Count == 0) return;

        for (int index = _lightningBeams.Count - 1; index >= 0; index--)
        {
            LightningBeam beam = _lightningBeams[index];
            if (beam.Node == null || !GodotObject.IsInstanceValid(beam.Node))
            {
                _lightningBeams.RemoveAt(index);
                continue;
            }

            beam.Remaining -= delta;
            if (beam.Remaining <= 0f)
            {
                beam.Node.QueueFree();
                _lightningBeams.RemoveAt(index);
                continue;
            }

            beam.Rebuild -= delta;
            if (beam.Rebuild <= 0f)
            {
                beam.Rebuild = 0.05f;
                BuildLightningMesh(beam);
            }

            float fade = beam.Remaining < 0.12f ? beam.Remaining / 0.12f : 1f;
            Color tint = beam.Info.Tint;
            beam.Material.AlbedoColor = new Color(tint.R, tint.G, tint.B, tint.A * fade);
        }
    }

    private void BuildLightningMesh(LightningBeam beam)
    {
        Vector3 start = AnchorPoint(beam.From, beam.FromPoint);
        Vector3 end = AnchorPoint(beam.To, beam.ToPoint);
        Vector3 axis = end - start;
        float length = axis.Length();
        if (length < 1f) return;

        int segments = Mathf.Clamp(Mathf.CeilToInt(length / Mathf.Max(16f, beam.Info.SegmentLength)), 2, 48);
        Vector3 forward = axis / length;
        Vector3 camera = _camera != null ? _camera.Camera.GlobalPosition : start + Vector3.Up * 400f;
        Vector3 side = forward.Cross((camera - (start + end) * 0.5f).Normalized());
        if (side.LengthSquared() < 0.0001f) side = forward.Cross(Vector3.Up);
        side = side.Normalized();

        float halfWidth = Mathf.Max(2f, beam.Info.Width * 0.5f);
        float jitter = beam.Info.NoiseScale * length;
        float uTotal = Mathf.Max(0.5f, length / 200f * Mathf.Max(0.1f, beam.Info.TextureScale) * 2f);

        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        Vector3 previousCenter = start;
        for (int step = 1; step <= segments; step++)
        {
            float t = (float)step / segments;
            Vector3 center = start.Lerp(end, t);
            if (step < segments)
            {
                center += side * ((float)GD.Randf() * 2f - 1f) * jitter;
                center += Vector3.Up * ((float)GD.Randf() * 2f - 1f) * jitter * 0.5f;
            }

            float u0 = uTotal * (step - 1) / segments;
            float u1 = uTotal * step / segments;

            Vector3 a = previousCenter - side * halfWidth;
            Vector3 b = previousCenter + side * halfWidth;
            Vector3 c = center - side * halfWidth;
            Vector3 d = center + side * halfWidth;

            AddLightningVertex(surface, a, new Vector2(u0, 0f));
            AddLightningVertex(surface, c, new Vector2(u1, 0f));
            AddLightningVertex(surface, d, new Vector2(u1, 1f));
            AddLightningVertex(surface, a, new Vector2(u0, 0f));
            AddLightningVertex(surface, d, new Vector2(u1, 1f));
            AddLightningVertex(surface, b, new Vector2(u0, 1f));

            previousCenter = center;
        }

        ArrayMesh mesh = surface.Commit();
        if (mesh == null) return;
        beam.Node.Mesh = mesh;
    }

    private static void AddLightningVertex(SurfaceTool surface, Vector3 point, Vector2 uv)
    {
        surface.SetUV(uv);
        surface.AddVertex(point);
    }

    private static Vector3 AnchorPoint(Unit unit, Vector3 fallback)
    {
        if (unit == null || !unit.Alive) return fallback;
        float height = unit.View != null ? unit.View.Height : 80f;
        return unit.Position + Vector3.Up * Mathf.Max(24f, height * 0.55f);
    }
}
