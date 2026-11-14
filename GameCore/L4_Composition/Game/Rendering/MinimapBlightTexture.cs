using Godot;
using System;
using OpenWC3.Game.UI;
using OpenWC3.UI;
using NVector4 = System.Numerics.Vector4;

namespace OpenWC3.Game;

internal sealed class MinimapBlightTexture
{
    private readonly byte[] _basePixels;
    private readonly byte[] _pixels;
    private readonly Image _image;
    private readonly ImageTexture _imageTexture;
    private ScoutedBlight _lastBlight;
    private int _version = -1;
    private NVector4 _bounds;
    private NVector4 _mask;

    internal MinimapBlightTexture(Image source)
    {
        _image = Image.CreateFromData(source.GetWidth(), source.GetHeight(), false,
            Image.Format.Rgba8, source.GetData());
        _basePixels = _image.GetData();
        _pixels = new byte[_basePixels.Length];
        _imageTexture = ImageTexture.CreateFromImage(_image);
        Texture = new GodotUiTexture(_imageTexture);
    }

    internal GodotUiTexture Texture { get; }

    internal bool Update(ScoutedBlight blight, NVector4 bounds, NVector4 mask)
    {
        int version = blight?.Version ?? 0;
        if (ReferenceEquals(blight, _lastBlight) && version == _version
            && bounds == _bounds && mask == _mask) return false;
        _lastBlight = blight;
        _version = version;
        _bounds = bounds;
        _mask = mask;

        Buffer.BlockCopy(_basePixels, 0, _pixels, 0, _pixels.Length);
        if (blight != null)
        {
            byte red = Channel(mask.X), green = Channel(mask.Y), blue = Channel(mask.Z);
            int width = _image.GetWidth(), height = _image.GetHeight();
            UiRect content = MinimapFrame.FitContentRect(new UiRect(0f, 0f, width, height), bounds);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!content.Contains(x + 0.5f, y + 0.5f)) continue;
                    float worldX = bounds.X
                        + (x + 0.5f - content.X) / content.Width * (bounds.Z - bounds.X);
                    float worldY = bounds.W
                        - (y + 0.5f - content.Y) / content.Height * (bounds.W - bounds.Y);
                    if (!blight.IsMinimapBlighted(worldX, worldY)) continue;
                    int index = (y * width + x) * 4;
                    _pixels[index] &= red;
                    _pixels[index + 1] &= green;
                    _pixels[index + 2] |= blue;
                }
            }
        }

        _image.SetData(_image.GetWidth(), _image.GetHeight(), false, Image.Format.Rgba8, _pixels);
        _imageTexture.Update(_image);
        return true;
    }

    private static byte Channel(float value)
        => (byte)Mathf.Clamp(Mathf.Round(value * 255f), 0, 255);
}
