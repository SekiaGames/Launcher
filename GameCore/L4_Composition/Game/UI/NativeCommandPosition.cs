namespace OpenWC3.Game.UI;

internal static class NativeCommandPosition
{
    internal static int ReadInteger(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        if (text[0] == '\'')
        {
            uint raw = 0;
            for (int i = 1; i < text.Length && i <= 4 && text[i] != '\'' && text[i] != '\0'; i++)
                raw = (raw << 8) | (byte)text[i];
            return unchecked((int)raw);
        }
        int cursor = 0;
        while (cursor < text.Length && text[cursor] is ' ' or '\t' or '\n' or '\r' or '\f' or '\v') cursor++;
        bool negative = cursor < text.Length && text[cursor] == '-';
        if (cursor < text.Length && text[cursor] is '+' or '-') cursor++;
        int radix = 10;
        if (cursor < text.Length && text[cursor] == '0')
        {
            radix = 8;
            if (cursor + 2 < text.Length && text[cursor + 1] is 'x' or 'X' && Digit(text[cursor + 2]) is >= 0 and < 16)
            {
                radix = 16;
                cursor += 2;
            }
        }
        uint value = 0, limit = negative ? 0x80000000u : 0x7fffffffu;
        for (; cursor < text.Length; cursor++)
        {
            int digit = Digit(text[cursor]);
            if (digit < 0 || digit >= radix) break;
            if (value > (limit - (uint)digit) / (uint)radix) return negative ? int.MinValue : int.MaxValue;
            value = value * (uint)radix + (uint)digit;
        }
        return negative ? unchecked(-(int)value) : (int)value;
    }

    private static int Digit(char c) => c is >= '0' and <= '9' ? c - '0'
        : c is >= 'a' and <= 'f' ? c - 'a' + 10
        : c is >= 'A' and <= 'F' ? c - 'A' + 10
        : -1;

    internal static bool TryRead(Func<string, string> read, string field, string rawX, string rawY,
        out int slot, string splitX = null, string splitY = null)
    {
        string[] parts = read(field)?.Split(',');
        string Component(int index, string raw, string split)
        {
            string value = parts != null && index < parts.Length ? parts[index] : null;
            if (!string.IsNullOrWhiteSpace(value)) return value;
            return (split == null ? null : read(split)) ?? (raw == null ? null : read(raw)) ?? value;
        }
        int x = ReadInteger(Component(0, rawX, splitX)), y = ReadInteger(Component(1, rawY, splitY));
        slot = x >= 0 && x < 4 && y >= 0 && y < 3 ? y * 4 + x : -1;
        return slot >= 0;
    }
}
