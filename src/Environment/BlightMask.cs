using Godot;
using System;
using System.Collections.Generic;
using WorldEdit.Core.Formats.W3x;

namespace Launcher;

/// <summary>
/// 腐地遮罩：一格一个字节，静态部分来自 w3e 角点的 blight 标志，动态部分由亡灵建筑盖章
/// （`Unit.BlightRadius`，缺省 `MeleeRules.BlightRadius`）。
/// 原版里腐地一旦生成就不会自己消失，所以这里只新增不清除，攒够改动再推给地形 shader。
/// </summary>
public sealed class BlightMask
{
    private const float CellSize = 128f;
    private const float RefreshSeconds = 0.5f;

    private readonly W3eTerrain _terrain;
    private readonly Image _image;
    private readonly ImageTexture _texture;
    private readonly byte[] _pixels;
    private float _timer;
    private bool _dirty;
    private int _logBudget = 6;

    /// <summary>当前腐地格数（静态 + 动态累计）。</summary>
    public int BlightedCells { get; private set; }

    public BlightMask(W3eTerrain terrain, ShaderMaterial material, Texture2D blightTexture)
    {
        _terrain = terrain;

        int width = Math.Max(1, terrain.Width);
        int height = Math.Max(1, terrain.Height);
        _pixels = new byte[width * height];

        W3eTerrain.Corner[] corners = terrain.Corners;
        for (int index = 0; index < _pixels.Length && index < corners.Length; index++)
        {
            if (!corners[index].Blight) continue;
            _pixels[index] = 255;
            BlightedCells++;
        }

        _image = Image.CreateFromData(width, height, false, Image.Format.L8, _pixels);
        _texture = ImageTexture.CreateFromImage(_image);

        material.SetShaderParameter("blight_mask", _texture);
        material.SetShaderParameter("blight_texture", blightTexture);
        material.SetShaderParameter("blight_scale", new Vector2(1f / (width * CellSize), 1f / (height * CellSize)));
        material.SetShaderParameter("blight_enabled", blightTexture != null);

        GD.Print($"[BLIGHT] 掩码 {width}x{height}，静态腐地 {BlightedCells} 格，"
            + $"贴图 {(blightTexture != null ? "有" : "缺")}");
    }

    /// <summary>每 0.5s 扫一遍亡灵建筑，把它们的腐地半径盖进掩码；有变化才上传纹理。</summary>
    public void Tick(float delta, IReadOnlyList<Unit> units)
    {
        _timer -= delta;
        if (_timer > 0f) return;
        _timer = RefreshSeconds;

        if (units != null)
        {
            for (int index = 0; index < units.Count; index++)
            {
                Unit unit = units[index];
                if (unit == null || !unit.Alive || !unit.IsBuilding || unit.IsUnderConstruction) continue;
                if (unit.Type == null || !string.Equals(unit.Type.Race, "undead", StringComparison.OrdinalIgnoreCase)) continue;

                float radius = unit.BlightRadius > 0f ? unit.BlightRadius : MeleeRules.BlightRadius;
                int painted = Stamp(unit.Position, radius);
                if (painted > 0 && _logBudget > 0)
                {
                    _logBudget--;
                    GD.Print($"[BLIGHT] {unit.Type.Name} 铺开腐地 {painted} 格（累计 {BlightedCells}）");
                }
            }
        }

        if (!_dirty) return;
        _dirty = false;
        _image.SetData(_terrain.Width, _terrain.Height, false, Image.Format.L8, _pixels);
        _texture.Update(_image);
    }

    /// <summary>以世界坐标为圆心盖一个腐地圆。</summary>
    public int Stamp(Vector3 world, float radius)
    {
        if (radius <= 0f) return 0;

        int width = _terrain.Width;
        int height = _terrain.Height;
        float centerX = -world.X / CellSize;
        float centerY = world.Z / CellSize;
        float radiusCells = radius / CellSize;
        float radiusSquared = radiusCells * radiusCells;

        int minX = Math.Max(0, (int)MathF.Floor(centerX - radiusCells));
        int maxX = Math.Min(width - 1, (int)MathF.Ceiling(centerX + radiusCells));
        int minY = Math.Max(0, (int)MathF.Floor(centerY - radiusCells));
        int maxY = Math.Min(height - 1, (int)MathF.Ceiling(centerY + radiusCells));

        int painted = 0;
        for (int y = minY; y <= maxY; y++)
        {
            float dy = y + 0.5f - centerY;
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - centerX;
                if (dx * dx + dy * dy > radiusSquared) continue;

                int index = y * width + x;
                if (_pixels[index] == 255) continue;
                _pixels[index] = 255;
                _dirty = true;
                painted++;
            }
        }

        if (painted > 0) BlightedCells += painted;
        return painted;
    }
}
