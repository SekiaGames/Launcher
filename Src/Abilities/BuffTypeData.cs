namespace OpenWC3;

/// <summary>
/// Buff 的物编数据。Launcher 还没有解析 war3map.w3h，先用一个只读字典壳承接客户端代码里的读取。
/// </summary>
public sealed class BuffTypeData : IRuntimeObjectData
{
    readonly Dictionary<string, string> _cells = new(StringComparer.OrdinalIgnoreCase);

    public string Id { get; init; } = "";

    public string BaseId { get; init; } = "";

    public void Set(string field, string value) => _cells[field] = value;

    public string GetString(string field, string fallback = "") => GetString(field, 1, 0, fallback);

    public string GetString(string field, int level, int dataPointer = 0, string fallback = "")
        => _cells.TryGetValue(field ?? "", out string value) ? value : fallback;

    public float GetFloat(string field, int level, int dataPointer, float fallback)
        => _cells.TryGetValue(field ?? "", out string value)
            && float.TryParse(value, out float parsed) ? parsed : fallback;

    public int GetInt(string field, int level, int dataPointer, int fallback)
        => _cells.TryGetValue(field ?? "", out string value)
            && int.TryParse(value, out int parsed) ? parsed : fallback;
}
