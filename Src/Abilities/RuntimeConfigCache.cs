using System.Globalization;
using System.Runtime.CompilerServices;

namespace OpenWC3;

// Raw configuration strings are immutable. Cache interpretation, not field lookup,
// caller fallbacks, or mutable ability/unit state. Weak keys release old map data.
public static class RuntimeConfigCache
{
    sealed class Parsed
    {
        internal volatile bool IntegerReady, FloatReady;
        internal int? Integer;
        internal float? Float;
        internal Dictionary<(string Positive, string Negative), int?> CultureIntegers;
        internal string[] Levels;
        internal IReadOnlyList<string> Arts, Rawcodes;
        internal TargetFlags? Targets;
        internal Dictionary<(int Separators, StringSplitOptions Options), Tokens> Splits;
        internal string Trimmed;
        internal Dictionary<(Type Type, string Variant), object> DefinitionValues;
    }
    static readonly ConditionalWeakTable<string, Parsed> Values = new();
    static Parsed Get(string raw) => Values.GetValue(raw, static _ => new Parsed());

    /// <summary>Caches a scalar interpretation, keeping caller-specific defaults outside the cache.</summary>
    public static T? ReadDefinitionValue<T>(string raw, string variant,
        Func<string, T?> parse) where T : struct
    {
        ArgumentNullException.ThrowIfNull(raw);
        Parsed parsed = Get(raw);
        lock (parsed)
        {
            parsed.DefinitionValues ??= new();
            var key = (typeof(T), variant);
            if (!parsed.DefinitionValues.TryGetValue(key, out object cached))
            {
                cached = parse(raw);
                parsed.DefinitionValues.Add(key, cached);
            }
            return (T?)cached;
        }
    }

    public static string TrimConfig(this string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        Parsed parsed = Get(raw);
        lock (parsed) return parsed.Trimmed ??= raw.Trim();
    }

    // Keep each caller's split rules: unlike ReadRawcodes/ReadArts, this does
    // not truncate, filter placeholders, deduplicate, or change casing.
    public static Tokens SplitConfig(this string raw, char separator,
        StringSplitOptions options = StringSplitOptions.None)
    {
        if (separator != ',') throw new ArgumentException("Expected a configuration comma separator.");
        return SplitConfigCore(raw, 0, options);
    }

    public static Tokens SplitConfig(this string raw, char first, char second,
        StringSplitOptions options = StringSplitOptions.None)
    {
        if (first != ',' || second != ';') throw new ArgumentException("Expected configuration comma/semicolon separators.");
        return SplitConfigCore(raw, 1, options);
    }

    public static Tokens SplitAnimationConfig(this string raw,
        StringSplitOptions options = StringSplitOptions.None) => SplitConfigCore(raw, 2, options);

    public static Tokens SplitConfig(this string raw, char[] separators,
        StringSplitOptions options = StringSplitOptions.None)
    {
        if (separators.Length == 4 && separators[0] == ',' && separators[1] == ';'
            && separators[2] == ' ' && separators[3] == '\t')
            return SplitConfigCore(raw, 2, options);
        if (separators.Length != 2 || separators[0] != ',' || separators[1] != ';')
            throw new ArgumentException("Unsupported configuration separators.");
        return SplitConfigCore(raw, 1, options);
    }

    static Tokens SplitConfigCore(string raw, int separators, StringSplitOptions options)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if ((options & ~(StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) != 0)
            throw new ArgumentException("Unsupported split options.");
        Parsed parsed = Get(raw);
        lock (parsed)
        {
            parsed.Splits ??= new();
            var key = (separators, options);
            if (!parsed.Splits.TryGetValue(key, out Tokens tokens))
            {
                tokens = new Tokens(separators == 0 ? raw.Split(',', options)
                    : raw.Split(separators == 1 ? new[] { ',', ';' } : new[] { ',', ';', ' ', '\t' }, options));
                parsed.Splits.Add(key, tokens);
            }
            return tokens;
        }
    }

