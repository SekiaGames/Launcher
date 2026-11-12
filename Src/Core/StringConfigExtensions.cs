namespace OpenWC3;

public static class StringConfigExtensions
{
    public static string[] SplitConfig(this string value, char separator, StringSplitOptions options)
        => string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : value.Split(separator, options);

    public static string[] SplitConfig(this string value)
        => string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
