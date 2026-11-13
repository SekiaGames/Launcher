using System.Text;

namespace OpenWC3.War3Formats.Text;

/// <summary>
/// Warcraft III maps in the wild mix UTF-8 and legacy Chinese code pages.
/// Decode strictly so invalid UTF-8 never becomes replacement-character mojibake.
/// </summary>
public static class War3TextEncoding
{
    static readonly UTF8Encoding StrictUtf8 = new(false, true);
    static readonly UTF8Encoding LenientUtf8 = new(false, false);
    static readonly Encoding Gb18030;

    static War3TextEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Gb18030 = Encoding.GetEncoding(54936,
            EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    public static string Decode(byte[] data) => data == null ? "" : Decode(data.AsSpan());

    // Profiles are line records. One damaged value must not change the encoding
    // of every other ability/item in the file (Fumo's HumanAbilityFunc.txt).
    public static string DecodeProfile(byte[] data)
    {
        if (data == null || data.Length == 0) return "";
        if (IsCleanText(data) || (data.Length >= 2 &&
            ((data[0] == 0xff && data[1] == 0xfe) || (data[0] == 0xfe && data[1] == 0xff))))
            return StripProfileByteOrderMarks(Decode(data));
        var result = new StringBuilder(data.Length);
        int start = 0;
        for (int end = 0; end <= data.Length; end++)
        {
            if (end < data.Length && data[end] != (byte)'\n') continue;
            var line = data.AsSpan(start, end - start);
            int nonAscii = 0, invalid = 0, runes = 0;
            for (int i = 0; i < line.Length;)
            {
                if (line[i] < 128) { i++; continue; }
                if (Rune.DecodeFromUtf8(line[i..], out _, out int consumed)
                    == System.Buffers.OperationStatus.Done)
                { nonAscii += consumed; runes++; i += consumed; }
                else { nonAscii++; invalid++; i++; }
            }
            // Only salvage overwhelmingly UTF-8 values. Pure legacy lines
            // retain the strict UTF-8 -> GB18030 -> Latin1 decoding chain.
            result.Append(invalid > 0 && runes >= 3 && invalid * 20 <= nonAscii
                ? LenientUtf8.GetString(line) : Decode(line));
            if (end < data.Length) result.Append('\n');
            start = end + 1;
        }
        return StripProfileByteOrderMarks(result.ToString());
    }

    /// <summary>Remove copied BOMs only at profile record starts. Preserve value characters.</summary>
    static string StripProfileByteOrderMarks(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('\uFEFF') < 0) return text ?? "";
        var result = new StringBuilder(text.Length);
        bool recordStart = true;
        foreach (char c in text)
        {
            if (recordStart && c == '\uFEFF') continue;
            result.Append(c);
            if (c is '\r' or '\n') recordStart = true;
            else if (c is not (' ' or '\t')) recordStart = false;
        }
        return result.ToString();
    }

    /// <summary>
    /// 解码可能**混编**的文本(部分行 UTF-8、部分行 GBK/GB18030)。
    ///
    /// 为什么要逐行回退: <see cref="Decode(ReadOnlySpan{byte})"/> 是**整文件级**判定 ——
    /// 只要全文能通过严格 UTF-8 校验就整体按 UTF-8 解。实机样本
    /// (1)神之墓地3.4.26.w3x 的 war3map.j 就是这种混编:
    /// **733 行**是合法 UTF-8(正常显示), **33 行**是 GBK(全是中文注释/故事文本),
    /// 全文因此"合法 UTF-8", 那 33 行就按 UTF-8 硬解成 `·ãõûÿ` 这样的乱码。
    /// 表现为触发器里的故事/提示文本乱码, 而物编、物品名等其它地方正常 ——
    /// 因为它们走的是各自独立、编码一致的物编文件。
    ///
    /// 判据: 某一行含非 ASCII 且**不是合法 UTF-8**, 就单独按 GB18030 解。
    /// 纯 ASCII 行、以及合法 UTF-8 的行都原样保留, 不影响既有行为。
    /// GB18030 是 GBK/GB2312 的超集, 对简繁中文都成立。
    /// </summary>
    public static string DecodeMixed(byte[] data)
    {
        if (data == null || data.Length == 0) return "";
        // 整文件就是合法 UTF-8(或纯 ASCII)时直接返回, 不逐行拆字符串 ——
        // 这是绝大多数地图的路径, 不能为了少数混编地图牺牲它的性能。
        if (IsCleanText(data) || (data.Length >= 2 &&
            ((data[0] == 0xff && data[1] == 0xfe) || (data[0] == 0xfe && data[1] == 0xff))))
            return Decode(data);
        int lineStart = data.Length >= 3 && data[0] == 0xef && data[1] == 0xbb && data[2] == 0xbf ? 3 : 0;
        var result = new System.Text.StringBuilder(data.Length);
        for (int i = lineStart; i <= data.Length; i++)
        {
            bool atEnd = i == data.Length;
            if (!atEnd && data[i] != (byte)'\n') continue;
            int length = i - lineStart;
            // Preserve CRLF and characters within generic values/paths.
            result.Append(DecodeLine(data.AsSpan(lineStart, length)));
            if (!atEnd) result.Append('\n');
            lineStart = i + 1;
        }
        return result.ToString();
    }

