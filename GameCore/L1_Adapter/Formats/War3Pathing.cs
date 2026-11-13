using System.Text;

namespace OpenWC3.War3Formats;

[Flags]
public enum WpmCellFlags : byte
{
    None = 0,
    Unwalkable = 0x02,
    Unflyable = 0x04,
    Unbuildable = 0x08,
    UnpeonHarvest = 0x10,
    Blighted = 0x20,
    Unfloatable = 0x40,
    Unamphibious = 0x80,
}

// war3map.wpm: 32x32 world-unit pathing cells.
public sealed class PathingData
{
    public int Version;
    public int Width, Height;
    public byte[] Cells = Array.Empty<byte>();

    public static PathingData Parse(byte[] data)
    {
        if (data == null || data.Length < 16) return null;
        using var stream = new MemoryStream(data, false);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "MP3W") return null;
        int version = reader.ReadInt32();
        int width = reader.ReadInt32();
        int height = reader.ReadInt32();
        if (width <= 0 || height <= 0 || (long)width * height > int.MaxValue) return null;
        int count = width * height;
        byte[] cells = reader.ReadBytes(count);
        if (cells.Length != count) return null;
        return new PathingData { Version = version, Width = width, Height = height, Cells = cells };
    }

    public WpmCellFlags GetFlags(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height
            ? (WpmCellFlags)Cells[y * Width + x]
            : WpmCellFlags.Unwalkable | WpmCellFlags.Unflyable | WpmCellFlags.Unbuildable;

    public bool IsWalkable(int x, int y) => (GetFlags(x, y) & WpmCellFlags.Unwalkable) == 0;
    public bool IsFlyable(int x, int y) => (GetFlags(x, y) & WpmCellFlags.Unflyable) == 0;
    public bool IsBuildable(int x, int y) => (GetFlags(x, y) & WpmCellFlags.Unbuildable) == 0;
    public bool IsFloatable(int x, int y) => (GetFlags(x, y) & WpmCellFlags.Unfloatable) == 0;
    public bool IsAmphibious(int x, int y) => (GetFlags(x, y) & WpmCellFlags.Unamphibious) == 0;
}

[Flags]
public enum PathingTextureBits : byte
{
    None = 0,
    Unwalkable = 1,
    Unflyable = 2,
    Unbuildable = 4,
}

public sealed record PathingTextureMask(int Width, int Height, PathingTextureBits[] Cells);
public sealed record PathingOverlay(float X, float Y, int RotationDegrees,
    PathingTextureMask Texture, bool BlocksVision = false,
    int SourceCreationNumber = -1, string SourceTypeId = "",
    float OcclusionHeight = 0f, bool UseBuildingRotation = true,
    // .doo snapshots predate map main; null preserves legacy current-state binding
    // for synthetic/runtime overlays that do not carry an authored life state.
    bool? SourceIsDead = null);

public static class PathingOverlayRotation
{
    // FUN_6f20a720: truncate to integer, take signed %360, then clear the
    // signed %90 remainder. Decoded masks already use native footprint axes.
    public static int QuantizeBuildingDegrees(int degrees)
    {
        int angle = degrees % 360;
        angle -= angle % 90;
        return angle < 0 ? angle + 360 : angle;
    }

    public static int TextureDegrees(int degrees) =>
        QuantizeBuildingDegrees(degrees);

    // CWidget uses authored facing (FUN_6f234f00 -> FUN_6f210d30).
    // Buildings use the same cardinal indices (65a1f1..65a2bd).
    public static int TextureDegrees(int degrees, bool useBuildingRotation) =>
        TextureDegrees(degrees);
}

