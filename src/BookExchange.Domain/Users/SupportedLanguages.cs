namespace BookExchange.Domain.Users;

public static class SupportedLanguages
{
    public const string PortugueseBrazil = "pt-BR";
    public const string English = "en";

    /// <summary>Used when nothing better is known (CLAUDE.md: default language).</summary>
    public const string Default = PortugueseBrazil;

    public static IReadOnlyList<string> All { get; } = [PortugueseBrazil, English];

    public static bool IsSupported(string? language) => language is PortugueseBrazil or English;
}
