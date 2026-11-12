namespace OpenWC3;

// Core-side view of map object data. The concrete SLK/w3a/w3h implementation lives in OpenWC3.Game,
// keeping the deterministic simulation independent from map/archive libraries.
public interface IRuntimeObjectData
{
    string Id { get; }
    string BaseId { get; }
    string GetString(string field, string fallback = "");
    string GetString(string field, int level, int dataPointer = 0, string fallback = "");
    // Numeric object cells use RuntimeObjectNumbers: explicit empty is zero;
    // missing fields retain the caller's fallback. Text configuration is separate.
    float GetFloat(string field, int level, int dataPointer, float fallback);
    int GetInt(string field, int level, int dataPointer, int fallback);
}
