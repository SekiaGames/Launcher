namespace OpenWC3.RHI;

// 渲染抽象层枚举: 对应 OpenGL 概念, 不向上层泄漏 GLEnum

public enum BufferTarget { ArrayBuffer, ElementArrayBuffer }
public enum BufferUsage { StaticDraw, DynamicDraw, StreamDraw }
public enum PrimitiveType { Points, Lines, LineStrip, LineLoop, Triangles, TriangleStrip, TriangleFan }
public enum DataType { UnsignedByte, UnsignedShort, UnsignedInt, Float }
public enum Capability { Blend, CullFace, DepthTest, ScissorTest, StencilTest }
public enum ShaderType { Vertex, Fragment }

public enum BlendFactor
{
    Zero, One,
    SrcColor, OneMinusSrcColor,
    DstColor, OneMinusDstColor,
    SrcAlpha, OneMinusSrcAlpha,
    DstAlpha, OneMinusDstAlpha,
    ConstantColor, OneMinusConstantColor,
    ConstantAlpha, OneMinusConstantAlpha,
}

public enum DepthFunction { Never, Less, Equal, Lequal, Greater, NotEqual, Gequal, Always }

[Flags]
public enum ClearMask { None = 0, ColorBufferBit = 1, DepthBufferBit = 2, StencilBufferBit = 4 }

public enum TextureTarget { Texture2D, Texture2DArray, Texture3D }

public enum TextureMinFilter
{
    Nearest, Linear,
    NearestMipmapNearest, LinearMipmapNearest,
    NearestMipmapLinear, LinearMipmapLinear,
}

public enum TextureMagFilter { Nearest, Linear }

public enum TextureWrap { ClampToEdge, Repeat, MirroredRepeat, ClampToBorder }

public enum InternalFormat
{
    R8, R16f, Rg8, Rg16f, Rgba8, Srgb8Alpha8, Rgba32f,
    DepthComponent16, DepthComponent24, Depth24Stencil8, R32f,
}

public enum PixelFormat { Red, Rg, Rgb, Bgr, Rgba, Bgra, DepthComponent, StencilIndex, DepthStencil }

public enum FramebufferAttachment
{
    ColorAttachment0, ColorAttachment1, ColorAttachment2, ColorAttachment3,
    DepthAttachment, StencilAttachment, DepthStencilAttachment,
}
