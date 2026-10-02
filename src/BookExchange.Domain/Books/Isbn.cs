namespace BookExchange.Domain.Books;

/// <summary>ISBN normalized to its 13-digit form (ISBN-10 is converted). Checksums are verified.</summary>
public static class Isbn
{
    /// <summary>Returns the ISBN-13 for a valid ISBN-10/13 (hyphens and spaces ignored), or null.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var compact = new string(input.Where(c => c is not ('-' or ' ')).ToArray()).ToUpperInvariant();
        return compact.Length switch
        {
            13 when compact.All(char.IsAsciiDigit) && IsValidIsbn13(compact) => compact,
            10 when IsValidIsbn10(compact) => ToIsbn13(compact),
            _ => null,
        };
    }

    private static bool IsValidIsbn13(string digits)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += (digits[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return (10 - (sum % 10)) % 10 == digits[12] - '0';
    }

    private static bool IsValidIsbn10(string chars)
    {
        if (!chars[..9].All(char.IsAsciiDigit) || !(char.IsAsciiDigit(chars[9]) || chars[9] == 'X'))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var value = chars[i] == 'X' ? 10 : chars[i] - '0';
            sum += value * (10 - i);
        }

        return sum % 11 == 0;
    }

    private static string ToIsbn13(string isbn10)
    {
        var body = "978" + isbn10[..9];
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += (body[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return body + ((10 - (sum % 10)) % 10);
    }
}