    public sealed class Tokens : IReadOnlyList<string>
    {
        readonly string[] _values;
        internal Tokens(string[] values) => _values = values;
        public int Count => _values.Length;
        public int Length => Count;
        public string this[int index] => _values[index];
        public string FirstOrDefault() => Count == 0 ? null : _values[0];
        public string LastOrDefault() => Count == 0 ? null : _values[^1];
        public string ElementAtOrDefault(int index) => index >= 0 && index < Count ? _values[index] : null;
        public Enumerator GetEnumerator() => new(_values);
        System.Collections.Generic.IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        public struct Enumerator : IEnumerator<string>
        {
            readonly string[] _values;
            int _index;
            internal Enumerator(string[] values) { _values = values; _index = -1; }
            public bool MoveNext() => ++_index < _values.Length;
            public string Current => _values[_index];
            object System.Collections.IEnumerator.Current => Current;
            public void Dispose() { }
            public void Reset() => _index = -1;
        }
    }

    public static int ReadInt(string raw, int fallback, bool currentCulture = false) =>
        TryReadInt(raw, out int value, currentCulture) ? value : fallback;

    public static bool TryReadInt(string raw, out int result, bool currentCulture = false)
    {
        result = default;
        if (raw == null) return false;
        if (currentCulture)
        {
            var format = CultureInfo.CurrentCulture.NumberFormat;
            if (format.PositiveSign == "+" && format.NegativeSign == "-") currentCulture = false;
        }
        Parsed parsed = Get(raw);
        if (!currentCulture && parsed.IntegerReady)
        {
            result = parsed.Integer.GetValueOrDefault();
            return parsed.Integer.HasValue;
        }
        lock (parsed)
        {
            if (currentCulture)
            {
                // Integer parsing only uses the format's positive/negative signs.
                // Check their values so mutable custom cultures remain observable.
                var format = CultureInfo.CurrentCulture.NumberFormat;
                var key = (format.PositiveSign, format.NegativeSign);
                parsed.CultureIntegers ??= new();
                if (!parsed.CultureIntegers.TryGetValue(key, out int? value))
                {
                    value = int.TryParse(raw, NumberStyles.Integer, format, out int number)
                        ? number : null;
                    parsed.CultureIntegers.Add(key, value);
                }
                result = value.GetValueOrDefault();
                return value.HasValue;
            }
            if (!parsed.IntegerReady)
            {
                parsed.Integer = int.TryParse(raw, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int value) ? value : null;
                parsed.IntegerReady = true;
            }
            result = parsed.Integer.GetValueOrDefault();
            return parsed.Integer.HasValue;
        }
    }

    public static float ReadFloat(string raw, float fallback) =>
        TryReadFloat(raw, out float value) ? value : fallback;

    public static bool TryReadFloat(string raw, out float result)
    {
        result = default;
        if (raw == null) return false;
        Parsed parsed = Get(raw);
        if (parsed.FloatReady)
        {
            result = parsed.Float.GetValueOrDefault();
            return parsed.Float.HasValue;
        }
        lock (parsed)
        {
            if (!parsed.FloatReady)
            {
                parsed.Float = float.TryParse(raw, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float value) ? value : null;
                parsed.FloatReady = true;
            }
            result = parsed.Float.GetValueOrDefault();
            return parsed.Float.HasValue;
        }
    }

    public static string SelectLevelValue(string raw, int level)
    {
        if (string.IsNullOrWhiteSpace(raw) || !raw.Contains(',')) return raw;
        Parsed parsed = Get(raw);
        lock (parsed)
        {
            parsed.Levels ??= raw.Split(',').Select(value => value.Trim()).ToArray();
            return parsed.Levels[Math.Clamp(Math.Max(1, level) - 1, 0, parsed.Levels.Length - 1)];
        }
    }

