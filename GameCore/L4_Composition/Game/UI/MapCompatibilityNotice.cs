namespace OpenWC3.Game.UI;

internal static class MapCompatibilityNotice
{
    public static string VersionText(W3iInfo info)
    {
        if (info == null) return "未知";
        return info.HasGameVersion
            ? $"{info.GameMajor}.{info.GameMinor}.{info.GameBuild}"
            : "旧版（未记录版本）";
    }

    public static string Warning(W3iInfo info)
    {
        if (info == null) return "";
        if (info.HasGameVersion && info.GameVersionNumber >= 132)
            return "此地图由魔兽 1.32 或更高版本保存。\n兼容性仍在完善，游玩可能不稳定。";
        if (info.FormatVersion >= 31 && !info.HasGameVersion)
            return "此地图使用较新的格式，游玩可能不稳定。";
        return "";
    }
}
