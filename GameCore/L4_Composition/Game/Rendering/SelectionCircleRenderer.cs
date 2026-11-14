using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class SelectionCircleRenderer : MeshInstance3D
{
    private const string CirclePath = @"ReplaceableTextures\Selection\SelectionCircleMed.blp";
    private const int Steps = 8;
    private static readonly float GroundBias = ShadowRenderer.GroundSplatOffset + SelectionCircleBaseZ;

    private static float SelectionCircleBaseZ => GeneratedTexts.TryGet(
        "UnitsMiscData", "Misc.SelectionCircleBaseZ", out string value)
        ? float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float parsed) ? parsed : 16f : 16f;

    private ImmediateMesh _mesh;
    private ShaderMaterial _material;
    private Texture2D _circleTexture;

    public override void _Ready()
    {
        CastShadow = ShadowCastingSetting.Off;
    }

    public void Frame(IReadOnlyList<Unit> units, Func<float, float, float> heightAt,
        Func<Unit, bool> enemy, Func<Unit, bool> visible)
    {
        EnsureReady();
        _mesh.ClearSurfaces();
        Visible = false;
        if (units == null || heightAt == null || _circleTexture == null) return;
        if (!AnySelected(units, visible)) return;

        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, _material);
        for (int index = 0; index < units.Count; index++)
        {
            Unit unit = units[index];
            if (!IsDrawn(unit, visible)) continue;
            float radius = unit.Type.SelectionDiameter * 0.5f;
            if (radius <= 0f) continue;

            Color color = enemy != null && enemy(unit)
                ? new Color(1f, .15f, .15f)
                : new Color(.2f, 1f, .25f);
            AppendCircle(_mesh, unit.Position, radius, color, heightAt);
        }
        _mesh.SurfaceEnd();
        Visible = true;
    }

    private void EnsureReady()
    {
        if (_mesh != null) return;
        _mesh = new ImmediateMesh();
        Mesh = _mesh;
        _circleTexture = Assets.LoadTexture(War3Assets.ConvertedPath(CirclePath));
        _material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/shadow_map.gdshader")
        };
        _material.SetShaderParameter("decal_texture", _circleTexture);
    }

    private static bool AnySelected(IReadOnlyList<Unit> units, Func<Unit, bool> visible)
    {
        for (int index = 0; index < units.Count; index++)
        {
            if (IsDrawn(units[index], visible)) return true;
        }
        return false;
    }

    private static bool IsDrawn(Unit unit, Func<Unit, bool> visible)
        => unit != null && unit.Alive && unit.Type != null
            && unit.View != null && unit.View.Selected
            && unit.Type.SelectionDiameter > 0f
            && unit.View.Root.IsVisibleInTree()
            && (visible == null || visible(unit));

    private static void AppendCircle(ImmediateMesh mesh, Vector3 center, float radius, Color color,
        Func<float, float, float> heightAt)
    {
        float span = radius * 2f;
        float step = span / Steps;
        float left = center.X - radius;
        float top = center.Z - radius;
        for (int row = 0; row < Steps; row++)
        {
            float z0 = top + row * step;
            float z1 = z0 + step;
            for (int column = 0; column < Steps; column++)
            {
                float x0 = left + column * step;
                float x1 = x0 + step;
                var uv00 = new Vector2((x0 - left) / span, (z0 - top) / span);
                var uv11 = new Vector2((x1 - left) / span, (z1 - top) / span);
                var uv10 = new Vector2((x1 - left) / span, (z0 - top) / span);
                var uv01 = new Vector2((x0 - left) / span, (z1 - top) / span);
                AddVertex(mesh, x0, z0, uv00, color, heightAt);
                AddVertex(mesh, x1, z1, uv11, color, heightAt);
                AddVertex(mesh, x1, z0, uv10, color, heightAt);
                AddVertex(mesh, x0, z0, uv00, color, heightAt);
                AddVertex(mesh, x0, z1, uv01, color, heightAt);
                AddVertex(mesh, x1, z1, uv11, color, heightAt);
            }
        }
    }

    private static void AddVertex(ImmediateMesh mesh, float x, float z, Vector2 uv, Color color,
        Func<float, float, float> heightAt)
    {
        mesh.SurfaceSetColor(color);
        mesh.SurfaceSetUV(uv);
        mesh.SurfaceAddVertex(new Vector3(x, heightAt(x, z) + GroundBias, z));
    }
}