    public static IReadOnlyList<string> ReadArts(string raw)
    {
        if (raw == null) return Array.Empty<string>();
        Parsed parsed = Get(raw);
        lock (parsed)
            return parsed.Arts ??= Array.AsReadOnly(raw.Split(',', ';').Select(item => item.Trim())
                .Where(item => item.Length > 0 && item != "_")
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public static IReadOnlyList<string> ReadRawcodes(string raw)
    {
        if (raw == null) return Array.Empty<string>();
        Parsed parsed = Get(raw);
        lock (parsed)
            return parsed.Rawcodes ??= Array.AsReadOnly(raw.Split(',', ';').Select(item => item.Trim())
                .Where(item => item.Length >= 4).Select(item => item[..4]).Distinct().ToArray());
    }

    public static TargetFlags ReadTargets(string raw)
    {
        if (raw == null) return TargetFlags.None;
        Parsed parsed = Get(raw);
        lock (parsed)
        {
            if (parsed.Targets is TargetFlags cached) return cached;
            TargetFlags flags = TargetFlags.None;
            foreach (string token in raw.Split(new[] { ',', ';' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                flags |= token.ToLowerInvariant() switch
                {
                    "ground" or "grou" => TargetFlags.Ground, "air" => TargetFlags.Air,
                    "structure" or "stru" or "structures" or "building" or "buildings" => TargetFlags.Structure,
                    "enemy" or "enemies" or "enem" => TargetFlags.Enemy, "player" or "play" => TargetFlags.Player,
                    "ally" or "allies" or "alli" or "allied" => TargetFlags.Ally,
                    "friend" or "friends" or "frie" => TargetFlags.Player|TargetFlags.Ally,
                    "neutral" or "neut" => TargetFlags.Neutral,
                    "self" => TargetFlags.Self,
                    "notself" or "nots" => TargetFlags.Player|TargetFlags.Ally|TargetFlags.Neutral|TargetFlags.Enemy,
                    "none" => TargetFlags.NoTargets,
                    "ward" or "totem" or "tote" => TargetFlags.Ward,
                    "item" => TargetFlags.Item, "tree" => TargetFlags.Tree, "wall" => TargetFlags.Wall,
                    "debris" or "debr" => TargetFlags.Debris,
                    "decoration" or "deco" => TargetFlags.Decoration, "bridge" => TargetFlags.Bridge,
                    "sapper" => TargetFlags.Sapper, "nonsapper" => TargetFlags.NonSapper,
                    "nonmagicimmune" => TargetFlags.NonMagicImmune, "nonethereal" => TargetFlags.NonEthereal,
                    "vulnerable" or "vuln" => TargetFlags.Vulnerable,
                    "invulnerable" or "invu" => TargetFlags.Invulnerable,
                    "hero" => TargetFlags.Hero, "nonhero" or "nonh" => TargetFlags.NonHero,
                    "organic" or "orga" => TargetFlags.Organic, "mechanical" or "mech" => TargetFlags.Mechanical,
                    "ancient" => TargetFlags.Ancient, "nonancient" => TargetFlags.NonAncient,
                    "dead" => TargetFlags.Dead, "alive" or "aliv" => TargetFlags.Alive,
                    // Game.dll's literal underscore token (not an unspecified mask).
                    "_" => TargetFlags.Ground|TargetFlags.Air|TargetFlags.Structure
                        |TargetFlags.Self|TargetFlags.Player|TargetFlags.Ally|TargetFlags.Neutral|TargetFlags.Enemy
                        |TargetFlags.Alive|TargetFlags.Vulnerable|TargetFlags.Hero|TargetFlags.NonHero
                        |TargetFlags.Organic|TargetFlags.Mechanical|TargetFlags.Sapper|TargetFlags.NonSapper
                        |TargetFlags.Ancient|TargetFlags.NonAncient,
                    _ => TargetFlags.None,
                };
            parsed.Targets = flags;
            return flags;
        }
    }
}