    /// <summary>全文是纯 ASCII 或合法 UTF-8 时为 true(不必逐行回退)。</summary>
    static bool IsCleanText(ReadOnlySpan<byte> data)
    {
        bool anyNonAscii = false;
        for (int i = 0; i < data.Length; i++)
            if (data[i] >= 0x80) { anyNonAscii = true; break; }
        if (!anyNonAscii) return true;
        try { StrictUtf8.GetString(data); return true; }
        catch (DecoderFallbackException) { return false; }
    }

    static string DecodeLine(ReadOnlySpan<byte> line)
    {
        bool anyNonAscii = false;
        for (int i = 0; i < line.Length; i++)
            if (line[i] >= 0x80) { anyNonAscii = true; break; }
        if (!anyNonAscii) return Encoding.Latin1.GetString(line);   // 纯 ASCII
        try { return StrictUtf8.GetString(line); }                 // 合法 UTF-8
        catch (DecoderFallbackException) { }
        try { return Gb18030.GetString(line); }                   // GBK/GB18030
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(line); }
    }

    public static string Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
        {
            // 一些中文地图带 UTF-8 BOM，但 WTS 中夹有截断的多字节字符。
            // BOM 已明确声明其余正文为 UTF-8；只替换损坏字节，不能让单个坏字符
            // 使整个地图载入失败，也不能把完整正文误判成 GB18030 后全部乱码。
            try { return StrictUtf8.GetString(data[3..]); }
            catch (DecoderFallbackException) { return LenientUtf8.GetString(data[3..]); }
        }
        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            return Encoding.Unicode.GetString(data[2..]);
        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            return Encoding.BigEndianUnicode.GetString(data[2..]);
        try
        {
            return StrictUtf8.GetString(data);
        }
        catch (DecoderFallbackException)
        {
            try { return Gb18030.GetString(data); }
            catch (DecoderFallbackException) { return Encoding.Latin1.GetString(data); }
        }
    }

    public static string ReadNullTerminated(BinaryReader reader,
        int maximumBytes = 16 * 1024 * 1024)
    {
        using var buffer = new MemoryStream();
        while (true)
        {
            int next = reader.BaseStream.ReadByte();
            if (next < 0) throw new EndOfStreamException();
            if (next == 0) break;
            buffer.WriteByte((byte)next);
            if (buffer.Length > maximumBytes)
                throw new InvalidDataException("Warcraft string is too large.");
        }
        return Decode(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)));
    }

    /// <summary>
    /// 读一个 NUL 结尾字符串: 字节整体是合法 UTF-8(或带 BOM/纯 ASCII)时走
    /// 原有 <see cref="Decode"/> 链; 否则用 <see cref="DecodeMixedUtf8Legacy"/>
    /// 分段混合解码。供 w3i 等地图信息字段使用。
    /// </summary>
    public static string ReadNullTerminatedMixed(BinaryReader reader,
        int maximumBytes = 16 * 1024 * 1024)
    {
        using var buffer = new MemoryStream();
        while (true)
        {
            int next = reader.BaseStream.ReadByte();
            if (next < 0) throw new EndOfStreamException();
            if (next == 0) break;
            buffer.WriteByte((byte)next);
            if (buffer.Length > maximumBytes)
                throw new InvalidDataException("Warcraft string is too large.");
        }
        var data = buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length));
        if (IsCleanText(data)) return Decode(data);
        // ★ 混编判据(见 IsMostlyValidUtf8): 只有"主体是合法 UTF-8、仅混少量
        //   传统码页字节"才走分段混合解码 —— 纯 GBK/CP949 文本的字节对会被
        //   贪心 UTF-8 扫描误吃(GBK 第二字节 0x40-0xFE 与 UTF-8 连续字节
        //   0x80-0xBF 高度重叠), 必须维持 Decode 原链。实测: GBK"兽族的繁荣
        //   与衰落"在无判据时被解成"ÊÞ×åµķ±ÈÙ…"。
        if (IsMostlyValidUtf8(data)) return DecodeMixedUtf8Legacy(data);
        return Decode(data);
    }

    /// <summary>
    /// 判断"主体是合法 UTF-8、仅混少量传统码页字节"——混编地图描述的典型形态
    /// (铁匠防御样本: 252 字节里只有 11 字节韩文非法, 占比 4.4%)。
    /// 判据: ① 非 ASCII 字节中非法占比 ≤ 30%; ② 同一合法 UTF-8 游程含 ≥ 9 个非 ASCII 字节
    /// (≥ 3 个连续中文字符, 纯 GBK 文本几乎不可能凑出这么长的合法序列)。
    /// </summary>
    static bool IsMostlyValidUtf8(ReadOnlySpan<byte> data)
    {
        int covered = 0, i = 0, n = data.Length, longestRun = 0, nonAscii = 0;
        foreach (byte value in data) if (value >= 0x80) nonAscii++;
        while (i < n)
        {
            int run = Utf8RunLength(data, i);
            if (run > 0)
            {
                int evidence = 0;
                for (int j = i; j < i + run; j++) if (data[j] >= 0x80) evidence++;
                covered += evidence;
                if (evidence > longestRun) longestRun = evidence;
                i += run;
            }
            else i++;
        }
        // ASCII URLs or prose cannot establish the encoding of Chinese text.
        if (longestRun < 9) return false;
        return nonAscii > 0 && 1.0 - (double)covered / nonAscii <= 0.30;
    }

    /// <summary>
    /// ★ 混编解码: 同一段字符串里 **UTF-8 与传统码页(GBK/CP949)共存**。
    ///
    /// 实机样本(2026-10-10 铁匠防御.w3x, w3i description 252 字节):
    ///   前段是合法 UTF-8 繁体中文"經營鐵匠鋪來進行防禦", 中间混入一段
    ///   **EUC-KR 韩文**(作者用韩文编辑器追加), 尾部又是 ASCII。任何单一
    ///   编码都解不了: 严格 UTF-8 在韩文段抛异常 → GB18030 也抛 →
    ///   跌到 Latin1 → 整段变"ç¶ç¨éåµ…"乱码。
    ///
    /// 策略: 贪心扫描**最长合法 UTF-8 游程**逐段切出; 非法字节起的段落用
    /// 传统码页解(GB18030 → CP949 → Latin1 逐段降级, 单段失败不拖垮整串)。
    /// ASCII 恒走 UTF-8 路径, 与原行为一致。
    ///
    /// ⚠ 只用于**地图信息类短文本**(w3i 的 name/author/description 等)。
    /// 不要用于整份脚本/WTS: 纯 GBK 文本里 GBK 双字节(第二字节 0x40-0xFE)
    /// 可能被误判成 UTF-8 连续序列, 混合扫描反而解坏 —— 那条路径维持
    /// <see cref="Decode"/> 的整体降级链。
    /// </summary>
    public static string DecodeMixedUtf8Legacy(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder(data.Length);
        int i = 0, n = data.Length;
        while (i < n)
        {
            int run = Utf8RunLength(data, i);
            if (run > 0)
            {
                sb.Append(Encoding.UTF8.GetString(data.Slice(i, run)));
                i += run;
                continue;
            }
            // 非法字节起: 找到下一个"合法 UTF-8 可重新开始"的位置之前,
            // 整段交给传统码页。段内逐级降级, 失败不拖垮整串。
            int j = i + 1;
            while (j < n && Utf8RunLength(data, j) == 0
                && !IsAscii(data[j])) j++;
            int legacyEnd = Math.Min(j, n);
            ReadOnlySpan<byte> seg = data.Slice(i, legacyEnd - i);
            sb.Append(DecodeLegacySegment(seg));
            i = legacyEnd;
        }
        return sb.ToString();
    }

    static string DecodeLegacySegment(ReadOnlySpan<byte> seg)
    {
        foreach (Encoding enc in LegacyEncodings)
        {
            try { return enc.GetString(seg); }
            catch (DecoderFallbackException) { }
        }
        return Encoding.Latin1.GetString(seg);
    }

    static readonly Encoding[] LegacyEncodings = CreateLegacyEncodings();

    static Encoding[] CreateLegacyEncodings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return new[]
        {
            Encoding.GetEncoding(54936, EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback),               // GB18030
            Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback),               // CP949 (EUC-KR)
        };
    }

    /// <summary>从 from 起最长合法 UTF-8 字节游程长度(0 = 该字节无法开始)。</summary>
    static int Utf8RunLength(ReadOnlySpan<byte> data, int from)
    {
        int pos = from, n = data.Length;
        while (pos < n)
        {
            byte c = data[pos];
            if (c < 0x80) { pos++; continue; }
            if (Rune.DecodeFromUtf8(data[pos..], out _, out int consumed)
                != System.Buffers.OperationStatus.Done) break;
            pos += consumed;
        }
        return pos - from;
    }

    static bool IsAscii(byte b) => b < 0x80;
}
