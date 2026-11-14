using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class War3GameScene
{
    /// <summary>
    /// 引擎闪电束：两点之间一条带噪点的带状网格。抖动与 UV 滚动都是
    /// `id/index/time` 的确定性函数（客户端 BattleOverlayRenderer.BuildLightningVertices），
    /// 所以每帧按同一公式重算即可，不需要随机数也不会有帧间跳变。
    /// 宽度/分段/颜色/贴图/噪点全部来自 `Units\LightningData.slk`。
    /// </summary>
    private sealed class LightningBeam
    {
        public MeshInstance3D Node;
        public ImmediateMesh Mesh;
        public StandardMaterial3D Material;
        public Unit From;
        public Unit To;
        public Vector3 FromGround;
        public Vector3 ToGround;
        public float StartHeight;
        public float EndHeight;
        public LightningInfo Info;
        public float Remaining;
        public float Duration = 0.3f;
        public float Age;
        public long Seed;
        public Color InstanceTint = Colors.White;
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

        // 一端看得见就画（客户端 DrawJassLightnings 对 ChainLightningBeamEffect 的迷雾判据）。
        if (!FogVisible(from.Position) && !FogVisible(to.Position)) return null;

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
        var mesh = new ImmediateMesh();
        var node = new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            TopLevel = true,
        };
        (_worldRoot ?? this).AddChild(node);

        var beam = new LightningBeam
        {
            Node = node,
            Mesh = mesh,
            Material = material,
            From = from,
            To = to,
            FromGround = fromPoint,
            ToGround = toPoint,
            StartHeight = (from?.FlyHeight ?? 0f) + 80f,
            EndHeight = (to?.FlyHeight ?? 0f) + 60f,
            Info = info,
            Remaining = Mathf.Max(0.08f, seconds),
            Duration = Mathf.Max(0.08f, seconds),
            // 客户端用 JASS 闪电句柄当抖动种子；这里的句柄就是列表槽位，用自增序号即可。
            Seed = ++_lightningSeed,
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

    private static long _lightningSeed;
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

            beam.Age += delta;
            beam.Remaining -= delta;
            if (beam.Remaining <= 0f)
            {
                beam.Node.QueueFree();
                _lightningBeams.RemoveAt(index);
                continue;
            }

            // 客户端每帧按同一个时间戳重算顶点：抖动量化到 24/Duration Hz，UV 连续滚动。
            BuildLightningMesh(beam);

            float fade = beam.Remaining < 0.12f ? beam.Remaining / 0.12f : 1f;
            Color tint = beam.Info.Tint * beam.InstanceTint;
            beam.Material.AlbedoColor = new Color(tint.R, tint.G, tint.B, tint.A * fade);
        }
    }

    private void BuildLightningMesh(LightningBeam beam)
    {
        ImmediateMesh mesh = beam.Mesh;
        if (mesh == null || !GodotObject.IsInstanceValid(mesh)) return;
        mesh.ClearSurfaces();

        Vector3 start = AnchorPoint(beam.From, beam.FromGround, beam.StartHeight);
        Vector3 end = AnchorPoint(beam.To, beam.ToGround, beam.EndHeight);
        Vector3 axis = end - start;
        float length = axis.Length();
        if (length < 1f) return;

        LightningInfo info = beam.Info;
        int segments = Mathf.Clamp(Mathf.CeilToInt(length / Mathf.Max(1f, info.SegmentLength)), 1, 64);
        Vector3 forward = axis / length;

        Vector3 viewDirection = _camera?.Camera != null
            ? -_camera.Camera.GlobalTransform.Basis.Z
            : Vector3.Back;
        Vector3 side = forward.Cross(viewDirection);
        if (side.LengthSquared() < 0.0001f) side = forward.Cross(Vector3.Up);
        if (side.LengthSquared() < 0.0001f) side = Vector3.Right;
        side = side.Normalized();
        Vector3 vertical = side.Cross(forward).Normalized();

        float halfWidth = Mathf.Max(1f, info.Width * 0.5f);
        float phase = beam.Age / Mathf.Max(0.01f, info.Linger);
        float amplitude = Mathf.Min(96f, length * Mathf.Max(0f, info.NoiseScale));
        float stride = length / Mathf.Max(1f, info.SegmentLength) * info.TextureScale;

        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        Vector3 previousCenter = start;
        for (int step = 1; step <= segments; step++)
        {
            float t = (float)step / segments;
            Vector3 center = start.Lerp(end, t);
            if (step < segments)
            {
                // 三处输入都参与哈希：闪电代号、段号、量化时间 → 同一时刻必得同一形状。
                double seed = beam.Seed * 12.9898
                    + step * 78.233
                    + Math.Floor(phase * 24.0) * 37.719;
                float raw = Mathf.Sin((float)seed) * 43758.5453f;
                float raw2 = Mathf.Sin((float)seed + 19.19f) * 24634.6345f;
                float noise = (raw - Mathf.Floor(raw)) * 2f - 1f;
                float noise2 = (raw2 - Mathf.Floor(raw2)) * 2f - 1f;
                center += side * noise * amplitude + vertical * noise2 * amplitude * 0.55f;
            }

            float u0 = ((step - 1) / (float)segments) * stride + phase;
            float u1 = (t * stride) + phase;
            AddLightningQuad(mesh, previousCenter - side * halfWidth, previousCenter + side * halfWidth,
                center - side * halfWidth, center + side * halfWidth, u0, u1);
            previousCenter = center;
        }
        mesh.SurfaceEnd();
    }

    /// <summary>客户端 AddLightningQuad 的同款绕序，v 沿带宽方向 0→1。</summary>
    private static void AddLightningQuad(ImmediateMesh mesh,
        Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, float u0, float u1)
    {
        AddLightningVertex(mesh, a0, u0, 0f);
        AddLightningVertex(mesh, a1, u0, 1f);
        AddLightningVertex(mesh, b1, u1, 1f);
        AddLightningVertex(mesh, a0, u0, 0f);
        AddLightningVertex(mesh, b1, u1, 1f);
        AddLightningVertex(mesh, b0, u1, 0f);
    }

    private static void AddLightningVertex(ImmediateMesh mesh, Vector3 point, float u, float v)
    {
        mesh.SurfaceSetUV(new Vector2(u, v));
        mesh.SurfaceAddVertex(point);
    }

    /// <summary>端点：地面坐标 + 起始/命中高度（客户端 ChainLightningBeamEffect 的 StartHeight/EndHeight）。</summary>
    private static Vector3 AnchorPoint(Unit unit, Vector3 ground, float height)
    {
        if (unit == null || !unit.Alive) return ground + Vector3.Up * height;
        return unit.Position + Vector3.Up * height;
    }

    private bool FogVisible(Vector3 point)
        => _fog == null || !_fog.Enabled || _fog.IsVisible(point);
}
