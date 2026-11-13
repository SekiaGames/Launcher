namespace OpenWC3.RHI;

// 纹理抽象 (对应 GL Texture Object)
public interface ITexture : IDisposable
{
    TextureTarget Target { get; }
    uint Width { get; }
    uint Height { get; }

    void SetMinFilter(TextureMinFilter filter);
    void SetMagFilter(TextureMagFilter filter);
    void SetWrapS(TextureWrap wrap);
    void SetWrapT(TextureWrap wrap);

    void Image2D(int level, InternalFormat internalFormat, uint width, uint height,
        PixelFormat format, DataType type, ReadOnlySpan<byte> pixels);
    void Image2D<T>(int level, InternalFormat internalFormat, uint width, uint height,
        PixelFormat format, DataType type, ReadOnlySpan<T> pixels) where T : unmanaged;
    void SubImage2D(int level, int x, int y, uint width, uint height,
        PixelFormat format, DataType type, ReadOnlySpan<byte> pixels);

    void Image3D(int level, InternalFormat internalFormat, uint width, uint height, uint depth,
        PixelFormat format, DataType type, ReadOnlySpan<byte> pixels);
    void Image3D<T>(int level, InternalFormat internalFormat, uint width, uint height, uint depth,
        PixelFormat format, DataType type, ReadOnlySpan<T> pixels) where T : unmanaged;
    void SubImage3D(int level, int x, int y, int z, uint width, uint height, uint depth,
        PixelFormat format, DataType type, ReadOnlySpan<byte> pixels);

    void GenerateMipmap();
}
