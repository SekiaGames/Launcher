using Godot;
using OpenWC3.RHI;

namespace OpenWC3.Game.UI;

public sealed class GodotUiTexture : ITexture
{
    public Texture2D Source { get; }

    public GodotUiTexture(Texture2D source) => Source = source;

    public TextureTarget Target => TextureTarget.Texture2D;

    public uint Width => (uint)(Source?.GetWidth() ?? 0);

    public uint Height => (uint)(Source?.GetHeight() ?? 0);

    public void SetMinFilter(TextureMinFilter filter) { }

    public void SetMagFilter(TextureMagFilter filter) { }

    public void SetWrapS(TextureWrap wrap) { }

    public void SetWrapT(TextureWrap wrap) { }

    public void Image2D(int level, InternalFormat internalFormat, uint width, uint height,
        PixelFormat format, DataType type, System.ReadOnlySpan<byte> pixels) { }

    public void Image2D<T>(int level, InternalFormat internalFormat, uint width, uint height,
        PixelFormat format, DataType type, System.ReadOnlySpan<T> pixels) where T : unmanaged { }

    public void SubImage2D(int level, int x, int y, uint width, uint height,
        PixelFormat format, DataType type, System.ReadOnlySpan<byte> pixels) { }

    public void Image3D(int level, InternalFormat internalFormat, uint width, uint height, uint depth,
        PixelFormat format, DataType type, System.ReadOnlySpan<byte> pixels) { }

    public void Image3D<T>(int level, InternalFormat internalFormat, uint width, uint height, uint depth,
        PixelFormat format, DataType type, System.ReadOnlySpan<T> pixels) where T : unmanaged { }

    public void SubImage3D(int level, int x, int y, int z, uint width, uint height, uint depth,
        PixelFormat format, DataType type, System.ReadOnlySpan<byte> pixels) { }

    public void GenerateMipmap() { }

    public void Dispose() { }
}
