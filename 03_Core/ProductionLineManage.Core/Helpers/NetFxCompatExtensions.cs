namespace ProductionLineManage.Core.Helpers
{
    /// <summary>
    /// .NET Framework 4.8 缺少的 BCL 扩展（GetValueOrDefault、Split(char, count) 等）。
    /// </summary>
    public static class NetFxCompatExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(
            this IReadOnlyDictionary<TKey, TValue> dictionary,
            TKey key)
        {
            return dictionary.TryGetValue(key, out var value) ? value : default;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(
            this IReadOnlyDictionary<TKey, TValue> dictionary,
            TKey key,
            TValue defaultValue)
        {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key)
        {
            return dictionary.TryGetValue(key, out var value) ? value : default;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(
            this Dictionary<TKey, TValue> dictionary,
            TKey key,
            TValue defaultValue)
        {
            return dictionary.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public static string[] Split(this string value, char separator, StringSplitOptions options)
        {
            return value.Split(new[] { separator }, options);
        }

        public static string[] Split(this string value, char separator, int count)
        {
            return value.Split(new[] { separator }, count);
        }

        public static string[] Split(this string value, string separator, StringSplitOptions options)
        {
            var parts = value.Split(new[] { separator }, StripTrim(options));
            if (HasTrimEntries(options))
            {
                for (var i = 0; i < parts.Length; i++)
                    parts[i] = parts[i].Trim();
            }
            return parts;
        }

        public static bool Contains(this string value, char ch)
        {
            return value != null && value.IndexOf(ch) >= 0;
        }

        public static bool Contains(this string value, string substring, StringComparison comparison)
        {
            return value != null && substring != null && value.IndexOf(substring, comparison) >= 0;
        }

        public static bool StartsWith(this string value, char ch)
        {
            return !string.IsNullOrEmpty(value) && value[0] == ch;
        }

        public static string Replace(this string value, string oldValue, string newValue, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(oldValue))
                return value;

            if (comparison == StringComparison.Ordinal)
                return value.Replace(oldValue, newValue);

            var result = new System.Text.StringBuilder();
            var index = 0;
            while (index < value.Length)
            {
                var match = value.IndexOf(oldValue, index, comparison);
                if (match < 0)
                {
                    result.Append(value.Substring(index));
                    break;
                }
                result.Append(value.Substring(index, match - index));
                result.Append(newValue);
                index = match + oldValue.Length;
            }
            return result.ToString();
        }

        private static bool HasTrimEntries(StringSplitOptions options) =>
            ((int)options & 2) != 0;

        private static StringSplitOptions StripTrim(StringSplitOptions options) =>
            (StringSplitOptions)((int)options & ~2);
    }
}