// Allocation-free view of decoded cells in world-facing, bottom-origin order.
public readonly struct PathingTextureView
{
    readonly PathingTextureMask _mask;
    readonly int _rotation;
    public int Width { get; }
    public int Height { get; }

    public PathingTextureView(PathingTextureMask mask,int degrees=-90)
    {
        _mask=mask;
        _rotation=PathingOverlayRotation.TextureDegrees(degrees);
        bool swap=_rotation%180!=0;
        Width=mask==null?0:swap?mask.Height:mask.Width;
        Height=mask==null?0:swap?mask.Width:mask.Height;
    }

    public PathingTextureBits this[int x,int y]
    {
        get
        {
            if(_mask==null||x<0||x>=Width||y<0||y>=Height)return PathingTextureBits.None;
            int sx=x,sy=y;
            switch(_rotation)
            {
                case 90:sx=y;sy=_mask.Height-1-x;break;
                case 180:sx=_mask.Width-1-x;sy=_mask.Height-1-y;break;
                case 270:sx=_mask.Width-1-y;sy=x;break;
            }
            return _mask.Cells[(_mask.Height-1-sy)*_mask.Width+sx];
        }
    }
}

// Warcraft pathing textures are uncompressed or RLE TGA files. File R/G/B encode
// unwalkable/unflyable/unbuildable. CPathingFootprint exchanges image dimensions
// and transposes top-origin image pixels; Cells retain logical top-left row order.
public static class PathingTextureDecoder
{
    public static PathingTextureMask Decode(byte[] data)
    {
        if (data == null || data.Length < 18) return null;
        int idLength = data[0], imageType = data[2];
        int width = BitConverter.ToUInt16(data, 12), height = BitConverter.ToUInt16(data, 14);
        int bytesPerPixel = data[16] / 8, descriptor = data[17];
        if ((imageType != 2 && imageType != 10) || width <= 0 || height <= 0 || bytesPerPixel < 3)
            return null;

        int offset = 18 + idLength, total = width * height, written = 0;
        var filePixels = new PathingTextureBits[total];
        while (written < total && offset < data.Length)
        {
            int count = 1;
            bool repeated = false;
            if (imageType == 10)
            {
                byte packet = data[offset++];
                repeated = (packet & 0x80) != 0;
                count = (packet & 0x7f) + 1;
            }

            if (repeated)
            {
                if (offset + bytesPerPixel > data.Length) return null;
                PathingTextureBits bits = ReadPixel(data, offset);
                offset += bytesPerPixel;
                for (int n = 0; n < count && written < total; n++) filePixels[written++] = bits;
            }
            else
            {
                for (int n = 0; n < count && written < total; n++)
                {
                    if (offset + bytesPerPixel > data.Length) return null;
                    filePixels[written++] = ReadPixel(data, offset);
                    offset += bytesPerPixel;
                }
            }
        }
        if (written != total) return null;

        bool topOrigin = (descriptor & 0x20) != 0;
        bool rightOrigin = (descriptor & 0x10) != 0;
        var logical = new PathingTextureBits[total];
        for (int fileY = 0; fileY < height; fileY++)
        for (int fileX = 0; fileX < width; fileX++)
        {
            int x = rightOrigin ? width - 1 - fileX : fileX;
            int y = topOrigin ? fileY : height - 1 - fileY;
            // FUN_6f200b00 passes +0xc as the image-width output and +8 as
            // image-height (6f200baa..bae). Its pixel loop reads image[x,y]
            // into footprint[y,x] (6f200c66..c7b). Native footprint row zero
            // maps to the lowest world Y; our logical rows run top to bottom.
            logical[(width - 1 - x) * height + y] = filePixels[fileY * width + fileX];
        }
        return new PathingTextureMask(height, width, logical);
    }

    static PathingTextureBits ReadPixel(byte[] data, int offset)
    {
        PathingTextureBits result = PathingTextureBits.None;
        // Game.dll FUN_6f200b00@6f200c80..c9b reads unmodified BGR bytes:
        // red/blue bit 0, green nonzero.
        if ((data[offset + 2] & 1) != 0) result |= PathingTextureBits.Unwalkable;
        if (data[offset + 1] != 0) result |= PathingTextureBits.Unflyable;
        if ((data[offset] & 1) != 0) result |= PathingTextureBits.Unbuildable;
        return result;
    }
}
