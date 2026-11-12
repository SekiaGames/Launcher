using Godot;
using System;
using System.Collections.Generic;
using OpenWC3.Game;

namespace OpenWC3.Game.UI;

/// <summary>
/// 小地图：用 w3e 每格的地表贴图平均色生成底图（地图自带的是 blp，Godot 不能直接读），
/// 单位按玩家色打点，中立敌对用红点，点击与拖拽移动镜头。
/// </summary>
public sealed partial class MinimapFrame : Control
{
    public const float MapSize = 190f;

    private Texture2D _texture;
    private Texture2D _fogTexture;
    private W3eTerrain _terrain;
    private Func<IReadOnlyList<Unit>> _units;
    private Func<Vector3> _cameraFocus;
    private Action<Vector3> _navigate;
    private Func<Unit, bool> _filter;
    private float _refresh;
    private bool _dragging;
    private Vector3 _pingWorld;
    private float _pingRemaining;
    private float _pingAge;

    public Action<Vector3, bool> RightNavigate;

    public void Configure(W3eTerrain terrain, Func<IReadOnlyList<Unit>> units, Func<Vector3> cameraFocus, Action<Vector3> navigate,
        Func<Unit, bool> filter = null)
    {
        _filter = filter;
        _terrain = terrain;
        _units = units;
        _cameraFocus = cameraFocus;
        _navigate = navigate;

        CustomMinimumSize = new Vector2(MapSize, MapSize);
        Size = new Vector2(MapSize, MapSize);
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
    }

    public void SetTexture(Texture2D texture)
    {
        _texture = texture;
        QueueRedraw();
    }

    public void SetFogTexture(Texture2D fogTexture)
    {
        _fogTexture = fogTexture;
        QueueRedraw();
    }

    public bool PingActive => _pingRemaining > 0f;

