using System.Collections.Frozen;

namespace SoftwareFactory.Infrastructure.Persistence.Conventions;

/// <summary>Snake_case text values of an enum, shared by the value converter and the check constraints.</summary>
internal static class EnumText<TEnum>
    where TEnum : struct, Enum
{
    private static readonly FrozenDictionary<TEnum, string> _toTextMap = Enum.GetValues<TEnum>()
        .ToFrozenDictionary(value => value, value => SnakeCase.Convert(value.ToString()));

    private static readonly FrozenDictionary<string, TEnum> _fromTextMap = _toTextMap
        .ToFrozenDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    public static IReadOnlyCollection<string> Values => _toTextMap.Values;

    public static string ToText(TEnum value) => _toTextMap[value];

    public static TEnum FromText(string text) =>
        _fromTextMap.TryGetValue(text, out var value)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(text), text, $"'{text}' is not a valid {typeof(TEnum).Name} value.");
}
