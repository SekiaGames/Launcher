using Godot;
using System;
using System.Collections.Generic;

namespace OpenWC3.Game;

public sealed partial class UnitShadowRenderer : Node3D
{
    private const string ShadowDirectory = @"ReplaceableTextures\Shadows\";

    private const float GroundOffset = ShadowRenderer.UnitShadowOffset;
    private const float Opacity = 0.5f;

    private static readonly Vector2 BuildingAnchor = new(0.3f, 0.3f);

    private readonly Dictionary<string, MeshInstance3D> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ShaderMaterial> _materials = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<Vector4>> _quads = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _order = new();
    private readonly List<string> _drawn = new();

    public void Frame(IReadOnlyList<Unit> units, Func<float, float, float> heightAt,
        Func<Unit, bool> visible)
    {
        if (units == null || heightAt == null) return;

        _order.Clear();
        for (int index = 0; index < units.Count; index++)
        {
            Unit unit = units[index];
            if (!IsDrawn(unit, visible)) continue;
            ComputeQuad(unit, out string art, out Vector4 quad);
            if (art == null) continue;

            if (!_quads.TryGetValue(art, out List<Vector4> list))
            {
                list = new List<Vector4>();
                _quads[art] = list;
            }
            if (list.Count == 0) _order.Add(art);
            list.Add(quad);
        }

        _drawn.Clear();
        for (int index = 0; index < _order.Count; index++)
        {
            string art = _order[index];
            List<Vector4> list = _quads[art];
            if (list.Count == 0) continue;
            Draw(art, list, heightAt);
            list.Clear();
            _drawn.Add(art);
        }

        if (_drawn.Count < _nodes.Count)
        {
            foreach (KeyValuePair<string, MeshInstance3D> pair in _nodes)
            {
                if (!_drawn.Contains(pair.Key)) pair.Value.Visible = false;
            }
        }
    }

    private static bool IsDrawn(Unit unit, Func<Unit, bool> visible)
        => unit != null && unit.Alive && unit.Type != null
            && unit.View != null && unit.View.Root != null
            && unit.View.Root.IsVisibleInTree()
            && (visible == null || visible(unit));

    private void ComputeQuad(Unit unit, out string art, out Vector4 quad)
    {
        art = null;
        quad = default;

        float mapX = -unit.Position.X;
        float mapY = unit.Position.Z;

        UnitType type = unit.Type;
        string building = Normalize(type.BuildingShadowArt);
        if (type.IsBuilding && building != null)
        {
            Texture2D texture = Resolve(building);
            if (texture == null) return;

            float width = texture.GetWidth() * 32f;
            float height = texture.GetHeight() * 32f;
            if (width <= 0f || height <= 0f) return;

            art = building;
            quad = WorldRect(mapX - width * BuildingAnchor.X, mapY - height * BuildingAnchor.Y,
                width, height);
            return;
        }

        string shadow = Normalize(type.ShadowArt);
        if (shadow == null) return;
        float unitWidth = type.ShadowWidth;
        float unitHeight = type.ShadowHeight;
        if (unitWidth <= 0f || unitHeight <= 0f) return;
        if (Resolve(shadow) == null) return;

        art = shadow;
        quad = WorldRect(mapX - type.ShadowOffsetX, mapY - type.ShadowOffsetY,
            unitWidth, unitHeight);
    }

    private static Vector4 WorldRect(float left, float top, float width, float height)
        => new(-(left + width), top, -left, top + height);

    private void Draw(string art, List<Vector4> quads, Func<float, float, float> heightAt)
    {
        if (!_nodes.TryGetValue(art, out MeshInstance3D node))
        {
            node = new MeshInstance3D
            {
                Name = "Shadow_" + art,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            node.Mesh = new ImmediateMesh();
            AddChild(node);
            _nodes[art] = node;
        }

        var mesh = (ImmediateMesh)node.Mesh;
        mesh.ClearSurfaces();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles, Material(art));
        for (int index = 0; index < quads.Count; index++) AppendQuad(mesh, quads[index], heightAt);
        mesh.SurfaceEnd();
        node.Visible = true;
    }

    private static void AppendQuad(ImmediateMesh mesh, Vector4 area,
        Func<float, float, float> heightAt)
    {
        float left = area.X, top = area.Y, right = area.Z, bottom = area.W;
        float spanX = Mathf.Max(0.001f, right - left);
        float spanZ = Mathf.Max(0.001f, bottom - top);
        int side = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(spanX, spanZ) / 128f), 2, 8);

        for (int row = 0; row < side; row++)
        {
            float v0 = (float)row / side;
            float v1 = (float)(row + 1) / side;
            float z0 = top + spanZ * v0;
            float z1 = top + spanZ * v1;
            for (int column = 0; column < side; column++)
            {
                float u0 = (float)column / side;
                float u1 = (float)(column + 1) / side;
                float x0 = left + spanX * u0;
                float x1 = left + spanX * u1;

                AddVertex(mesh, x0, z0, u0, 1f - v0, heightAt);
                AddVertex(mesh, x1, z1, u1, 1f - v1, heightAt);
                AddVertex(mesh, x1, z0, u1, 1f - v0, heightAt);
                AddVertex(mesh, x0, z0, u0, 1f - v0, heightAt);
                AddVertex(mesh, x0, z1, u0, 1f - v1, heightAt);
                AddVertex(mesh, x1, z1, u1, 1f - v1, heightAt);
            }
        }
    }

    private static void AddVertex(ImmediateMesh mesh, float x, float z, float u, float v,
        Func<float, float, float> heightAt)
    {
        mesh.SurfaceSetColor(new Color(0f, 0f, 0f, Opacity));
        mesh.SurfaceSetUV(new Vector2(u, v));
        mesh.SurfaceAddVertex(new Vector3(x, heightAt(x, z) + GroundOffset, z));
    }

    private ShaderMaterial Material(string art)
    {
        if (_materials.TryGetValue(art, out ShaderMaterial cached)) return cached;

        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/shaders/shadow_map.gdshader"),
        };
        material.SetShaderParameter("decal_texture", Resolve(art));
        _materials[art] = material;
        return material;
    }

    private Texture2D Resolve(string name)
    {
        if (_textures.TryGetValue(name, out Texture2D texture)) return texture;
        texture = Assets.LoadTexture(War3Assets.ConvertedPath(ShadowDirectory + name + ".blp"));
        _textures[name] = texture;
        return texture;
    }

    private static string Normalize(string art)
    {
        if (string.IsNullOrWhiteSpace(art)) return null;
        string name = art.Trim();
        if (name is "_" or "-") return null;
        if (name.EndsWith(".blp", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }
        return name;
    }
}
