using Godot;
using System;

namespace OpenWC3.Game;

public static class MapThumbnail
{
    public static Texture2D Load(MapSource map)
    {
        byte[] data = map?.Package?.ReadMapFile("war3mapMap.blp");
        if (data == null || data.Length < 148) return null;
        Image image = Decode(data);
        return image == null ? null : ImageTexture.CreateFromImage(image);
    }

    private static Image Decode(byte[] data)
    {
        bool blp2 = data[3] == (byte)'2';
        int encoding = BitConverter.ToInt32(data, 4);
        int alphaBits = blp2 ? 8 : BitConverter.ToInt32(data, 8);
        int width = BitConverter.ToInt32(data, 12);
        int height = BitConverter.ToInt32(data, 16);
        if (width <= 0 || height <= 0 || (long)width * height > 4096 * 4096) return null;

        int offset = BitConverter.ToInt32(data, 20);
        int size = BitConverter.ToInt32(data, 84);
        if (offset < 148 || offset >= data.Length) offset = 148;

        if (encoding == 0) return DecodeJpeg(data, offset);
        if (encoding != 1) return null;
        return DecodePaletted(data, offset, width, height, alphaBits, blp2);
    }

    private static Image DecodeJpeg(byte[] data, int start)
    {
        int soi = -1;
        for (int i = start; i + 1 < data.Length; i++)
        {
            if (data[i] != 0xFF || data[i + 1] != 0xD8) continue;
            soi = i;
            break;
        }
        if (soi < 0) return null;

        var image = new Image();
        byte[] jpeg = new byte[data.Length - soi];
        Array.Copy(data, soi, jpeg, 0, jpeg.Length);
        Error error = image.LoadJpgFromBuffer(jpeg);
        if (error != Error.Ok)
        {
            GD.Print($"[thumb] 缩略图 JPEG 解码失败 {error}（{jpeg.Length} 字节）");
            return null;
        }
        return image;
    }

    private static Image DecodePaletted(byte[] data, int offset, int width, int height,
        int alphaBits, bool blp2)
    {
        int paletteSize = 256 * 4;
        if (offset + paletteSize > data.Length) return null;
        var palette = new byte[256, 4];
        for (int i = 0; i < 256; i++)
        {
            int src = offset + i * 4;
            palette[i, 0] = data[src + 2];
            palette[i, 1] = data[src + 1];
            palette[i, 2] = data[src];
            palette[i, 3] = data[src + 3];
        }

        int indexStart = offset + paletteSize;
        int pixels = width * height;
        if (indexStart + pixels > data.Length) return null;
        int alphaStart = indexStart + pixels;
        int alphaStride = alphaBits switch
        {
            0 => 0,
            1 => (width + 7) / 8,
            4 => (width + 1) / 2,
            _ => width,
        };
        if (alphaBits > 0 && alphaStart + alphaStride * height > data.Length) alphaBits = 0;
        if (blp2) alphaBits = 8;

        var rgba = new byte[pixels * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = data[indexStart + y * width + x];
                int target = (y * width + x) * 4;
                rgba[target] = palette[index, 0];
                rgba[target + 1] = palette[index, 1];
                rgba[target + 2] = palette[index, 2];
                rgba[target + 3] = alphaBits > 0
                    ? ReadAlpha(data, alphaStart + y * alphaStride, x, alphaBits)
                    : palette[index, 3];
            }
        }
        return Image.CreateFromData(width, height, false, Image.Format.Rgba8, rgba);
    }

    private static byte ReadAlpha(byte[] data, int rowStart, int x, int alphaBits) => alphaBits switch
    {
        1 => (byte)(((data[rowStart + (x >> 3)] >> (x & 7)) & 1) != 0 ? 255 : 0),
        4 => (byte)((data[rowStart + (x >> 1)] >> ((x & 1) == 0 ? 4 : 0) & 0xF) * 17),
        _ => data[rowStart + x],
    };
}
