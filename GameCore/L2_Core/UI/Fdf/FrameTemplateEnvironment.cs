namespace OpenWC3.UI;

// FDF 模板环境: 命名帧模板 + 装饰字符串 (对齐 Warsmash FrameTemplateEnvironment)
// INHERITS 按 name 查模板; StringList 注册装饰字符串, Text 字段值经 GetDecoratedString 解析
public sealed class FrameTemplateEnvironment
{
    readonly Dictionary<string, FrameDefinition> _frames = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _strings = new(StringComparer.OrdinalIgnoreCase);

    public void Put(string name, FrameDefinition frame)
    {
        if (!string.IsNullOrEmpty(name)) _frames[name] = frame;
    }

    public FrameDefinition GetFrame(string name) =>
        _frames.TryGetValue(name, out var f) ? f : null;

    public void AddDecoratedString(string id, string value)
    {
        if (!string.IsNullOrEmpty(id)) _strings[id] = value;
    }

    public string GetDecoratedString(string id) =>
        _strings.TryGetValue(id, out var v) ? v : id;

    public bool TryGetDecoratedString(string id,out string value)=>_strings.TryGetValue(id,out value);
}
