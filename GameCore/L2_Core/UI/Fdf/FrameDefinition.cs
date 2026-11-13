namespace OpenWC3.UI;

// FDF 数据模型: 解析产物, 与运行时 UiFrame 分离 (对齐 Warsmash FrameDefinition)
public sealed class FrameDefinition
{
    public string FrameClass;   // FRAME/SIMPLEFRAME/BACKDROP/TEXT/TEXTURE/BUTTON/STATUSBAR/CONTROL/...
    public string Name;
    public HashSet<string> Flags = new(StringComparer.OrdinalIgnoreCase); // 无值 flag (DecorateFileNames/BackdropTileBackground/...)
    public List<FrameField> Fields = new();
    public List<FrameDefinition> Children = new();

    // INHERITS: 合并 Flags + Fields(继承在前, 本帧覆盖) + Children(withChildren 时追加)
    public void InheritFrom(FrameDefinition other, bool withChildren)
    {
        Flags.UnionWith(other.Flags);
        var merged = new List<FrameField>(other.Fields);
        merged.AddRange(Fields);
        Fields = merged;
        if (withChildren)
        {
            var mergedChildren = new List<FrameDefinition>(other.Children);
            mergedChildren.AddRange(Children);
            Children = mergedChildren;
        }
    }
}

public sealed class FrameField
{
    public string Key;
    public List<string> Values = new();   // 原始字面量, Inflator 按字段语义解释

    public string ValueText => Values.Count > 0 ? Values[0] : null;
    // 原版 FDF 数值字面量常带 f 后缀 (如 SetPoint ..., 0.002f, -0.001),
    // 直接 float.TryParse 会失败并静默返回 0, 让锚点偏移全部归零。
    public float ValueFloat(int index = 0)
    {
        if (Values.Count <= index) return 0f;
        string raw = Values[index];
        if (string.IsNullOrWhiteSpace(raw)) return 0f;
        raw = raw.Trim();
        if (raw.EndsWith("f", System.StringComparison.OrdinalIgnoreCase)
            || raw.EndsWith("F", System.StringComparison.Ordinal))
            raw = raw[..^1];
        return float.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.NumberFormatInfo.InvariantInfo, out float v) ? v : 0f;
    }
}
