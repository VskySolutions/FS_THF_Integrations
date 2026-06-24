using System.Globalization;

namespace IntegrationHub.Infrastructure.Persistence.ModifiedLog;

/// <summary>Formats a tracked field value into the human-readable label stored in the Modified Log.</summary>
public interface IFieldValueFormatter
{
    /// <summary>Formats a value (enum→label, bool→Yes/No, decimal→number); null for an unset value.</summary>
    string? Format(object? value);
}

/// <summary>Default <see cref="IFieldValueFormatter"/> covering enums, booleans, decimals and dates.</summary>
public sealed class FieldValueFormatter : IFieldValueFormatter
{
    public string? Format(object? value) => value switch
    {
        null => null,
        bool b => b ? "Yes" : "No",
        Enum e => SplitPascalCase(e.ToString()),
        decimal d => d.ToString("N2", CultureInfo.InvariantCulture),
        double d => d.ToString("N2", CultureInfo.InvariantCulture),
        float f => f.ToString("N2", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("u", CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    private static string SplitPascalCase(string value)
        => string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
}
