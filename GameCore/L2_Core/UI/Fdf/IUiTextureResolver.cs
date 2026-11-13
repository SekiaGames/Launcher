using OpenWC3.RHI;

namespace OpenWC3.UI;

// 纹理解析接口: Inflator 调用, Game 层实现 (War3ResourceSource + War3UiSkin, 处理 DecorateFileNames)
public interface IUiTextureResolver
{
    ITexture Resolve(string path);

    /// <summary>
    /// 禁用态图标。原版 <c>CCommandButton</c> 0x6F336EF0: 把图标路径改写成
    /// <c>&lt;CommandButtonDisabledArtPath&gt;DIS&lt;文件名&gt;</c> (格式串 "%sDIS%s"),
    /// 取不到时回落 skin 的 <c>DefaultButton</c>。不实现 BLP 解码的解析器返回 null 即可。
    /// </summary>
    ITexture ResolveDisabledIcon(string icon) => null;
}

// FDF 坐标 (0.8x0.6 比例) -> 设计单位 (1024x768 量级) 缩放
// 0.8 比例宽对应 1024 设计单位, 故比例 = 1024/0.8 = 1280
public static class FdfScale
{
    public const float ToDesignUnits = 1280f;

    // 纹理像素 -> 设计单位: 原版把纹理像素当作 px/1000 的比例尺寸 (open-realm r_backdrop.c)
    public const float TexturePixelsToDesign = ToDesignUnits / 1000f;

    public static float S(float fdfValue) => fdfValue * ToDesignUnits;
    public static float FromTexturePixels(float pixels) => pixels * TexturePixelsToDesign;
}