    public void Ping(Vector3 world)
    {
        _pingWorld = world;
        _pingRemaining = 2.2f;
        _pingAge = 0f;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (_pingRemaining > 0f)
        {
            _pingRemaining -= (float)delta;
            _pingAge += (float)delta;
            QueueRedraw();
        }

        _refresh += (float)delta;
        if (_refresh < 0.15f) return;
        _refresh = 0f;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0f, 0f, MapSize, MapSize), new Color(0.02f, 0.03f, 0.04f, 0.9f), true);
        if (_texture != null)
        {
            DrawTextureRect(_texture, new Rect2(0f, 0f, MapSize, MapSize), false);
        }

        if (_fogTexture != null)
        {
            // 雾贴图行 0 对应地图南侧，小地图上北下南，所以纵向翻转
            DrawSetTransform(new Vector2(0f, MapSize), 0f, new Vector2(1f, -1f));
            DrawTextureRect(_fogTexture, new Rect2(0f, 0f, MapSize, MapSize), false);
            DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
        }

        if (_terrain != null && _units != null)
        {
            foreach (Unit unit in _units())
            {
                if (!unit.Alive) continue;
                if (_filter != null && !_filter(unit)) continue;
                Vector2 point = WorldToMap(unit.Position);
                if (point.X < 0f || point.Y < 0f || point.X > MapSize || point.Y > MapSize) continue;

                Color color = unit.Player switch
                {
                    MeleeRules.PlayerNeutralHostile => new Color(0.85f, 0.2f, 0.15f),
                    MeleeRules.PlayerNeutralPassive => new Color(0.55f, 0.55f, 0.55f),
                    _ => UnitSceneComponent.ColorOf(unit.Player),
                };

                float scale = unit.IsBuilding ? 3.5f : 2.5f;
                DrawRect(new Rect2(point.X - scale * 0.5f, point.Y - scale * 0.5f, scale, scale), color, true);
            }
        }

        if (_cameraFocus != null && _terrain != null)
        {
            Vector2 center = WorldToMap(_cameraFocus());
            DrawRect(new Rect2(center.X - 16f, center.Y - 11f, 32f, 22f), new Color(1f, 1f, 1f, 0.8f), false, 1f);
        }

        if (_terrain != null && _pingRemaining > 0f)
        {
            Vector2 ping = WorldToMap(_pingWorld);
            float cycle = _pingAge % 0.9f;
            float radius = 3f + cycle * 14f;
            float alpha = Mathf.Clamp(1f - cycle / 0.9f, 0f, 1f);
            DrawArc(ping, radius, 0f, Mathf.Tau, 24, new Color(0.35f, 1f, 0.45f, alpha), 2f);
            DrawCircle(ping, 2.5f, new Color(0.35f, 1f, 0.45f, 0.9f));
        }

        DrawRect(new Rect2(0f, 0f, MapSize, MapSize), new Color(0.45f, 0.42f, 0.3f, 1f), false, 1f);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && _dragging)
        {
            Navigate(motion.Position);
            AcceptEvent();
            return;
        }

        if (@event is not InputEventMouseButton button) return;

        if (button.ButtonIndex == MouseButton.Left)
        {
            _dragging = button.Pressed;
            if (button.Pressed)
            {
                Navigate(button.Position);
                AcceptEvent();
            }
            return;
        }

        if (button.ButtonIndex == MouseButton.Right && button.Pressed)
        {
            RightNavigate?.Invoke(MapToWorld(button.Position), button.ShiftPressed);
            AcceptEvent();
        }
    }

    private void Navigate(Vector2 local)
    {
        if (_terrain == null || _navigate == null) return;
        _navigate(MapToWorld(local));
    }

    private Vector2 WorldToMap(Vector3 world)
    {
        float cellsX = Mathf.Max(1, _terrain.Width - 1);
        float cellsY = Mathf.Max(1, _terrain.Height - 1);
        float mapX = _terrain.OffsetX - world.X;
        float mapY = world.Z + _terrain.OffsetY;
        float u = (mapX - _terrain.OffsetX) / (cellsX * MeleeRules.CellWidth);
        float v = (mapY - _terrain.OffsetY) / (cellsY * MeleeRules.CellWidth);
        return new Vector2(u * MapSize, (1f - v) * MapSize);
    }

    private Vector3 MapToWorld(Vector2 local)
    {
        float cellsX = Mathf.Max(1, _terrain.Width - 1);
        float cellsY = Mathf.Max(1, _terrain.Height - 1);
        float u = Mathf.Clamp(local.X / MapSize, 0f, 1f);
        float v = 1f - Mathf.Clamp(local.Y / MapSize, 0f, 1f);
        float mapX = _terrain.OffsetX + u * cellsX * MeleeRules.CellWidth;
        float mapY = _terrain.OffsetY + v * cellsY * MeleeRules.CellWidth;
        float worldX = -(mapX - _terrain.OffsetX);
        float worldZ = mapY - _terrain.OffsetY;
        return new Vector3(worldX, MapWorldBuilder.HeightAt(_terrain, worldX, worldZ), worldZ);
    }

    /// <summary>用每格地表贴图的平均色拼底图，按高度做明暗、水面加蓝。</summary>
    public static Texture2D BuildTexture(W3eTerrain terrain, TerrainTextureLoader tiles, int resolution = 190)
    {
        if (terrain == null || tiles == null) return null;

        var palette = new Color[terrain.GroundTextures.Count];
        for (int i = 0; i < terrain.GroundTextures.Count; i++)
        {
            string id = terrain.GroundTextures[i];
            Image source = tiles.Get(id);
            if (source == null)
            {
                palette[i] = new Color(0.25f, 0.3f, 0.25f);
                continue;
            }

            // 地表贴图是 4x4/8x4 的变体图集，取左上那一格的中间区域平均色最稳
            int tile = Mathf.Max(1, source.GetHeight() / 4);
            int inset = Mathf.Max(1, tile / 4);
            Image sample = source.GetRegion(new Rect2I(inset, inset, tile - inset * 2, tile - inset * 2));
            sample.Resize(1, 1, Image.Interpolation.Bilinear);
            palette[i] = sample.GetPixel(0, 0);
        }

        int cellsX = Mathf.Max(1, terrain.Width - 1);
        int cellsY = Mathf.Max(1, terrain.Height - 1);
        var image = Image.CreateEmpty(resolution, resolution, false, Image.Format.Rgba8);

        for (int py = 0; py < resolution; py++)
        {
            int cy = Mathf.Clamp(cellsY - 1 - py * cellsY / resolution, 0, cellsY - 1);
            for (int px = 0; px < resolution; px++)
            {
                int cx = Mathf.Clamp(px * cellsX / resolution, 0, cellsX - 1);
                W3eTerrain.Corner corner = terrain.Corners[cy * terrain.Width + cx];

                Color color = corner.GroundTexture >= 0 && corner.GroundTexture < palette.Length
                    ? palette[corner.GroundTexture]
                    : new Color(0.25f, 0.3f, 0.25f);

                if (corner.Blight)
                {
                    color = new Color(
                        Mathf.Lerp(color.R, 0.32f, 0.55f),
                        Mathf.Lerp(color.G, 0.24f, 0.55f),
                        Mathf.Lerp(color.B, 0.36f, 0.55f),
                        color.A);
                }

                float height = Mathf.Clamp(corner.HeightWorld / 320f, -1f, 3f);
                float shade = Mathf.Clamp(1f + height * 0.28f, 0.45f, 1.5f);
                color = new Color(
                    Mathf.Clamp(color.R * shade, 0f, 1f),
                    Mathf.Clamp(color.G * shade, 0f, 1f),
                    Mathf.Clamp(color.B * shade, 0f, 1f),
                    1f);

                if (corner.HasWater)
                {
                    color = color.Lerp(new Color(0.16f, 0.32f, 0.52f), 0.75f);
                }

                image.SetPixel(px, py, color);
            }
        }

        return ImageTexture.CreateFromImage(image);
    }
}
