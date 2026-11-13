using System.Text.RegularExpressions;

namespace OpenWC3.UI;

// WC3 文本工具: 颜色码剥离与定宽换行。
// 之前 TooltipFrame/UnitInfoPanelFrame/War3LoadingScreenRenderer 各有一份, 已三方漂移
// (|n 换成空格还是换行、要不要 Trim), 统一到这里。
public static class UiText
{
    static readonly Regex ColorCodes = new(@"\|c[0-9a-fA-F]{8}|\|r",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // 去掉 |cAARRGGBB / |r; newlineAsSpace=true 时把 |n 压成空格 (单行控件用)
    public static string Strip(string value, bool newlineAsSpace = true)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return ColorCodes.Replace(value, "")
            .Replace("|n", newlineAsSpace ? " " : "\n")
            .Replace("|N", newlineAsSpace ? " " : "\n")
            .Trim();
    }

    /// <summary>
    /// 原版 TextBlock 断行语义的复刻（Game.dll）：
    ///   · 宽度累计 = 0x6f0db5c0：逐「文本簇」累加 advance，加入下一簇会超宽就停；
    ///     单簇本身就超宽时强制放一个（等价原版"至少推进一个 token"）。
    ///   · 空格回退 = 0x6f0d8e40：在已放下的前缀里找最后一个 iswspace 簇当断点，
    ///     找不到（整个词都放不下）才按字符硬断 —— 即"整词优先、超长词才断字"。
    ///   · 行尾空格丢弃、下一行行首空格跳过（原版 0x6f0d8d80 的 trim 行为）。
    ///   · |cAARRGGBB / |r / || 是零宽簇，不参与宽度，但原样保留在输出里。
    /// 硬换行只有 |n / |N / CR / LF 三种（0x6f0d8780 对 0xa/0xd 返回 2）。
    /// </summary>
    public static List<string> WrapToWidth(string value, float width, Func<string, float> measure)
    {
        var output = new List<string>();
        if (string.IsNullOrEmpty(value)) { output.Add(""); return output; }
        if (measure == null || width <= 0f)
        {
            foreach (string raw in NormalizeBreaks(value).Split('\n')) output.Add(raw);
            return output;
        }
        foreach (string paragraph in NormalizeBreaks(value).Split('\n'))
        {
            if (paragraph.Length == 0) { output.Add(""); continue; }
            List<Cluster> clusters = BuildClusters(paragraph);
            int start = 0;
            while (start < clusters.Count)
            {
                int end = start, lastSpace = -1;
                var probe = new System.Text.StringBuilder();
                while (end < clusters.Count)
                {
                    Cluster cluster = clusters[end];
                    probe.Append(cluster.Plain);
                    bool fits = measure(probe.ToString()) <= width;
                    if (!fits && end > start) { probe.Length -= cluster.Plain.Length; break; }
                    if (cluster.Space) lastSpace = end;
                    end++;
                    if (!fits) break;
                }
                if (end <= start) end = start + 1;
                // 整词优先：放得下的前缀里存在空格就退到最后一个空格（不与行首同位）。
                int breakAt = end < clusters.Count && lastSpace > start ? lastSpace : end;
                if (breakAt <= start) breakAt = start + 1;
                var line = new System.Text.StringBuilder();
                for (int index = start; index < breakAt; index++) line.Append(clusters[index].Raw);
                output.Add(line.ToString().TrimEnd(' ', '\t'));
                start = breakAt;
                while (start < clusters.Count && clusters[start].Space) start++;
            }
        }
        if (output.Count == 0) output.Add("");
        return output;
    }

    static string NormalizeBreaks(string value) => (value ?? "")
        .Replace("|n", "\n").Replace("|N", "\n")
        .Replace("\r\n", "\n").Replace('\r', '\n').Replace('\t', ' ');

    readonly struct Cluster
    {
        internal Cluster(string raw, string plain, bool space)
        { Raw = raw; Plain = plain; Space = space; }
        internal readonly string Raw;      // 原样输出（含颜色码）
        internal readonly string Plain;    // 参与宽度测量（颜色码为空）
        internal readonly bool Space;
    }

    static List<Cluster> BuildClusters(string text)
    {
        var list = new List<Cluster>(text.Length);
        for (int index = 0; index < text.Length;)
        {
            if (text[index] == '|' && index + 1 < text.Length)
            {
                char code = char.ToLowerInvariant(text[index + 1]);
                if (code == 'c' && index + 9 < text.Length && IsHex(text, index + 2, 8))
                {
                    list.Add(new Cluster(text.Substring(index, 10), "", false));
                    index += 10; continue;
                }
                if (code == 'r')
                {
                    list.Add(new Cluster(text.Substring(index, 2), "", false));
                    index += 2; continue;
                }
                if (code == '|')
                {
                    // 转义竖线：原样两字符输出，但只算一个字形宽。
                    list.Add(new Cluster(text.Substring(index, 2), "|", false));
                    index += 2; continue;
                }
            }
            int length = char.IsHighSurrogate(text[index]) && index + 1 < text.Length ? 2 : 1;
            string glyph = text.Substring(index, length);
            list.Add(new Cluster(glyph, glyph, glyph == " "));
            index += length;
        }
        return list;
    }

    static bool IsHex(string value, int offset, int count)
    {
        for (int index = 0; index < count; index++)
            if (!Uri.IsHexDigit(value[offset + index])) return false;
        return true;
    }

    // 按列数硬换行, 优先在空格处断开
    public static IEnumerable<string> Wrap(string value, int columns)
    {
        if (string.IsNullOrEmpty(value) || columns <= 0) yield break;
        foreach (string paragraph in value.Split('\n'))
        {
            string remaining = paragraph.Trim();
            while (remaining.Length > columns)
            {
                int split = remaining.LastIndexOf(' ', columns);
                if (split <= 0) split = columns;
                yield return remaining[..split].Trim();
                remaining = remaining[split..].Trim();
            }
            if (remaining.Length > 0) yield return remaining;
        }
    }
}
