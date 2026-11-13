namespace OpenWC3;

// Numeric object cells differ from arbitrary text configuration: an explicit
// empty cell is zero. A missing field (null) or malformed number still uses
// the caller's fallback. Resolve level/pointer/alias precedence before parsing.
// Game.dll 690290/68fe20 -> 17bff0/17b900 reads empty cells as numeric zero.
public static class RuntimeObjectNumbers
{
    public static bool TryReadFloat(string raw,out float value)
    {
        if(raw!=null&&string.IsNullOrWhiteSpace(raw)) { value=0;return true; }
        return RuntimeConfigCache.TryReadFloat(raw,out value);
    }

    public static bool TryReadInt(string raw,out int value,bool currentCulture=false)
    {
        if(raw!=null&&string.IsNullOrWhiteSpace(raw)) { value=0;return true; }
        return RuntimeConfigCache.TryReadInt(raw,out value,currentCulture);
    }

    public static float ReadFloat(string raw,float fallback)
        =>TryReadFloat(raw,out float value)?value:fallback;

    public static int ReadInt(string raw,int fallback,bool currentCulture=false)
        =>TryReadInt(raw,out int value,currentCulture)?value:fallback;
}
