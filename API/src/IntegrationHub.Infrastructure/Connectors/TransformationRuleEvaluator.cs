using System.Globalization;
using IntegrationHub.Application.Abstractions.Connectors;

namespace IntegrationHub.Infrastructure.Connectors;

/// <summary>
/// Evaluates transformation rule expressions. Rule grammar is <c>kind:args</c>:
/// <list type="bullet">
/// <item><c>date:&lt;sourceFormat&gt;|&lt;targetFormat&gt;</c> — reformat a date string.</item>
/// <item><c>lookup:K1=V1;K2=V2[;default=D]</c> — code lookup / value mapping.</item>
/// <item><c>valuemap:...</c> — alias of <c>lookup</c>.</item>
/// <item><c>concat:fieldA,'-',fieldB</c> — concatenate fields and quoted literals.</item>
/// </list>
/// An empty/unknown rule passes the source value through unchanged.
/// </summary>
internal sealed class TransformationRuleEvaluator : ITransformationRuleEvaluator
{
    public object? Evaluate(string? rule, object? sourceValue, IReadOnlyDictionary<string, object?> sourceFields)
    {
        if (string.IsNullOrWhiteSpace(rule))
        {
            return sourceValue;
        }

        var separatorIndex = rule.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return sourceValue;
        }

        var kind = rule[..separatorIndex].Trim().ToLowerInvariant();
        var args = rule[(separatorIndex + 1)..];

        return kind switch
        {
            "date" => ApplyDate(args, sourceValue),
            "lookup" or "valuemap" => ApplyLookup(args, sourceValue),
            "concat" => ApplyConcat(args, sourceFields),
            _ => sourceValue,
        };
    }

    private static object? ApplyDate(string args, object? sourceValue)
    {
        var value = sourceValue?.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return sourceValue;
        }

        var parts = args.Split('|', 2);
        var targetFormat = parts.Length == 2 ? parts[1].Trim() : parts[0].Trim();
        var sourceFormat = parts.Length == 2 ? parts[0].Trim() : null;

        DateTime parsed;
        var success = !string.IsNullOrWhiteSpace(sourceFormat)
            ? DateTime.TryParseExact(value, sourceFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
            : DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);

        return success ? parsed.ToString(targetFormat, CultureInfo.InvariantCulture) : value;
    }

    private static object? ApplyLookup(string args, object? sourceValue)
    {
        var key = sourceValue?.ToString() ?? string.Empty;
        string? fallback = null;

        foreach (var pair in args.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length != 2)
            {
                continue;
            }

            var k = kv[0].Trim();
            var v = kv[1].Trim();
            if (string.Equals(k, "default", StringComparison.OrdinalIgnoreCase))
            {
                fallback = v;
            }
            else if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
            {
                return v;
            }
        }

        return fallback ?? sourceValue;
    }

    private static object ApplyConcat(string args, IReadOnlyDictionary<string, object?> sourceFields)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var token in args.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            // Quoted token is a literal; otherwise it is a field name.
            if (token.Length >= 2 && token[0] == '\'' && token[^1] == '\'')
            {
                builder.Append(token[1..^1]);
            }
            else if (sourceFields.TryGetValue(token, out var fieldValue))
            {
                builder.Append(fieldValue);
            }
        }

        return builder.ToString();
    }
}
